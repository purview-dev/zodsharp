using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;
#if NET11_0_OR_GREATER
using Microsoft.CodeAnalysis.CSharp;
using ZodSharp.Core;
using ZodSharp.JsonSchema;
using ZodSharp.Rules;
#endif

namespace ZodSharp.SourceGenerators;

/// <summary>
/// .NET 11 and C# 15 inputs: async DataAnnotations (Change 8), IAsyncValidateOptions (Change 6), unions and
/// closed hierarchies (Change 7).
/// </summary>
partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task OptionsValidator_GivenAsyncRulesAndIAsyncValidateOptions_ImplementsBothInterfaces(
		CancellationToken cancellationToken
	)
	{
		// Arrange — IAsyncValidateOptions<T> ships with .NET 11's Microsoft.Extensions.Options; the generator
		// detects it by name, so a declared copy stands in for it here.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;
			using ZodSharp.Core;

			namespace Microsoft.Extensions.Options
			{
				public interface IAsyncValidateOptions<TOptions> : IValidateOptions<TOptions>
					where TOptions : class
				{
					Task<ValidateOptionsResult> ValidateAsync(string? name, TOptions options, CancellationToken cancellationToken = default);
				}
			}

			namespace Testing
			{
				[ZodSchema(GenerateIValidateOptions = true)]
				public class StorageOptions
				{
					[Required]
					public string? Bucket { get; set; }

					internal static ValueTask<ValidationResult<StorageOptions>> CustomValidationAsync(
						StorageOptions value,
						CancellationToken ct
					) =>
						ValueTask.FromResult(
							ValidationResult<StorageOptions>.Failure(
								ValidationError.Create("bucket_missing", "Bucket does not exist", StorageOptionsSchema.Paths.Bucket)
							)
						);
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var validatorType = assembly.GetType("Testing.StorageOptionsValidator")!;
		dynamic validator = Activator.CreateInstance(validatorType)!;
		var options = Create(assembly, "Testing.StorageOptions", ("Bucket", "media"));

		// Act
		Microsoft.Extensions.Options.ValidateOptionsResult syncOutcome = validator.Validate(null, (dynamic)options);
		Task<Microsoft.Extensions.Options.ValidateOptionsResult> pending = validator.ValidateAsync(
			null,
			(dynamic)options,
			cancellationToken
		);
		var asyncOutcome = await pending;

		// Assert — the synchronous path runs the synchronous rules only; the async path (ValidateOnStart) runs all.
		await Assert
			.That(validatorType.GetInterfaces().Any(static type => type.Name == "IAsyncValidateOptions`1"))
			.IsTrue();
		await Assert.That(syncOutcome.Succeeded).IsTrue();
		await Assert.That(asyncOutcome.Failures!.Single()).IsEqualTo("Bucket: Bucket does not exist (bucket_missing)");
	}

	[Test]
	public async Task OptionsValidator_GivenHandAuthoredOptionsValidator_SkipsTheGeneratedValidator(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using Microsoft.Extensions.Options;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema(GenerateIValidateOptions = true)]
				public class QueueOptions
				{
					[Range(1, 10)]
					public int Workers { get; set; }
				}

				[OptionsValidator]
				public partial class QueueOptionsValidator : IValidateOptions<QueueOptions>
				{
					public ValidateOptionsResult Validate(string? name, QueueOptions options) => ValidateOptionsResult.Success;
				}
			}
			""";

		// Act
		using var result = await GenerateAsync(source, cancellationToken);
		var generated = result.GetSource("QueueOptionsSchema")!;

		// Assert — the generated validator would duplicate the hand-authored one (SYSLIB1205); ZODSGEN050 reports it.
		await Assert.That(generated).DoesNotContain("class QueueOptionsValidator");
		await Assert.That(generated).ContainsGeneratedCode("class QueueOptionsSchemaValidator");
	}

#if NET11_0_OR_GREATER
	static readonly ZodSourceGeneratorTestOptions PreviewCompileOptions = new()
	{
		CompileToAssembly = true,
		LanguageVersion = LanguageVersion.Preview,
	};

	[Test]
	public async Task AsyncValidationAttribute_GivenMember_RunsInValidateAsyncAndSyncValidateThrows(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;

			namespace Testing
			{
				public sealed class NotTakenAttribute : AsyncValidationAttribute
				{
					protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
						throw new System.NotSupportedException("Validated asynchronously.");

					protected override async Task<ValidationResult?> IsValidAsync(
						object? value,
						ValidationContext validationContext,
						CancellationToken cancellationToken
					)
					{
						await Task.Yield();
						return value as string == "taken"
							? new ValidationResult($"{validationContext.DisplayName} is taken")
							: ValidationResult.Success;
					}
				}

				[ZodSchema]
				public class Signup
				{
					[NotTaken]
					[Display(Name = "User name")]
					public string? Name { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var signup = Create(assembly, "Testing.Signup", ("Name", "taken"));

		// Act
		var asyncErrors = await RunValidationAsync(assembly, "Testing.SignupSchema", signup, null, cancellationToken);
		var memberErrors = await RunMemberValidationAsync(
			assembly,
			"Testing.SignupSchema",
			signup,
			"Name",
			null,
			cancellationToken
		);
		var syncException = Unwrap(() => RunValidate(assembly, "Testing.SignupSchema", signup));

		// Assert
		await Assert.That(asyncErrors.Length).IsEqualTo(1);
		await Assert.That(asyncErrors[0].Message).IsEqualTo("User name is taken");
		await Assert.That(asyncErrors[0].FullPath.ToDottedString()).IsEqualTo("Name");
		await Assert.That(memberErrors.Length).IsEqualTo(1);
		await Assert.That(syncException).IsTypeOf<InvalidOperationException>();
	}

	[Test]
	public async Task AsyncValidatableObject_GivenType_RunsAfterMembersInValidateAsync(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Collections.Generic;
			using System.ComponentModel.DataAnnotations;
			using System.Runtime.CompilerServices;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Reservation : IAsyncValidatableObject
				{
					public int Nights { get; set; }

					public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => [];

					public async IAsyncEnumerable<ValidationResult> ValidateAsync(
						ValidationContext validationContext,
						[EnumeratorCancellation] CancellationToken cancellationToken = default
					)
					{
						await Task.Yield();
						if (Nights > 5)
							yield return new ValidationResult("Too long", [nameof(Nights)]);
					}
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var reservation = Create(assembly, "Testing.Reservation", ("Nights", 9));

		// Act
		var errors = await RunValidationAsync(
			assembly,
			"Testing.ReservationSchema",
			reservation,
			null,
			cancellationToken
		);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Nights");
		await Assert.That(errors[0].Code).IsEqualTo(ZodGeneratedValidation.DataAnnotationsErrorCode);
	}

	[Test]
	public async Task Union_GivenMemberTypedAsUnion_DispatchesToTheActiveCaseWithoutValueInThePath(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Cat
				{
					[MinLength(2)]
					public string? Name { get; set; }
				}

				[ZodSchema]
				public class Dog
				{
					[Range(1, 100)]
					public int Weight { get; set; } = 1;
				}

				public union Pet(Cat, Dog);

				[ZodSchema]
				public class Owner
				{
					public Pet Pet { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var cat = Create(assembly, "Testing.Cat", ("Name", "A"));
		var pet = Activator.CreateInstance(assembly.GetType("Testing.Pet")!, cat)!;
		var owner = Create(assembly, "Testing.Owner", ("Pet", pet));

		// Act
		var errors = RunValidate(assembly, "Testing.OwnerSchema", owner);
		var resolved = RunTryResolveMember(
			assembly,
			"Testing.OwnerSchema",
			owner,
			errors[0].FullPath,
			out var container,
			out var member
		);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Pet.Name");
		await Assert.That(resolved).IsTrue();
		await Assert.That(ReferenceEquals(container, cat)).IsTrue();
		await Assert.That(member).IsEqualTo("Name");
	}

	[Test]
	public async Task ClosedHierarchy_GivenMemberTypedAsClosedBase_DispatchesToTheDerivedSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public closed class Shape
				{
				}

				[ZodSchema]
				public sealed class Circle : Shape
				{
					[Range(1, 10)]
					public int Radius { get; set; }
				}

				[ZodSchema]
				public sealed class Square : Shape
				{
					[Range(1, 10)]
					public int Side { get; set; } = 1;
				}

				[ZodSchema]
				public class Drawing
				{
					public Shape? Shape { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var drawing = Create(assembly, "Testing.Drawing", ("Shape", Create(assembly, "Testing.Circle")));

		// Act
		var errors = RunValidate(assembly, "Testing.DrawingSchema", drawing);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Shape.Radius");
	}

	[Test]
	public async Task UnionTarget_GivenZodSchemaOnUnion_DispatchesToTheActiveCaseSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Cat
				{
					[MinLength(2)]
					public string? Name { get; set; }
				}

				[ZodSchema]
				public class Dog
				{
					[Range(1, 100)]
					public int Weight { get; set; } = 1;
				}

				[ZodSchema]
				public readonly union Pet(Cat, Dog);
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var petType = assembly.GetType("Testing.Pet")!;
		var invalidCat = Activator.CreateInstance(petType, Create(assembly, "Testing.Cat", ("Name", "A")))!;
		var validDog = Activator.CreateInstance(petType, Create(assembly, "Testing.Dog", ("Weight", 5)))!;

		// Act
		var invalidErrors = RunValidate(assembly, "Testing.PetSchema", invalidCat);
		var validErrors = RunValidate(assembly, "Testing.PetSchema", validDog);

		// Assert — the case's error is reported under its own path, without a synthetic union member.
		await Assert.That(invalidErrors.Length).IsEqualTo(1);
		await Assert.That(invalidErrors[0].Code).IsEqualTo("too_small");
		await Assert.That(invalidErrors[0].FullPath.ToDottedString()).IsEqualTo("Name");
		await Assert.That(validErrors.IsDefaultOrEmpty).IsTrue();
	}

	[Test]
	public async Task UnionTarget_GivenEmptyUnion_ReportsInvalidUnion(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Cat
				{
					public string? Name { get; set; }
				}

				[ZodSchema]
				public class Dog
				{
					public int Weight { get; set; }
				}

				[ZodSchema]
				public readonly union Pet(Cat, Dog);
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var empty = Activator.CreateInstance(assembly.GetType("Testing.Pet")!)!;

		// Act
		var errors = RunValidate(assembly, "Testing.PetSchema", empty);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("invalid_union");
		await Assert.That(errors[0].GetUnionErrors().Length).IsEqualTo(2);
	}

	[Test]
	public async Task UnionTarget_GivenAsyncCase_RequiresAsyncValidationAndAwaitsIt(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;

			namespace Testing
			{
				public sealed class TakenAttribute : AsyncValidationAttribute
				{
					protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
						throw new System.NotSupportedException("Validated asynchronously.");

					protected override Task<ValidationResult?> IsValidAsync(
						object? value,
						ValidationContext validationContext,
						CancellationToken cancellationToken
					) => Task.FromResult<ValidationResult?>(new ValidationResult("Name is taken"));
				}

				[ZodSchema]
				public class Cat
				{
					[Taken]
					public string? Name { get; set; }
				}

				[ZodSchema]
				public class Dog
				{
					public int Weight { get; set; }
				}

				[ZodSchema]
				public readonly union Pet(Cat, Dog);
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var cat = Activator.CreateInstance(assembly.GetType("Testing.Pet")!, Create(assembly, "Testing.Cat"))!;
		var requiresAsync = (bool)GetStaticField(assembly, "Testing.PetSchema", "RequiresAsyncValidation")!;

		// Act
		var asyncErrors = await RunValidationAsync(assembly, "Testing.PetSchema", cat, null, cancellationToken);
		var syncException = Unwrap(() => RunValidate(assembly, "Testing.PetSchema", cat));

		// Assert
		await Assert.That(requiresAsync).IsTrue();
		await Assert.That(asyncErrors.Length).IsEqualTo(1);
		await Assert.That(asyncErrors[0].Message).IsEqualTo("Name is taken");
		await Assert.That(asyncErrors[0].FullPath.ToDottedString()).IsEqualTo("Name");
		await Assert.That(syncException).IsTypeOf<InvalidOperationException>();
	}

	[Test]
	public async Task UnionMember_GivenRequiredAndEmptyUnion_ReportsMissingField(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Cat
				{
					public string? Name { get; set; }
				}

				[ZodSchema]
				public class Dog
				{
					public int Weight { get; set; }
				}

				public readonly union Pet(Cat, Dog);

				[ZodSchema]
				public class Owner
				{
					[Required]
					public Pet Pet { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var owner = Create(assembly, "Testing.Owner");

		// Act
		var errors = RunValidate(assembly, "Testing.OwnerSchema", owner);

		// Assert — an empty union is the member's absent value even though the union is a struct.
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("missing_field");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Pet");
	}

	[Test]
	public async Task UnionMember_GivenNonSentinelAndEmptyUnion_ReportsTheSentinelRule(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using ZodSharp;
			using ZodSharp.Rules;

			namespace Testing
			{
				[ZodSchema]
				public class Cat
				{
					public string? Name { get; set; }
				}

				[ZodSchema]
				public class Dog
				{
					public int Weight { get; set; }
				}

				public readonly union Pet(Cat, Dog);

				[ZodSchema]
				public class Owner
				{
					[NonSentinel]
					public Pet Pet { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var owner = Create(assembly, "Testing.Owner");

		// Act
		var errors = RunValidate(assembly, "Testing.OwnerSchema", owner);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo(NonSentinelRule<object>.ErrorCode);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Pet");
	}

	[Test]
	public async Task UnionTarget_GivenHandWrittenUnionWithTryGetValue_DispatchesWithoutBoxing(
		CancellationToken cancellationToken
	)
	{
		// Arrange - a [Union] struct that implements the non-boxing access pattern.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using System.Runtime.CompilerServices;
			using ZodSharp;

			namespace Testing
			{
				[ZodSchema]
				public class Cat
				{
					[MinLength(2)]
					public string? Name { get; set; }
				}

				[ZodSchema]
				[Union]
				public struct Pet
				{
					readonly object? _value;

					public Pet(Cat value) => _value = value;

					public object? Value => _value;

					public bool HasValue => _value is not null;

					public bool TryGetValue(out Cat value)
					{
						if (_value is Cat cat)
						{
							value = cat;
							return true;
						}

						value = null!;
						return false;
					}
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var generated = string.Join(
			"\n",
			result
				.DriverResult.Results.SelectMany(static run => run.GeneratedSources)
				.Select(static source => source.SourceText.ToString())
		);
		var assembly = result.CompilationResult.Assembly!;
		var petType = assembly.GetType("Testing.Pet")!;
		var invalidCat = Activator.CreateInstance(petType, Create(assembly, "Testing.Cat", ("Name", "A")))!;
		var empty = Activator.CreateInstance(petType)!;

		// Act
		var errors = RunValidate(assembly, "Testing.PetSchema", invalidCat);
		var emptyErrors = RunValidate(assembly, "Testing.PetSchema", empty);

		// Assert — the generated dispatch uses TryGetValue (no boxing) and still reports the case's error.
		await Assert.That(generated).Contains("TryGetValue(out global::Testing.Cat");
		await Assert.That(generated).DoesNotContain(".Value is global::Testing.Cat");
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("too_small");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Name");
		await Assert.That(emptyErrors[0].Code).IsEqualTo("invalid_union");
	}

	[Test]
	public async Task JsonSchema_GivenMemberTypedAsUnion_ExportsOneOfTheCaseSchemas(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using ZodSharp;

			namespace Testing
			{
				public union Pet(Cat, Dog);

				[ZodSchema]
				public class Cat
				{
					public string? Name { get; set; }
				}

				[ZodSchema]
				public class Dog
				{
					public string? Breed { get; set; }
				}

				[ZodSchema]
				public class Owner
				{
					public Pet Pet { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var schemaType = assembly.GetType("Testing.OwnerSchema")!;

		// Act
		var definition = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert — the member dispatches to the case schemas, so its schema is one of them, not the empty schema.
		var pet = definition.Properties!["pet"];
		await Assert.That(pet.OneOf).IsNotNull();
		await Assert.That(pet.OneOf!.Count).IsEqualTo(2);
		await Assert.That(pet.OneOf![0].Properties!.ContainsKey("name")).IsTrue();
		await Assert.That(pet.OneOf![1].Properties!.ContainsKey("breed")).IsTrue();
	}

	[Test]
	public async Task JsonSchema_GivenMemberTypedAsClosedHierarchy_ExportsOneOfWithTheDiscriminator(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Text.Json.Serialization;
			using ZodSharp;

			namespace Testing
			{
				[JsonPolymorphic(InferClosedTypePolymorphism = true)]
				public closed class Shape
				{
					public string Id { get; set; } = "";
				}

				[ZodSchema]
				public sealed class Circle : Shape
				{
					public double Radius { get; set; }
				}

				[ZodSchema]
				public sealed class Square : Shape
				{
					public double Side { get; set; }
				}

				[ZodSchema]
				public class Drawing
				{
					public Shape Shape { get; set; } = null!;
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var schemaType = assembly.GetType("Testing.DrawingSchema")!;

		// Act
		var definition = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert — inferred polymorphism writes "$type", so each branch constrains it to the derived type.
		var shape = definition.Properties!["shape"];
		await Assert.That(shape.OneOf).IsNotNull();
		await Assert.That(shape.OneOf!.Count).IsEqualTo(2);
		var circle = shape.OneOf!.Single(candidate => candidate.Properties!.ContainsKey("radius"));
		await Assert.That(circle.Properties!.ContainsKey("id")).IsTrue();
		await Assert.That(circle.Required!).Contains("id");
		await Assert.That(circle.Properties!["$type"].Enum!).Contains("Circle");
		await Assert.That(circle.Required!).Contains("$type");
		var square = shape.OneOf!.Single(candidate => candidate.Properties!.ContainsKey("side"));
		await Assert.That(square.Properties!.ContainsKey("id")).IsTrue();
		await Assert.That(square.Properties!["$type"].Enum!).Contains("Square");
	}

	[Test]
	public async Task ClosedHierarchy_GivenDerivedCaseWithInvalidBaseProperty_ValidatesTheBaseProperty(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the derived case's schema validates the inherited member too, under the case's own path.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public closed class Shape
				{
					[Required]
					public string? Id { get; set; }
				}

				[ZodSchema]
				public sealed class Circle : Shape
				{
					[Range(1, 10)]
					public int Radius { get; set; }
				}

				[ZodSchema]
				public class Drawing
				{
					public Shape Shape { get; set; } = null!;
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var circle = Create(assembly, "Testing.Circle", ("Id", null), ("Radius", 5));
		var drawing = Create(assembly, "Testing.Drawing", ("Shape", circle));

		// Act
		var errors = RunValidate(assembly, "Testing.DrawingSchema", drawing);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("missing_field");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Shape.Id");
	}

	[Test]
	public async Task JsonSchema_GivenClosedHierarchyWithExplicitDerivedTypes_UsesTheConfiguredDiscriminators(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source = """
			using System.Text.Json.Serialization;
			using ZodSharp;

			namespace Testing
			{
				[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
				[JsonDerivedType(typeof(Circle), "circle")]
				[JsonDerivedType(typeof(Square), 2)]
				public closed class Shape
				{
				}

				[ZodSchema]
				public sealed class Circle : Shape
				{
					public double Radius { get; set; }
				}

				[ZodSchema]
				public sealed class Square : Shape
				{
					public double Side { get; set; }
				}

				[ZodSchema]
				public class Drawing
				{
					public Shape Shape { get; set; } = null!;
				}
			}
			""";

		using var result = await GenerateAsync(source, PreviewCompileOptions, cancellationToken);
		var assembly = result.CompilationResult.Assembly!;
		var schemaType = assembly.GetType("Testing.DrawingSchema")!;

		// Act
		var definition = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert — a string discriminator stays a string, and an integer one is written as a JSON number.
		var shape = definition.Properties!["shape"];
		var circle = shape.OneOf!.Single(candidate => candidate.Properties!.ContainsKey("radius"));
		await Assert.That(circle.Properties!["kind"].Enum!).Contains("circle");
		var square = shape.OneOf!.Single(candidate => candidate.Properties!.ContainsKey("side"));
		await Assert.That(square.Properties!["kind"].Enum!).Contains(2);
		await Assert.That(square.Properties!["kind"].Enum!).DoesNotContain("2");
	}
#endif
}
