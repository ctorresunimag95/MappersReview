namespace CustomMapper.SourceGenerator.Tests;

public class MapperSnapshotTests
{
    private static readonly string _source = """
        using CustomMapper.SourceGenerator.Runtime;

        namespace CustomMapper.SourceGenerator.Tests
        {
            [Mapper]
            public partial class TestMapper
            {
                public partial UserDto Map(User value);

                private void ExtendMap(User source, UserDto destination) { }
            }

            public class User { public string Name { get; set; } public int Age { get; set; } }
            public class UserDto { public string Name { get; set; } public int Age { get; set; } }
        }
        """;

    [Fact]
    public void Generates_mapper_implementation()
    {
        var files = TestHelper.RunGenerator(_source);

        Assert.True(files.ContainsKey("CustomMapper.SourceGenerator.Tests.TestMapper.g.cs"),
            "Mapper implementation file was not generated.");

        var impl = files["CustomMapper.SourceGenerator.Tests.TestMapper.g.cs"];

        Assert.Contains("partial class TestMapper", impl);
        Assert.Contains("destination.Name = source.Name;", impl);
        Assert.Contains("destination.Age = source.Age;", impl);
        Assert.Contains("ExtendMap(source, destination);", impl);
    }

    [Fact]
    public void Generates_DI_registration()
    {
        var files = TestHelper.RunGenerator(_source);

        Assert.True(files.ContainsKey("GeneratedMapperRegistration.g.cs"),
            "DI registration file was not generated.");

        var reg = files["GeneratedMapperRegistration.g.cs"];

        Assert.Contains("AddGeneratedMappers", reg);
        Assert.Contains("TestMapper", reg);
    }

    [Fact]
    public void InitOnly_properties_use_object_initializer_syntax()
    {
        const string source = """
            using CustomMapper.SourceGenerator.Runtime;
            namespace T {
                [Mapper] public partial class M {
                    public partial Dst Map(Src s);
                }
                public class Src { public int X { get; set; } public int Y { get; set; } }
                public class Dst { public int X { get; init; } public int Y { get; init; } }
            }
            """;

        var files = TestHelper.RunGenerator(source);
        var impl = files["T.M.g.cs"];

        Assert.DoesNotContain("destination.X = source.X;", impl);
        Assert.Contains("new global::T.Dst", impl);
        Assert.Contains("X = source.X", impl);
        Assert.Contains("Y = source.Y", impl);
    }

    [Fact]
    public void Mutable_only_properties_use_statement_mode()
    {
        var files = TestHelper.RunGenerator(_source);
        var impl = files["CustomMapper.SourceGenerator.Tests.TestMapper.g.cs"];

        Assert.Contains("var destination = new", impl);
        Assert.Contains("destination.Name = source.Name;", impl);
    }

    [Fact]
    public void UseConstructor_generates_named_ctor_call()
    {
        const string source = """
            using CustomMapper.SourceGenerator.Runtime;
            namespace T {
                [Mapper] public partial class M {
                    [UseConstructor] public partial Dst Map(Src s);
                }
                public class Src { public int X { get; set; } public string Name { get; set; } }
                public class Dst {
                    public Dst(int x, string name) { X = x; Name = name; }
                    public int X { get; }
                    public string Name { get; }
                }
            }
            """;

        var files = TestHelper.RunGenerator(source);
        var impl = files["T.M.g.cs"];

        Assert.Contains("new global::T.Dst(", impl);
        Assert.Contains("x: source.X", impl);
        Assert.Contains("name: source.Name", impl);
        Assert.DoesNotContain("destination.X = source.X;", impl);
    }

    [Fact]
    public void UseConstructor_assigns_post_ctor_mutable_props()
    {
        const string source = """
            using CustomMapper.SourceGenerator.Runtime;
            namespace T {
                [Mapper] public partial class M {
                    [UseConstructor] public partial Dst Map(Src s);
                }
                public class Src { public int Id { get; set; } public string Tag { get; set; } }
                public class Dst {
                    public Dst(int id) { Id = id; }
                    public int Id { get; }
                    public string Tag { get; set; }
                }
            }
            """;

        var files = TestHelper.RunGenerator(source);
        var impl = files["T.M.g.cs"];

        Assert.Contains("id: source.Id", impl);
        Assert.Contains("destination.Tag = source.Tag;", impl);
    }

    [Fact]
    public void UseConstructor_emits_CMSG005_for_unmatched_parameter()
    {
        const string source = """
            using CustomMapper.SourceGenerator.Runtime;
            namespace T {
                [Mapper] public partial class M {
                    [UseConstructor] public partial Dst Map(Src s);
                }
                public class Src { public int Id { get; set; } }
                public class Dst {
                    public Dst(int id, string requiredName) { Id = id; }
                    public int Id { get; }
                }
            }
            """;

        var (_, diagnostics) = TestHelper.RunGeneratorWithDiagnostics(source);
        Assert.True(diagnostics.Any(d => d.Id == "CMSG005" && d.GetMessage().Contains("requiredName")),
            "CMSG005 diagnostic not found for unmatched constructor parameter 'requiredName'");
    }

    [Fact]
    public void UseConstructor_emits_CMSG006_for_stranded_initonly()
    {
        const string source = """
            using CustomMapper.SourceGenerator.Runtime;
            namespace T {
                [Mapper] public partial class M {
                    [UseConstructor] public partial Dst Map(Src s);
                }
                public class Src { public int Id { get; set; } }
                public class Dst {
                    public Dst(int id) { Id = id; }
                    public int Id { get; }
                    public string Extra { get; init; }
                }
            }
            """;

        var (_, diagnostics) = TestHelper.RunGeneratorWithDiagnostics(source);
        Assert.True(diagnostics.Any(d => d.Id == "CMSG006" && d.GetMessage().Contains("Extra")),
            "CMSG006 diagnostic not found for stranded init-only property 'Extra'");
    }

    [Fact]
    public void InitOnly_default_mode_compiles_without_CS8852()
    {
        const string source = """
            using CustomMapper.SourceGenerator.Runtime;
            namespace T {
                [Mapper] public partial class M {
                    public partial Dst Map(Src s);
                }
                public class Src { public int X { get; set; } }
                public class Dst { public int X { get; init; } }
            }
            """;

        var (_, diagnostics) = TestHelper.RunGeneratorWithDiagnostics(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "CS8852");
    }
}
