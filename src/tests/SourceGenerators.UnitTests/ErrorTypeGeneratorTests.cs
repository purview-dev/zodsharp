using System.Collections.Immutable;
using Purview.SourceGeneratorFramework;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

public class ErrorTypeGeneratorTests : ErrorTypeGeneratorTestBase
{
	const string Source = """
		namespace Testing;

		public static partial class ConcurrentErrorType
		{
			[ErrorType]
			public static readonly ErrorType SaveFailed = new(
				Code: "aggregate_save_failed",
				Category: "invalid_value",
				Description: "The order could not be saved because it was modified concurrently.",
				HttpStatus: 409,
				MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save",
				Parameters:
				[
					new ErrorTypeParameter("OrderId", typeof(string)),
					new ErrorTypeParameter("AggregateType", typeof(string))
				]);
		}
		""";

	[Test]
	public async Task GivenErrorTypeFieldWithParameters_GeneratesCreateAndThrowMethods(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = Source;

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoErrorDiagnostics();
		var query = result.Generated();

		var stringType = TypeReference.Create<string>();
		var nullableString = query.MakeNullable(TypeReference.Create<string>());
		var nullableStringArray = query.MakeNullable(TypeReference.Create<string>().MakeArray());
		var nullableInt = query.MakeNullable(TypeReference.Create<int>());
		var nullableBool = query.MakeNullable(TypeReference.Create<bool>());

		await Assert
			.That(query)
			.HasGeneratedMethod(
				"CreateSaveFailed",
				[stringType, stringType, nullableStringArray, nullableString, nullableInt, nullableInt, nullableBool]
			);
		await Assert
			.That(query)
			.HasGeneratedMethodReturnType("CreateSaveFailed", TypeReference.Create<ValidationError>());

		await Assert
			.That(query)
			.HasGeneratedMethod(
				"ThrowSaveFailed",
				[stringType, stringType, nullableStringArray, nullableString, nullableInt, nullableInt, nullableBool]
			);

		var throwMethod = query.GetMethod("ThrowSaveFailed").Node;
		await Assert.That(throwMethod.AttributeLists.ToString()).Contains("DoesNotReturn");
	}

	[Test]
	public async Task GivenErrorTypeField_GeneratedCreateCarriesParametersAndFormatsMessage(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = Source;

		// Act
		var result = await GenerateAsync(source, cancellationToken);
		var generated = result.GetSource();

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("[\"OrderId\"] = orderId,");
		await Assert.That(generated).ContainsGeneratedCode("[\"AggregateType\"] = aggregateType,");
		await Assert.That(generated).ContainsGeneratedCode("ErrorTypeParameters.Create(errorType.Parameters,");
		await Assert.That(generated).ContainsGeneratedCode("errorType.FormatMessage(");
		await Assert.That(generated).ContainsGeneratedCode("errorType.Category");
		await Assert.That(generated).ContainsGeneratedCode("throw new ZodException([CreateSaveFailed(");
	}

	[Test]
	public async Task GivenErrorTypeFieldWithoutParameters_GeneratesParameterlessMethods(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Testing;

			public static partial class ConcurrentErrorType
			{
				[ErrorType]
				public static readonly ErrorType SaveFailed = new(
					Code: "aggregate_save_failed",
					Description: "The order could not be saved.",
					HttpStatus: 409);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoErrorDiagnostics();
		var generated = result.GetSource();
		await Assert
			.That(generated)
			.ContainsGeneratedCode("public static global::ZodSharp.Core.ValidationError CreateSaveFailed(");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"ErrorTypeParameters.Create(errorType.Parameters, new global::System.Collections.Generic.Dictionary<string, object?>"
			);
	}

