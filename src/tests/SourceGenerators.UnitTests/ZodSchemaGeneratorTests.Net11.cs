using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;
#if NET11_0_OR_GREATER
using Microsoft.CodeAnalysis.CSharp;
using ZodSharp.Core;
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
#endif
}
