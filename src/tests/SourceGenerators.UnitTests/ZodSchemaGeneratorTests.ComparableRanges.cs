using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Generate_GivenComparableRanges_EmitsParseBasedBoundaryFields(CancellationToken cancellationToken)
	{
		const string source = """
			using System;
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public class Limits
				{
					[Range(typeof(TimeSpan), "0", "365.00:00:00", ParseLimitsInInvariantCulture = true)]
					public TimeSpan MaxTimeRange { get; set; } = TimeSpan.FromDays(365);

					[Range(typeof(DateTimeOffset), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)]
					public DateTimeOffset Window { get; set; }

					[Range(typeof(Version), "1.0", "2.0", ParseLimitsInInvariantCulture = true)]
					public Version ApiVersion { get; set; } = new(1, 0);

					[Range(typeof(DateTime), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)]
					public DateTime When { get; set; }

					[Range(typeof(DateOnly), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)]
					public DateOnly Day { get; set; }

					[Range(typeof(TimeOnly), "06:00:00", "22:00:00", ParseLimitsInInvariantCulture = true)]
					public TimeOnly Hour { get; set; }
				}
			}
			""";

		var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("LimitsSchema");

		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"static readonly global::System.TimeSpan RangeMinimum_MaxTimeRange = global::System.TimeSpan.Parse(\"0\", global::System.Globalization.CultureInfo.InvariantCulture);"
			);
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"static readonly global::System.TimeSpan RangeMaximum_MaxTimeRange = global::System.TimeSpan.Parse(\"365.00:00:00\", global::System.Globalization.CultureInfo.InvariantCulture);"
			);
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"maxTimeRangeValue < RangeMinimum_MaxTimeRange || maxTimeRangeValue > RangeMaximum_MaxTimeRange"
			);

		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"static readonly global::System.DateTimeOffset RangeMinimum_Window = global::System.DateTimeOffset.Parse(\"2020-01-01\", global::System.Globalization.CultureInfo.InvariantCulture);"
			);

		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"static readonly global::System.Version RangeMinimum_ApiVersion = global::System.Version.Parse(\"1.0\");"
			);
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"apiVersionValue.CompareTo(RangeMinimum_ApiVersion) < 0 || apiVersionValue.CompareTo(RangeMaximum_ApiVersion) > 0"
			);

		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"static readonly global::System.DateTime RangeMinimum_When = global::System.DateTime.Parse(\"2020-01-01\", global::System.Globalization.CultureInfo.InvariantCulture);"
			);
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"static readonly global::System.DateOnly RangeMinimum_Day = global::System.DateOnly.Parse(\"2020-01-01\", global::System.Globalization.CultureInfo.InvariantCulture);"
			);
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				"static readonly global::System.TimeOnly RangeMinimum_Hour = global::System.TimeOnly.Parse(\"06:00:00\", global::System.Globalization.CultureInfo.InvariantCulture);"
			);
	}

	[Test]
	public async Task ComparableRanges_Runtime_OutOfRangeValuesFail(CancellationToken cancellationToken)
	{
		var source = """
			using System;
			using System.ComponentModel.DataAnnotations;

			namespace Testing
			{
				[ZodSchema]
				public class Limits
				{
					[Range(typeof(TimeSpan), "0", "365.00:00:00", ParseLimitsInInvariantCulture = true)]
					public TimeSpan MaxTimeRange { get; set; } = TimeSpan.FromDays(365);

					[Range(typeof(DateTimeOffset), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)]
					public DateTimeOffset Window { get; set; }

					[Range(typeof(Version), "1.0", "2.0", ParseLimitsInInvariantCulture = true)]
					public Version ApiVersion { get; set; } = new(1, 0);

					[Range(typeof(DateOnly), "2020-01-01", "2030-12-31", ParseLimitsInInvariantCulture = true)]
					public DateOnly Day { get; set; }

					[Range(typeof(TimeOnly), "06:00:00", "22:00:00", ParseLimitsInInvariantCulture = true)]
					public TimeOnly Hour { get; set; }
				}
			}
			""";

		var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var modelType = assembly.GetType("Testing.Limits")!;
		var schemaType = assembly.GetType("Testing.LimitsSchema")!;

		async Task AssertFails(string property, object value)
		{
			var instance = Activator.CreateInstance(modelType)!;
			modelType.GetProperty("MaxTimeRange")!.SetValue(instance, TimeSpan.FromDays(10));
			modelType.GetProperty("Window")!.SetValue(instance, new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
			modelType.GetProperty("ApiVersion")!.SetValue(instance, new Version(2, 0));
			modelType.GetProperty("Day")!.SetValue(instance, new DateOnly(2025, 6, 1));
			modelType.GetProperty("Hour")!.SetValue(instance, new TimeOnly(12, 0));
			modelType.GetProperty(property)!.SetValue(instance, value);

			var result = schemaType.GetMethod("Validate")!.Invoke(null, [instance])!;
			var errors = (ImmutableArray<ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;

			await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
			await Assert.That(errors.Length).IsEqualTo(1);
			await Assert.That(errors[0].Code).IsEqualTo("invalid_range");
			await Assert.That(errors[0].Path[0]).IsEqualTo(property);
		}

		await AssertFails("MaxTimeRange", TimeSpan.FromDays(-1));
		await AssertFails("MaxTimeRange", TimeSpan.FromDays(400));
		await AssertFails("Window", new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero));
		await AssertFails("ApiVersion", new Version(3, 0));
		await AssertFails("Day", new DateOnly(2031, 1, 1));
		await AssertFails("Hour", new TimeOnly(23, 0));

		// Valid values produce success.
		var valid = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("MaxTimeRange")!.SetValue(valid, TimeSpan.FromDays(10));
		modelType.GetProperty("Window")!.SetValue(valid, new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));
		modelType.GetProperty("ApiVersion")!.SetValue(valid, new Version(2, 0));
		modelType.GetProperty("Day")!.SetValue(valid, new DateOnly(2025, 6, 1));
		modelType.GetProperty("Hour")!.SetValue(valid, new TimeOnly(12, 0));

		var validResult = schemaType.GetMethod("Validate")!.Invoke(null, [valid])!;
		await Assert.That((bool)validResult.GetType().GetProperty("IsSuccess")!.GetValue(validResult)!).IsTrue();
	}
}