	[Test]
	public async Task GivenTypedParameters_GeneratesStronglyTypedSignature(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;

			namespace Testing;

			public static partial class SizedErrorType
			{
				[ErrorType]
				public static readonly ErrorType OutOfRange = new(
					Code: "out_of_range",
					MessageFormat: "'{Field}' must be between {Minimum} and {Maximum} but got {Actual} (tags: {Tags})",
					Parameters:
					[
						new ErrorTypeParameter("Field", typeof(string)),
						new ErrorTypeParameter("Minimum", typeof(int?)),
						new ErrorTypeParameter("Actual", typeof(decimal)),
						new ErrorTypeParameter("Tags", typeof(string[])),
						new ErrorTypeParameter("Ids", typeof(List<string>))
					]);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoErrorDiagnostics();
		var query = result.Generated();
		var nullableInt = query.MakeNullable(TypeReference.Create<int>());

		await Assert
			.That(query)
			.HasGeneratedMethod(
				"CreateOutOfRange",
				[
					TypeReference.Create<string>(),
					nullableInt,
					TypeReference.Create<decimal>(),
					TypeReference.Create<string>().MakeArray(),
					TypeReference.Create<List<string>>(),
					query.MakeNullable(TypeReference.Create<string>().MakeArray()),
					query.MakeNullable(TypeReference.Create<string>()),
					nullableInt,
					nullableInt,
					query.MakeNullable(TypeReference.Create<bool>()),
				]
			);
	}

	[Test]
	public async Task GivenParametersThatCollideWithMetadataNames_MetadataParametersAreDisambiguated(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Testing;

			public static partial class SizeErrorType
			{
				[ErrorType]
				public static readonly ErrorType TooShort = new(
					Code: "too_small",
					MessageFormat: "'{Field}' must be at least {Minimum} characters.",
					Parameters:
					[
						new ErrorTypeParameter("Field", typeof(string)),
						new ErrorTypeParameter("Minimum", typeof(int))
					]);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoErrorDiagnostics();
		var query = result.Generated();

		var createMethod = query.GetMethod("CreateTooShort").Node;
		var parameterNames = createMethod
			.ParameterList.Parameters.Select(static p => p.Identifier.ValueText)
			.ToImmutableArray();

		await Assert.That(parameterNames).Contains("field");
		await Assert.That(parameterNames).Contains("minimumValue");
		await Assert.That(parameterNames).Contains("minimum");
	}

	[Test]
	public async Task GivenPositionalParametersArgument_GeneratesCreateAndThrowMethods(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			namespace Testing;

			public static partial class ConcurrentErrorType
			{
				[ErrorType]
				public static readonly ErrorType SaveFailed = new(
					"aggregate_save_failed",
					null,
					null,
					409,
					null,
					null,
					"Order '{OrderId}' failed to save",
					[new ErrorTypeParameter("OrderId", typeof(string))]);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoErrorDiagnostics();
		var query = result.Generated();
		var stringType = TypeReference.Create<string>();
		var nullableString = query.MakeNullable(stringType);
		var nullableStringArray = query.MakeNullable(stringType.MakeArray());
		var nullableInt = query.MakeNullable(TypeReference.Create<int>());
		var nullableBool = query.MakeNullable(TypeReference.Create<bool>());

		await Assert
			.That(query)
			.HasGeneratedMethod(
				"CreateSaveFailed",
				[stringType, nullableStringArray, nullableString, nullableInt, nullableInt, nullableBool]
			);
		await Assert.That(query).HasGeneratedMethod("ThrowSaveFailed");
	}

	[Test]
	public async Task GivenParamInvocationParameters_GeneratesCreateAndThrowMethods(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Testing;

			public static partial class ConcurrentErrorType
			{
				[ErrorType]
				public static readonly ErrorType SaveFailed = new(
					Code: "aggregate_save_failed",
					MessageFormat: "Order '{OrderId}' (of type {AggregateType}) failed to save",
					Parameters:
					[
						new ErrorTypeParameter("OrderId", typeof(string)),
						ErrorType.Param<string>("AggregateType")
					]);
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoErrorDiagnostics();
		var query = result.Generated();
		var stringType = TypeReference.Create<string>();
		var nullableString = query.MakeNullable(stringType);
		var nullableStringArray = query.MakeNullable(stringType.MakeArray());
		var nullableInt = query.MakeNullable(TypeReference.Create<int>());
		var nullableBool = query.MakeNullable(TypeReference.Create<bool>());

		await Assert
			.That(query)
			.HasGeneratedMethod(
				"CreateSaveFailed",
				[stringType, stringType, nullableStringArray, nullableString, nullableInt, nullableInt, nullableBool]
			);
		await Assert.That(query).HasGeneratedMethod("ThrowSaveFailed");
	}

	[Test]
	public async Task GivenNonPartialContainingClass_DoesNotGenerate(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Testing;

			public static class ConcurrentErrorType
			{
				[ErrorType]
				public static readonly ErrorType SaveFailed = new(
					Code: "aggregate_save_failed",
					HttpStatus: 409)
				{
					Parameters =
					[
						new ErrorTypeParameter("OrderId", typeof(string)),
						new ErrorTypeParameter("AggregateType", typeof(string))
					]
				};
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoErrorDiagnostics();
		await Assert.That(result.Generated().HasMethod("CreateSaveFailed")).IsFalse();
	}

	[Test]
	public async Task GivenFieldOfAnotherType_DoesNotGenerate(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			namespace Testing;

			public sealed record Other(string Code);

			public static partial class SomeType
			{
				[ErrorType]
				public static readonly Other Value = new("something");
			}
			""";

		// Act
		var result = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasNoErrorDiagnostics();
		await Assert.That(result.Generated().HasMethod("CreateValue")).IsFalse();
	}
}
