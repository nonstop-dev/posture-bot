/*
Actions:
+ 1. Send first message after bot starts: "Выпрями спину!"
+ 2. Send the same every hour (by timer). Like 9:00 am, 10:00 am etc.
3. Send only in work days
4. Add settings for bot to schedule sending
5. Add changing the message
6. Add timezone customization

Later:
Localization

*/

using NonStop.SitUpStraight.Bot;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<SitUpStraightService>();
var app = builder.Build();
app.Run();