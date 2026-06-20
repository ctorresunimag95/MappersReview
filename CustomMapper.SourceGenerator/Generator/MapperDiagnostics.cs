using Microsoft.CodeAnalysis;

namespace CustomMapper.SourceGenerator.Generator
{
    internal static class MapperDiagnostics
    {
        private const string Category = "CustomMapper.SourceGenerator";

        public static readonly DiagnosticDescriptor NotPartial = new(
            id: "CMSG001",
            title: "[Mapper] class must be partial",
            messageFormat: "Class '{0}' is decorated with [Mapper] but is not declared 'partial'; the generator cannot implement its mapping methods",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidMethodSignature = new(
            id: "CMSG002",
            title: "Unsupported mapping method signature",
            messageFormat: "Partial method '{0}' is not a supported mapping method; expected a non-void return type and exactly one source parameter",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnmappedProperty = new(
            id: "CMSG003",
            title: "Destination property not auto-mapped",
            messageFormat: "Destination property '{0}.{1}' was not auto-mapped; no source property with an exact matching name and type was found. Assign it in ExtendMap if required.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidExtendMap = new(
            id: "CMSG004",
            title: "ExtendMap has an invalid signature",
            messageFormat: "A method named 'ExtendMap' was found on '{0}' but does not match the expected signature 'void ExtendMap({1} source, {2} destination)'; it will not be invoked",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
