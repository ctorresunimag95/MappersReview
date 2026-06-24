using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CustomMapper.SourceGenerator.Generator
{
    internal static class PropertyMappingResolver
    {
        internal static MapMethodModel BuildModel(
            IMethodSymbol method,
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            ISet<string> ignored,
            bool hasExtendMap,
            List<EquatableDiagnostic> diagnostics)
        {
            var (assignments, unmapped) = ResolveAssignments(sourceType, destinationType, ignored);

            foreach (var propName in unmapped)
            {
                diagnostics.Add(EquatableDiagnostic.Create(
                    MapperDiagnostics.UnmappedProperty, method,
                    destinationType.Name, propName));
            }

            return new MapMethodModel(
                method.Name,
                SymbolMappingHelpers.ToGlobal(sourceType),
                SymbolMappingHelpers.ToGlobal(destinationType),
                hasExtendMap,
                assignments: assignments.ToEquatableArray(),
                useConstructor: false,
                ctorBindings: new List<ConstructorParameterBinding>().ToEquatableArray(),
                postCtorAssignments: new List<PropertyAssignment>().ToEquatableArray());
        }

        private static (List<PropertyAssignment> assignments, List<string> unmapped) ResolveAssignments(
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            ISet<string> ignored)
        {
            var assignments = new List<PropertyAssignment>();
            var unmapped = new List<string>();

            var sourceProps = SymbolMappingHelpers.GetReadableProperties(sourceType);
            var mutableProps = SymbolMappingHelpers.GetMutableProperties(destinationType);
            var initOnlyProps = SymbolMappingHelpers.GetInitOnlyProperties(destinationType);

            foreach (var destProp in mutableProps)
            {
                if (ignored.Contains(destProp.Name))
                    continue;

                if (sourceProps.TryGetValue(destProp.Name, out var srcProp)
                    && SymbolEqualityComparer.Default.Equals(srcProp.Type, destProp.Type))
                    assignments.Add(new PropertyAssignment(destProp.Name, srcProp.Name, isInitOnly: false));
                else
                    unmapped.Add(destProp.Name);
            }

            foreach (var destProp in initOnlyProps)
            {
                if (ignored.Contains(destProp.Name))
                    continue;

                if (sourceProps.TryGetValue(destProp.Name, out var srcProp)
                    && SymbolEqualityComparer.Default.Equals(srcProp.Type, destProp.Type))
                    assignments.Add(new PropertyAssignment(destProp.Name, srcProp.Name, isInitOnly: true));
                else
                    unmapped.Add(destProp.Name);
            }

            return (assignments, unmapped);
        }
    }
}
