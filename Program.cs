/*
Actions:
- delete my logs
- feedback form
- sending a quick message to bot
- delete cache (?)
- customize start and end hour for sending
- sometimes send special messages
- Add timezone customization
- Add metrics: subscribers count
- Localization
- Resolve all todos in the code
*/

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