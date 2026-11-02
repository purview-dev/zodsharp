using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ZodSharp.Core;

namespace ZodSharp.Http;

/// <summary>
/// Creates <see cref="JsonContent"/> request bodies from validated values, so an invalid value is never sent.
/// </summary>
/// <remarks>
/// Safe under trimming, Native AOT and Blazor WebAssembly: the contract always comes from a supplied
/// <see cref="JsonTypeInfo{T}"/>, typically from a source-generated <see cref="JsonSerializerContext"/>.
/// </remarks>
public static class ZodJsonContent
{
	/// <summary>
	/// Validates <paramref name="value"/> and, when it is valid, creates a JSON request body for the validated value.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="schema">The schema to validate with.</param>
	/// <param name="value">The value to validate and send.</param>
	/// <param name="jsonTypeInfo">The contract for <typeparamref name="T"/>.</param>
	/// <param name="mediaType">The media type of the body; <c>application/json; charset=utf-8</c> when omitted.</param>
	/// <returns>The request body, or the validation errors.</returns>
	public static ValidationResult<JsonContent> Create<T>(
		IZodSchema<T, T> schema,
		T value,
		JsonTypeInfo<T> jsonTypeInfo,
		MediaTypeHeaderValue? mediaType = null
	)
	{
		ArgumentNullException.ThrowIfNull(schema);
		ArgumentNullException.ThrowIfNull(jsonTypeInfo);

		return ToContent(schema.Validate(value), jsonTypeInfo, mediaType);
	}

	/// <summary>
	/// Validates <paramref name="value"/> (running async refinements) and, when it is valid, creates a JSON request
	/// body for the validated value.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="schema">The schema to validate with.</param>
	/// <param name="value">The value to validate and send.</param>
	/// <param name="jsonTypeInfo">The contract for <typeparamref name="T"/>.</param>
	/// <param name="mediaType">The media type of the body; <c>application/json; charset=utf-8</c> when omitted.</param>
	/// <param name="cancellationToken">Cancels the validation.</param>
	/// <returns>The request body, or the validation errors.</returns>
	public static ValueTask<ValidationResult<JsonContent>> CreateAsync<T>(
		IZodSchema<T, T> schema,
		T value,
		JsonTypeInfo<T> jsonTypeInfo,
		MediaTypeHeaderValue? mediaType = null,
		CancellationToken cancellationToken = default
	) => CreateCoreAsync(schema, null, null, value, jsonTypeInfo, mediaType, cancellationToken);

	/// <summary>
	/// Validates <paramref name="value"/> with a validator that has access to <paramref name="services"/>, so
	/// service-aware rules run (for example an async uniqueness check that resolves a repository), and, when it is
	/// valid, creates a JSON request body for the validated value.
	/// </summary>
	/// <typeparam name="T">The validated type.</typeparam>
	/// <param name="validator">The validator, typically a generated <c>{TypeName}SchemaValidator</c>.</param>
	/// <param name="value">The value to validate and send.</param>
	/// <param name="jsonTypeInfo">The contract for <typeparamref name="T"/>.</param>
	/// <param name="services">
	/// The services service-aware rules resolve from: a component's injected <see cref="IServiceProvider"/> in Blazor,
	/// the request services on a server.
	/// </param>
	/// <param name="mediaType">The media type of the body; <c>application/json; charset=utf-8</c> when omitted.</param>
	/// <param name="cancellationToken">Cancels the validation.</param>
	/// <returns>The request body, or the validation errors.</returns>
	public static ValueTask<ValidationResult<JsonContent>> CreateAsync<T>(
		IZodSchemaValidator<T> validator,
		T value,
		JsonTypeInfo<T> jsonTypeInfo,
		IServiceProvider? services,
		MediaTypeHeaderValue? mediaType = null,
		CancellationToken cancellationToken = default
	) => CreateCoreAsync(validator, validator, services, value, jsonTypeInfo, mediaType, cancellationToken);

	/// <summary>
	/// The one validation path: <paramref name="servicesValidator"/>, when supplied, validates with
	/// <paramref name="services"/>; otherwise <paramref name="schema"/> validates without them.
	/// </summary>
	internal static async ValueTask<ValidationResult<JsonContent>> CreateCoreAsync<T>(
		IZodSchema<T, T> schema,
		IZodSchemaValidator<T>? servicesValidator,
		IServiceProvider? services,
		T value,
		JsonTypeInfo<T> jsonTypeInfo,
		MediaTypeHeaderValue? mediaType,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(schema);
		ArgumentNullException.ThrowIfNull(jsonTypeInfo);

		var result = servicesValidator is null
			? await schema.ValidateAsync(value, cancellationToken).ConfigureAwait(false)
			: await servicesValidator.ValidateAsync(value, services, cancellationToken).ConfigureAwait(false);

		return ToContent(result, jsonTypeInfo, mediaType);
	}

	static ValidationResult<JsonContent> ToContent<T>(
		ValidationResult<T> result,
		JsonTypeInfo<T> jsonTypeInfo,
		MediaTypeHeaderValue? mediaType
	)
	{
		if (!result.IsSuccess)
			return ValidationResult<JsonContent>.Failure(result.Errors);

		// Ownership passes to the caller, through the result.
#pragma warning disable CA2000 // Dispose objects before losing scope
		return ValidationResult<JsonContent>.Success(JsonContent.Create(result.Value, jsonTypeInfo, mediaType));
#pragma warning restore CA2000 // Dispose objects before losing scope
	}
}
