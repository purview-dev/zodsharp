using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using ZodSharp.AspNetCore.ClientValidation;
using ZodSharp.Blazor.Fixtures;
using ZodSharp.Core;
using ZodSharp.Rules;

namespace ZodSharp.Blazor.Ssr;

/// <summary>
/// The mapping from ZodSharp rules to client rules, and the provider's payload merge.
/// </summary>
public class ClientValidationRulesTests
{
	[Test]
	public async Task FromAttribute_GivenCultureSensitiveComparison_EmitsNoClientRule()
	{
		// Arrange
		StartsWithAttribute attribute = new("ab", StringComparison.CurrentCulture);

		// Act
		var rules = ZodClientValidationRules.FromAttribute(attribute);

		// Assert
		await Assert.That(rules).IsEmpty();
	}

	[Test]
	public async Task FromAttribute_GivenDataAnnotationsAttribute_EmitsNoClientRule()
	{
		// Arrange
		RequiredAttribute attribute = new();

		// Act
		var rules = ZodClientValidationRules.FromAttribute(attribute);

		// Assert
		await Assert.That(rules).IsEmpty();
	}

	[Test]
	public async Task FromAttribute_GivenCustomRuleAttribute_MapsTheRulesMetadata()
	{
		// Arrange
		OrderPrefixAttribute attribute = new() { Prefix = "ORD-" };

		// Act
		var rule = ZodClientValidationRules.FromAttribute(attribute).Single();

		// Assert
		await Assert.That(rule.Name).IsEqualTo(ZodClientValidationRules.StartsWith);
		await Assert.That(rule.Parameters["prefix"]).IsEqualTo("ORD-");
		await Assert.That(rule.Message).IsEqualTo("'{value}' must start with ORD-");
		await Assert.That(rule.Parameters[ZodClientValidationRules.MessageTemplateParameter]).IsEqualTo(rule.Message);
	}

	[Test]
	public async Task For_GivenRule_ReturnsPlatformClientValidationRules()
	{
		// Arrange
		UUIDRule rule = new(UuidVersion.V4);

		// Act
		var rules = ZodClientValidationRules.For(rule);

		// Assert
		await Assert.That(rules.Single().Name).IsEqualTo(ZodClientValidationRules.Uuid);
		await Assert.That(rules.Single().Parameters["version"]).IsEqualTo("4");
	}

	[Test]
	public async Task FromJsonSchema_GivenRuntimeSchemaProperty_MapsTheKeywords()
	{
		// Arrange
		var schema = Z.ToJsonSchema(Z.Object().Field("Reference", Z.String().Min(3).Max(10)).Build());

		// Act
		var rules = ZodClientValidationRules.FromJsonSchema(schema.Properties!["Reference"], required: true);

		// Assert
		await Assert.That(rules.Select(static r => r.Name).ToArray()).IsEquivalentTo(["required", "length"]);
		await Assert.That(rules[1].Parameters["min"]).IsEqualTo("3");
		await Assert.That(rules[1].Parameters["max"]).IsEqualTo("10");
	}

	[Test]
	public async Task RenderClientValidationRules_GivenRegisteredRuntimeSchema_EmitsItsRules()
	{
		// Arrange
		Order order = new();
		ZodClientValidationOptions options = new();
		options.AddSchema<Order>(Z.Object().Field("Reference", Z.String().Min(3)).Build());
		ZodClientValidationProvider provider = new(inner: null, options);
		EditContext editContext = new(order);
		Dictionary<FieldIdentifier, string> fields = new()
		{
			[editContext.Field(nameof(Order.Reference))] = "Model.Reference",
		};

		// Act
		var json = ReadRules(provider.RenderClientValidationRules(editContext, fields));

		// Assert
		await Assert.That(json).Contains("\"name\":\"Model.Reference\"");
		await Assert.That(json).Contains("\"name\":\"minlength\"");
	}

