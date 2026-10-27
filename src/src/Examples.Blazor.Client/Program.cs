using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ZodSharp.Examples;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Posts go to the server project that hosts this client (the sample's /api endpoints).
builder.Services.AddScoped(static sp => new HttpClient
{
	BaseAddress = new Uri(sp.GetRequiredService<IWebAssemblyHostEnvironment>().BaseAddress),
});

// The registry the service-aware async email-uniqueness rule on RegisterCustomerRequest resolves. In the browser it
// asks the server; on the server it is the in-memory store itself.
builder.Services.AddScoped<ICustomerEmailRegistry, HttpCustomerEmailRegistry>();

// The same two calls as the server Program.cs: the generated, trim-safe registration of the Examples.Shared
// [ZodSchema] models (no assembly scanning), then ZodSharp for Blazor forms.
builder.Services.AddZodSharpZodSharpExamplesSharedSchemas();
builder.Services.AddZodSharpBlazor();

// Validation messages in the selected culture (English or French).
SampleLocalization.AddSampleLocalization(builder.Services);

var host = builder.Build();

// Adopt the culture the server rendered the page in before the app starts: RunAsync then loads the matching
// satellite assemblies (SampleMessages.fr.resx).
await SampleLocalization.ApplyDocumentCultureAsync(host.Services);

await host.RunAsync();
