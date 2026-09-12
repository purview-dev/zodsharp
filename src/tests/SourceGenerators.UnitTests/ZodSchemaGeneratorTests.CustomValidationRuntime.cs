using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task CustomValidation_Runtime_SyncErrorsRemainPresent(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using ZodSharp.Core;

namespace Testing
{
	[ZodSchema]
	public class RuntimeModel
	{
		[Required]
		[StringLength(50, MinimumLength = 3)]
		public string Name { get; set; } = string.Empty;

		internal static ValueTask<ValidationResult<RuntimeModel>> CustomValidationAsync(
			RuntimeModel value, CancellationToken ct) =>
			ValueTask.FromResult(ValidationResult<RuntimeModel>.Success(value));
	}
}";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.RuntimeModel")!;
		var schemaType = assembly.GetType("Testing.RuntimeModelSchema")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Name")!.SetValue(instance, "AB"); // too short — sync error

		var validateMethod = schemaType.GetMethod("Validate")!;
		var result = validateMethod.Invoke(null, [instance])!;
		var isSuccessProp = result.GetType().GetProperty("IsSuccess")!;
		var isSuccess = (bool)isSuccessProp.GetValue(result)!;

		await Assert.That(isSuccess).IsFalse().Because("Sync validation should fail for short name");
	}

	[Test]
	public async Task CustomValidation_Runtime_CustomErrorAddedToSyncErrors(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using ZodSharp.Core;

namespace Testing
{
	[ZodSchema]
	public class MergeModel
	{
		[Required]
		[StringLength(50, MinimumLength = 3)]
		public string Name { get; set; } = string.Empty;

		internal static ValueTask<ValidationResult<MergeModel>> CustomValidationAsync(
			MergeModel value, CancellationToken ct)
		{
			var errors = new[] { new ValidationError(""custom"", ""Custom validation failed"", [""Name""]) };
			return ValueTask.FromResult(ValidationResult<MergeModel>.Failure(errors));
		}
	}
}";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.MergeModel")!;
		//var schemaType = assembly.GetType("Testing.MergeModelSchema")!;
		var validatorType = assembly.GetType("Testing.MergeModelSchemaValidator")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Name")!.SetValue(instance, "AB"); // sync error: too short

		dynamic validator = Activator.CreateInstance(validatorType)!;
		dynamic dynInstance = instance;

		dynamic task = validator.ValidateAsync(dynInstance, CancellationToken.None);
		var result = await task;

		dynamic dynResult = result;
		bool isSuccess = dynResult.IsSuccess;
		var errors = (System.Collections.Immutable.ImmutableArray<ValidationError>)dynResult.Errors;

		await Assert.That(isSuccess).IsFalse();
		// Should have sync error (too short) + custom error
		await Assert.That(errors.Length).IsEqualTo(2);
	}

	[Test]
	public async Task CustomValidation_Runtime_BothSuccess_ProducesSuccess(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using ZodSharp.Core;

namespace Testing
{
	[ZodSchema]
	public class BothPassModel
	{
		[Required]
		[StringLength(50, MinimumLength = 3)]
		public string Name { get; set; } = string.Empty;

		internal static ValueTask<ValidationResult<BothPassModel>> CustomValidationAsync(
			BothPassModel value, CancellationToken ct) =>
			ValueTask.FromResult(ValidationResult<BothPassModel>.Success(value));
	}
}";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.BothPassModel")!;
		//var schemaType = assembly.GetType("Testing.BothPassModelSchema")!;
		var validatorType = assembly.GetType("Testing.BothPassModelSchemaValidator")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Name")!.SetValue(instance, "ValidName");

		dynamic validator = Activator.CreateInstance(validatorType)!;
		dynamic dynInstance = instance;

		dynamic task = validator.ValidateAsync(dynInstance, CancellationToken.None);
		var result = await task;

		dynamic dynResult = result;
		bool isSuccess = dynResult.IsSuccess;

		await Assert.That(isSuccess).IsTrue();
	}

	[Test]
	public async Task CustomValidation_Runtime_CancellationTokenPassed(CancellationToken cancellationToken)
	{
		var source =
			@"
using System.Threading;
using System.Threading.Tasks;
using ZodSharp.Core;

namespace Testing
{
	[ZodSchema]
	public class CancellationModel
	{
		public string? Name { get; set; }

		internal static ValueTask<ValidationResult<CancellationModel>> CustomValidationAsync(
			CancellationModel value, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			return ValueTask.FromResult(ValidationResult<CancellationModel>.Success(value));
		}
	}
}";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.CancellationModel")!;
		var validatorType = assembly.GetType("Testing.CancellationModelSchemaValidator")!;

		var instance = Activator.CreateInstance(modelType)!;
		var validator = Activator.CreateInstance(validatorType)!;
		dynamic dynValidator = validator;
		dynamic dynInstance = instance;

		using CancellationTokenSource cts = new();
		await cts.CancelAsync();

		dynamic task = dynValidator.ValidateAsync(dynInstance, cts.Token);
		await Assert.That(async () => await task).ThrowsExactly<OperationCanceledException>();
	}
}
