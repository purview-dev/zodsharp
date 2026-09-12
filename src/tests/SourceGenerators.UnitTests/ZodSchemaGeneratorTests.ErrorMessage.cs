using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task GeneratedValidate_GivenRegularExpressionErrorMessage_FormatsPropertyNamePlaceholder(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public sealed class RouteOptions
				{
					[Required(AllowEmptyStrings = false)]
					[RegularExpression("^/", ErrorMessage = "{0} must start with '/'.")]
					public string RoutePrefix { get; set; } = "/admin/api";
				}
			}
			""";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.RouteOptions")!;
		var schemaType = assembly.GetType("Testing.RouteOptionsSchema")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("RoutePrefix")!.SetValue(instance, "admin/api");

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
		var errors = (ImmutableArray<ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo("RoutePrefix must start with '/'.");
	}

	[Test]
	public async Task GeneratedValidate_GivenRangeErrorMessage_FormatsMinMaxPlaceholders(
		CancellationToken cancellationToken
	)
	{
		var source = """
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public sealed class PagingOptions
				{
					[Range(1, int.MaxValue, ErrorMessage = "{0} must be between {1} and {2}.")]
					public int DefaultPageSize { get; set; } = 50;
				}
			}
			""";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.PagingOptions")!;
		var schemaType = assembly.GetType("Testing.PagingOptionsSchema")!;

		var instance = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("DefaultPageSize")!.SetValue(instance, 0);

		var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
		var errors = (ImmutableArray<ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo("DefaultPageSize must be between 1 and 2147483647.");
	}
}
