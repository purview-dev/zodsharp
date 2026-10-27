using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task CustomValidation_GivenExplicitNameMissing_ProducesZODSGEN007(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[ZodSchema(CustomValidationMethodName = "DoesNotExistAsync")]
				public class MissingMethod { public string? Name { get; set; } }
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationMethodNotFound);
	}

	[Test]
	public async Task CustomValidation_GivenInvalidReturnType_ProducesZODSGEN008(CancellationToken cancellationToken)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class BadReturn
				{
					public string? Name { get; set; }

					internal static Task<ValidationResult<BadReturn>> CustomValidationAsync(
						BadReturn value, CancellationToken ct) =>
						Task.FromResult(ValidationResult<BadReturn>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidReturnType);
	}

	[Test]
	public async Task CustomValidation_GivenWrongParameterCount_ProducesZODSGEN009(CancellationToken cancellationToken)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class WrongParams
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<WrongParams>> CustomValidationAsync(
						WrongParams value) =>
						ValueTask.FromResult(ValidationResult<WrongParams>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidParameterCount);
	}

	[Test]
	public async Task CustomValidation_GivenWrongModelParameterType_ProducesZODSGEN010(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class WrongModel
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<WrongModel>> CustomValidationAsync(
						string value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<WrongModel>.Success(null!));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidModelParameter);
	}

	[Test]
	public async Task CustomValidation_GivenWrongCancellationTokenType_ProducesZODSGEN011(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class WrongCT
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<WrongCT>> CustomValidationAsync(
						WrongCT value, string ct) =>
						ValueTask.FromResult(ValidationResult<WrongCT>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidCancellationToken);
	}

	[Test]
	public async Task CustomValidation_GivenWrongCancellationTokenType_SaysItMustBeTheLastParameter(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class WrongServiceAwareCT
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<WrongServiceAwareCT>> CustomValidationAsync(
						WrongServiceAwareCT value, IServiceProvider services, string ct) =>
						ValueTask.FromResult(ValidationResult<WrongServiceAwareCT>.Success(value));
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidCancellationToken);
		await Assert
			.That(
				DiagnosticLibrary.CustomValidationInvalidCancellationToken.MessageFormat.ToString(
					System.Globalization.CultureInfo.InvariantCulture
				)
			)
			.Contains("last parameter");
		await Assert
			.That(
				DiagnosticLibrary.CustomValidationInvalidCancellationToken.MessageFormat.ToString(
					System.Globalization.CultureInfo.InvariantCulture
				)
			)
			.DoesNotContain("second parameter");
	}

	[Test]
	public async Task CustomValidation_GivenServiceAwareMethodWithWrongServicesType_ProducesZODSGEN054(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class WrongServices
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<WrongServices>> CustomValidationAsync(
						WrongServices value, string services, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<WrongServices>.Success(value));
				}
			}
			""";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidServicesParameter);
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.CustomValidationInvalidParameterCount);
	}

	[Test]
	public async Task CustomValidation_GivenGenericMethod_ProducesZODSGEN012(CancellationToken cancellationToken)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class GenericMethod
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<T>> CustomValidationAsync<T>(
						T value, CancellationToken ct) where T : class =>
						ValueTask.FromResult(ValidationResult<T>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationGenericMethod);
	}

	[Test]
	public async Task CustomValidation_GivenInstanceMethod_ProducesZODSGEN013(CancellationToken cancellationToken)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class InstanceMethod
				{
					public string? Name { get; set; }

					internal ValueTask<ValidationResult<InstanceMethod>> CustomValidationAsync(
						InstanceMethod value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<InstanceMethod>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidStaticInstance);
	}

	[Test]
	public async Task CustomValidation_GivenPrivateMethod_ProducesZODSGEN014(CancellationToken cancellationToken)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class PrivateMethod
				{
					public string? Name { get; set; }

					private static ValueTask<ValidationResult<PrivateMethod>> CustomValidationAsync(
						PrivateMethod value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<PrivateMethod>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInaccessible);
	}

	[Test]
	public async Task CustomValidation_GivenMultipleOverloads_DoesNotReportAmbiguity(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public partial class MultipleOverloads
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<MultipleOverloads>> CustomValidationAsync(
						MultipleOverloads value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<MultipleOverloads>.Success(value));

					internal static ValueTask<ValidationResult<MultipleOverloads>> CustomValidationAsync(
						MultipleOverloads value) =>
						ValueTask.FromResult(ValidationResult<MultipleOverloads>.Success(value));
				}
			}
			""";

		// Only one overload matches the required signature, so the resolver must not
		// report ambiguity and must not report the non-matching overload's diagnostic.
		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasNoDiagnostics();
	}

	[Test]
	public async Task CustomValidation_GivenInvalidMethodName_ProducesZODSGEN016(CancellationToken cancellationToken)
	{
		const string source = """
			namespace Testing
			{
				[ZodSchema(CustomValidationMethodName = "<invalid>")]
				public class InvalidName { public string? Name { get; set; } }
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidMethodName);
	}

	[Test]
	public async Task CustomValidation_GivenAbstractMethod_ProducesZODSGEN017(CancellationToken cancellationToken)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public abstract class AbstractMethod
				{
					public string? Name { get; set; }

					internal abstract ValueTask<ValidationResult<AbstractMethod>> CustomValidationAsync(
						AbstractMethod value, CancellationToken ct);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationAbstractMethod);
	}

	[Test]
	public async Task CustomValidation_GivenUnimplementedPartialMethod_ProducesZODSGEN018(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public partial class UnimplementedPartial
				{
					public string? Name { get; set; }

					internal static partial ValueTask<ValidationResult<UnimplementedPartial>> CustomValidationAsync(
						UnimplementedPartial value, CancellationToken ct);
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationUnimplementedPartial);
	}

	[Test]
	public async Task CustomValidation_GivenInvalidParameterModifier_ProducesZODSGEN019(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class InvalidModifier
				{
					public string? Name { get; set; }

					internal static ValueTask<ValidationResult<InvalidModifier>> CustomValidationAsync(
						ref InvalidModifier value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<InvalidModifier>.Success(value));
				}
			}
			""";

		var result = await AnalyzeAsync(source, cancellationToken);
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CustomValidationInvalidParameterModifier);
	}
}
