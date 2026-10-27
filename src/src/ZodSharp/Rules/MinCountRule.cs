using System.Collections;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that requires a collection to contain at least a minimum number of elements (or entries, for a
/// dictionary). The trim- and Native AOT-safe counterpart of <c>[MinLength]</c> on a collection member.
/// </summary>
/// <remarks>
/// <para>
/// The generated <c>[MinCount]</c> attribute is what a <c>[ZodSchema]</c> model uses: the generator emits the count
/// check itself, with the same error identity (<c>too_small</c>, the <c>array</c>/<c>record</c> origin and the
/// <c>count</c> parameter) as <c>[MinLength]</c> on the same member. Unlike
/// <c>System.ComponentModel.DataAnnotations.MinLengthAttribute</c>, whose constructor is annotated
/// <c>[RequiresUnreferencedCode]</c> because it may reflect over a <c>Count</c> property, nothing here reflects.
/// </para>
/// <para>
/// The rule counts through <see cref="ICollection"/> when the value implements it and by enumerating otherwise.
/// </para>
/// </remarks>
[Core.ZodRule]
public readonly record struct MinCountRule : Core.IValidationRule<IEnumerable>, Core.IZodRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "too_small";

	/// <summary>Gets the message format; <c>{0}</c> is the minimum count and <c>{1}</c> the actual count.</summary>
	public const string MessageFormat = "Collection must contain at least {0} element(s), but got {1}";

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="MinCountRule"/> struct.
	/// </summary>
	/// <param name="minCount">The inclusive minimum number of elements.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public MinCountRule(int minCount, string? message = null, string? code = null)
	{
		MinCount = minCount;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>Gets the inclusive minimum number of elements.</summary>
	public int MinCount { get; }

	/// <summary>
	/// Validates that the collection contains at least <see cref="MinCount"/> elements. A <see langword="null"/>
	/// collection is not counted; use <c>[Required]</c> to reject it.
	/// </summary>
	/// <param name="value">The collection to validate.</param>
	/// <returns>True if valid, false otherwise.</returns>
	public bool IsValid(in IEnumerable value) => value is null || CollectionCount.Of(value) >= MinCount;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The collection that failed validation.</param>
	/// <returns>The error message.</returns>
	public string GetErrorMessage(in IEnumerable value) =>
		RuleMessage.Format(_message ?? MessageFormat, MinCount, CollectionCount.Of(value));

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Array;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.TooSmall;

	Core.ErrorBound? Core.IZodRule.Lower => Core.ErrorBound.Including(MinCount);
}
