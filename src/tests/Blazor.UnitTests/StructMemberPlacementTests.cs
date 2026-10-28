using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ZodSharp.Blazor.Fixtures;
using static ZodSharp.Blazor.Fixtures.Errors;

namespace ZodSharp.Blazor;

/// <summary>
/// An error inside a struct member (<c>Email.Value</c>, where <c>Email</c> is a value object) belongs to the struct's
/// nearest reference-type owner: a boxed copy of the struct cannot be a <see cref="FieldIdentifier"/> model, so the
/// message is placed on <c>FieldIdentifier(model, "Email")</c>, next to the input bound to <c>Email</c>. Both routes are
/// covered Server-style (the reflection fallback available) and WebAssembly-style (the
/// <c>ZodSharp.Reflection.IsSupported</c> switch off, as trimming sets it).
/// </summary>
public class StructMemberPlacementTests
{
	const string ReflectionSwitch = "ZodSharp.Reflection.IsSupported";

	[Test]
	public async Task DataAnnotationsValidator_GivenInvalidValueObjectMember_ServerStyle_PlacesTheMessageOnTheOwningMember(
		CancellationToken cancellationToken
	) => await AssertPlacedOnOwningMembersAsync(FormHost.DataAnnotations(), cancellationToken);

	[Test]
	[NotInParallel]
	public async Task DataAnnotationsValidator_GivenInvalidValueObjectMember_WebAssemblyStyle_PlacesTheMessageOnTheOwningMember(
		CancellationToken cancellationToken
	)
	{
		using var trimmed = WithoutReflection();
		await AssertPlacedOnOwningMembersAsync(FormHost.DataAnnotations(), cancellationToken);
	}

	[Test]
	public async Task ZodValidator_GivenInvalidValueObjectMember_ServerStyle_PlacesTheMessageOnTheOwningMember(
		CancellationToken cancellationToken
	) => await AssertPlacedOnOwningMembersAsync(FormHost.Zod(), cancellationToken);

	[Test]
	[NotInParallel]
	public async Task ZodValidator_GivenInvalidValueObjectMember_WebAssemblyStyle_PlacesTheMessageOnTheOwningMember(
		CancellationToken cancellationToken
	)
	{
		using var trimmed = WithoutReflection();
		await AssertPlacedOnOwningMembersAsync(FormHost.Zod(), cancellationToken);
	}

	[Test]
	public async Task DataAnnotationsValidator_GivenValidatorWithoutMemberResolution_WalksToTheOwningMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a validator that does not resolve members, so placement goes through the reflection walk.
		SignUp model = new();
		FakeValidator<SignUp> validator = new()
		{
			Errors = static _ => [At("invalid_string", "Email is invalid", M("Email"), M("Value"))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(model);
		var form = context.Render(editContext, FormHost.DataAnnotations());

		// Act
		var valid = await form.ValidateAsync(editContext);

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		await Assert.That(valid).IsFalse();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(model, nameof(SignUp.Email))))
			.IsEquivalentTo(["Email is invalid"]);
	}

	[Test]
	public async Task ZodValidator_GivenValidatorWithoutMemberResolution_WalksToTheOwningMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		SignUp model = new();
		FakeValidator<SignUp> validator = new()
		{
			Errors = static _ => [At("too_big", "Latitude is out of range", M("Location"), M("Latitude"))],
		};
		await using var context = FormHost.CreateContext(factory => factory.Register(validator));
		EditContext editContext = new(model);
		var form = context.Render(editContext, FormHost.Zod());

		// Act
		await form.ValidateAsync(editContext);

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(model, nameof(SignUp.Location))))
			.IsEquivalentTo(["Latitude is out of range"]);
	}

	[Test]
	public async Task AddZodProblemDetails_GivenAnIssueInsideAValueObject_PlacesItOnTheOwningMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		SignUp model = new();
		await using var context = CreateContext();
		EditContext editContext = new(model);
		context.Render(editContext, FormHost.Zod());
		ZodValidationProblemDetails problem = new()
		{
			Errors = new Dictionary<string, string[]> { ["Email.Value"] = ["That address is taken"] },
		};

		// Act
		editContext.AddZodProblemDetails(problem);

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		await Assert
			.That(editContext.GetValidationMessages(new FieldIdentifier(model, nameof(SignUp.Email))))
			.IsEquivalentTo(["That address is taken"]);
	}

	static async Task AssertPlacedOnOwningMembersAsync(RenderFragment validator, CancellationToken cancellationToken)
	{
		// Arrange
		SignUp model = new()
		{
			Email = new() { Value = "not-an-email" },
			BackupEmail = new() { Value = null },
			Location = new() { Latitude = 120 },
		};
		await using var context = CreateContext();
		EditContext editContext = new(model);
		var form = context.Render(editContext, validator);

		// Act
		var valid = await form.ValidateAsync(editContext);

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		var email = editContext.GetValidationMessages(new FieldIdentifier(model, nameof(SignUp.Email))).ToArray();
		var backup = editContext
			.GetValidationMessages(new FieldIdentifier(model, nameof(SignUp.BackupEmail)))
			.ToArray();
		var location = editContext.GetValidationMessages(new FieldIdentifier(model, nameof(SignUp.Location))).ToArray();
		string[] all = [.. editContext.GetValidationMessages()];

		await Assert.That(valid).IsFalse();

		// Each message sits next to the member's input, and none is lost or left only in the summary.
		await Assert.That(email).HasSingleItem();
		await Assert.That(backup).HasSingleItem();
		await Assert.That(location).HasSingleItem();
		await Assert.That(all.Length).IsEqualTo(3);

		// A scalar value object's messages name the owning member (its display name), never its own 'Value'; a struct
		// that is not a scalar keeps its member's name.
		await Assert.That(email[0]).Contains("Email");
		await Assert.That(backup[0]).Contains("Backup email");
		await Assert.That(all.Any(static message => message.Contains("Value", StringComparison.Ordinal))).IsFalse();
		await Assert.That(location[0]).Contains("Latitude");
	}

	static BunitContext CreateContext() =>
		FormHost.CreateContext(static factory =>
			ZodSharp.Generated.PurviewZodSharpBlazorUnitTestsZodSharpSchemas.Register(factory)
		);

	// The switch trimming sets: the reflection fallbacks are unavailable, as in a Blazor WebAssembly client. Tests that
	// use it run on their own ([NotInParallel]), because the switch is process-wide.
	static Restore WithoutReflection()
	{
		AppContext.SetSwitch(ReflectionSwitch, false);
		return new();
	}

	readonly struct Restore : IDisposable
	{
		public void Dispose() => AppContext.SetSwitch(ReflectionSwitch, true);
	}
}
