using System.Collections.Immutable;
using CustomMapper.SourceGenerator.Generator;
using CustomMapper.SourceGenerator.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CustomMapper.SourceGenerator.Tests;

public static class TestHelper
{
    public static Dictionary<string, string> RunGenerator(string source)
    {
        var (files, _) = RunGeneratorWithDiagnostics(source);
        return files;
    }

    public static (Dictionary<string, string> Files, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithDiagnostics(string source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(MapperAttribute).Assembly.Location),
        };

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new MapperGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);

        var runResult = driver.GetRunResult();
        var files = runResult
            .GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());

        var allDiagnostics = runResult.Diagnostics;

        return (files, allDiagnostics);
    }
}
