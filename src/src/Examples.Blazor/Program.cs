var builder = WebApplication.CreateBuilder(args);
SampleApp.ConfigureServices(builder.Services);

var app = builder.Build();
SampleApp.Configure(app);

await app.RunAsync();
