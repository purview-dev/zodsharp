using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

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
/// <para>
/// The JSON Schema mapping applies to the same types. <see cref="DateTimeOffset"/> uses an offset-aware
/// pattern that rejects every RFC 3339 representation of the instant-based bounds; a UTC-only contract is
/// available as a narrower alternative. See <see cref="DateTimeOffsetBehaviorAttribute"/> and the overload
/// of <see cref="ApplyTo(JsonSchema.JsonSchemaDefinition, DateTimeOffsetSerialization)"/>.
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

	/// <summary>
	/// The JSON Schema <c>pattern</c> that rejects <see cref="DateOnly.MinValue"/> and
	/// <see cref="DateOnly.MaxValue"/> in their System.Text.Json wire form (<c>yyyy-MM-dd</c>).
	/// </summary>
	public const string DateOnlyJsonSchemaPattern = "^(?!0001-01-01$)(?!9999-12-31$)";

	/// <summary>
	/// The JSON Schema <c>pattern</c> that rejects <see cref="TimeOnly.MinValue"/> and
	/// <see cref="TimeOnly.MaxValue"/> in their System.Text.Json wire form (<c>HH:mm:ss[.fffffff]</c>).
	/// </summary>
	public const string TimeOnlyJsonSchemaPattern = "^(?!00:00(?::00(?:\\.0+)?)?$)(?!23:59:59\\.9999999\\d*$)";

	/// <summary>
	/// The JSON Schema <c>pattern</c> that rejects the deterministic System.Text.Json wire forms of
	/// <see cref="DateTime.MinValue"/> and <see cref="DateTime.MaxValue"/>: no offset, <c>Z</c>, or a zero
	/// offset. A non-zero offset is intentionally not rejected — System.Text.Json converts such a value into
	/// the receiver's local time zone, so it no longer represents the sentinel ticks.
	/// </summary>
	public const string DateTimeJsonSchemaPattern =
		"^(?!0001-01-01T00:00:00(?:\\.0+)?(?:Z|[+-]00:00)?$)"
		+ "(?!9999-12-31T23:59:59\\.9999999\\d*(?:Z|[+-]00:00)?$)";

	/// <summary>
	/// The JSON Schema <c>pattern</c> that rejects every RFC 3339 representation of
	/// <see cref="DateTimeOffset.MinValue"/> and <see cref="DateTimeOffset.MaxValue"/>, whatever offset the
	/// wire form carries.
	/// </summary>
	/// <remarks>
	/// <para>
	/// DateTimeOffset equality compares instants, and RFC 3339 permits the same instant at different offsets.
	/// For the minimum, the offset must be non-negative and the local wall clock equals the offset
	/// (<c>0001-01-01T05:30:00+05:30</c>); for the maximum, the local wall clock is
	/// <c>23:59:59.9999999</c> minus the offset (<c>9999-12-31T18:29:59.9999999-05:30</c>).
	/// </para>
	/// <para>
	/// The pattern correlates the local time with the offset using a backreference for the minimum and two
	/// lookaheads for the maximum (the hour and minute relations are independent). It is therefore exact for
	/// every offset System.Text.Json can write, not just the zero-offset form.
	/// </para>
	/// </remarks>
	public static readonly string DateTimeOffsetJsonSchemaPattern = BuildDateTimeOffsetJsonSchemaPattern();

	/// <summary>
	/// The JSON Schema <c>pattern</c> for a UTC-only <see cref="DateTimeOffset"/> contract: it requires a
	/// <c>Z</c> or zero offset and rejects <see cref="DateTimeOffset.MinValue"/> and
	/// <see cref="DateTimeOffset.MaxValue"/>.
	/// </summary>
	/// <remarks>
	/// Because every accepted string has a zero offset, it denotes exactly one instant, so this pattern is
	/// an exact representation of the instant-based sentinel check. Pair it with a converter that also
	/// rejects non-UTC input; see <see cref="DateTimeOffsetBehaviorAttribute"/>.
	/// </remarks>
	public const string DateTimeOffsetUtcJsonSchemaPattern =
		"^(?=.*(?:Z|[+-]00:00)$)"
		+ "(?!0001-01-01T00:00:00(?:\\.0+)?(?:Z|[+-]00:00)$)"
		+ "(?!9999-12-31T23:59:59\\.9999999\\d*(?:Z|[+-]00:00)$)";

	static string BuildDateTimeOffsetJsonSchemaPattern()
	{
		// Maximum: local time = 23:59:59.9999999 minus the offset. The hour relation
		// (local 23-offsetHour) and the minute relation (local 59-offsetMinute) are independent, so two
		// lookaheads correlate them without enumerating every offset pair.
		StringBuilder maxHours = new("9999-12-31T23:[0-5]\\d:59\\.9999999\\d*(?:Z|[+-]00:00|-00:[0-5]\\d)");
		for (var offsetHour = 1; offsetHour <= 14; offsetHour++)
		{
			maxHours.Append(
				CultureInfo.InvariantCulture,
				$"|9999-12-31T{TwoDigits(23 - offsetHour)}:[0-5]\\d:59\\.9999999\\d*-{TwoDigits(offsetHour)}:[0-5]\\d"
			);
		}

		StringBuilder maxMinutes = new("9999-12-31T[0-2]\\d:59:59\\.9999999\\d*(?:Z|[+-]00:00|-[0-2]\\d:00)");
		for (var offsetMinute = 1; offsetMinute <= 59; offsetMinute++)
		{
			maxMinutes.Append(
				CultureInfo.InvariantCulture,
				$"|9999-12-31T[0-2]\\d:{TwoDigits(59 - offsetMinute)}:59\\.9999999\\d*-[0-2]\\d:{TwoDigits(offsetMinute)}"
			);
		}

		// Minimum: local time = the offset, so numbered backreferences require both components to match. The
		// offset cannot be negative (it would fall below DateTimeOffset.MinValue); zero-offset forms may be
		// written as Z or -00:00 as well as +00:00 (which the backreferences cover). Numbered rather than named
		// groups keep the pattern usable by JSON Schema validators built on regex engines that do not support
		// named backreferences.
		return "^(?!"
			+ "(?=(?:"
			+ maxHours
			+ ")$)(?=(?:"
			+ maxMinutes
			+ ")$))"
			+ "(?!0001-01-01T(0\\d|1[0-4]):([0-5]\\d):00(?:\\.0+)?\\+\\1:\\2"
			+ "|0001-01-01T00:00:00(?:\\.0+)?(?:Z|-00:00))";
	}

	static string TwoDigits(int value) => value.ToString("00", CultureInfo.InvariantCulture);

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
	/// Applies the JSON Schema constraint that rejects the value's sentinel, assuming the default
	/// <see cref="DateTimeOffsetSerialization.PreserveOffset"/> wire form.
	/// </summary>
	/// <param name="schema">The schema for the value being validated.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema) =>
		ApplyTo(schema, DateTimeOffsetSerialization.PreserveOffset);

	/// <summary>
	/// Applies the JSON Schema constraint that rejects the value's sentinel, where the wire form is known.
	/// </summary>
	/// <param name="schema">The schema for the value being validated.</param>
	/// <param name="dateTimeOffsetSerialization">
	/// The wire form assumed for a <see cref="DateTimeOffset"/> value. It is ignored for every other type.
	/// </param>
	/// <remarks>
	/// <para>
	/// A string sentinel (null/empty/whitespace) becomes <c>minLength: 1</c> plus a non-whitespace pattern; a
	/// GUID sentinel (<see cref="Guid.Empty"/>) becomes a canonical-Guid pattern that rejects the all-zero
	/// UUID. <see cref="DateOnly"/>, <see cref="TimeOnly"/> and <see cref="DateTime"/> become patterns that
	/// reject the System.Text.Json wire forms of their bounds; those mappings are exact for the deterministic
	/// wire forms.
	/// </para>
	/// <para>
	/// <see cref="DateTimeOffset"/> compares instants, and RFC 3339 permits offset-equivalent
	/// representations. <see cref="DateTimeOffsetSerialization.PreserveOffset"/> uses an offset-aware
	/// pattern that correlates the local time with the offset and rejects every such representation of
	/// <see cref="DateTimeOffset.MinValue"/> and <see cref="DateTimeOffset.MaxValue"/>.
	/// <see cref="DateTimeOffsetSerialization.Utc"/> is the narrower alternative: the schema requires a zero
	/// offset, so the same bounds reduce to a shorter pattern.
	/// </para>
	/// <para>
	/// A type with no known sentinel always passes, so it contributes no constraint and is not reported as
	/// unsupported.
	/// </para>
	/// </remarks>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema, DateTimeOffsetSerialization dateTimeOffsetSerialization)
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

		if (typeof(T) == typeof(DateOnly))
		{
			JsonSchema.JsonSchemaRules.ConstrainPattern(schema, DateOnlyJsonSchemaPattern);
			return;
		}

		if (typeof(T) == typeof(TimeOnly))
		{
			JsonSchema.JsonSchemaRules.ConstrainPattern(schema, TimeOnlyJsonSchemaPattern);
			return;
		}

		if (typeof(T) == typeof(DateTime))
		{
			JsonSchema.JsonSchemaRules.ConstrainPattern(schema, DateTimeJsonSchemaPattern);
			return;
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			if (dateTimeOffsetSerialization == DateTimeOffsetSerialization.Utc)
			{
				JsonSchema.JsonSchemaRules.ConstrainPattern(schema, DateTimeOffsetUtcJsonSchemaPattern);
				return;
			}

			JsonSchema.JsonSchemaRules.ConstrainPattern(schema, DateTimeOffsetJsonSchemaPattern);
			return;
		}

		// No built-in sentinel is known for T, so IsValid always passes and the schema needs no constraint.
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
