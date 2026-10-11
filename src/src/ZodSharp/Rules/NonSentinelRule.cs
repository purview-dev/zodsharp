using System.Runtime.CompilerServices;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that rejects "sentinel" values — the framework default or boundary values that are
/// commonly persisted by an ORM to represent an unset field (for example <see cref="Guid.Empty"/> or
/// <see cref="DateTime.MinValue"/>/<see cref="DateTime.MaxValue"/>).
/// </summary>
/// <typeparam name="T">The value type; only the supported sentinel types are inspected.</typeparam>
/// <remarks>
/// <para>
/// Built-in detection covers the types that map to database columns with column-level sentinels:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="Guid"/> — <see cref="Guid.Empty"/>.</description></item>
/// <item><description><see cref="DateTime"/> — <see cref="DateTime.MinValue"/> and <see cref="DateTime.MaxValue"/>.</description></item>
/// <item><description><see cref="DateTimeOffset"/> — <see cref="DateTimeOffset.MinValue"/> and <see cref="DateTimeOffset.MaxValue"/>.</description></item>
/// <item><description><see cref="DateOnly"/> — <see cref="DateOnly.MinValue"/> and <see cref="DateOnly.MaxValue"/>.</description></item>
/// <item><description><see cref="TimeOnly"/> — <see cref="TimeOnly.MinValue"/> and <see cref="TimeOnly.MaxValue"/>.</description></item>
/// <item><description><see cref="string"/> — <see langword="null"/>, empty, or whitespace.</description></item>
/// </list>
/// <para>
/// Every other type is treated as valid, so the rule can be closed with a type it does not know about
/// without rejecting that type's entire value range. Detection uses a <c>typeof(T)</c> dispatch and
/// reinterprets the value in place, so no boxing occurs on the validation path.
/// </para>
/// </remarks>
[Core.ZodRule]
public readonly record struct NonSentinelRule<T> : Core.IValidationRule<T>, Core.IZodRule, JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_value";

	/// <summary>Gets the message format; <c>{0}</c> is a description of the offending sentinel value.</summary>
	public const string MessageFormat = "Value has a sentinel value, but got {0}";

	/// <summary>
	/// The JSON Schema <c>pattern</c> for a canonical Guid that is not the all-zero <see cref="Guid.Empty"/>.
	/// </summary>
	public const string GuidJsonSchemaPattern =
		"^(?!00000000-0000-0000-0000-000000000000$)[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

	readonly string _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="NonSentinelRule{T}"/> struct.
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public NonSentinelRule(string? message = null, string? code = null)
	{
		_message = message.Or(MessageFormat);
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is not a sentinel value.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in T value) => !SentinelValues<T>.IsSentinel(value);

	/// <summary>
	/// Applies the JSON Schema constraint that rejects the value's sentinel, where the wire form is known.
	/// </summary>
	/// <param name="schema">The schema for the value being validated.</param>
	/// <remarks>
	/// A string sentinel (null/empty/whitespace) becomes <c>minLength: 1</c> plus a non-whitespace pattern; a
	/// GUID sentinel (<see cref="Guid.Empty"/>) becomes a <c>not: { const: ... }</c> against the all-zero
	/// UUID. A date/time sentinel depends on the exact serialized form, which the exporter cannot know here,
	/// so it is reported through <see cref="JsonSchema.JsonSchemaDefinition.UnsupportedRules"/> instead of
	/// being guessed or dropped.
	/// </remarks>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);

		if (typeof(T) == typeof(string))
		{
			schema.MinLength = 1;
			JsonSchema.JsonSchemaRules.ConstrainPattern(schema, "\\S");
			return;
		}

		if (typeof(T) == typeof(Guid))
		{
			// Expressed as a pattern rather than `not: { const: ... }` so it survives converters that do not
			// implement JSON Schema `not` (for example Zod's fromJSONSchema). The negative lookahead rejects
			// the all-zero UUID while the body requires the canonical 8-4-4-4-12 form. Composed through
			// ConstrainPattern so a second pattern (for example a UUID version constraint) is not lost.
			JsonSchema.JsonSchemaRules.ConstrainPattern(schema, GuidJsonSchemaPattern);
			return;
		}

		JsonSchema.JsonSchemaRules.MarkUnsupported(schema, nameof(NonSentinelRule<>));
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in T value) =>
		RuleMessage.Format(_message ?? MessageFormat, SentinelValues<T>.Describe(value));

	/// <summary>Gets the error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => null;
}

