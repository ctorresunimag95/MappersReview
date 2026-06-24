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
        private const string UseConstructorAttributeMetadataName = "CustomMapper.SourceGenerator.Runtime.UseConstructorAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classes = context.SyntaxProvider.ForAttributeWithMetadataName(
                    MapperAttributeMetadataName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                    transform: static (ctx, ct) => Transform(ctx, ct))
                .Where(static result => result is not null)
                .Select(static (result, _) => result!);

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

        private static TransformResult? Transform(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol classSymbol) return null;
            var classSyntax = (ClassDeclarationSyntax)ctx.TargetNode;

            var diagnostics = new List<EquatableDiagnostic>();

            bool isPartial = classSyntax.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));
            if (!isPartial)
            {
                diagnostics.Add(EquatableDiagnostic.Create(
                    MapperDiagnostics.NotPartial, classSymbol, classSymbol.Name));
                return new TransformResult(null, diagnostics.ToEquatableArray());
            }

            var methods = new List<MapMethodModel>();
            var ignoreMap = IgnoreMappingHelpers.BuildIgnoreMap(classSyntax, ctx.SemanticModel);

            foreach (var member in classSymbol.GetMembers())
            {
                ct.ThrowIfCancellationRequested();
                if (member is not IMethodSymbol method) continue;
                if (!method.IsPartialDefinition) continue;
                if (method.Name == SourceGenerationHelper.ExtendMapMethodName) continue;

                if (method.ReturnsVoid || method.Parameters.Length != 1)
                {
                    diagnostics.Add(EquatableDiagnostic.Create(
                        MapperDiagnostics.InvalidMethodSignature, method, method.Name));
                    continue;
                }

                var sourceType = method.Parameters[0].Type;
                var destinationType = method.ReturnType;
                var ignored = IgnoreMappingHelpers.ResolveIgnoredProperties(ignoreMap, destinationType);

                bool useConstructor = method.GetAttributes().Any(static a =>
                    a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        == $"global::{UseConstructorAttributeMetadataName}");

                bool hasExtendMap = ResolveExtendMap(classSymbol, sourceType, destinationType, diagnostics);

                methods.Add(useConstructor
                    ? ConstructorMappingResolver.BuildModel(method, sourceType, destinationType, ignored, hasExtendMap, diagnostics)
                    : PropertyMappingResolver.BuildModel(method, sourceType, destinationType, ignored, hasExtendMap, diagnostics));
            }

            var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : classSymbol.ContainingNamespace.ToDisplayString();

            var model = new MapperClassModel(
                ns,
                classSymbol.Name,
                SymbolMappingHelpers.ClassAccessibility(classSymbol),
                methods.ToEquatableArray());

            return new TransformResult(model, diagnostics.ToEquatableArray());
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

            if (nameMatchFound)
            {
                diagnostics.Add(EquatableDiagnostic.Create(
                    MapperDiagnostics.InvalidExtendMap, classSymbol,
                    classSymbol.Name, sourceType.Name, destinationType.Name));
            }

            return false;
        }
    }
}
