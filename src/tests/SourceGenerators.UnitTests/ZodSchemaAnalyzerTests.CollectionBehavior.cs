using ZodSharp.SourceGenerators.Helpers;

namespace ZodSharp.SourceGenerators;

public partial class ZodSchemaAnalyzerTests
{
	[Test]
	public async Task CollectionBehavior_GivenNonCollectionProperty_ProducesZODSGEN046(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the setting can never take effect on a scalar property.
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Widget
    {
        [CollectionBehavior(AllowNulls = true)]
        public string Name { get; set; } = string.Empty;
    }
}";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CollectionBehaviorNotApplicable);
	}

	[Test]
	public async Task CollectionBehavior_GivenNonNullableValueElementCollection_ProducesZODSGEN046(
		CancellationToken cancellationToken
	)
	{
		// Arrange — an int[] can never contain null, so the setting has no effect.
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Widget
    {
        [CollectionBehavior(AllowNulls = true)]
        public int[] Values { get; set; } = [];
    }
}";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CollectionBehaviorNotApplicable);
	}

	[Test]
	public async Task CollectionBehavior_GivenNullableElementCollection_ProducesNoZODSGEN046(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
    [ZodSchema]
    public class Widget
    {
        [CollectionBehavior(AllowNulls = true)]
        public string[] Items { get; set; } = [];
    }
}";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.CollectionBehaviorNotApplicable);
	}

	[Test]
	public async Task CollectionBehavior_GivenDictionaryOfNonNullableValues_ProducesZODSGEN046(
		CancellationToken cancellationToken
	)
	{
		// Arrange — a Dictionary<string, int> cannot contain a null value, so the setting has no effect.
		const string source =
			@"
using System.Collections.Generic;

namespace Testing
{
    [ZodSchema]
    public class Widget
    {
        [CollectionBehavior(AllowNulls = true)]
        public Dictionary<string, int> Counts { get; set; } = [];
    }
}";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).HasDiagnostic(DiagnosticLibrary.CollectionBehaviorNotApplicable);
	}

	[Test]
	public async Task CollectionBehavior_GivenDictionaryOfNullableValues_ProducesNoZODSGEN046(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
using System.Collections.Generic;

namespace Testing
{
    [ZodSchema]
    public class Widget
    {
        [CollectionBehavior(AllowNulls = true)]
        public Dictionary<string, string?> Values { get; set; } = [];
    }
}";

		// Act
		var result = await AnalyzeAsync(source, cancellationToken);

		// Assert
		await Assert.That(result).DoesNotHaveDiagnostic(DiagnosticLibrary.CollectionBehaviorNotApplicable);
	}
}
