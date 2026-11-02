using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ZodSharp;
using ZodSharp.Core;

namespace System.Net.Http;

/// <summary>
/// Reads and validates JSON HTTP bodies with ZodSharp.
/// </summary>
public static class ZodHttpContentExtensions
{
	extension(HttpContent content)
	{
		/// <summary>
		/// Deserializes the JSON body and validates it, in one pass. A body that cannot be read as
		/// <typeparamref name="T"/> (malformed JSON, a <see langword="null"/> document, a value the serializer cannot
		/// convert) is an <c>invalid_json</c> error rather than an exception, exactly as
		/// <c>DeserializeAndValidateAsync</c> reports it.
		/// </summary>
		/// <typeparam name="T">The validated type.</typeparam>
		/// <param name="schema">The schema to validate with.</param>
		/// <param name="jsonTypeInfo">
		/// The contract for <typeparamref name="T"/>, typically from a source-generated
		/// <see cref="JsonSerializerContext"/>.
		/// </param>
		/// <param name="cancellationToken">Cancels the read and the validation.</param>
		/// <returns>The validated value, or the errors.</returns>
		/// <remarks>
		/// Safe under trimming, Native AOT and Blazor WebAssembly. The body is read as UTF-8, the encoding JSON
		/// exchanged over a network must use (RFC 8259).
		/// </remarks>
		public async Task<ValidationResult<T>> ReadFromJsonAndValidateAsync<T>(
			IZodSchema<T, T> schema,
			JsonTypeInfo<T> jsonTypeInfo,
			CancellationToken cancellationToken = default
		)
		{
			ArgumentNullException.ThrowIfNull(content);
			ArgumentNullException.ThrowIfNull(schema);
			ArgumentNullException.ThrowIfNull(jsonTypeInfo);

			var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			await using (stream.ConfigureAwait(false))
			{
				return await schema
					.DeserializeAndValidateAsync(stream, jsonTypeInfo, cancellationToken)
					.ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Deserializes the JSON body and validates it with a validator that has access to
		/// <paramref name="services"/>, so service-aware rules run. Unreadable bodies are <c>invalid_json</c> errors,
		/// as for the overload without services.
		/// </summary>
		/// <typeparam name="T">The validated type.</typeparam>
		/// <param name="validator">The validator, typically a generated <c>{TypeName}SchemaValidator</c>.</param>
		/// <param name="jsonTypeInfo">
		/// The contract for <typeparamref name="T"/>, typically from a source-generated
		/// <see cref="JsonSerializerContext"/>.
		/// </param>
		/// <param name="services">The services service-aware rules resolve from.</param>
		/// <param name="cancellationToken">Cancels the read and the validation.</param>
		/// <returns>The validated value, or the errors.</returns>
		/// <remarks>Safe under trimming, Native AOT and Blazor WebAssembly. The body is read as UTF-8.</remarks>
		public async Task<ValidationResult<T>> ReadFromJsonAndValidateAsync<T>(
			IZodSchemaValidator<T> validator,
			JsonTypeInfo<T> jsonTypeInfo,
			IServiceProvider? services,
			CancellationToken cancellationToken = default
		)
		{
			ArgumentNullException.ThrowIfNull(content);
			ArgumentNullException.ThrowIfNull(validator);
			ArgumentNullException.ThrowIfNull(jsonTypeInfo);

			var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			await using (stream.ConfigureAwait(false))
			{
				return await validator
					.DeserializeAndValidateAsync(stream, jsonTypeInfo, services, cancellationToken)
					.ConfigureAwait(false);
			}
		}
	}
}
