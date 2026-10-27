using System.Collections;

namespace ZodSharp.Rules;

/// <summary>
/// Validation rule that requires a collection to contain at most a maximum number of elements (or entries, for a
/// dictionary). The trim- and Native AOT-safe counterpart of <c>[MaxLength]</c> on a collection member.
/// </summary>
/// <remarks>
/// The generated <c>[MaxCount]</c> attribute is what a <c>[ZodSchema]</c> model uses: the generator emits the count
/// check itself, with the same error identity (<c>too_big</c>, the <c>array</c>/<c>record</c> origin and the
/// <c>count</c> parameter) as <c>[MaxLength]</c> on the same member, without the BCL attribute's
/// <c>[RequiresUnreferencedCode]</c> constructor.
/// </remarks>
[Core.ZodRule]
public readonly record struct MaxCountRule : Core.IValidationRule<IEnumerable>, Core.IZodRule
{
	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public const string ErrorCode = "too_big";

	/// <summary>Gets the message format; <c>{0}</c> is the maximum count and <c>{1}</c> the actual count.</summary>
	public const string MessageFormat = "Collection must contain at most {0} element(s), but got {1}";

	readonly string? _message;

	/// <summary>
	/// Initializes a new instance of the <see cref="MaxCountRule"/> struct.
	/// </summary>
	/// <param name="maxCount">The inclusive maximum number of elements.</param>
	/// <param name="message">Optional error message/ message format.</param>
	/// <param name="code">Optional error code override. If one is not specified then the <see cref="ErrorCode"/> is used.</param>
	public MaxCountRule(int maxCount, string? message = null, string? code = null)
	{
		MaxCount = maxCount;
		_message = message.OrNull();
		Code = code.Or(ErrorCode);
	}

	/// <summary>Gets the inclusive maximum number of elements.</summary>
	public int MaxCount { get; }

	/// <summary>
	/// Validates that the collection contains at most <see cref="MaxCount"/> elements. A <see langword="null"/>
	/// collection is not counted.
	/// </summary>
	/// <param name="value">The collection to validate.</param>
	/// <returns>True if valid, false otherwise.</returns>
	public bool IsValid(in IEnumerable value) => value is null || CollectionCount.Of(value) <= MaxCount;

	/// <summary>
	/// Gets the error message for a failed validation.
	/// </summary>
	/// <param name="value">The collection that failed validation.</param>
	/// <returns>The error message.</returns>
	public string GetErrorMessage(in IEnumerable value) =>
		RuleMessage.Format(_message ?? MessageFormat, MaxCount, CollectionCount.Of(value));

	/// <summary>Gets the Zod-compatible error code reported when the rule fails.</summary>
	public string Code => field.Or(ErrorCode);

	string? Core.IZodRule.Code => Code;

	string? Core.IZodRule.Origin => Core.ZodTypeNames.Array;

	string? Core.IZodRule.Category => Core.ZodIssueCategories.TooBig;

	Core.ErrorBound? Core.IZodRule.Upper => Core.ErrorBound.Including(MaxCount);
}
