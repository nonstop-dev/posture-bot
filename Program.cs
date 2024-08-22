/*
Actions:
- Send only in work days (customizeble)
- Add settings for bot to schedule sending (every hour, twice a day)
- Add changing the message
- Add timezone customization
- Add metrics: subscribers count

Later:
Localization

*/

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
var app = builder.Build();
app.Run();