using Microsoft.EntityFrameworkCore;
using NonStop.SitUpStraight.Bot.Models;

namespace NonStop.SitUpStraight.Bot.Db;

public class SitUpStraightDbContext(IConfiguration configuration) : DbContext
{
    public DbSet<Subscriber> Subscribers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("SitUpStraightDb"));
    }
}