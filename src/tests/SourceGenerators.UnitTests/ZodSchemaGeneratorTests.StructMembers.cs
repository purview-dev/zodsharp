using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	const string StructMemberSource =
		AutomaticScalarPrelude
		+ """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar]
				[ZodSchema]
				public readonly partial record struct EmailAddress
				{
					[Required]
					[MinLength(3)]
					public string? Value { get; init; }
				}

				[ZodSchema]
				public partial record struct Coordinates
				{
					[Range(-90, 90)]
					public double Latitude { get; set; }
				}

				[ZodSchema]
				public sealed class SignUp
				{
					public EmailAddress Email { get; set; }

					[Display(Name = "Backup email")]
					public EmailAddress? BackupEmail { get; set; }

					public Coordinates Location { get; set; }

					public System.Collections.Generic.List<Coordinates> Stops { get; set; } = [];
				}
			}
			""";

	[Test]
	[Arguments("Email.Value", "Email")]
	[Arguments("BackupEmail.Value", "BackupEmail")]
	[Arguments("Location.Latitude", "Location")]
	[Arguments("Stops[1].Latitude", "Stops")]
	public async Task TryResolveMember_GivenPathInsideAStructMember_ResolvesToTheReferenceTypeOwner(
		string path,
		string expectedMember,
		CancellationToken cancellationToken
	)
	{
		// Arrange - a boxed copy of a struct cannot own a field, so the nearest reference-type owner does.
		using var driverResult = await GenerateAsync(
			StructMemberSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.SignUp")!;
		var model = Activator.CreateInstance(modelType)!;
		var coordinatesType = assembly.GetType("Testing.Coordinates")!;
		var stops = (System.Collections.IList)modelType.GetProperty("Stops")!.GetValue(model)!;
		stops.Add(Activator.CreateInstance(coordinatesType));
		stops.Add(Activator.CreateInstance(coordinatesType));
		var emailType = assembly.GetType("Testing.EmailAddress")!;
		modelType.GetProperty("BackupEmail")!.SetValue(model, Activator.CreateInstance(emailType));
		var resolve = assembly
			.GetType("Testing.SignUpSchema")!
			.GetMethods()
			.Single(static method => method.Name == "TryResolveMember" && method.GetParameters().Length == 4);
		object?[] arguments = [model, Core.ZodPath.ParseDotted(path), null, null];

		// Act
		var resolved = (bool)resolve.Invoke(null, arguments)!;

		// Assert
		await Assert.That(resolved).IsTrue();
		await Assert.That(arguments[2]).IsSameReferenceAs(model);
		await Assert.That(arguments[3]).IsEqualTo(expectedMember);
	}

	[Test]
	public async Task Validate_GivenInvalidScalarValueObjectMember_NamesTheErrorAfterTheOwningMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
			StructMemberSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.SignUp")!;
		var emailType = assembly.GetType("Testing.EmailAddress")!;
		var coordinatesType = assembly.GetType("Testing.Coordinates")!;
		var model = Activator.CreateInstance(modelType)!;
		var shortEmail = Activator.CreateInstance(emailType)!;
		emailType.GetProperty("Value")!.SetValue(shortEmail, "ab");
		modelType.GetProperty("Email")!.SetValue(model, shortEmail);
		modelType.GetProperty("BackupEmail")!.SetValue(model, Activator.CreateInstance(emailType));
		var location = Activator.CreateInstance(coordinatesType)!;
		coordinatesType.GetProperty("Latitude")!.SetValue(location, 120d);
		modelType.GetProperty("Location")!.SetValue(model, location);
		var validate = assembly.GetType("Testing.SignUpSchema")!.GetMethod("Validate")!;

		// Act
		var result = validate.Invoke(null, [model])!;

		// Assert
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;
		var email = errors.Single(static error => error.FullPath.ToDottedString() == "Email.Value");
		var backup = errors.Single(static error => error.FullPath.ToDottedString() == "BackupEmail.Value");
		var latitude = errors.Single(static error => error.FullPath.ToDottedString() == "Location.Latitude");

		// A scalar's own member is an implementation detail: the message and displayName name the owning member, while
		// member and declaringType still identify the rule's member for localization.
		await Assert.That(email.Message).Contains("'Email'");
		await Assert.That(email.Parameters![Core.ZodIssueParameters.DisplayName]).IsEqualTo("Email");
		await Assert.That(email.Parameters![Core.ZodIssueParameters.Member]).IsEqualTo("Value");
		await Assert.That(backup.Message).IsEqualTo("Backup email is required");
		await Assert.That(latitude.Message).Contains("'Latitude'");
	}
}
