using System.Text.Json;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// Thrown by a validating converter when the value it buffered cannot be read into the target type. It records
/// where inside that value reading failed, because the serializer only knows the converted value's own path.
/// </summary>
/// <remarks>
/// The converter reads its value through a second serializer call over a buffered element, whose
/// <see cref="JsonException.Path"/> is relative to that element (<c>$.Age</c>). Left as it is, the serializer keeps
/// that relative path and the error is reported at <c>Age</c> instead of <c>Members[1].Age</c>. This exception
/// leaves <see cref="JsonException.Path"/> for the serializer to fill in with the converted value's path, and
/// <see cref="JsonDeserializationErrors"/> joins the two.
/// </remarks>
sealed class ZodJsonReadException : JsonException
{
	const string DefaultMessage = "The JSON value could not be converted.";

	public ZodJsonReadException()
		: base(DefaultMessage) { }

	public ZodJsonReadException(string? message)
		: base(message) { }

	public ZodJsonReadException(string? message, Exception? innerException)
		: base(message, innerException) { }

	public ZodJsonReadException(ZodPath relativePath, JsonException innerException)
		: base(DefaultMessage, innerException)
	{
		RelativePath = relativePath;
	}

	/// <summary>Gets where reading failed, relative to the converted value.</summary>
	public ZodPath RelativePath { get; }

	/// <summary>Gets the full location: the serializer's path for the converted value, then <see cref="RelativePath"/>.</summary>
	public ZodPath FullPath => RelativePath.Prepend(ZodPath.ParseJsonPath(Path));

	/// <summary>
	/// The location of <paramref name="exception"/> relative to the element it was read from, including any
	/// location a nested validating converter recorded.
	/// </summary>
	public static ZodPath RelativePathOf(JsonException exception) =>
		exception is ZodJsonReadException nested ? nested.FullPath : ZodPath.ParseJsonPath(exception.Path);
}
