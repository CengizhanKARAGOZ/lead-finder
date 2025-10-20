using LeadFinder.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Website> Websites => Set<Website>();
    public DbSet<LeadScore> LeadScores => Set<LeadScore>();
}