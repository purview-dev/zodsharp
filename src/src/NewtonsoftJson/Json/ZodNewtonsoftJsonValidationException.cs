using System.Collections.Immutable;
using Newtonsoft.Json;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// The <see cref="JsonSerializationException"/> thrown by a validating converter (see
/// <see cref="NewtonsoftJsonExtensions.CreateValidatingConverter{T}"/>) when the value it read or is writing
/// fails its schema. It carries the full <see cref="ValidationError"/>s rather than a flattened message.
/// </summary>
/// <remarks>
/// The converter records the reader's (or writer's) path when it throws, so <see cref="Errors"/> is located
/// under the converted value's path: a nested <c>Child</c> property whose <c>Name</c> failed reports
/// <c>Child.Name</c>. <c>DeserializeAndValidate</c> unwraps the exception into those errors.
/// </remarks>
public sealed class ZodNewtonsoftJsonValidationException : JsonSerializationException
{
	const string DefaultMessage = "Validation failed.";

	/// <summary>
	/// Initializes a new instance with no errors.
	/// </summary>
	public ZodNewtonsoftJsonValidationException()
		: this(DefaultMessage) { }

	/// <summary>
	/// Initializes a new instance with a message and no errors.
	/// </summary>
	/// <param name="message">The message.</param>
	public ZodNewtonsoftJsonValidationException(string message)
		: base(message)
	{
		Errors = [];
	}

	/// <summary>
	/// Initializes a new instance with a message, an inner exception and no errors.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The inner exception.</param>
	public ZodNewtonsoftJsonValidationException(string message, Exception innerException)
		: base(message, innerException)
	{
		Errors = [];
	}

	/// <summary>
	/// Initializes a new instance carrying validation errors relative to the converted value, which are
	/// located under <paramref name="path"/>.
	/// </summary>
	/// <param name="errors">The validation errors, with paths relative to the converted value.</param>
	/// <param name="path">The JSON path of the converted value (as reported by Newtonsoft.Json).</param>
	/// <param name="lineNumber">The line number of the converted value, or zero when unknown.</param>
	/// <param name="linePosition">The line position of the converted value, or zero when unknown.</param>
	public ZodNewtonsoftJsonValidationException(
		IEnumerable<ValidationError> errors,
		string? path,
		int lineNumber,
		int linePosition
	)
		: this([.. errors ?? throw new ArgumentNullException(nameof(errors))], path, lineNumber, linePosition) { }

	ZodNewtonsoftJsonValidationException(
		ImmutableArray<ValidationError> errors,
		string? path,
		int lineNumber,
		int linePosition
	)
		: base(BuildMessage(errors), path ?? string.Empty, lineNumber, linePosition, null)
	{
		var prefix = ZodPath.ParseJsonPath(path);
		if (prefix.IsRoot)
		{
			Errors = errors;
			return;
		}

		var builder = ImmutableArray.CreateBuilder<ValidationError>(errors.Length);
		foreach (var error in errors)
			builder.Add(error.WithPathPrefix(prefix));

		Errors = builder.MoveToImmutable();
	}

	/// <summary>
	/// Gets the validation errors, located under <see cref="JsonSerializationException.Path"/>.
	/// </summary>
	public ImmutableArray<ValidationError> Errors { get; }

	static string BuildMessage(ImmutableArray<ValidationError> errors) =>
		errors.IsDefaultOrEmpty
			? DefaultMessage
			: "Validation failed: " + string.Join(", ", errors.Select(static e => e.Message));
}
