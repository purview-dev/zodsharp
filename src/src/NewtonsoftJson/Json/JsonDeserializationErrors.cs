using System.Collections.Immutable;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ZodSharp.Core;

namespace ZodSharp.Json;

/// <summary>
/// Turns Newtonsoft.Json deserialization failures into <see cref="ValidationError"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Every failure uses the one code <see cref="NewtonsoftJsonExtensions.InvalidJsonCode"/> and the category
/// <see cref="ZodIssueCategories.InvalidType"/>. The location is kept: the serializer's path becomes the
/// error's <see cref="ValidationError.FullPath"/> and the line number and line position become the
/// <c>line</c> and <c>position</c> parameters.
/// </para>
/// <para>
/// The message is fixed and generic. The serializer's own message names CLR types and quotes the input, so it
/// is never passed to a client; the expected type is described with Zod's type names instead.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(NewtonsoftJsonTrimming.Message)]
[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(NewtonsoftJsonTrimming.Message)]
static class JsonDeserializationErrors
{
	/// <summary>The message format of every deserialization failure.</summary>
	public const string MessageFormat = "The JSON value could not be converted to {expected}.";

	/// <summary>The parameter holding the (one-based) line number reported by the serializer.</summary>
	public const string LineParameter = "line";

	/// <summary>The parameter holding the (one-based) position in the line reported by the serializer.</summary>
	public const string PositionParameter = "position";

	/// <summary>
	/// Maps an exception thrown while deserializing to validation errors.
	/// </summary>
	/// <param name="exception">The exception.</param>
	/// <param name="resolver">The contract resolver, used to describe the expected type at the failing path.</param>
	/// <param name="rootType">The deserialized type.</param>
	/// <param name="errors">The errors, when the exception is a deserialization failure.</param>
	/// <returns><see langword="true"/> when the exception is a deserialization failure.</returns>
	public static bool TryMap(
		Exception exception,
		IContractResolver? resolver,
		Type rootType,
		out ImmutableArray<ValidationError> errors
	)
	{
		switch (exception)
		{
			// A validating converter failed: its errors are the result, already located under its path.
			case ZodNewtonsoftJsonValidationException validation:
				errors = validation.Errors;
				return true;

			case JsonReaderException reader:
				errors = [Create(reader.Path, resolver, rootType, reader.LineNumber, reader.LinePosition)];
				return true;

			case JsonSerializationException serialization:
				errors =
				[
					Create(
						serialization.Path,
						resolver,
						rootType,
						serialization.LineNumber,
						serialization.LinePosition
					),
				];
				return true;

			case JsonException:
				errors = [Create(null, resolver, rootType, 0, 0)];
				return true;

			default:
				errors = default;
				return false;
		}
	}

	/// <summary>
	/// The error reported when the JSON deserialized to <see langword="null"/>.
	/// </summary>
	/// <param name="resolver">The contract resolver.</param>
	/// <param name="rootType">The deserialized type.</param>
	/// <returns>The error.</returns>
	public static ValidationError NullValue(IContractResolver? resolver, Type rootType) =>
		Create(null, resolver, rootType, 0, 0, received: "null");

	static ValidationError Create(
		string? jsonPath,
		IContractResolver? resolver,
		Type rootType,
		int line,
		int position,
		string? received = null
	)
	{
		var path = ZodPath.ParseJsonPath(jsonPath);
		var expected = DescribeExpected(resolver ?? JsonSerializer.CreateDefault().ContractResolver, rootType, path);

		Dictionary<string, object?> parameters = new(StringComparer.Ordinal)
		{
			[ZodIssueParameters.Expected] = expected,
		};
		if (received is not null)
			parameters[ZodIssueParameters.Received] = received;

		// Newtonsoft reports zero when it has no line information.
		if (line > 0)
		{
			parameters[LineParameter] = line;
			parameters[PositionParameter] = position;
		}

		return ValidationError.Create(
			NewtonsoftJsonExtensions.InvalidJsonCode,
			ZodMessageFormat.Format(MessageFormat, parameters, CultureInfo.InvariantCulture),
			path,
			parameters,
			category: ZodIssueCategories.InvalidType
		) with
		{
			MessageFormat = MessageFormat,
		};
	}

	/// <summary>
	/// Walks <paramref name="path"/> through the contracts to name the JSON expected where reading failed.
	/// </summary>
	static string DescribeExpected(IContractResolver resolver, Type rootType, ZodPath path)
	{
		try
		{
			var type = rootType;
			foreach (var segment in path.Segments)
			{
				var next = Step(resolver.ResolveContract(type), segment);
				if (next is null)
					return JsonKinds.Unknown;

				type = next;
			}

			return JsonKinds.OfContract(resolver.ResolveContract(type)) ?? JsonKinds.Unknown;
		}
		catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
		{
			// Describing the expected type is best effort; the error itself is still reported.
			return JsonKinds.Unknown;
		}
	}

	static Type? Step(JsonContract contract, ZodPathSegment segment) =>
		contract switch
		{
			JsonObjectContract obj when segment.Kind == ZodPathSegmentKind.Member => obj
				.Properties.GetClosestMatchProperty(segment.Name!)
				?.PropertyType,
			JsonArrayContract array when segment.Kind == ZodPathSegmentKind.Index => array.CollectionItemType,
			JsonDictionaryContract dictionary when segment.Kind != ZodPathSegmentKind.Index =>
				dictionary.DictionaryValueType,
			_ => null,
		};
}
