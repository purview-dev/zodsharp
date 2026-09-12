using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.Schemas;

public class ZodObjectShapeTests
{
	static ZodObject CreateUserSchema() =>
		Z.Object()
			.Field("name", Z.String().Min(1))
			.Field("age", Z.Number().Min(0))
			.Field("email", Z.String().Email())
			.Build();

	[Test]
	public async Task Extend_GivenNewField_ReturnsObjectWithExtraField()
	{
		// Arrange
		var baseObj = Z.Object().Field("name", Z.String()).Build();

		// Act
		var extended = baseObj.Extend("age", Z.Number().Min(0));
		var result = extended.Validate(new Dictionary<string, object?> { ["name"] = "John", ["age"] = 30.0 });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).ContainsKey("age");
	}

	[Test]
	public async Task Extend_GivenExistingField_ReplacesSchema()
	{
		// Arrange
		var base_ = Z.Object().Field("name", Z.String()).Build();

		// Act — replace name schema with one requiring min length 5.
		var extended = base_.Extend("name", Z.String().Min(5));
		var shortResult = extended.Validate(new Dictionary<string, object?> { ["name"] = "Jo" });

		// Assert
		await Assert.That(shortResult.IsSuccess).IsFalse();
	}

	[Test]
	public async Task Extend_ReplacingOptionalKey_DropsStaleOptionalMetadata()
	{
		// Arrange — both fields are optional after partial().
		var partial = Z.Object().Field("name", Z.String()).Field("age", Z.Number()).Build().Partial();

		// Act — replace age with a required schema.
		var extended = partial.Extend("age", Z.Number().Min(0));

		// Assert — age is required again, so an object missing it must fail.
		var result = extended.Validate(new Dictionary<string, object?> { ["name"] = "John" });
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "missing_field")).IsTrue();

		// And age still validates when present.
		var withAge = extended.Validate(new Dictionary<string, object?> { ["name"] = "John", ["age"] = 30.0 });
		await Assert.That(withAge.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Extend_AddingOptionalSchemaField_MarksKeyOptional()
	{
		// Arrange
		var base_ = Z.Object().Field("name", Z.String()).Build();

		// Act — add a schema-level optional field.
		var extended = base_.Extend("email", Z.Optional(Z.String()));

		// Assert — the key is optional even though it was never in optionalKeys.
		var result = extended.Validate(new Dictionary<string, object?> { ["name"] = "John" });
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Merge_GivenTwoObjects_CombinesShapes()
	{
		// Arrange
		var a = Z.Object().Field("name", Z.String()).Build();
		var b = Z.Object().Field("age", Z.Number()).Build();

		// Act
		var merged = a.Merge(b);
		var result = merged.Validate(new Dictionary<string, object?> { ["name"] = "John", ["age"] = 30.0 });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value).ContainsKey("name");
		await Assert.That(result.Value).ContainsKey("age");
	}

	[Test]
	public async Task Merge_GivenOverlappingKey_RightSchemaWins()
	{
		// Arrange — left requires min 5, right requires min 1.
		var a = Z.Object().Field("name", Z.String().Min(5)).Build();
		var b = Z.Object().Field("name", Z.String().Min(1)).Build();

		// Act — "Jo" passes the right's rule but not the left's.
		var result = a.Merge(b).Validate(new Dictionary<string, object?> { ["name"] = "Jo" });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Merge_GivenRightObjectIsStrict_ResultIsStrict()
	{
		// Arrange
		var a = Z.Object().Field("name", Z.String()).Build();
		var b = Z.Object().Field("name", Z.String()).Build().Strict();

		// Act
		var result = a.Merge(b).Validate(new Dictionary<string, object?> { ["name"] = "John", ["extra"] = "x" });

		// Assert — the right (strict) policy wins over the left (strip).
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "unrecognized_key")).IsTrue();
	}

	[Test]
	public async Task Merge_GivenLeftObjectIsStrict_RightStripWins_ResultIsStrip()
	{
		// Arrange
		var a = Z.Object().Field("name", Z.String()).Build().Strict();
		var b = Z.Object().Field("age", Z.Number()).Build();

		// Act
		var result = a.Merge(b)
			.Validate(
				new Dictionary<string, object?>
				{
					["name"] = "John",
					["age"] = 30.0,
					["extra"] = "x",
				}
			);

		// Assert — the right (strip) policy wins over the left (strict).
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.ContainsKey("extra")).IsFalse();
	}

	[Test]
	public async Task Merge_GivenRightCatchall_ResultUsesRightCatchall()
	{
		// Arrange — left catchall requires strings, right catchall requires numbers.
		var a = Z.Object().Field("name", Z.String()).Build().Catchall(new FieldSchemaWrapper<string>(Z.String()));
		var b = Z.Object().Field("age", Z.Number()).Build().Catchall(new FieldSchemaWrapper<double>(Z.Number()));

		// Act
		var merged = a.Merge(b);
		var valid = merged.Validate(
			new Dictionary<string, object?>
			{
				["name"] = "John",
				["age"] = 30.0,
				["extra"] = 42,
			}
		);
		var invalid = merged.Validate(
			new Dictionary<string, object?>
			{
				["name"] = "John",
				["age"] = 30.0,
				["extra"] = "bad",
			}
		);

		// Assert — the right catchall (number) applies.
		await Assert.That(valid.IsSuccess).IsTrue();
		await Assert.That(invalid.IsSuccess).IsFalse();
	}

	[Test]
	public async Task Merge_GivenRightOptionalKey_KeyIsOptionalInResult()
	{
		// Arrange
		var a = Z.Object().Field("age", Z.Number()).Build();
		var b = Z.Object().Field("age", Z.Number()).Build().Partial();

		// Act
		var result = a.Merge(b).Validate([]);

		// Assert — age is optional because the right object made it so.
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Merge_GivenRightRequiredKeyOverridesLeftOptional_KeyRequired()
	{
		// Arrange
		var a = Z.Object().Field("name", Z.String()).Build().Partial();
		var b = Z.Object().Field("name", Z.String()).Build();

		// Act
		var result = a.Merge(b).Validate([]);

		// Assert — name is required again because the right object made it so.
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "missing_field")).IsTrue();
	}

	[Test]
	public async Task Pick_GivenKeys_ReturnsObjectWithOnlyThoseKeys()
	{
		// Arrange
		var schema = CreateUserSchema();

		// Act
		var picked = schema.Pick("name", "email");
		var resultWithAll = picked.Validate(
			new Dictionary<string, object?> { ["name"] = "John", ["email"] = "john@example.com" }
		);
		var resultMissingAge = picked.Validate(
			new Dictionary<string, object?> { ["name"] = "John", ["email"] = "john@example.com" }
		);

		// Assert — age is not in the picked shape, so it's not required.
		await Assert.That(resultWithAll.IsSuccess).IsTrue();
		await Assert.That(resultMissingAge.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Omit_GivenKeys_ReturnsObjectWithoutThoseKeys()
	{
		// Arrange
		var schema = CreateUserSchema();

		// Act
		var omitted = schema.Omit("age");
		var result = omitted.Validate(
			new Dictionary<string, object?> { ["name"] = "John", ["email"] = "john@example.com" }
		);

		// Assert — age is omitted, so it's not required.
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!).ContainsKey("name");
		await Assert.That(result.Value!.ContainsKey("age")).IsFalse();
	}

	[Test]
	public async Task Partial_GivenMissingFields_AllowsMissing()
	{
		// Arrange
		var schema = CreateUserSchema().Partial();

		// Act — only name provided; age and email are missing but optional.
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = "John" });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Partial_GivenPresentFields_StillValidatesThem()
	{
		// Arrange
		var schema = CreateUserSchema().Partial();

		// Act — age is present but invalid (negative).
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = "John", ["age"] = -5.0 });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task Required_GivenPartialThenRequired_EnforcesAllFields()
	{
		// Arrange
		var partial = CreateUserSchema().Partial();
		var required = partial.Required();

		// Act — only name provided; age and email are required again.
		var result = required.Validate(new Dictionary<string, object?> { ["name"] = "John" });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
	}

	[Test]
	public async Task Required_GivenSchemaLevelOptionalField_RequiresFieldPresence()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.Optional(Z.String())).Build().Required();

		// Act
		var result = schema.Validate([]);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert
			.That(result.Errors.Any(static e => e.Code == "missing_field" && e.Path.SequenceEqual(["name"])))
			.IsTrue();
	}

	[Test]
	public async Task Required_GivenPresentSchemaLevelOptionalField_PreservesFieldValidation()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.Optional(Z.String())).Build().Required();

		// Act
		var valueResult = schema.Validate(new Dictionary<string, object?> { ["name"] = "John" });
		var nullResult = schema.Validate(new Dictionary<string, object?> { ["name"] = null });

		// Assert — Required controls presence; the underlying C# schema still accepts an explicit null.
		await Assert.That(valueResult.IsSuccess).IsTrue();
		await Assert.That(nullResult.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Partial_GivenRequiredSchemaLevelOptionalField_AllowsMissingAgain()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.Optional(Z.String())).Build().Required().Partial();

		// Act
		var result = schema.Validate([]);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Strict_GivenUnknownKey_ReturnsError()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.String()).Build().Strict();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = "John", ["extra"] = "unknown" });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "unrecognized_key")).IsTrue();
	}

	[Test]
	public async Task Passthrough_GivenUnknownKey_KeepsItInOutput()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.String()).Build().Passthrough();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = "John", ["extra"] = "kept" });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!["extra"]).IsEqualTo("kept");
	}

	[Test]
	public async Task Strip_GivenUnknownKey_DropsItFromOutput()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.String()).Build().Strip();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = "John", ["extra"] = "dropped" });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.ContainsKey("extra")).IsFalse();
	}

	[Test]
	public async Task Catchall_GivenUnknownKey_ValidatesAndIncludesIt()
	{
		// Arrange — catchall requires strings; an int extra should fail.
		var schema = Z.Object().Field("name", Z.String()).Build().Catchall(new FieldSchemaWrapper<string>(Z.String()));

		// Act
		var validExtra = schema.Validate(new Dictionary<string, object?> { ["name"] = "John", ["extra"] = "ok" });
		var invalidExtra = schema.Validate(new Dictionary<string, object?> { ["name"] = "John", ["extra"] = 42 });

		// Assert
		await Assert.That(validExtra.IsSuccess).IsTrue();
		await Assert.That(validExtra.Value!["extra"]).IsEqualTo("ok");
		await Assert.That(invalidExtra.IsSuccess).IsFalse();
	}

	[Test]
	public async Task ObjectValidate_GivenMissingOptionalSchemaField_AllowsMissing()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.Optional(Z.String())).Build();

		// Act
		var result = schema.Validate([]);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.ContainsKey("name")).IsFalse();
	}

	[Test]
	public async Task ObjectValidate_GivenNullOptionalField_IsSuccess()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.Optional(Z.String())).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = null });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.ContainsKey("name")).IsTrue();
		await Assert.That(result.Value!["name"]).IsNull();
	}

	[Test]
	public async Task ObjectValidate_GivenNullRequiredStringField_ReturnsFailure()
	{
		// Arrange
		var schema = Z.Object().Field("name", Z.String()).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["name"] = null });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "invalid_type")).IsTrue();
	}

	[Test]
	public async Task ObjectValidate_GivenNullNumberField_ReturnsFailure()
	{
		// Arrange
		var schema = Z.Object().Field("age", Z.Number()).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["age"] = null });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "invalid_type")).IsTrue();
	}

	[Test]
	public async Task ObjectValidate_GivenNullNullableNumberField_IsSuccess()
	{
		// Arrange
		var schema = Z.Object().Field("age", Z.Nullable(Z.Number())).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["age"] = null });

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.ContainsKey("age")).IsTrue();
		await Assert.That(result.Value!["age"]).IsNull();
	}

	[Test]
	public async Task ObjectValidate_GivenNullableNumberField_ValidatesValue()
	{
		// Arrange
		var schema = Z.Object().Field("age", Z.Nullable(Z.Number().Positive())).Build();

		// Act
		var valid = schema.Validate(new Dictionary<string, object?> { ["age"] = 30 });
		var invalid = schema.Validate(new Dictionary<string, object?> { ["age"] = -5.0 });

		// Assert
		await Assert.That(valid.IsSuccess).IsTrue();
		await Assert.That(valid.Value!["age"]).IsEqualTo(30.0);
		await Assert.That(invalid.IsSuccess).IsFalse();
	}

	[Test]
	public async Task ObjectValidate_GivenNullableNumberField_RejectsWrongType()
	{
		// Arrange
		var schema = Z.Object().Field("age", Z.Nullable(Z.Number())).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["age"] = "not-a-number" });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "invalid_type")).IsTrue();
	}

	[Test]
	public async Task ObjectValidate_GivenMissingNullableField_ReturnsFailure()
	{
		// Arrange — nullable accepts null but does not make the key optional.
		var schema = Z.Object().Field("age", Z.Nullable(Z.Number())).Build();

		// Act
		var result = schema.Validate([]);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "missing_field")).IsTrue();
	}

	[Test]
	public async Task ObjectValidate_GivenNullLiteralField_ReturnsFailureWithoutThrowing()
	{
		// Arrange
		var schema = Z.Object().Field("status", Z.Literal("active")).Build();

		// Act
		var result = schema.Validate(new Dictionary<string, object?> { ["status"] = null });

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Any(static e => e.Code == "invalid_literal")).IsTrue();
	}

	[Test]
	public async Task ObjectValidate_GivenMissingDefaultField_ProvidesDefaultInOutput()
	{
		// Arrange — a defaulted field supplied as a raw shape entry.
		IZodSchema<object, object> defaultField = new ZodDefault<object>(new PassthroughSchema(), "fallback")!;
		var shape = ImmutableDictionary<string, IZodSchema<object, object>>.Empty.Add("name", defaultField);
		ZodObject schema = new(shape);

		// Act
		var result = schema.Validate([]);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!["name"]).IsEqualTo("fallback");
	}

	sealed class PassthroughSchema : IZodSchema<object>
	{
		public ValidationResult<object> Validate(object value) => ValidationResult<object>.Success(value);

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}
}
