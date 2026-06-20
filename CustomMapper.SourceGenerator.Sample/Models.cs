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
