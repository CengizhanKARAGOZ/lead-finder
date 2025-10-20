namespace LeadFinder.Api.Data.Entities;

public class LeadScore
{
    public long Id { get; set; }
    public long? BusinessId { get; set; }
    public long? WebsiteId { get; set; }
    public int Score { get; set; }
    public string ReasonsJson { get; set; } = "[]";
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}

