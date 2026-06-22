using AutoMapper;
using BenchmarkDotNet.Attributes;
using Mapster;
using MappersComparasion.Mappers;
using MappersComparasion.Models;

namespace MappersComparasion.Benchmarks;

[MemoryDiagnoser]
public class MapperBenchmarks
{
    private List<User> _users = null!;
    private MapperlyUserMapper _mapperly = null!;
    private IMapper _autoMapper = null!;
    private CustomSourceGeneratorUserMapper _customSourceGenerator = null!;

    [Params(1, 100, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _users = Enumerable.Range(0, Count).Select(i => new User
        {
            Id = i,
            FirstName = "Jane",
            LastName = "Doe",
            BirthDate = new DateTime(1990, 1, 1),
            Address = new Address { Street = "123 Main St", City = "New York", Country = "US" }
        }).ToList();

        _mapperly = new MapperlyUserMapper();
        _customSourceGenerator = new CustomSourceGeneratorUserMapper();

        TypeAdapterConfig<User, UserDto>.NewConfig()
            .Map(d => d.City, s => s.Address.City);

        _autoMapper = new MapperConfiguration(cfg => cfg.AddProfile<UserMappingProfile>()).CreateMapper();
    }

    [Benchmark(Baseline = true)]
    public List<UserDto> Manual()
        => _users.Select(ManualMapper.Map).ToList();

    [Benchmark]
    public List<UserDto> Mapster()
        => _users.Adapt<List<UserDto>>();

    [Benchmark]
    public List<UserDto> AutoMapper()
        => _autoMapper.Map<List<UserDto>>(_users);

    [Benchmark]
    public List<UserDto> Mapperly()
        => _users.Select(_mapperly.Map).ToList();

    [Benchmark]
    public List<FacetUserDto> Facet()
        => _users.Select(u => new FacetUserDto(u)).ToList();

    [Benchmark]
    public List<UserDto> CustomSourceGenerator()
        => _users.Select(_customSourceGenerator.Map).ToList();
}
