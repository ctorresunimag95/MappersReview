using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace CustomMapper.SourceGenerator.Generator
{
    internal static class ConstructorMappingResolver
    {
        internal static MapMethodModel BuildModel(
            IMethodSymbol method,
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            ISet<string> ignored,
            bool hasExtendMap,
            List<EquatableDiagnostic> diagnostics)
        {
            var (ctorBindings, postCtorAssignments, unmatchedParams, strandedInitOnly) =
                ResolveConstructorMapping(sourceType, destinationType, ignored);

            foreach (var (paramName, paramTypeName) in unmatchedParams)
            {
                diagnostics.Add(EquatableDiagnostic.Create(
                    MapperDiagnostics.ConstructorParameterUnmatched, method,
                    destinationType.Name, paramName, paramTypeName));
            }

            foreach (var propName in strandedInitOnly)
            {
                diagnostics.Add(EquatableDiagnostic.Create(
                    MapperDiagnostics.InitOnlyNotCoveredByConstructor, method,
                    destinationType.Name, propName));
            }

            return new MapMethodModel(
                method.Name,
                SymbolMappingHelpers.ToGlobal(sourceType),
                SymbolMappingHelpers.ToGlobal(destinationType),
                hasExtendMap,
                assignments: new List<PropertyAssignment>().ToEquatableArray(),
                useConstructor: true,
                ctorBindings: ctorBindings.ToEquatableArray(),
                postCtorAssignments: postCtorAssignments.ToEquatableArray());
        }

        private static (
            List<ConstructorParameterBinding> ctorBindings,
            List<PropertyAssignment> postCtorAssignments,
            List<(string paramName, string paramTypeName)> unmatchedParams,
            List<string> strandedInitOnly) ResolveConstructorMapping(
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            ISet<string> ignored)
        {
            var ctorBindings = new List<ConstructorParameterBinding>();
            var postCtorAssignments = new List<PropertyAssignment>();
            var unmatchedParams = new List<(string, string)>();
            var strandedInitOnly = new List<string>();

            var sourceProps = SymbolMappingHelpers.GetReadableProperties(sourceType);
            var mutableProps = SymbolMappingHelpers.GetMutableProperties(destinationType);
            var initOnlyProps = SymbolMappingHelpers.GetInitOnlyProperties(destinationType);

            if (destinationType is not INamedTypeSymbol namedType)
                return (ctorBindings, postCtorAssignments, unmatchedParams, strandedInitOnly);

            var constructors = namedType.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Constructor &&
                            m.DeclaredAccessibility == Accessibility.Public)
                .ToList();

            IMethodSymbol? chosenConstructor = null;
            int bestScore = -1;

            foreach (var ctor in constructors)
            {
                int score = 0;
                foreach (var param in ctor.Parameters)
                {
                    var matchedSourceProp = sourceProps.FirstOrDefault(kvp =>
                        string.Equals(kvp.Key, param.Name, System.StringComparison.OrdinalIgnoreCase)
                        && SymbolEqualityComparer.Default.Equals(kvp.Value.Type, param.Type));

                    if (matchedSourceProp.Key != null)
                        score++;
                }

                if (score > bestScore || (score == bestScore && chosenConstructor != null && ctor.Parameters.Length > chosenConstructor.Parameters.Length))
                {
                    bestScore = score;
                    chosenConstructor = ctor;
                }
            }

            if (chosenConstructor == null)
                chosenConstructor = namedType.GetMembers()
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => m.MethodKind == MethodKind.Constructor && m.IsImplicitlyDeclared);

            if (chosenConstructor == null)
                return (ctorBindings, postCtorAssignments, unmatchedParams, strandedInitOnly);

            var coveredByConstructor = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var param in chosenConstructor.Parameters)
            {
                var matchedSourceProp = sourceProps.FirstOrDefault(kvp =>
                    string.Equals(kvp.Key, param.Name, System.StringComparison.OrdinalIgnoreCase)
                    && SymbolEqualityComparer.Default.Equals(kvp.Value.Type, param.Type));

                if (matchedSourceProp.Key != null)
                {
                    ctorBindings.Add(new ConstructorParameterBinding(param.Name, matchedSourceProp.Value.Name));
                    coveredByConstructor.Add(param.Name);
                }
                else
                {
                    unmatchedParams.Add((param.Name, param.Type.Name));
                }
            }

            foreach (var destProp in mutableProps)
            {
                if (ignored.Contains(destProp.Name))
                    continue;

                if (!coveredByConstructor.Contains(destProp.Name))
                {
                    if (sourceProps.TryGetValue(destProp.Name, out var srcProp)
                        && SymbolEqualityComparer.Default.Equals(srcProp.Type, destProp.Type))
                    {
                        postCtorAssignments.Add(new PropertyAssignment(destProp.Name, srcProp.Name, isInitOnly: false));
                    }
                }
            }

            foreach (var destProp in initOnlyProps)
            {
                if (ignored.Contains(destProp.Name))
                    continue;

                if (!coveredByConstructor.Contains(destProp.Name))
                    strandedInitOnly.Add(destProp.Name);
            }

            return (ctorBindings, postCtorAssignments, unmatchedParams, strandedInitOnly);
        }
    }
}
