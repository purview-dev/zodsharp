// Packed-consumer gate, consumer (d): a minimal API over a referenced [ZodSchema] class library, with the OpenAPI
// XML-comment generator reading both projects' documentation. The script reads /openapi/v1.json, posts an invalid
// registration and registers a customer whose email is taken.
using Microsoft.AspNetCore.OpenApi;
using PackedContracts;
using PackedMultiApi;
using ZodSharp;
using ZodSharp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Both assemblies' generated registrations; ZodSharp as the Microsoft.Extensions.Validation resolver; the ZodSharp
// ProblemDetails contract; and the OpenAPI transformers.
builder.Services.AddZodSharp();
builder.Services.AddZodSharpPackedContractsSchemas();
builder.Services.AddZodSharpPackedMultiApiSchemas();
builder.Services.AddZodSharpValidation();
builder.Services.AddZodSharpProblemDetails();
builder.Services.AddOpenApi(static options => options.AddZodSharp());

ErrorTypeRegistry.Default.Register(CustomerErrors.CustomerExists);

var app = builder.Build();
app.UseExceptionHandler();
app.MapOpenApi();

app.MapPost("/customers", static (RegisterCustomer customer) =>
{
	// The [ErrorType] helper generated into the referenced library, raised from this assembly.
	if (string.Equals(customer.Email, "taken@example.com", StringComparison.OrdinalIgnoreCase))
		CustomerErrors.ThrowCustomerExists(customer.Email!);

	return TypedResults.Ok(new CustomerRegistered(customer.Email!, customer.DisplayName!));
});

await app.RunAsync();

namespace PackedMultiApi
{
	/// <summary>The acknowledgement returned once a customer is registered.</summary>
	/// <param name="Email">The registered email address.</param>
	/// <param name="DisplayName">The registered display name.</param>
	public sealed record CustomerRegistered(string Email, string DisplayName);

	/// <summary>A note this application validates itself, so both projects generate schemas.</summary>
	[ZodSchema]
	public sealed class AccountNote
	{
		/// <summary>The note text.</summary>
		[System.ComponentModel.DataAnnotations.Required]
		public string? Text { get; set; }
	}
}
