using System.Net;
using System.Net.Sockets;

namespace ZodSharp.Rules;

/// <summary>
/// Specifies the type of IP address to validate against.
/// </summary>
public enum IPAddressRuleType
{
	/// <summary>
	/// Validates that the value is an IPv4 address.
	/// </summary>
	IPv4,

	/// <summary>
	/// Validates that the value is an IPv6 address.
	/// </summary>
	IPv6,

	/// <summary>
	/// Validates that the value is either an IPv4 or IPv6 address.
	/// </summary>
	Any,
}

/// <summary>
/// Validation rule for IPv4 and IPv6 address format.
/// Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct IPAddressRule : Core.IValidationRule<string>, Core.IStringValidationRule, Core.IZodRule, JsonSchema.IJsonSchemaRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the default message format; <c>{1}</c> is the required address type. It never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Invalid {1} address";

	readonly IPAddressRuleType _ruleType;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="IPAddressRule"/> struct with the default rule type (Any).
	/// </summary>
	/// <remarks>
	/// A struct always has a parameterless constructor; declaring it explicitly stops <c>new IPAddressRule()</c>
	/// from defaulting to the first enum member (<see cref="IPAddressRuleType.IPv4"/>).
	/// </remarks>
	public IPAddressRule()
	{
		_ruleType = IPAddressRuleType.Any;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="IPAddressRule"/> struct with default rule type (Any).
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public IPAddressRule(string? message = null, string? code = null)
		: this(IPAddressRuleType.Any, message, code) { }

	/// <summary>
	/// Initializes a new instance of the IPAddressRule struct.
	/// </summary>
	/// <param name="ruleType">The type of IP address to validate against (defaults to <see cref="IPAddressRuleType.Any"/>)</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public IPAddressRule(
		IPAddressRuleType ruleType = IPAddressRuleType.Any,
		string? message = null,
		string? code = null
	)
	{
		_ruleType = ruleType;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is an IPv4 or IPv6 address.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && IsValid(value.AsSpan());

	/// <summary>
	/// Validates that the span is an IPv4 or IPv6 address without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		if (value.IsEmpty || value.IsWhiteSpace())
			return false;

		// Use IPAddress.TryParse to validate the IP address format without throwing exceptions
		if (IPAddress.TryParse(value, out var address))
		{
			if (_ruleType == IPAddressRuleType.Any)
				return address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;
			else if (_ruleType == IPAddressRuleType.IPv4)
				return address.AddressFamily == AddressFamily.InterNetwork;
			else if (_ruleType == IPAddressRuleType.IPv6)
				return address.AddressFamily == AddressFamily.InterNetworkV6;
		}

		return false;
	}

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(in string value) => RuleMessage.Format(_message ?? MessageFormat, value, TypeName);

	/// <summary>
	/// Gets the error message for a failed span validation.
	/// </summary>
	/// <param name="value">The value that failed validation</param>
	/// <returns>The error message</returns>
	public string GetErrorMessage(ReadOnlySpan<char> value) =>
		RuleMessage.Format(_message ?? MessageFormat, value.ToString(), TypeName);

	string TypeName =>
		_ruleType switch
		{
			IPAddressRuleType.IPv4 => "IPv4",
			IPAddressRuleType.IPv6 => "IPv6",
			IPAddressRuleType.Any => "IPv4/IPv6",
			_ => "IPv4/IPv6",
		};

	string FormatName =>
		_ruleType switch
		{
			IPAddressRuleType.IPv4 => "ipv4",
			IPAddressRuleType.IPv6 => "ipv6",
			IPAddressRuleType.Any => "ip",
			_ => "ip",
		};

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => RuleParameters.Format(FormatName);

	/// <summary>
	/// Applies <c>format: ipv4</c>/<c>ipv6</c>. An address of either family has no single JSON Schema format, so it
	/// is recorded as unsupported and described by the <c>x-zod-ipVersion</c> vendor extension instead.
	/// </summary>
	/// <param name="schema">The schema being built for the validated string.</param>
	public void ApplyTo(JsonSchema.JsonSchemaDefinition schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		switch (_ruleType)
		{
			case IPAddressRuleType.IPv4:
				schema.Format = "ipv4";
				break;
			case IPAddressRuleType.IPv6:
				schema.Format = "ipv6";
				break;
			case IPAddressRuleType.Any:
			default:
				schema.SetVendorExtension("x-zod-ipVersion", "any");
				JsonSchema.JsonSchemaRules.MarkUnsupported(schema, nameof(IPAddressRule));
				break;
		}
	}
}