/// <summary>
/// Detects the sentinel value(s) for a supported type without boxing. Types that are not known to have a
/// sentinel are treated as never sentinel.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
static class SentinelValues<T>
{
	/// <summary>
	/// Determines whether <paramref name="value"/> is a sentinel value for <typeparamref name="T"/>.
	/// </summary>
	/// <param name="value">The value to inspect.</param>
	/// <returns><see langword="true"/> when the value is a known sentinel; otherwise <see langword="false"/>.</returns>
	public static bool IsSentinel(in T value)
	{
		if (typeof(T) == typeof(Guid))
			return Unsafe.As<T, Guid>(ref Unsafe.AsRef(in value)) == Guid.Empty;

		if (typeof(T) == typeof(DateTime))
		{
			var typed = Unsafe.As<T, DateTime>(ref Unsafe.AsRef(in value));
			return typed == DateTime.MinValue || typed == DateTime.MaxValue;
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			var typed = Unsafe.As<T, DateTimeOffset>(ref Unsafe.AsRef(in value));
			return typed == DateTimeOffset.MinValue || typed == DateTimeOffset.MaxValue;
		}

		if (typeof(T) == typeof(DateOnly))
		{
			var typed = Unsafe.As<T, DateOnly>(ref Unsafe.AsRef(in value));
			return typed == DateOnly.MinValue || typed == DateOnly.MaxValue;
		}

		if (typeof(T) == typeof(TimeOnly))
		{
			var typed = Unsafe.As<T, TimeOnly>(ref Unsafe.AsRef(in value));
			return typed == TimeOnly.MinValue || typed == TimeOnly.MaxValue;
		}

		if (typeof(T) == typeof(string))
		{
			var typed = Unsafe.As<T, string?>(ref Unsafe.AsRef(in value));
			return string.IsNullOrWhiteSpace(typed);
		}

		return false;
	}

	/// <summary>
	/// Describes the sentinel <paramref name="value"/> represents, for use in an error message (for example
	/// <c>"an empty GUID"</c>). A value that is not a known sentinel is described by its own string
	/// representation.
	/// </summary>
	/// <param name="value">The value to describe.</param>
	/// <returns>A human-readable description of the value.</returns>
	public static string Describe(in T value)
	{
		if (typeof(T) == typeof(Guid))
			return Unsafe.As<T, Guid>(ref Unsafe.AsRef(in value)) == Guid.Empty ? "an empty GUID" : Raw(in value);

		if (typeof(T) == typeof(DateTime))
		{
			var typed = Unsafe.As<T, DateTime>(ref Unsafe.AsRef(in value));
			return typed == DateTime.MinValue ? "DateTime.MinValue"
				: typed == DateTime.MaxValue ? "DateTime.MaxValue"
				: Raw(in value);
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			var typed = Unsafe.As<T, DateTimeOffset>(ref Unsafe.AsRef(in value));
			return typed == DateTimeOffset.MinValue ? "DateTimeOffset.MinValue"
				: typed == DateTimeOffset.MaxValue ? "DateTimeOffset.MaxValue"
				: Raw(in value);
		}

		if (typeof(T) == typeof(DateOnly))
		{
			var typed = Unsafe.As<T, DateOnly>(ref Unsafe.AsRef(in value));
			return typed == DateOnly.MinValue ? "DateOnly.MinValue"
				: typed == DateOnly.MaxValue ? "DateOnly.MaxValue"
				: Raw(in value);
		}

		if (typeof(T) == typeof(TimeOnly))
		{
			var typed = Unsafe.As<T, TimeOnly>(ref Unsafe.AsRef(in value));
			return typed == TimeOnly.MinValue ? "TimeOnly.MinValue"
				: typed == TimeOnly.MaxValue ? "TimeOnly.MaxValue"
				: Raw(in value);
		}

		if (typeof(T) == typeof(string))
		{
			var typed = Unsafe.As<T, string?>(ref Unsafe.AsRef(in value));
			return typed switch
			{
				null => "null",
				{ Length: 0 } => "an empty string",
				_ when string.IsNullOrWhiteSpace(typed) => "whitespace",
				_ => typed,
			};
		}

		return Raw(in value);
	}

	static string Raw(in T value) => value?.ToString() ?? "null";
}
