using Facet;

namespace MappersComparasion.Models;

public class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string City { get; set; } = "";

    public override string ToString()
    {
        return $"{Id} - {FirstName} {LastName} from {City}";
    }

}

public class FacetUserMapConfiguration
{
    public static void Map(User source, FacetUserDto target)
    {
        target.City = source.Address.City;
    }
}

[Facet(typeof(User), Configuration = typeof(FacetUserMapConfiguration))]
public partial class FacetUserDto
{
    public string City { get; set; } = "";
}
