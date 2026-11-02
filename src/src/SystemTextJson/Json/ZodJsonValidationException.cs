using System.Collections.Immutable;
using System.Text.Json;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// The <see cref="JsonException"/> thrown by a validating converter (see
/// <see cref="SystemTextJsonExtensions.CreateValidatingConverter{T}"/>) when the value it read or is writing
/// fails its schema. It carries the full <see cref="ValidationError"/>s rather than a flattened message.
/// </summary>
/// <remarks>
/// System.Text.Json fills in <see cref="JsonException.Path"/> as the exception leaves the converter, so
/// <see cref="Errors"/> is located under the converted value's path: a nested <c>Child</c> property whose
/// <c>Name</c> failed reports <c>Child.Name</c>. <c>DeserializeAndValidate</c> unwraps the exception into
/// those errors.
/// </remarks>
public sealed class ZodJsonValidationException : JsonException
{
	const string DefaultMessage = "Validation failed.";

	/// <summary>
	/// Initializes a new instance with no errors.
	/// </summary>
	public ZodJsonValidationException()
		: this(DefaultMessage) { }

	/// <summary>
	/// Initializes a new instance with a message and no errors.
	/// </summary>
	/// <param name="message">The message.</param>
	public ZodJsonValidationException(string? message)
		: base(message)
	{
		Errors = [];
	}

	/// <summary>
	/// Initializes a new instance with a message, an inner exception and no errors.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The inner exception.</param>
	public ZodJsonValidationException(string? message, Exception? innerException)
		: base(message, innerException)
	{
		Errors = [];
	}

	/// <summary>
	/// Initializes a new instance carrying validation errors, with paths relative to the converted value.
	/// </summary>
	/// <param name="errors">The validation errors.</param>
	public ZodJsonValidationException(IEnumerable<ValidationError> errors)
		: this([.. errors ?? throw new ArgumentNullException(nameof(errors))]) { }

	ZodJsonValidationException(ImmutableArray<ValidationError> errors)
		: base(BuildMessage(errors))
	{
		Errors = errors;
	}

	/// <summary>
	/// Gets the validation errors, located under <see cref="JsonException.Path"/> when the serializer has
	/// supplied it.
	/// </summary>
	public ImmutableArray<ValidationError> Errors
	{
		get
		{
			var prefix = ZodPath.ParseJsonPath(Path);
			if (prefix.IsRoot)
				return field;

			var builder = ImmutableArray.CreateBuilder<ValidationError>(field.Length);
			foreach (var error in field)
				builder.Add(error.WithPathPrefix(prefix));

			return builder.MoveToImmutable();
		}
	}

	static string BuildMessage(ImmutableArray<ValidationError> errors) =>
		errors.IsDefaultOrEmpty
			? DefaultMessage
			: "Validation failed: " + string.Join(", ", errors.Select(static e => e.Message));
}
