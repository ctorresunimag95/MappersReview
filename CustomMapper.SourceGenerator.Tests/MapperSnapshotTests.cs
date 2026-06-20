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
}
