namespace LeadFinder.Api.Data.Entities;

public class Business
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public string City { get; set; } = "";
    public string? Keyword { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
