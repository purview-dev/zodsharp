using System.Globalization;
using ZodSharp.Rules;

namespace ZodSharp.JsonSchema;

/// <summary>
/// Implemented by a schema (or validator) that can describe its own JSON Schema, so
/// <see cref="ToJsonSchemaConverter"/> can export it.
/// </summary>
/// <remarks>
/// The source generator emits this on every generated <c>{Type}SchemaValidator</c>. That is what lets an
/// attribute-generated <c>[ZodSchema]</c> schema be exported through <c>Z.ToJsonSchema</c> without rebuilding
/// its rules by hand with <c>Z.Object()</c>/<c>Z.String()</c>.
/// </remarks>
public interface IJsonSchemaProvider
{
	/// <summary>Builds the JSON Schema definition for the schema's target type.</summary>
	/// <returns>The JSON Schema definition, without document metadata such as <c>$schema</c>/<c>$id</c>.</returns>
	JsonSchemaDefinition BuildJsonSchema();
}

/// <summary>
/// Implemented by a validation rule that can describe its own JSON Schema constraint, so the exporter
/// preserves it instead of dropping it.
/// </summary>
/// <remarks>
/// A rule that cannot be represented (or that simply does not implement this interface) is reported through
/// <see cref="JsonSchemaDefinition.UnsupportedRules"/>; it is never dropped silently.
/// </remarks>
public interface IJsonSchemaRule
{
	/// <summary>Applies this rule's JSON Schema constraint(s) to <paramref name="schema"/>.</summary>
	/// <param name="schema">The schema being built for the value the rule validates.</param>
	void ApplyTo(JsonSchemaDefinition schema);
}

/// <summary>
/// Helpers for applying <see cref="IJsonSchemaRule"/> rules to a schema during export.
/// </summary>
public static class JsonSchemaRules
{
	/// <summary>
	/// The JSON Schema <c>pattern</c> for the System.Text.Json <see cref="TimeOnly"/> wire form
	/// (<c>HH:mm:ss[.fffffff]</c>, with no offset).
	/// </summary>
	/// <remarks>
	/// JSON Schema's <c>format: time</c> is an RFC 3339 full-time and therefore requires an offset. A
	/// System.Text.Json <see cref="TimeOnly"/> is a local time and never carries one, so a strict
	/// <c>format: time</c> consumer rejects every value. The local lexical space is expressed as this pattern
	/// instead, which a standards-based consumer can evaluate without a format assertion.
	/// </remarks>
	public const string TimeOnlyPattern = "^(?:[01]\\d|2[0-3]):[0-5]\\d:[0-5]\\d(?:\\.\\d{1,7})?$";

	/// <summary>
	/// Applies <paramref name="rule"/> to <paramref name="schema"/> when it implements
	/// <see cref="IJsonSchemaRule"/>; otherwise records the rule as unsupported so the exported schema is
	/// visibly incomplete rather than silently weaker.
	/// </summary>
	/// <param name="schema">The schema the rule constrains.</param>
	/// <param name="rule">The rule instance.</param>
	public static void Apply(JsonSchemaDefinition schema, object rule)
	{
		ArgumentNullException.ThrowIfNull(schema);
		ArgumentNullException.ThrowIfNull(rule);

		if (rule is IJsonSchemaRule mapped)
		{
			mapped.ApplyTo(schema);
			return;
		}

		MarkUnsupported(schema, rule.GetType().Name);
	}

	/// <summary>
	/// Applies <paramref name="rule"/> with the wire-form assumption a <c>DateTimeOffset</c> sentinel mapping
	/// needs; every other rule ignores <paramref name="dateTimeOffsetSerialization"/>.
	/// </summary>
	/// <param name="schema">The schema the rule constrains.</param>
	/// <param name="rule">The rule instance.</param>
	/// <param name="dateTimeOffsetSerialization">
	/// The wire form assumed for a <see cref="DateTimeOffset"/> value.
	/// </param>
	/// <remarks>
	/// The generated <c>BuildJsonSchema</c> calls this overload for <see cref="NonSentinelRule{T}"/> so the
	/// rule itself owns both the default and the UTC mapping. A rule that does not describe its own JSON
	/// Schema constraint is still reported as unsupported.
	/// </remarks>
	public static void Apply(
		JsonSchemaDefinition schema,
		object rule,
		DateTimeOffsetSerialization dateTimeOffsetSerialization
	)
	{
		ArgumentNullException.ThrowIfNull(schema);
		ArgumentNullException.ThrowIfNull(rule);

		if (rule is NonSentinelRule<DateTimeOffset> dateTimeOffsetRule)
		{
			dateTimeOffsetRule.ApplyTo(schema, dateTimeOffsetSerialization);
			return;
		}

		Apply(schema, rule);
	}

