/*
Actions:
- Send only in work days (customizeble)
- Add settings for bot to schedule sending (every hour, twice a day)
- special messages in the morning and at the evening
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
var app = builder.Build();

app.Run();