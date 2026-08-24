using Microsoft.EntityFrameworkCore;
using NonStop.Posture.Bot.Models;

namespace NonStop.Posture.Bot.Db;

public class PostureDbContext(IConfiguration configuration) : DbContext
{
    public DbSet<Subscriber> Subscribers { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("PostureDb") ?? configuration.GetConnectionString("PostureDb"));
    }
}