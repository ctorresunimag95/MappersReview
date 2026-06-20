using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CustomMapper.SourceGenerator.Generator
{
    [Generator(LanguageNames.CSharp)]
    public sealed class MapperGenerator : IIncrementalGenerator
    {
        private const string MapperAttributeMetadataName = "CustomMapper.SourceGenerator.Runtime.MapperAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classes = context.SyntaxProvider.ForAttributeWithMetadataName(
                    MapperAttributeMetadataName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, ct) => Transform(ctx, ct))
                .Where(static result => result is not null)
                .Select(static (result, _) => result!);

            // Per-class partial implementation + diagnostics.
            context.RegisterSourceOutput(classes, static (spc, result) =>
            {
                foreach (var diagnostic in result.Diagnostics.AsImmutableArray())
                {
                    spc.ReportDiagnostic(diagnostic.ToDiagnostic());
                }

                if (result.Model is { } model && model.Methods.Length > 0)
                    spc.AddSource(
                        SourceGenerationHelper.HintName(model),
                        SourceText.From(SourceGenerationHelper.MapperClass(model), Encoding.UTF8));
            });

            // Single DI registration file from all discovered mappers.
            var models = classes
                .Select(static (result, _) => result.Model)
                .Where(static model => model is not null && model.Methods.Length > 0)
                .Select(static (model, _) => model!)
                .Collect();

            context.RegisterSourceOutput(models, static (spc, allModels) =>
            {
                if (allModels.Length == 0) return;
                spc.AddSource("GeneratedMapperRegistration.g.cs",
                    SourceText.From(SourceGenerationHelper.Registration(allModels), Encoding.UTF8));
            });
        }

        // ---- Transform: symbols -> equatable model (no symbols leak into pipeline state) ----

        private static TransformResult? Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol) return null;
            var classSyntax = (ClassDeclarationSyntax)ctx.TargetNode;

            var diagnostics = new List<EquatableDiagnostic>();

            // CMSG001: must be partial.
            bool isPartial = classSyntax.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));
            if (!isPartial)
            {
                diagnostics.Add(EquatableDiagnostic.Create(
                    MapperDiagnostics.NotPartial, classSymbol, classSymbol.Name));
                return new TransformResult(null, diagnostics.ToEquatableArray());
            }

            var methods = new List<MapMethodModel>();

            foreach (var member in classSymbol.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                if (member is not IMethodSymbol method) continue;
                if (!method.IsPartialDefinition) continue;
                if (method.Name == SourceGenerationHelper.ExtendMapMethodName) continue;

                // CMSG002: must return non-void and take exactly one parameter.
                if (method.ReturnsVoid || method.Parameters.Length != 1)
                {
                    diagnostics.Add(EquatableDiagnostic.Create(
                        MapperDiagnostics.InvalidMethodSignature, method, method.Name));
                    continue;
                }

                var sourceType = method.Parameters[0].Type;
                var destinationType = method.ReturnType;

                var (assignments, unmapped) = ResolveAssignments(sourceType, destinationType);

                // CMSG003: warn for unmapped writable destination properties.
                foreach (var propName in unmapped)
                {
                    diagnostics.Add(EquatableDiagnostic.Create(
                        MapperDiagnostics.UnmappedProperty, method,
                        destinationType.Name, propName));
                }

                bool hasExtendMap = ResolveExtendMap(classSymbol, sourceType, destinationType, diagnostics);

                methods.Add(new MapMethodModel(
                    method.Name,
                    ToGlobal(sourceType),
                    ToGlobal(destinationType),
                    hasExtendMap,
                    assignments.ToEquatableArray()));
            }

            var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : classSymbol.ContainingNamespace.ToDisplayString();

            var model = new MapperClassModel(
                ns,
                classSymbol.Name,
                Accessibility(classSymbol),
                methods.ToEquatableArray());

            return new TransformResult(model, diagnostics.ToEquatableArray());
        }

        private static (List<PropertyAssignment> assignments, List<string> unmapped) ResolveAssignments(
            ITypeSymbol sourceType, ITypeSymbol destinationType)
        {
            var assignments = new List<PropertyAssignment>();
            var unmapped = new List<string>();

            var sourceProps = GetReadableProperties(sourceType);

            foreach (var destProp in GetWritableProperties(destinationType))
            {
                if (sourceProps.TryGetValue(destProp.Name, out var srcProp)
                    && SymbolEqualityComparer.Default.Equals(srcProp.Type, destProp.Type))
                    assignments.Add(new PropertyAssignment(destProp.Name, srcProp.Name));
                else
                    unmapped.Add(destProp.Name);
            }

            return (assignments, unmapped);
        }

        private static Dictionary<string, IPropertySymbol> GetReadableProperties(ITypeSymbol type)
        {
            var result = new Dictionary<string, IPropertySymbol>();
            foreach (var t in SelfAndBases(type))
            {
                foreach (var prop in t.GetMembers().OfType<IPropertySymbol>())
                {
                    if (prop.IsStatic || prop.IsIndexer || prop.GetMethod is null) continue;
                    if (prop.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public) continue;
                    if (!result.ContainsKey(prop.Name)) result[prop.Name] = prop;
                }
            }
            return result;
        }

        private static List<IPropertySymbol> GetWritableProperties(ITypeSymbol type)
        {
            var seen = new HashSet<string>();
            var result = new List<IPropertySymbol>();
            foreach (var t in SelfAndBases(type))
            {
                foreach (var prop in t.GetMembers().OfType<IPropertySymbol>())
                {
                    if (prop.IsStatic || prop.IsIndexer || prop.SetMethod is null) continue;
                    if (prop.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public) continue;
                    if (prop.SetMethod.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Public) continue;
                    if (seen.Add(prop.Name)) result.Add(prop);
                }
            }
            return result;
        }

        private static IEnumerable<ITypeSymbol> SelfAndBases(ITypeSymbol type)
        {
            for (ITypeSymbol? t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
                yield return t;
        }

        private static bool ResolveExtendMap(
            INamedTypeSymbol classSymbol,
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            List<EquatableDiagnostic> diagnostics)
        {
            bool nameMatchFound = false;
            foreach (var member in classSymbol.GetMembers(SourceGenerationHelper.ExtendMapMethodName))
            {
                if (member is not IMethodSymbol method) continue;
                nameMatchFound = true;

                bool valid = method.ReturnsVoid
                    && method.Parameters.Length == 2
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, sourceType)
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, destinationType);

                if (valid) return true;
            }

            // CMSG004: a name match exists but no overload fits this pair.
            if (nameMatchFound)
            {
                diagnostics.Add(EquatableDiagnostic.Create(
                    MapperDiagnostics.InvalidExtendMap, classSymbol,
                    classSymbol.Name, sourceType.Name, destinationType.Name));
            }

            return false;
        }

        // ---- Helpers ----

        private static string ToGlobal(ITypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        private static string Accessibility(INamedTypeSymbol symbol) =>
            symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public ? "public" : "internal";
    }
}
