#if NET11_0_OR_GREATER
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components.Forms;
using ZodSharp.Core;
using ZodSharp.JsonSchema;
using ZodSharp.Rules;

namespace ZodSharp.AspNetCore.ClientValidation;

/// <summary>
/// One client-side rule: the name the JavaScript validator is registered under
/// (<c>Blazor.formValidation.addValidator</c>), the message shown when it fails, and its string parameters.
/// </summary>
/// <param name="Name">The validator name.</param>
/// <param name="Message">The message.</param>
/// <param name="Parameters">The parameters.</param>
public sealed record ZodClientRule(string Name, string Message, IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// Maps ZodSharp rules to the client-side validators in <c>zodsharp-validation.js</c> (Purview.ZodSharp.Blazor).
/// </summary>
/// <remarks>
/// <para>
/// The mapping reads the rule's <see cref="IZodRule"/> metadata - its format parameters (<c>prefix</c>,
/// <c>suffix</c>, <c>includes</c>, <c>version</c>, <c>pattern</c>, <c>divisor</c>, <c>expected</c>) and its
/// <see cref="IZodRule.Lower"/>/<see cref="IZodRule.Upper"/> bounds - so a custom rule that reports the same metadata
/// gets the same client rule. A rule with no client equivalent (async, service-aware, culture-sensitive comparisons,
/// formats without a JavaScript validator) produces no client rule and is enforced on the server only.
/// </para>
/// <para>
/// Messages carry a <c>{value}</c> placeholder where the rule's message includes the validated value; the JavaScript
/// validator substitutes the input's value.
/// </para>
/// </remarks>
public static class ZodClientValidationRules
{
	/// <summary>
	/// The placeholder the JavaScript validators replace with the input's value.
	/// </summary>
	public const string ValuePlaceholder = "{value}";

	/// <summary>
	/// The parameter carrying a message template that contains <see cref="ValuePlaceholder"/>.
	/// </summary>
	public const string MessageTemplateParameter = "template";

	/// <summary>The validator name for a required prefix.</summary>
	public const string StartsWith = "zod-startswith";

	/// <summary>The validator name for a required suffix.</summary>
	public const string EndsWith = "zod-endswith";

	/// <summary>The validator name for a required substring.</summary>
	public const string Includes = "zod-includes";

	/// <summary>The validator name for a UUID (optionally of one version).</summary>
	public const string Uuid = "zod-uuid";

	/// <summary>The validator name for a CUID2.</summary>
	public const string Cuid2 = "zod-cuid2";

	/// <summary>The validator name for a ULID.</summary>
	public const string Ulid = "zod-ulid";

	/// <summary>The validator name for an IP address.</summary>
	public const string IPAddress = "zod-ip";

	/// <summary>The validator name for a CIDR block.</summary>
	public const string Cidr = "zod-cidr";

	/// <summary>The validator name for an integer.</summary>
	public const string WholeNumber = "zod-int";

	/// <summary>The validator name for a multiple of a divisor.</summary>
	public const string MultipleOf = "zod-multipleof";

	/// <summary>The validator name for an inclusive lower bound (number or date).</summary>
	public const string Min = "zod-min";

	/// <summary>The validator name for an inclusive upper bound (number or date).</summary>
	public const string Max = "zod-max";

	/// <summary>The validator name for an exclusive lower bound (number or date).</summary>
	public const string GreaterThan = "zod-gt";

	/// <summary>The validator name for an exclusive upper bound (number or date).</summary>
	public const string LessThan = "zod-lt";

	/// <summary>The validator name for a regular expression that must match somewhere in the value.</summary>
	public const string Regex = "zod-regex";

	/// <summary>
	/// Gets the client rules for <paramref name="rule"/>, without messages - for a custom validation attribute that
	/// implements <c>IClientValidationRuleProvider</c> (whose message comes from the attribute).
	/// </summary>
	/// <param name="rule">The rule.</param>
	/// <returns>The client rules; empty when the rule has no client equivalent.</returns>
	public static IReadOnlyList<ClientValidationRule> For(IZodRule rule)
	{
		ArgumentNullException.ThrowIfNull(rule);
		return [.. FromRule(rule, string.Empty).Select(static r => new ClientValidationRule(r.Name, r.Parameters))];
	}

	/// <summary>
	/// Gets the client rules for <paramref name="rule"/>.
	/// </summary>
	/// <param name="rule">The rule.</param>
	/// <param name="message">The message shown when a rule fails; may contain <see cref="ValuePlaceholder"/>.</param>
	/// <param name="comparison">For prefix and suffix rules, the comparison the rule uses.</param>
	/// <returns>The client rules; empty when the rule has no client equivalent.</returns>
	public static IReadOnlyList<ZodClientRule> FromRule(
		IZodRule rule,
		string message,
		StringComparison comparison = StringComparison.Ordinal
	)
	{
		ArgumentNullException.ThrowIfNull(rule);
		ArgumentNullException.ThrowIfNull(message);

		List<ZodClientRule> rules = [];
		var parameters = rule.Parameters;
		var format = Get(parameters, ZodIssueParameters.Format);

		switch (format)
		{
			case "starts_with" when TryGetComparison(comparison, out var mode):
				Add(
					rules,
					StartsWith,
					message,
					("prefix", Get(parameters, ZodIssueParameters.Prefix)),
					("comparison", mode)
				);
				return rules;
			case "ends_with" when TryGetComparison(comparison, out var mode):
				Add(
					rules,
					EndsWith,
					message,
					("suffix", Get(parameters, ZodIssueParameters.Suffix)),
					("comparison", mode)
				);
				return rules;
			case "starts_with" or "ends_with":
				return rules; // Culture-sensitive comparisons have no faithful JavaScript equivalent.
			case "includes":
				Add(rules, Includes, message, ("includes", Get(parameters, ZodIssueParameters.Includes)));
				return rules;
			case "uuid":
				Add(rules, Uuid, message, ("version", Get(parameters, ZodIssueParameters.Version)));
				return rules;
			case "cuid2":
				Add(rules, Cuid2, message);
				return rules;
			case "ulid":
				Add(rules, Ulid, message);
				return rules;
			case "ip" or "ipv4" or "ipv6":
				Add(rules, IPAddress, message, ("version", Version(format)));
				return rules;
			case "cidr" or "cidrv4" or "cidrv6":
				Add(rules, Cidr, message, ("version", Version(format)));
				return rules;
			case "regex":
				Add(rules, Regex, message, ("pattern", Get(parameters, ZodIssueParameters.Pattern)));
				return rules;
			default:
				break;
		}

		if (string.Equals(Get(parameters, ZodIssueParameters.Expected), "int", StringComparison.Ordinal))
		{
			Add(rules, WholeNumber, message);
			return rules;
		}

		if (Get(parameters, ZodIssueParameters.Divisor) is { } divisor)
		{
			Add(rules, MultipleOf, message, ("divisor", divisor));
			return rules;
		}

		// Bounds of values (not lengths or counts, whose origin is string, array or collection).
		var origin = rule.Origin;
		if (origin is ZodTypeNames.Number or ZodTypeNames.BigInt or ZodTypeNames.Date)
		{
			if (rule.Lower is { } lower && TryFormatBound(lower.Value, out var lowerValue, out var lowerKind))
				Add(rules, lower.Inclusive ? Min : GreaterThan, message, ("value", lowerValue), ("kind", lowerKind));

			if (rule.Upper is { } upper && TryFormatBound(upper.Value, out var upperValue, out var upperKind))
				Add(rules, upper.Inclusive ? Max : LessThan, message, ("value", upperValue), ("kind", upperKind));
		}

		return rules;
	}

	/// <summary>
	/// Gets the client rules for a ZodSharp rule attribute (a <see cref="ValidationAttribute"/> mapped to a rule with
	/// <see cref="ZodRuleAttribute"/>).
	/// </summary>
	/// <param name="attribute">The attribute.</param>
	/// <returns>The client rules; empty when the attribute is not a ZodSharp rule attribute or has no client equivalent.</returns>
	/// <remarks>
	/// The built-in rule attributes are mapped directly; any other <see cref="ZodRuleAttribute"/>-mapped attribute is read by
	/// reflection over its properties and its rule's constructors, so this is annotated
	/// <see cref="RequiresUnreferencedCodeAttribute"/>. <see cref="For(IZodRule)"/> and
	/// <see cref="FromRule(IZodRule, string, StringComparison)"/> are trim-safe.
	/// </remarks>
	[RequiresUnreferencedCode(ZodClientValidationTrimming.Message)]
	public static IReadOnlyList<ZodClientRule> FromAttribute(ValidationAttribute attribute)
	{
		ArgumentNullException.ThrowIfNull(attribute);

		try
		{
			return attribute switch
			{
				StartsWithAttribute { Prefix: { Length: > 0 } prefix } a => FromStringRule(
					new StartsWithRule(prefix, a.Comparison, a.Message),
					a.Comparison
				),
				EndsWithAttribute { Suffix: { Length: > 0 } suffix } a => FromStringRule(
					new EndsWithRule(suffix, a.Comparison, a.Message),
					a.Comparison
				),
				IncludesAttribute { Substring: { } substring } a => FromStringRule(
					new IncludesRule(substring, a.Message)
				),
				UUIDAttribute { Version: UuidVersion.None } a => FromStringRule(new UUIDRule(a.Message)),
				UUIDAttribute a => FromStringRule(new UUIDRule(a.Version, a.Message)),
				Cuid2Attribute a => FromStringRule(new Cuid2Rule(a.Message)),
				ULIDAttribute a => FromStringRule(new ULIDRule(a.Message)),
				IPAddressAttribute a => FromStringRule(new IPAddressRule(a.RuleType, a.Message)),
				CidrAttribute a => FromStringRule(new CidrRule(a.RuleType, a.Message)),
				RegexAttribute { Pattern: { Length: > 0 } pattern } a => FromStringRule(
					new RegexRule(pattern, a.Message)
				),
				IntAttribute a => FromRule(
					new IntRule<double>(a.Message),
					ValueMessage(a.Message ?? IntRule<double>.MessageFormat)
				),
				MultipleOfAttribute a when a.Divisor != 0 => FromRule(
					new MultipleOfRule<double>(a.Divisor, a.Message),
					BoundMessage(a.Message ?? MultipleOfRule<double>.MessageFormat, a.Divisor)
				),
				MinValueAttribute a => FromRule(
					new MinValueRule<double>(a.MinValue, a.Message),
					BoundMessage(a.Message ?? MinValueRule<double>.MessageFormat, a.MinValue)
				),
				MaxValueAttribute a => FromRule(
					new MaxValueRule<double>(a.MaxValue, a.Message),
					BoundMessage(a.Message ?? MaxValueRule<double>.MessageFormat, a.MaxValue)
				),
				GreaterThanAttribute a => FromRule(
					new GreaterThanRule<double>(a.ExclusiveMinimum, a.Message),
					BoundMessage(a.Message ?? GreaterThanRule<double>.MessageFormat, a.ExclusiveMinimum)
				),
				GreaterThanOrEqualAttribute a => FromRule(
					new GreaterThanOrEqualRule<double>(a.MinValue, a.Message),
					BoundMessage(a.Message ?? GreaterThanOrEqualRule<double>.MessageFormat, a.MinValue)
				),
				LessThanAttribute a => FromRule(
					new LessThanRule<double>(a.ExclusiveMaximum, a.Message),
					BoundMessage(a.Message ?? LessThanRule<double>.MessageFormat, a.ExclusiveMaximum)
				),
				LessThanOrEqualAttribute a => FromRule(
					new LessThanOrEqualRule<double>(a.MaxValue, a.Message),
					BoundMessage(a.Message ?? LessThanOrEqualRule<double>.MessageFormat, a.MaxValue)
				),
				_ => FromCustomAttribute(attribute),
			};
		}
		catch (ArgumentException)
		{
			// An attribute whose values the rule rejects is reported by the server; it has no client rule.
			return [];
		}
	}

	/// <summary>
	/// Gets the client rules a JSON Schema property describes (<c>minLength</c>, <c>maxLength</c>, <c>pattern</c>,
	/// <c>minimum</c>, <c>maximum</c>, exclusive bounds, <c>multipleOf</c>, <c>type: integer</c> and the
	/// <c>uuid</c>/<c>ipv4</c>/<c>ipv6</c> formats) - how a runtime schema, which has no attributes, gets client rules.
	/// </summary>
	/// <param name="property">The property schema.</param>
	/// <param name="required">Whether the property is required.</param>
	/// <returns>The client rules.</returns>
	public static IReadOnlyList<ZodClientRule> FromJsonSchema(JsonSchemaDefinition property, bool required)
	{
		ArgumentNullException.ThrowIfNull(property);

		List<ZodClientRule> rules = [];
		if (required)
			Add(rules, "required", "Required");

		if (property.MinLength is { } minLength && property.MaxLength is { } maxLength)
			Add(
				rules,
				"length",
				Invariant($"Must be between {minLength} and {maxLength} characters"),
				("min", Invariant(minLength)),
				("max", Invariant(maxLength))
			);
		else if (property.MinLength is { } min)
			Add(rules, "minlength", Invariant($"Must be at least {min} characters"), ("min", Invariant(min)));
		else if (property.MaxLength is { } max)
			Add(rules, "maxlength", Invariant($"Must be at most {max} characters"), ("max", Invariant(max)));

		if (property.Pattern is { Length: > 0 } pattern)
			Add(rules, Regex, "Invalid format", ("pattern", pattern));

		if (string.Equals(property.Type, "integer", StringComparison.Ordinal))
			Add(rules, WholeNumber, "Expected integer");

		if (property.Minimum is { } minimum)
			Add(
				rules,
				Min,
				Invariant($"Must be at least {minimum}"),
				("value", Invariant(minimum)),
				("kind", "number")
			);

		if (property.ExclusiveMinimum is { } exclusiveMinimum)
			Add(
				rules,
				GreaterThan,
				Invariant($"Must be greater than {exclusiveMinimum}"),
				("value", Invariant(exclusiveMinimum)),
				("kind", "number")
			);

		if (property.Maximum is { } maximum)
			Add(rules, Max, Invariant($"Must be at most {maximum}"), ("value", Invariant(maximum)), ("kind", "number"));

		if (property.ExclusiveMaximum is { } exclusiveMaximum)
			Add(
				rules,
				LessThan,
				Invariant($"Must be less than {exclusiveMaximum}"),
				("value", Invariant(exclusiveMaximum)),
				("kind", "number")
			);

		if (property.MultipleOf is { } multipleOf)
			Add(
				rules,
				MultipleOf,
				Invariant($"Must be a multiple of {multipleOf}"),
				("divisor", Invariant(multipleOf))
			);

		switch (property.Format)
		{
			case "uuid":
				Add(rules, Uuid, "Invalid UUID format");
				break;
			case "ipv4":
				Add(rules, IPAddress, "Invalid IPv4 address", ("version", "v4"));
				break;
			case "ipv6":
				Add(rules, IPAddress, "Invalid IPv6 address", ("version", "v6"));
				break;
			default:
				break;
		}

		return rules;
	}

	static IReadOnlyList<ZodClientRule> FromStringRule<TRule>(
		TRule rule,
		StringComparison comparison = StringComparison.Ordinal
	)
		where TRule : IZodRule, IStringValidationRule =>
		FromRule(rule, rule.GetErrorMessage(ValuePlaceholder), comparison);

	/// <summary>
	/// A custom attribute mapped with <c>[ZodRule(typeof(TRule))]</c> to a non-generic rule that implements
	/// <see cref="IZodRule"/>: the rule is constructed from the attribute's properties, matched to the constructor's
	/// parameters by name.
	/// </summary>
	[RequiresUnreferencedCode(ZodClientValidationTrimming.Message)]
	static IReadOnlyList<ZodClientRule> FromCustomAttribute(ValidationAttribute attribute)
	{
		var attributeType = attribute.GetType();
		if (
			attributeType.GetCustomAttribute<ZodRuleAttribute>() is not { RuleType: { } ruleType }
			|| ruleType.IsGenericTypeDefinition
			|| !typeof(IZodRule).IsAssignableFrom(ruleType)
		)
			return [];

		var properties = attributeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
		foreach (var constructor in ruleType.GetConstructors().OrderByDescending(static c => c.GetParameters().Length))
		{
			var parameters = constructor.GetParameters();
			var arguments = new object?[parameters.Length];
			var bound = true;
			for (var i = 0; i < parameters.Length && bound; i++)
			{
				var parameter = parameters[i];
				var property = properties.FirstOrDefault(p =>
					string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
					&& parameter.ParameterType.IsAssignableFrom(p.PropertyType)
				);
				if (property is not null)
					arguments[i] = property.GetValue(attribute);
				else if (parameter.HasDefaultValue)
					arguments[i] = parameter.DefaultValue;
				else
					bound = false;
			}

			if (!bound)
				continue;

			object rule;
			try
			{
				rule = constructor.Invoke(arguments);
			}
			catch (TargetInvocationException)
			{
				return [];
			}

			var message = rule switch
			{
				IStringValidationRule text => text.GetErrorMessage(ValuePlaceholder),
				_ => attribute.FormatErrorMessage(string.Empty),
			};
			return FromRule((IZodRule)rule, message);
		}

		return [];
	}

	static string ValueMessage(string format) => SafeFormat(format, ValuePlaceholder, null);

	static string BoundMessage(string format, double bound) =>
		SafeFormat(format, bound.ToString(CultureInfo.InvariantCulture), ValuePlaceholder);

	static string SafeFormat(string format, string arg0, string? arg1)
	{
		try
		{
			return arg1 is null ? RuleMessage.Format(format, arg0) : RuleMessage.Format(format, arg0, arg1);
		}
		catch (FormatException)
		{
			return format;
		}
	}

	static bool TryGetComparison(StringComparison comparison, [NotNullWhen(true)] out string? mode)
	{
		mode = comparison switch
		{
			StringComparison.Ordinal => "ordinal",
			StringComparison.OrdinalIgnoreCase => "ignorecase",
			_ => null,
		};
		return mode is not null;
	}

	static string Version(string format) =>
		format.EndsWith("v4", StringComparison.Ordinal) ? "v4"
		: format.EndsWith("v6", StringComparison.Ordinal) ? "v6"
		: "any";

	static bool TryFormatBound(object value, [NotNullWhen(true)] out string? text, out string kind)
	{
		kind = "number";
		switch (value)
		{
			case DateTime dateTime:
				kind = "date";
				text = dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
				return true;
			case DateTimeOffset offset:
				kind = "date";
				text = offset.DateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
				return true;
			case DateOnly date:
				kind = "date";
				text = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
				return true;
			case double number when double.IsFinite(number):
				text = number.ToString("R", CultureInfo.InvariantCulture);
				return true;
			case float number when float.IsFinite(number):
				text = number.ToString("R", CultureInfo.InvariantCulture);
				return true;
			case IConvertible convertible
			and (int or long or short or byte or sbyte or uint or ulong or ushort or decimal):
				text = convertible.ToString(CultureInfo.InvariantCulture);
				return true;
			default:
				text = null;
				return false;
		}
	}

	static string? Get(IReadOnlyDictionary<string, object?>? parameters, string name) =>
		parameters is not null && parameters.TryGetValue(name, out var value) && value is not null
			? Convert.ToString(value, CultureInfo.InvariantCulture)
			: null;

	static string Invariant(FormattableString value) => FormattableString.Invariant(value);

	static string Invariant(IFormattable value) => value.ToString(null, CultureInfo.InvariantCulture);

	static void Add(
		List<ZodClientRule> rules,
		string name,
		string message,
		params (string Key, string? Value)[] parameters
	)
	{
		Dictionary<string, string> values = [with(StringComparer.Ordinal)];
		foreach (var (key, value) in parameters)
		{
			if (value is not null)
				values[key] = value;
		}

		if (message.Contains(ValuePlaceholder, StringComparison.Ordinal))
			values[MessageTemplateParameter] = message;

		rules.Add(new(name, message, values));
	}
}
#endif
