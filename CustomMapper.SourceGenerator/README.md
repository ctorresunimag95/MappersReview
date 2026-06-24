# CustomMapper.SourceGenerator

A very small and simple way to use this package is:

## 1. Reference the generator

In the project that should use the mapper, add a reference to the generator project as an analyzer:

```xml
<ItemGroup>
  <ProjectReference Include="..\CustomMapper.SourceGenerator\CustomMapper.SourceGenerator.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="true" />
</ItemGroup>
```

## 2. Create a partial mapper class

```csharp
using CustomMapper.SourceGenerator.Runtime;

[Mapper]
public partial class CustomerMapper
{
    public partial CustomerDto Map(Customer source);
}
```

That is enough for the generator to create the mapping implementation.

## 3. Optional: customize the result

If you need extra logic after the normal property copy:

```csharp
[Mapper]
public partial class CustomerMapper
{
    public partial CustomerDto Map(Customer source);

    private void ExtendMap(Customer source, CustomerDto destination)
    {
        destination.FullName = $"{source.FirstName} {source.LastName}";
    }
}
```

## 4. Optional: use constructor mapping

```csharp
[Mapper]
public partial class AddressMapper
{
    [UseConstructor]
    public partial AddressDto MapAddress(Address source);
}
```

## 5. Optional: ignore specific properties

You can skip mapping for selected destination properties with the ignore attribute:

```csharp
using CustomMapper.SourceGenerator.Runtime;

[Mapper]
[MapperIgnore(typeof(CustomerDto), nameof(CustomerDto.InternalNote))]
public partial class CustomerMapper
{
    public partial CustomerDto Map(Customer source);
}
```

You can also configure ignored properties from a `ConfigureMapper` method if you prefer:

```csharp
using CustomMapper.SourceGenerator.Runtime;

[Mapper]
public partial class CustomerMapper
{
    public partial CustomerDto Map(Customer source);

    private void ConfigureMapper(MapperConfig config)
    {
        config.Ignore<CustomerDto>(nameof(CustomerDto.InternalNote));
    }
}
```

## 6. Use it

You can then call the generated method directly:

```csharp
var mapper = new CustomerMapper();
var dto = mapper.Map(customer);
```

If you want, this package can also be used together with the runtime registration helpers for DI-based usage.