	/// <summary>
	/// Records <paramref name="ruleName"/> in <see cref="JsonSchemaDefinition.UnsupportedRules"/>.
	/// </summary>
	/// <param name="schema">The schema that could not reproduce the rule.</param>
	/// <param name="ruleName">The rule's type name.</param>
	public static void MarkUnsupported(JsonSchemaDefinition schema, string ruleName)
	{
		ArgumentNullException.ThrowIfNull(schema);
		ArgumentException.ThrowIfNullOrEmpty(ruleName);

		schema.UnsupportedRules ??= [];
		if (!schema.UnsupportedRules.Contains(ruleName, StringComparer.Ordinal))
			schema.UnsupportedRules.Add(ruleName);
	}

	/// <summary>
	/// Adds a <c>pattern</c> constraint to <paramref name="schema"/> without discarding a pattern that is
	/// already present. JSON Schema has a single <c>pattern</c> keyword, so two rules that both constrain
	/// the string's shape would otherwise silently overwrite one another (last writer wins).
	/// </summary>
	/// <param name="schema">The schema the pattern constrains.</param>
	/// <param name="pattern">The pattern to require in addition to any already present.</param>
	/// <remarks>
	/// The first pattern uses the <c>pattern</c> keyword, which keeps single-constraint exports unchanged.
	/// A second, different pattern promotes the constraints to <c>allOf</c>, with each branch carrying the
	/// schema's type: a consumer that discards a type-less branch (for example Zod's
	/// <c>fromJSONSchema</c>, which maps one to <c>z.any()</c>) would otherwise drop the constraint.
	/// </remarks>
	public static void ConstrainPattern(JsonSchemaDefinition schema, string pattern)
	{
		ArgumentNullException.ThrowIfNull(schema);
		ArgumentException.ThrowIfNullOrEmpty(pattern);

		// Fast path: nothing constrains the pattern yet, so the single keyword is enough.
		if (schema.Pattern is null && schema.AllOf is not { Count: > 0 })
		{
			schema.Pattern = pattern;
			return;
		}

		if (string.Equals(schema.Pattern, pattern, StringComparison.Ordinal))
			return;

		if (
			schema.Pattern is null
			&& schema.AllOf is { } branches
			&& branches.Any(branch => string.Equals(branch.Pattern, pattern, StringComparison.Ordinal))
		)
		{
			return;
		}

		schema.AllOf ??= [];

		if (schema.Pattern is { Length: > 0 } current)
		{
			schema.AllOf.Insert(0, PatternBranch(schema.Type, current));
			schema.Pattern = null;
		}

		schema.AllOf.Add(PatternBranch(schema.Type, pattern));
	}

	static JsonSchemaDefinition PatternBranch(string? type, string pattern) =>
		new() { Type = type ?? "string", Pattern = pattern };

	/// <summary>
	/// Builds the JSON Schema for a native enum: the defined members, minus any <paramref name="disallowed"/>
	/// member, in their numeric wire form (System.Text.Json's default enum representation).
	/// </summary>
	/// <typeparam name="TEnum">The enum type.</typeparam>
	/// <param name="disallowed">Members excluded from the allowed set.</param>
	/// <returns>The enum schema.</returns>
	public static JsonSchemaDefinition EnumSchema<TEnum>(params TEnum[] disallowed)
		where TEnum : struct, Enum
	{
		var disallowedSet = disallowed is { Length: > 0 } ? new HashSet<TEnum>(disallowed) : null;

		return new JsonSchemaDefinition
		{
			Enum =
			[
				.. Enum.GetValues<TEnum>()
					.Where(value => disallowedSet is null || !disallowedSet.Contains(value))
					.Select(static value =>
						Convert.ChangeType(value, Enum.GetUnderlyingType(typeof(TEnum)), CultureInfo.InvariantCulture)
					),
			],
		};
	}
}

/// <summary>
/// Converts a rule's typed bound to the <see cref="double"/> a JSON Schema numeric keyword expects.
/// </summary>
/// <remarks>
/// Rules are generic over the value they validate, so the bound is only known at the call site. A bound that
/// is not numeric (for example a <see cref="DateTime"/> bound closed by <c>ZodDate</c>) is reported as
/// unsupported by the caller rather than silently mistranslated.
/// </remarks>
static class JsonSchemaNumbers
{
	/// <summary>
	/// Converts <paramref name="value"/> to a <see cref="double"/> when it is a numeric type.
	/// </summary>
	/// <param name="value">The boxed bound.</param>
	/// <param name="number">The converted value when the method returns <see langword="true"/>.</param>
	/// <returns><see langword="true"/> when the value is numeric.</returns>
	internal static bool TryToDouble(object? value, out double number)
	{
		switch (value)
		{
			case byte b:
				number = b;
				return true;
			case sbyte sb:
				number = sb;
				return true;
			case short s:
				number = s;
				return true;
			case ushort us:
				number = us;
				return true;
			case int i:
				number = i;
				return true;
			case uint ui:
				number = ui;
				return true;
			case long l:
				number = l;
				return true;
			case ulong ul:
				number = ul;
				return true;
			case float f:
				number = f;
				return true;
			case double d:
				number = d;
				return true;
			case decimal m:
				number = (double)m;
				return true;
			default:
				number = 0;
				return false;
		}
	}
}
