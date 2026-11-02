using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PackedWasm;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The generated, trim-safe registration of this assembly's [ZodSchema] models, then ZodSharp for Blazor forms.
builder.Services.AddZodSharpPackedWasmSchemas();
builder.Services.AddZodSharpBlazor();

await builder.Build().RunAsync();
