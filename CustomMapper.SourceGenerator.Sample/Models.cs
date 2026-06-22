namespace Sample.Models;

// Flat source model — no nested members, so v1 exact-name/exact-type mapping covers it fully.
public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public class CustomerDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";

    // Not present on the source — filled in by ExtendMap (expect a CMSG003 warning for this property).
    public string DisplayName { get; set; } = "";

    public override string ToString() => $"{Id}: {DisplayName} <{Email}>";
}

public class OrderLine
{
    public int ProductId { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderLineDto
{
    public int ProductId { get; init; }
    public decimal UnitPrice { get; init; }
}

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class AddressDto
{
    public AddressDto(string street, string city, string zip)
    {
        Street = street;
        City = city;
        Zip = zip;
    }

    public string Street { get; }
    public string City { get; }
    public string Zip { get; }
}
