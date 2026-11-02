using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using ZodSharp.Core;
using ZodSharp.Http;

namespace System.Net.Http;

/// <summary>
/// Sends validated JSON request bodies with ZodSharp: the value is validated first and the request is only sent
/// when it is valid.
/// </summary>
/// <remarks>
/// <para>
/// Each method returns a failed <see cref="ValidationResult{T}"/>, and sends nothing, when the value is invalid; otherwise
/// it returns the <see cref="HttpResponseMessage"/>, whatever its status. A rejected request's validation problem is
/// read with <c>TryReadZodValidationProblemAsync</c>. Safe under trimming, Native AOT and Blazor WebAssembly.
/// </para>
/// <para>
/// The overloads that take an <see cref="IZodSchemaValidator{T}"/> and an <see cref="IServiceProvider"/> run
/// service-aware rules too (an async uniqueness check that resolves a repository, for example); pass a component's
/// injected <see cref="IServiceProvider"/> in Blazor, or the request services on a server.
/// </para>
/// </remarks>
public static class ZodHttpClientExtensions
{
	extension(HttpClient client)
	{
		/// <summary>Validates <paramref name="value"/> and, when it is valid, POSTs it as JSON.</summary>
		/// <typeparam name="T">The validated type.</typeparam>
		/// <param name="requestUri">The URI the request is sent to.</param>
		/// <param name="value">The value to validate and send.</param>
		/// <param name="schema">The schema to validate with.</param>
		/// <param name="jsonTypeInfo">The contract for <typeparamref name="T"/>.</param>
		/// <param name="cancellationToken">Cancels the validation and the request.</param>
		/// <returns>The response, or the validation errors when nothing was sent.</returns>
		public Task<ValidationResult<HttpResponseMessage>> PostAsJsonValidatedAsync<T>(
			[StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri,
			T value,
			IZodSchema<T, T> schema,
			JsonTypeInfo<T> jsonTypeInfo,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Post,
				CreateUri(requestUri),
				value,
				schema,
				null,
				null,
				jsonTypeInfo,
				cancellationToken
			);

		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchema{T, T}, JsonTypeInfo{T}, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PostAsJsonValidatedAsync<T>(
			Uri? requestUri,
			T value,
			IZodSchema<T, T> schema,
			JsonTypeInfo<T> jsonTypeInfo,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Post,
				requestUri,
				value,
				schema,
				null,
				null,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>
		/// Validates <paramref name="value"/>, running service-aware rules with <paramref name="services"/>, and, when it
		/// is valid, POSTs it as JSON.
		/// </summary>
		/// <typeparam name="T">The validated type.</typeparam>
		/// <param name="requestUri">The URI the request is sent to.</param>
		/// <param name="value">The value to validate and send.</param>
		/// <param name="validator">The validator, typically a generated <c>{TypeName}SchemaValidator</c>.</param>
		/// <param name="jsonTypeInfo">The contract for <typeparamref name="T"/>.</param>
		/// <param name="services">The services service-aware rules resolve from.</param>
		/// <param name="cancellationToken">Cancels the validation and the request.</param>
		/// <returns>The response, or the validation errors when nothing was sent.</returns>
		public Task<ValidationResult<HttpResponseMessage>> PostAsJsonValidatedAsync<T>(
			[StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri,
			T value,
			IZodSchemaValidator<T> validator,
			JsonTypeInfo<T> jsonTypeInfo,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Post,
				CreateUri(requestUri),
				value,
				validator,
				validator,
				services,
				jsonTypeInfo,
				cancellationToken
			);

		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchemaValidator{T}, JsonTypeInfo{T}, IServiceProvider?, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PostAsJsonValidatedAsync<T>(
			Uri? requestUri,
			T value,
			IZodSchemaValidator<T> validator,
			JsonTypeInfo<T> jsonTypeInfo,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Post,
				requestUri,
				value,
				validator,
				validator,
				services,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>Validates <paramref name="value"/> and, when it is valid, PUTs it as JSON.</summary>
		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchema{T, T}, JsonTypeInfo{T}, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PutAsJsonValidatedAsync<T>(
			[StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri,
			T value,
			IZodSchema<T, T> schema,
			JsonTypeInfo<T> jsonTypeInfo,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Put,
				CreateUri(requestUri),
				value,
				schema,
				null,
				null,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>Validates <paramref name="value"/> and, when it is valid, PUTs it as JSON.</summary>
		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchema{T, T}, JsonTypeInfo{T}, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PutAsJsonValidatedAsync<T>(
			Uri? requestUri,
			T value,
			IZodSchema<T, T> schema,
			JsonTypeInfo<T> jsonTypeInfo,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Put,
				requestUri,
				value,
				schema,
				null,
				null,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>
		/// Validates <paramref name="value"/>, running service-aware rules with <paramref name="services"/>, and, when it
		/// is valid, PUTs it as JSON.
		/// </summary>
		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchemaValidator{T}, JsonTypeInfo{T}, IServiceProvider?, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PutAsJsonValidatedAsync<T>(
			[StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri,
			T value,
			IZodSchemaValidator<T> validator,
			JsonTypeInfo<T> jsonTypeInfo,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Put,
				CreateUri(requestUri),
				value,
				validator,
				validator,
				services,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>
		/// Validates <paramref name="value"/>, running service-aware rules with <paramref name="services"/>, and, when it
		/// is valid, PUTs it as JSON.
		/// </summary>
		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchemaValidator{T}, JsonTypeInfo{T}, IServiceProvider?, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PutAsJsonValidatedAsync<T>(
			Uri? requestUri,
			T value,
			IZodSchemaValidator<T> validator,
			JsonTypeInfo<T> jsonTypeInfo,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Put,
				requestUri,
				value,
				validator,
				validator,
				services,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>Validates <paramref name="value"/> and, when it is valid, PATCHes it as JSON.</summary>
		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchema{T, T}, JsonTypeInfo{T}, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PatchAsJsonValidatedAsync<T>(
			[StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri,
			T value,
			IZodSchema<T, T> schema,
			JsonTypeInfo<T> jsonTypeInfo,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Patch,
				CreateUri(requestUri),
				value,
				schema,
				null,
				null,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>Validates <paramref name="value"/> and, when it is valid, PATCHes it as JSON.</summary>
		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchema{T, T}, JsonTypeInfo{T}, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PatchAsJsonValidatedAsync<T>(
			Uri? requestUri,
			T value,
			IZodSchema<T, T> schema,
			JsonTypeInfo<T> jsonTypeInfo,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Patch,
				requestUri,
				value,
				schema,
				null,
				null,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>
		/// Validates <paramref name="value"/>, running service-aware rules with <paramref name="services"/>, and, when it
		/// is valid, PATCHes it as JSON.
		/// </summary>
		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchemaValidator{T}, JsonTypeInfo{T}, IServiceProvider?, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PatchAsJsonValidatedAsync<T>(
			[StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri,
			T value,
			IZodSchemaValidator<T> validator,
			JsonTypeInfo<T> jsonTypeInfo,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Patch,
				CreateUri(requestUri),
				value,
				validator,
				validator,
				services,
				jsonTypeInfo,
				cancellationToken
			);

		/// <summary>
		/// Validates <paramref name="value"/>, running service-aware rules with <paramref name="services"/>, and, when it
		/// is valid, PATCHes it as JSON.
		/// </summary>
		/// <inheritdoc cref="PostAsJsonValidatedAsync{T}(HttpClient, string?, T, IZodSchemaValidator{T}, JsonTypeInfo{T}, IServiceProvider?, CancellationToken)"/>
		public Task<ValidationResult<HttpResponseMessage>> PatchAsJsonValidatedAsync<T>(
			Uri? requestUri,
			T value,
			IZodSchemaValidator<T> validator,
			JsonTypeInfo<T> jsonTypeInfo,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		) =>
			SendValidatedAsync(
				client,
				HttpMethod.Patch,
				requestUri,
				value,
				validator,
				validator,
				services,
				jsonTypeInfo,
				cancellationToken
			);
	}

	// HttpClient's own string overloads convert the same way: an empty string is "no URI" (BaseAddress alone).
	static Uri? CreateUri(string? uri) => string.IsNullOrEmpty(uri) ? null : new Uri(uri, UriKind.RelativeOrAbsolute);

	static async Task<ValidationResult<HttpResponseMessage>> SendValidatedAsync<T>(
		HttpClient client,
		HttpMethod method,
		Uri? requestUri,
		T value,
		IZodSchema<T, T> schema,
		IZodSchemaValidator<T>? servicesValidator,
		IServiceProvider? services,
		JsonTypeInfo<T> jsonTypeInfo,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(client);

		var content = await ZodJsonContent
			.CreateCoreAsync(
				schema,
				servicesValidator,
				services,
				value,
				jsonTypeInfo,
				mediaType: null,
				cancellationToken
			)
			.ConfigureAwait(false);
		if (!content.IsSuccess)
			return ValidationResult<HttpResponseMessage>.Failure(content.Errors);

		// Disposing the request disposes the body; the response's content is already buffered (ResponseContentRead).
		using HttpRequestMessage request = new(method, requestUri) { Content = content.Value };
		var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
		return ValidationResult<HttpResponseMessage>.Success(response);
	}
}
