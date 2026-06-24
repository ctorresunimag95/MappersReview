using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CustomMapper.SourceGenerator.Generator
{
    internal static class SymbolMappingHelpers
    {
        internal static Dictionary<string, IPropertySymbol> GetReadableProperties(ITypeSymbol type)
        {
            var result = new Dictionary<string, IPropertySymbol>();
            foreach (var t in SelfAndBases(type))
            {
                foreach (var prop in t.GetMembers().OfType<IPropertySymbol>())
                {
                    if (prop.IsStatic || prop.IsIndexer || prop.GetMethod is null) continue;
                    if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                    if (!result.ContainsKey(prop.Name)) result[prop.Name] = prop;
                }
            }
            return result;
        }

        internal static List<IPropertySymbol> GetMutableProperties(ITypeSymbol type)
        {
            var seen = new HashSet<string>();
            var result = new List<IPropertySymbol>();
            foreach (var t in SelfAndBases(type))
            {
                foreach (var prop in t.GetMembers().OfType<IPropertySymbol>())
                {
                    if (prop.IsStatic || prop.IsIndexer || prop.SetMethod is null) continue;
                    if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                    if (prop.SetMethod.DeclaredAccessibility != Accessibility.Public) continue;
                    if (prop.SetMethod.IsInitOnly) continue;
                    if (seen.Add(prop.Name)) result.Add(prop);
                }
            }
            return result;
        }

        internal static List<IPropertySymbol> GetInitOnlyProperties(ITypeSymbol type)
        {
            var seen = new HashSet<string>();
            var result = new List<IPropertySymbol>();
            foreach (var t in SelfAndBases(type))
            {
                foreach (var prop in t.GetMembers().OfType<IPropertySymbol>())
                {
                    if (prop.IsStatic || prop.IsIndexer || prop.SetMethod is null) continue;
                    if (prop.DeclaredAccessibility != Accessibility.Public) continue;
                    if (prop.SetMethod.DeclaredAccessibility != Accessibility.Public) continue;
                    if (!prop.SetMethod.IsInitOnly) continue;
                    if (seen.Add(prop.Name)) result.Add(prop);
                }
            }
            return result;
        }

        internal static IEnumerable<ITypeSymbol> SelfAndBases(ITypeSymbol type)
        {
            for (ITypeSymbol? t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
                yield return t;
        }

        internal static string ToGlobal(ITypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        internal static string ClassAccessibility(INamedTypeSymbol symbol) =>
            symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";
    }
}
