using System.Collections.Immutable;
using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	/// <summary>
	/// A model with one member validated by a DataAnnotations size attribute and one by the matching ZodSharp
	/// collection-count attribute, so the two members' errors can be compared field by field.
	/// </summary>
	const string CollectionCountSource =
		@"
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Rules;

namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		[MinLength(2)]
		public List<int> MinLegacy { get; set; } = [];

		[MinCount(2)]
		public List<int> MinCounted { get; set; } = [];

		[MaxLength(1)]
		public List<int> MaxLegacy { get; set; } = [1, 2];

		[MaxCount(1)]
		public List<int> MaxCounted { get; set; } = [1, 2];

		[Length(1, 3)]
		public Dictionary<string, int> RangeLegacy { get; set; } = [];

		[CountRange(1, 3)]
		public Dictionary<string, int> RangeCounted { get; set; } = [];
	}
}";

	[Test]
	[Arguments("MinLegacy", "MinCounted")]
	[Arguments("MaxLegacy", "MaxCounted")]
	[Arguments("RangeLegacy", "RangeCounted")]
	public async Task CollectionCount_GivenCountAttribute_ReportsTheSameErrorIdentityAsTheLengthAttribute(
		string legacyMember,
		string countedMember,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var driverResult = await GenerateAsync(
			CollectionCountSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var widget = Activator.CreateInstance(assembly!.GetType("Testing.Widget")!)!;

		// Act
		var errors = ValidateWidget(assembly, widget);
		var legacy = errors.Single(error => error.Path[0] == legacyMember);
		var counted = errors.Single(error => error.Path[0] == countedMember);

		// Assert — everything but the member name is identical.
		await Assert.That(counted.Code).IsEqualTo(legacy.Code);
		await Assert.That(counted.Category).IsEqualTo(legacy.Category);
		await Assert.That(counted.Origin).IsEqualTo(legacy.Origin);
		await Assert.That(counted.Minimum).IsEqualTo(legacy.Minimum);
		await Assert.That(counted.Maximum).IsEqualTo(legacy.Maximum);
		await Assert.That(counted.Inclusive).IsEqualTo(legacy.Inclusive);
		await Assert.That(counted.MessageFormat).IsEqualTo(legacy.MessageFormat);
		await Assert
			.That(counted.Message.Replace(countedMember, legacyMember, StringComparison.Ordinal))
			.IsEqualTo(legacy.Message);
	}

	[Test]
	public async Task CollectionCount_GivenSatisfiedCounts_ReportsNoCountErrors(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
using System.Collections.Generic;
using ZodSharp;
using ZodSharp.Rules;

namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		[MinCount(1)]
		[MaxCount(2)]
		public int[] Values { get; set; } = [1, 2];

		[CountRange(0, 1)]
		public IReadOnlyCollection<string> Tags { get; set; } = [""a""];
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var widget = Activator.CreateInstance(assembly!.GetType("Testing.Widget")!)!;

		// Act
		var errors = ValidateWidget(assembly, widget);

		// Assert
		await Assert.That(errors.IsDefaultOrEmpty).IsTrue();
	}

	[Test]
	public async Task CollectionCount_GivenCustomMessage_UsesTheDataAnnotationsPlaceholders(
		CancellationToken cancellationToken
	)
	{
		// Arrange — Message is an alias of ErrorMessage: {0} is the display name, {1} the bound.
		const string source =
			@"
using System.Collections.Generic;
using ZodSharp;
using ZodSharp.Rules;

namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		[MinCount(2, Message = ""{0} needs {1} or more"")]
		public List<int> Values { get; set; } = [];
	}
}";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();
		var widget = Activator.CreateInstance(assembly!.GetType("Testing.Widget")!)!;

		// Act
		var errors = ValidateWidget(assembly, widget);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Message).IsEqualTo("Values needs 2 or more");
	}

	[Test]
	public async Task CollectionCount_JsonSchema_GivenCountAttributes_ExportsItemBounds(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
using System.Collections.Generic;
using ZodSharp;
using ZodSharp.Rules;

namespace Testing
{
	[ZodSchema]
	public class Widget
	{
		[MinCount(1)]
		[MaxCount(4)]
		public List<int> Values { get; set; } = [];

		[CountRange(2, 5)]
		public Dictionary<string, int> Counts { get; set; } = [];
	}
}";

		// Act
		var definition = await BuildDefinitionAsync(source, "WidgetSchema", cancellationToken);

		// Assert
		var values = definition.Properties!["values"];
		await Assert.That(values.MinItems).IsEqualTo(1);
		await Assert.That(values.MaxItems).IsEqualTo(4);

		var counts = definition.Properties!["counts"];
		await Assert.That(counts.MinProperties).IsEqualTo(2);
		await Assert.That(counts.MaxProperties).IsEqualTo(5);
	}

	static ImmutableArray<ValidationError> ValidateWidget(System.Reflection.Assembly assembly, object widget)
	{
		var result = assembly.GetType("Testing.WidgetSchema")!.GetMethod("Validate")!.Invoke(null, [widget])!;
		dynamic dynResult = result;
		return (ImmutableArray<ValidationError>)dynResult.Errors;
	}
}
