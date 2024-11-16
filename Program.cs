using NonStop.SitUpStraight.Bot.BackgroundServices;
using NonStop.SitUpStraight.Bot.Db;
using NonStop.SitUpStraight.Bot.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();
builder.Services.AddHostedService<SitUpStraightService>();
builder.Services.AddDbContext<SitUpStraightDbContext>();
builder.Services.AddSingleton<ITimezonesService, TimezonesService>();
builder.Services.AddSingleton<IMarkupService, MarkupService>();
var app = builder.Build();

app.Run();