	[Test]
	public async Task RenderClientValidationRules_GivenInnerPayload_MergesRulesIntoTheSameField()
	{
		// Arrange
		GeneratedOrder order = new();
		FixedProvider inner = new(
			/*lang=json,strict*/"""{"fields":[{"name":"Model.Reference","rules":[{"name":"required","message":"Required"}]}]}"""
		);
		ZodClientValidationProvider provider = new(inner, new());
		EditContext editContext = new(order);
		Dictionary<FieldIdentifier, string> fields = new()
		{
			[editContext.Field(nameof(GeneratedOrder.Reference))] = "Model.Reference",
		};

		// Act
		var json = ReadRules(provider.RenderClientValidationRules(editContext, fields));

		// Assert
		await Assert
			.That(json)
			.IsEqualTo(
				/*lang=json,strict*/"""{"fields":[{"name":"Model.Reference","rules":[{"name":"required","message":"Required"},{"name":"zod-startswith","message":"String must start with \u0027ORD-\u0027","params":{"prefix":"ORD-","comparison":"ordinal"}}]}]}"""
			);
	}

	[Test]
	public async Task RenderClientValidationRules_GivenNoZodRules_ReturnsTheInnerFragmentUnchanged()
	{
		// Arrange
		Order order = new();
		FixedProvider inner = new( /*lang=json,strict*/
			"""{"fields":[]}"""
		);
		ZodClientValidationProvider provider = new(inner, new());
		EditContext editContext = new(order);
		Dictionary<FieldIdentifier, string> fields = new()
		{
			[editContext.Field(nameof(Order.Reference))] = "Model.Reference",
		};

		// Act
		var fragment = provider.RenderClientValidationRules(editContext, fields);

		// Assert
		await Assert.That(fragment).IsSameReferenceAs(inner.Fragment);
	}

#pragma warning disable BL0006 // Do not use RenderTree types: reading the rendered payload is the point of the test.
	static string ReadRules(RenderFragment? fragment)
	{
		ArgumentNullException.ThrowIfNull(fragment);
		using RenderTreeBuilder builder = new();
		fragment(builder);
		var frames = builder.GetFrames();
		for (var i = 0; i < frames.Count; i++)
		{
			var frame = frames.Array[i];
			if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "data-rules")
				return (string)frame.AttributeValue;
		}

		throw new InvalidOperationException("No data-rules attribute was rendered.");
	}
#pragma warning restore BL0006

	sealed class FixedProvider(string json) : ClientValidationProvider
	{
		public RenderFragment Fragment { get; } =
			builder =>
			{
				builder.OpenElement(0, "blazor-client-validation-data");
				builder.AddAttribute(1, "data-rules", json);
				builder.CloseElement();
			};

		public override RenderFragment? RenderClientValidationRules(
			EditContext editContext,
			IReadOnlyDictionary<FieldIdentifier, string> renderedFields
		) => Fragment;
	}
}

/// <summary>
/// A hand-authored rule that reports Zod's <c>starts_with</c> metadata, so it gets the same client rule as the
/// built-in <see cref="StartsWithRule"/>.
/// </summary>
public readonly record struct OrderPrefixRule : IValidationRule<string>, IStringValidationRule, IZodRule
{
	public const string ErrorCode = "invalid_order_reference";

	public const string MessageFormat = "'{0}' must start with {1}";

	readonly string _prefix;

	public OrderPrefixRule(string prefix) => _prefix = prefix;

	public bool IsValid(in string value) => value?.StartsWith(_prefix, StringComparison.Ordinal) == true;

	public bool IsValid(ReadOnlySpan<char> value) => value.StartsWith(_prefix, StringComparison.Ordinal);

	public string GetErrorMessage(in string value) => RuleMessage.Format(MessageFormat, value, _prefix);

	public string GetErrorMessage(ReadOnlySpan<char> value) => GetErrorMessage(value.ToString());

	string? IZodRule.Code => ErrorCode;

	string? IZodRule.Origin => ZodTypeNames.String;

	IReadOnlyDictionary<string, object?>? IZodRule.Parameters =>
		new Dictionary<string, object?>
		{
			[ZodIssueParameters.Format] = "starts_with",
			[ZodIssueParameters.Prefix] = _prefix,
		};
}

[ZodRule(typeof(OrderPrefixRule))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class OrderPrefixAttribute : ValidationAttribute
{
	public string Prefix { get; set; } = string.Empty;
}
