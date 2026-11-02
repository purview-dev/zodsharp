// The slim builder is the Native AOT-friendly host. Everything the sample registers and maps lives in
// SampleApp, so the smoke tests start exactly the same application in memory.
var builder = WebApplication.CreateSlimBuilder(args);
SampleApp.ConfigureServices(builder);

var app = builder.Build();
SampleApp.Configure(app);

await app.RunAsync();
