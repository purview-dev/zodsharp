using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	/// <summary>
	/// Member types that define their own equality operators and conversions, as Purview.ValueObjects scalars do: an
	/// operator against the underlying value (<c>==(string?, EmailAddress)</c>) plus implicit conversions both ways.
	/// A generated <c>value.Member == null</c> is ambiguous between those operators and the type's own equality
	/// (CS9342 for a <c>Nullable&lt;T&gt;</c> member, CS0034 for a reference type), so the generator must test for
	/// null with a pattern, which never binds a user-defined operator.
	/// </summary>
	const string OperatorSafetySource =
		AutomaticScalarPrelude
		+ """
			namespace Testing
			{
				[Purview.ValueObjects.Serialization.Scalar<string>]
				[ZodSchema]
				public readonly partial record struct EmailAddress
					: Purview.ValueObjects.IScalarValueObject<EmailAddress, string>
				{
					public EmailAddress(string value) => Value = value;

					[Required]
					[MinLength(3)]
					public string Value { get; init; }

					public static bool operator ==(EmailAddress left, string? right) => left.Value == right;

					public static bool operator !=(EmailAddress left, string? right) => !(left == right);

					public static bool operator ==(string? left, EmailAddress right) => right.Value == left;

					public static bool operator !=(string? left, EmailAddress right) => !(left == right);

					public static implicit operator EmailAddress(string? value) => new(value!);

					public static implicit operator string(EmailAddress value) => value.Value;
				}

				[Purview.ValueObjects.Serialization.Scalar<int?>(Nullable = true)]
				[ZodSchema]
				public readonly partial record struct Score : Purview.ValueObjects.IScalarValueObject<Score, int?>
				{
					public int? Value { get; init; }

					public static bool operator ==(Score left, int? right) => left.Value == right;

					public static bool operator !=(Score left, int? right) => !(left == right);

					public static bool operator ==(int? left, Score right) => right.Value == left;

					public static bool operator !=(int? left, Score right) => !(left == right);

					public static implicit operator Score(int? value) => new() { Value = value };

					public static implicit operator int?(Score value) => value.Value;
				}

				[ZodSchema]
				public sealed class Token
				{
					[MinLength(1)]
					public string Text { get; set; } = "token";

					public static bool operator ==(Token? left, Token? right) =>
						ReferenceEquals(left, right) || (left is not null && right is not null && left.Text == right.Text);

					public static bool operator !=(Token? left, Token? right) => !(left == right);

					public static bool operator ==(Token? left, string? right) => left?.Text == right;

					public static bool operator !=(Token? left, string? right) => !(left == right);

					public static bool operator ==(string? left, Token? right) => right?.Text == left;

					public static bool operator !=(string? left, Token? right) => !(left == right);

					public static implicit operator Token(string? text) => new() { Text = text ?? string.Empty };

					public static implicit operator string?(Token? token) => token?.Text;

					public override bool Equals(object? obj) => obj is Token other && other.Text == Text;

					public override int GetHashCode() => Text.GetHashCode();
				}

				[ZodSchema]
				public partial class Contact
				{
					[Required]
					public EmailAddress? Email { get; set; }

					public EmailAddress? Secondary { get; set; }

					[RequiredZod]
					public Score? Score { get; set; }

					[Required]
					public Token? Backup { get; set; }

					public Token Primary { get; set; } = new();
				}
			}
			""";

	[Test]
	public async Task NullChecks_GivenMemberTypesWithUserDefinedEqualityOperators_CompileWithoutDiagnostics(
		CancellationToken cancellationToken
	)
	{
		// Act
		using var driverResult = await GenerateAsync(OperatorSafetySource, cancellationToken);
		var generated = driverResult.GetSource("ContactSchema");

		// Assert - every null test is a pattern, so none of them binds an operator the member type declares.
		await Assert.That(generated).DoesNotContain("== null");
		await Assert.That(generated).DoesNotContain("!= null");
		await Assert.That(generated).ContainsGeneratedCode("value.Email is null");
		await Assert.That(generated).ContainsGeneratedCode("value.Secondary is not null");
		await Assert.That(generated).ContainsGeneratedCode("value.Score is null || value.Score.Value.Value is null");
		await Assert.That(generated).ContainsGeneratedCode("value.Backup is null");
		await Assert.That(generated).ContainsGeneratedCode("value.Primary is null");

		var diagnostics = driverResult.CompilationResult.Compilation.GetDiagnostics(cancellationToken);
		// No error anywhere, and nothing at all in the generated sources (the test source's own usings repeat the
		// harness's global ones, which is noise outside the generated code).
		Microsoft.CodeAnalysis.Diagnostic[] problems =
		[
			.. diagnostics.Where(static diagnostic =>
				diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
				|| (
					diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning
					&& diagnostic.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true
				)
			),
		];
		await Assert.That(problems).IsEmpty();
	}

	[Test]
	public async Task NullChecks_GivenMemberTypesWithUserDefinedEqualityOperators_RejectMissingValuesAtRuntime(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
			OperatorSafetySource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var contactType = assembly.GetType("Testing.Contact")!;
		var validate = assembly.GetType("Testing.ContactSchema")!.GetMethod("Validate")!;

		// Act - nothing set: Email, Score and Backup are required, and Primary keeps its default.
		var result = validate.Invoke(null, [Activator.CreateInstance(contactType)!])!;

		// Assert
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;
		string[] paths =
		[
			.. errors.Select(static error => error.FullPath.ToDottedString()).Order(StringComparer.Ordinal),
		];
		await Assert.That(paths).IsEquivalentTo(["Backup", "Email", "Score"]);
	}
}
