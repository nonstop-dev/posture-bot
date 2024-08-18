/*
Actions:
- Save subscribers into db and restore after bot's restart
3. Send only in work days
4. Add settings for bot to schedule sending
5. Add changing the message
6. Add timezone customization

Later:
Localization

*/

using NonStop.SitUpStraight.Bot;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();
builder.Services.AddHostedService<SitUpStraightService>();
var app = builder.Build();
app.Run();