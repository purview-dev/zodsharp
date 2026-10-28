namespace ZodSharp.Core;

public class ZodGeneratedValidationTests
{
	static readonly ZodGeneratedMember ValueMember = new(
		"Testing.EmailAddress",
		"Value",
		"Value",
		ZodPath.ParseDotted("Value")
	);

	static readonly ZodGeneratedMember EmailMember = new(
		"Testing.SignUp",
		"Email",
		"Contact email",
		ZodPath.ParseDotted("Email")
	);

	[Test]
	public async Task AddNested_GivenOwner_RenamesTheDisplayNameAndReformatsTemplatedMessages(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var templated = ZodGeneratedValidation.Error(
			"invalid_string",
			"Field '{displayName}' is invalid.",
			ZodPath.ParseDotted("Value"),
			ValueMember
		);
		var custom = ZodGeneratedValidation.ForMember(
			ValidationError.Create("custom", "Use a work address", ZodPath.ParseDotted("Value")),
			ValueMember
		);
		var nested = ValidationResult<string>.Failure([templated, custom]);
		List<ValidationError>? errors = null;

		// Act
		ZodGeneratedValidation.AddNested(ref errors, nested, ZodPath.ParseDotted("Email"), EmailMember);

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		await Assert.That(errors).IsNotNull();
		await Assert.That(errors![0].Message).IsEqualTo("Field 'Contact email' is invalid.");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Email.Value");
		await Assert.That(errors[0].Parameters![ZodIssueParameters.DisplayName]).IsEqualTo("Contact email");
		await Assert.That(errors[0].Parameters![ZodIssueParameters.Member]).IsEqualTo("Value");

		// A message the template does not produce is the author's own text, so it is kept.
		await Assert.That(errors[1].Message).IsEqualTo("Use a work address");
		await Assert.That(errors[1].Parameters![ZodIssueParameters.DisplayName]).IsEqualTo("Contact email");
	}

	[Test]
	[Arguments(true)]
	[Arguments(false)]
	public async Task ResolveToReferenceOwner_GivenResolvedContainer_ReplacesOnlyAValueTypeContainer(
		bool containerIsStruct,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		object owner = new();
		var container = containerIsStruct ? Box(new KeyValuePair<string, int>("a", 1)) : Box(new object());
		var original = container;
		var memberName = "Value";

		// Act
		var resolved = ZodGeneratedValidation.ResolveToReferenceOwner(
			true,
			owner,
			"Email",
			ref container,
			ref memberName
		);

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		await Assert.That(resolved).IsTrue();
		if (containerIsStruct)
		{
			await Assert.That(container).IsSameReferenceAs(owner);
			await Assert.That(memberName).IsEqualTo("Email");
		}
		else
		{
			await Assert.That(container).IsSameReferenceAs(original);
			await Assert.That(memberName).IsEqualTo("Value");
		}
	}

	static object? Box<T>(T value) => value;
}
