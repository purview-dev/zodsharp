using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
using ZodSharp.Blazor.Fixtures;
using ZodSharp.Extensions.Validation;

namespace ZodSharp.Blazor;

/// <summary>
/// Forms over real <c>[ZodSchema]</c> models, registered through the generated, trim-safe registration - including the
/// headline WebAssembly guarantee: nested validation with no <c>[ValidatableType]</c> and no
/// Microsoft.Extensions.Validation generator.
/// </summary>
public class GeneratedSchemaTests
{
	[Test]
	public async Task DataAnnotationsValidator_GivenNestedZodSchemaModelWithoutValidationsGenerator_ValidatesTheWholeGraph()
	{
		// Arrange
		GeneratedPerson person = new()
		{
			Name = "ab",
			Address = new() { Street = null },
			PreviousAddresses = [new() { Street = "1 High St" }, new() { Street = null }],
		};
		await using var context = CreateContext();
		EditContext editContext = new(person);
		var form = context.Render(editContext, FormHost.DataAnnotations());

		// Act
		var valid = await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(valid).IsFalse();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(person, nameof(GeneratedPerson.Name))))
			.IsNotEmpty();
		await Assert
			.That(
				editContext.GetValidationMessages(new FieldIdentifier(person.Address, nameof(GeneratedAddress.Street)))
			)
			.IsNotEmpty();
		await Assert
			.That(
				editContext.GetValidationMessages(
					new FieldIdentifier(person.PreviousAddresses[1], nameof(GeneratedAddress.Street))
				)
			)
			.IsNotEmpty();
		await Assert
			.That(
				editContext.GetValidationMessages(
					new FieldIdentifier(person.PreviousAddresses[0], nameof(GeneratedAddress.Street))
				)
			)
			.IsEmpty();
		await Assert.That(editContext.GetValidationMessages().Count()).IsEqualTo(3);
	}

	[Test]
	public async Task Compilation_GivenThisTestAssembly_HasNoValidationsGeneratorOutputOrValidatableTypes()
	{
		// Arrange
		var assembly = typeof(GeneratedSchemaTests).Assembly;
		await using var context = CreateContext();

		// Act
		var generated = assembly
			.GetTypes()
			.Where(static t => t.FullName?.Contains("ValidatableInfoResolver", StringComparison.Ordinal) == true)
			.ToArray();
		var validatable = assembly.GetTypes().Where(static t => t.IsDefined(typeof(ValidatableTypeAttribute), false));
		var resolvers = context.Services.GetRequiredService<IOptions<ValidationOptions>>().Value.Resolvers;

		// Assert
		await Assert.That(generated).IsEmpty();
		await Assert.That(validatable).IsEmpty();
		await Assert.That(resolvers.Count).IsEqualTo(1);
		await Assert.That(resolvers[0]).IsTypeOf<ZodValidatableInfoResolver>();
	}

	[Test]
	public async Task DataAnnotationsValidator_GivenNestedCollectionAndDictionaryErrors_PlacesEachOnItsOwningField()
	{
		// Arrange
		GeneratedOrder order = new()
		{
			Reference = "ORD-1",
			Customer = new() { Email = "not-an-email" },
			Lines = [new() { Sku = "A" }, new() { Sku = "B", Quantity = 0 }],
			ByCode = new()
			{
				["a"] = new() { Sku = "A" },
				["b"] = new() { Sku = null },
			},
		};
		await using var context = CreateContext();
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.DataAnnotations());

		// Act
		await form.ValidateAsync(editContext);

		// Assert
		await Assert
			.That(
				editContext.GetValidationMessages(new FieldIdentifier(order.Customer, nameof(GeneratedCustomer.Email)))
			)
			.IsNotEmpty();
		await Assert
			.That(
				editContext.GetValidationMessages(new FieldIdentifier(order.Lines[1], nameof(GeneratedLine.Quantity)))
			)
			.IsNotEmpty();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.ByCode["b"], nameof(GeneratedLine.Sku))))
			.IsNotEmpty();
		await Assert.That(editContext.GetValidationMessages().Count()).IsEqualTo(3);
	}

	[Test]
	public async Task DataAnnotationsValidator_GivenFieldChange_RunsOnlyThatMembersOwnRules()
	{
		// Arrange
		GeneratedOrder order = new()
		{
			Reference = "bad",
			Customer = new() { Email = "not-an-email" },
		};
		await using var context = CreateContext();
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.DataAnnotations());
		FieldIdentifier email = new(order.Customer, nameof(GeneratedCustomer.Email));

		// Act
		await form.ChangeAsync(editContext, email);

		// Assert
		await Assert.That(editContext.GetValidationMessages(email)).IsNotEmpty();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order, nameof(GeneratedOrder.Reference))))
			.IsEmpty();
	}

	[Test]
	public async Task ZodValidator_GivenGeneratedModel_KeepsCodesAndPlacesNestedErrors()
	{
		// Arrange
		GeneratedOrder order = new() { Reference = "bad", Lines = [new() { Sku = "A", Quantity = 500 }] };
		await using var context = CreateContext();
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());

		// Act
		await form.ValidateAsync(editContext);

		// Assert
		var reference = editContext.GetZodErrors(() => order.Reference);
		await Assert.That(reference.Select(static e => e.Code)).Contains("invalid_string");
		var quantity = editContext.GetZodErrors(new FieldIdentifier(order.Lines[0], nameof(GeneratedLine.Quantity)));
		await Assert.That(quantity.Single().Category).IsEqualTo("too_big");
	}

	[Test]
	public async Task ZodValidator_GivenFieldChangeOnGeneratedModel_UsesMemberValidation()
	{
		// Arrange
		GeneratedOrder order = new() { Reference = "bad", Lines = [new() { Sku = null, Quantity = 0 }] };
		await using var context = CreateContext();
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.Zod());
		FieldIdentifier quantity = new(order.Lines[0], nameof(GeneratedLine.Quantity));

		// Act
		await form.ChangeAsync(editContext, quantity);

		// Assert
		await Assert.That(editContext.GetValidationMessages(quantity)).IsNotEmpty();
		await Assert.That(editContext.GetValidationMessages().Count()).IsEqualTo(1);
	}

	[Test]
	public async Task AddZodProblemDetails_GivenGeneratedModel_ResolvesPathsWithTheGeneratedResolver()
	{
		// Arrange
		GeneratedOrder order = new() { Lines = [new(), new()] };
		await using var context = CreateContext();
		EditContext editContext = new(order);
		context.Render(editContext, FormHost.Zod());
		ZodValidationProblemDetails problem = new()
		{
			Errors = new Dictionary<string, string[]> { ["Lines[1].Sku"] = ["Sku is required"] },
		};

		// Act
		editContext.AddZodProblemDetails(problem);

		// Assert
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(order.Lines[1], nameof(GeneratedLine.Sku))))
			.IsEquivalentTo(["Sku is required"]);
	}

	[Test]
	public async Task DataAnnotationsValidator_GivenValidGeneratedModel_IsValid()
	{
		// Arrange
		GeneratedOrder order = new()
		{
			Reference = "ORD-1",
			Customer = new() { Email = "someone@example.com" },
			Lines = [new() { Sku = "A", Quantity = 5 }],
		};
		await using var context = CreateContext();
		EditContext editContext = new(order);
		var form = context.Render(editContext, FormHost.DataAnnotations());

		// Act
		var valid = await form.ValidateAsync(editContext);

		// Assert
		await Assert.That(editContext.IsValidationFaulted()).IsFalse();
		await Assert.That(valid).IsTrue();
	}

	static BunitContext CreateContext() =>
		FormHost.CreateContext(static factory =>
			Generated.PurviewZodSharpBlazorUnitTestsZodSharpSchemas.Register(factory)
		);
}
