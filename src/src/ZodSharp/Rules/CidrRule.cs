using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace ZodSharp.Rules;

/// <summary>
/// Specifies the type of CIDR block to validate against.
/// </summary>
public enum CidrRuleType
{
	/// <summary>
	/// Validates that the value is an IPv4 CIDR block.
	/// </summary>
	IPv4,

	/// <summary>
	/// Validates that the value is an IPv6 CIDR block.
	/// </summary>
	IPv6,

	/// <summary>
	/// Validates that the value is either an IPv4 or IPv6 CIDR block.
	/// </summary>
	Any,
}

/// <summary>
/// Validation rule for CIDR notation (an IP address with a prefix length), equivalent to Zod's
/// <c>z.string().cidr()</c>. Uses struct to avoid allocations.
/// </summary>
[Core.ZodRule]
public readonly record struct CidrRule : Core.IValidationRule<string>, Core.IStringValidationRule, Core.IZodRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "invalid_string";

	/// <summary>Gets the default message format; <c>{1}</c> is the required type. It never contains the input; a custom message may use <c>{0}</c> for the offending value.</summary>
	public const string MessageFormat = "Invalid {1} CIDR";

	readonly CidrRuleType _ruleType;
	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="CidrRule"/> struct with the default rule type (Any).
	/// </summary>
	/// <remarks>
	/// A struct always has a parameterless constructor; declaring it explicitly stops <c>new CidrRule()</c>
	/// from defaulting to the first enum member (<see cref="CidrRuleType.IPv4"/>).
	/// </remarks>
	public CidrRule()
	{
		_ruleType = CidrRuleType.Any;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CidrRule"/> struct with the default rule type (Any).
	/// </summary>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public CidrRule(string? message = null, string? code = null)
		: this(CidrRuleType.Any, message, code) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="CidrRule"/> struct.
	/// </summary>
	/// <param name="ruleType">The type of CIDR block to validate against (defaults to <see cref="CidrRuleType.Any"/>).</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public CidrRule(CidrRuleType ruleType = CidrRuleType.Any, string? message = null, string? code = null)
	{
		_ruleType = ruleType;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>
	/// Validates that the value is an IPv4 or IPv6 CIDR block.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(in string value) => value is not null && IsValid(value.AsSpan());

	/// <summary>
	/// Validates that the span is an IPv4 or IPv6 CIDR block without materialising a string.
	/// </summary>
	/// <param name="value">The value to validate</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool IsValid(ReadOnlySpan<char> value)
	{
		var separator = value.LastIndexOf('/');
		if (separator <= 0 || separator == value.Length - 1)
			return false;

		if (!int.TryParse(value[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix))
			return false;

		if (!IPAddress.TryParse(value[..separator], out var address))
			return false;

#pragma warning disable IDE0072 // Add missing cases
		return address.AddressFamily switch
		{
			AddressFamily.InterNetwork => (_ruleType is CidrRuleType.Any or CidrRuleType.IPv4)
				&& prefix is >= 0 and <= 32,
			AddressFamily.InterNetworkV6 => (_ruleType is CidrRuleType.Any or CidrRuleType.IPv6)
				&& prefix is >= 0 and <= 128,
			_ => false,
		};
#pragma warning restore IDE0072 // Add missing cases
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
			CidrRuleType.IPv4 => "IPv4",
			CidrRuleType.IPv6 => "IPv6",
			CidrRuleType.Any => "IPv4/IPv6",
			_ => "IPv4/IPv6",
		};

	string FormatName =>
		_ruleType switch
		{
			CidrRuleType.IPv4 => "cidrv4",
			CidrRuleType.IPv6 => "cidrv6",
			CidrRuleType.Any => "cidr",
			_ => "cidr",
		};

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.String;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.InvalidFormat;

	IReadOnlyDictionary<string, object?>? Core.IZodRule.Parameters => RuleParameters.Format(FormatName);
}
