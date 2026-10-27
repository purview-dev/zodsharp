using System.Collections.Immutable;
using System.Text;

namespace ZodSharp.Core;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ZodException : Exception
{
	/// <summary>
	/// The validation errors that caused this exception.
	/// </summary>
	public ImmutableArray<ValidationError> Errors { get; }

	/// <summary>
	/// Initializes a new instance of the ZodException class.
	/// </summary>
	/// <param name="errors">The validation errors</param>
	public ZodException(ImmutableArray<ValidationError> errors)
		: base(BuildMessage(errors))
	{
		Errors = errors.IsDefault ? [] : errors;
	}

	const int MaxIssuesInMessage = 5;

	static string BuildMessage(ImmutableArray<ValidationError> errors)
	{
		var count = errors.IsDefault ? 0 : errors.Length;
		if (count == 0)
			return "Validation failed with 0 error(s)";

		StringBuilder builder = new();
		builder.Append("Validation failed with ").Append(count).Append(" error(s): ");
		for (var i = 0; i < Math.Min(count, MaxIssuesInMessage); i++)
		{
			if (i > 0)
				builder.Append("; ");
			AppendIssue(builder, errors[i]);
		}

		if (count > MaxIssuesInMessage)
			builder.Append("; and ").Append(count - MaxIssuesInMessage).Append(" more");

		return builder.ToString();
	}

	static void AppendIssue(StringBuilder builder, in ValidationError error)
	{
		var path = error.FullPath.ToDottedString();
		builder
			.Append(path.Length == 0 ? "(root)" : path)
			.Append(": ")
			.Append(error.Message)
			.Append(" (")
			.Append(error.Code)
			.Append(')');
	}

	/// <summary>
	/// Initializes a new instance of the ZodException class.
	/// </summary>
	/// <param name="errors">The validation errors</param>
	public ZodException(IEnumerable<ValidationError> errors)
		: this([.. errors]) { }

	/// <summary>
	/// Returns a string representation of the exception including all validation errors.
	/// </summary>
	/// <returns>A string representation of the exception</returns>
	public override string ToString()
	{
		if (Errors.IsDefaultOrEmpty)
			return base.ToString();

		StringBuilder builder = new(Message);
		foreach (var error in Errors)
		{
			builder.Append('\n');
			AppendIssue(builder, error);
		}

		return builder.ToString();
	}

	/// <inheritdoc/>
	public ZodException() { }

	/// <inheritdoc/>
	public ZodException(string message)
		: base(message) { }

	/// <inheritdoc/>
	public ZodException(string message, Exception innerException)
		: base(message, innerException) { }
}
