namespace LeadFinder.Api.Data.Entities;

public class Website
{
    public long Id { get; set; }
    public long? BusinessId { get; set; }
    public string Domain { get; set; } = "";
    public string HomepageUrl { get; set; } = "";
    public string? EmailsCsv { get; set; }
    public string? PhonesCsv { get; set; }
    public Business? Business { get; set; }
}