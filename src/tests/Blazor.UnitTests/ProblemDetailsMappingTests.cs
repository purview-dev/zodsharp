using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.AspNetCore;
using ZodSharp.Blazor.Fixtures;
using ZodSharp.Core;
using ZodSharp.Http;
using static ZodSharp.Blazor.Fixtures.Errors;

namespace ZodSharp.Blazor;

/// <summary>
/// Server validation problems mapped onto forms with <c>AddZodProblemDetails</c>. Reading them
/// (<c>TryReadZodValidationProblemAsync</c>) is covered by Purview.ZodSharp.Http's tests.
/// </summary>
public class ProblemDetailsMappingTests
{
	[Test]
	public async Task AddZodProblemDetails_GivenServerResponse_PlacesIssuesBySegments(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		Order order = new() { Lines = [new(), new()] };
		await using var app = await StartServerAsync(
			ValidationResult<Order>.Failure([
				ValidationError.Create(
					"too_small",
					"Quantity must be at least 1",
					ZodPath.From(M("Lines"), I(1), M("Quantity")),
					origin: "number",
					minimum: 1,
					inclusive: true,
					category: "too_small"
				),
				At("invalid_string", "Email is invalid", M("Customer"), M("Email")),
			]),
			cancellationToken
		);
		using var client = app.GetTestClient();
		using var response = await client.PostAsJsonAsync("/orders", new { }, cancellationToken);
		EditContext editContext = new(order);

		// Act
		var problem = await response.TryReadZodValidationProblemAsync(cancellationToken);
		editContext.AddZodProblemDetails(problem!);

		// Assert
		await Assert.That(problem).IsNotNull();
		await Assert.That(problem!.Issues).IsNotNull();
		FieldIdentifier quantity = new(order.Lines[1], nameof(OrderLine.Quantity));
		await Assert.That(editContext.GetValidationMessages(quantity)).IsEquivalentTo(["Quantity must be at least 1"]);
		var error = editContext.GetZodErrors(quantity).Single();
		await Assert.That(error.Code).IsEqualTo("too_small");
		await Assert.That(error.Origin).IsEqualTo("number");
		await Assert.That(error.Lower?.Inclusive).IsTrue();
		await Assert
			.That(Convert.ToInt32(error.Lower?.Value, System.Globalization.CultureInfo.InvariantCulture))
			.IsEqualTo(1);
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.Customer, nameof(Customer.Email))))
			.IsEquivalentTo(["Email is invalid"]);
	}

	[Test]
	public async Task AddZodProblemDetails_GivenOnlyDottedErrors_ParsesTheKeys()
	{
		// Arrange
		Order order = new() { Lines = [new()] };
		EditContext editContext = new(order);
		ZodValidationProblemDetails problem = new()
		{
			Errors = new Dictionary<string, string[]>
			{
				["Lines[0].Sku"] = ["Sku is required"],
				["Reference"] = ["Reference is required"],
				[""] = ["Order is invalid"],
			},
		};

		// Act
		editContext.AddZodProblemDetails(problem);

		// Assert
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.Lines[0], nameof(OrderLine.Sku))))
			.IsEquivalentTo(["Sku is required"]);
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, nameof(Order.Reference))))
			.IsEquivalentTo(["Reference is required"]);
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, string.Empty)))
			.IsEquivalentTo(["Order is invalid"]);
		await Assert.That(editContext.GetZodErrors(() => order.Reference).Single().Code).IsEqualTo("custom");
	}

	[Test]
	public async Task AddZodProblemDetails_GivenCamelCasedSegmentsAndDictionaryKey_PlacesOnClrFields()
	{
		// Arrange
		Order order = new() { ByCode = new() { ["abc"] = new() } };
		EditContext editContext = new(order);
		ZodValidationProblemDetails problem = new()
		{
			Issues =
			[
				new()
				{
					Path = "byCode.abc.sku",
					Segments = [Json("\"byCode\""), Json("\"abc\""), Json("\"sku\"")],
					Code = "too_small",
					Message = "Sku is required",
				},
			],
		};

		// Act
		editContext.AddZodProblemDetails(problem);

		// Assert
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.ByCode["abc"], nameof(OrderLine.Sku))))
			.IsEquivalentTo(["Sku is required"]);
	}

	[Test]
	public async Task AddZodErrors_GivenErrorsFromOutsideTheForm_PlacesThemAndKeepsTheFullErrors()
	{
		// Arrange
		Order order = new() { Lines = [new()] };
		EditContext editContext = new(order);
		ValidationError[] errors =
		[
			At("too_small", "Sku is required", M("Lines"), I(0), M("Sku")),
			At("custom", "Order is invalid"),
		];

		// Act
		editContext.AddZodErrors(errors);

		// Assert
		FieldIdentifier sku = new(order.Lines[0], nameof(OrderLine.Sku));
		await Assert.That(editContext.GetValidationMessages(sku)).IsEquivalentTo(["Sku is required"]);
		await Assert.That(editContext.GetZodErrors(sku).Single().Code).IsEqualTo("too_small");
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, string.Empty)))
			.IsEquivalentTo(["Order is invalid"]);
	}

	[Test]
	public async Task AddZodErrors_GivenEarlierProblemDetails_ReplacesThem()
	{
		// Arrange
		Order order = new();
		EditContext editContext = new(order);
		editContext.AddZodProblemDetails(
			new() { Errors = new Dictionary<string, string[]> { ["Reference"] = ["Reference is taken"] } }
		);

		// Act
		editContext.AddZodErrors([At("custom", "Email is invalid", M("Customer"), M("Email"))]);

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Email is invalid"]);
	}

	[Test]
	public async Task AddZodProblemDetails_GivenFieldChange_ClearsOnlyThatFieldsServerErrors()
	{
		// Arrange
		Order order = new();
		EditContext editContext = new(order);
		editContext.AddZodProblemDetails(
			new()
			{
				Errors = new Dictionary<string, string[]>
				{
					["Reference"] = ["Reference is taken"],
					["Customer.Email"] = ["Email is taken"],
				},
			}
		);

		// Act
		editContext.NotifyFieldChanged(new FieldIdentifier(order, nameof(Order.Reference)));

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Email is taken"]);
		await Assert.That(editContext.GetZodErrors().Single().Message).IsEqualTo("Email is taken");
	}

	[Test]
	public async Task AddZodProblemDetails_GivenClearOnFieldChangeDisabled_KeepsServerErrors()
	{
		// Arrange
		Order order = new();
		EditContext editContext = new(order);
		editContext.AddZodProblemDetails(
			new() { Errors = new Dictionary<string, string[]> { ["Reference"] = ["Reference is taken"] } },
			clearOnFieldChange: false
		);

		// Act
		editContext.NotifyFieldChanged(new FieldIdentifier(order, nameof(Order.Reference)));

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["Reference is taken"]);
	}

	[Test]
	public async Task AddZodProblemDetails_GivenSecondProblem_ReplacesTheFirst()
	{
		// Arrange
		Order order = new();
		EditContext editContext = new(order);
		editContext.AddZodProblemDetails(
			new() { Errors = new Dictionary<string, string[]> { ["Reference"] = ["first"] } }
		);

		// Act
		editContext.AddZodProblemDetails(
			new() { Errors = new Dictionary<string, string[]> { ["Customer.Email"] = ["second"] } }
		);

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEquivalentTo(["second"]);
	}

	[Test]
	public async Task AddZodProblemDetails_GivenSuppliedStore_AddsMessagesToIt()
	{
		// Arrange
		Order order = new();
		EditContext editContext = new(order);
		ValidationMessageStore store = new(editContext);

		// Act
		editContext.AddZodProblemDetails(
			new() { Errors = new Dictionary<string, string[]> { ["Reference"] = ["Reference is taken"] } },
			store
		);
		store.Clear();

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEmpty();
	}

	[Test]
	public async Task ClearZodErrors_GivenServerErrors_RemovesThem()
	{
		// Arrange
		Order order = new();
		EditContext editContext = new(order);
		editContext.AddZodProblemDetails(
			new() { Errors = new Dictionary<string, string[]> { ["Reference"] = ["Reference is taken"] } }
		);

		// Act
		editContext.ClearZodErrors();

		// Assert
		await Assert.That(editContext.GetValidationMessages()).IsEmpty();
		await Assert.That(editContext.GetZodErrors()).IsEmpty();
	}

	static System.Text.Json.JsonElement Json(string json) => System.Text.Json.JsonElement.Parse(json);

	static async Task<WebApplication> StartServerAsync(
		ValidationResult<Order> result,
		CancellationToken cancellationToken
	)
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseTestServer();
		builder.Services.AddZodSharpProblemDetails();
		var app = builder.Build();
		app.MapPost("/orders", (HttpContext context) => ZodTypedResults.ValidationProblem(result, context));
		await app.StartAsync(cancellationToken);
		return app;
	}
}
