using System.Net.Http.Json;
using System.Text.Json;
using ZodSharp.Blazor;

namespace System.Net.Http;

/// <summary>
/// Reads ZodSharp validation problems from HTTP responses.
/// </summary>
public static class ZodHttpResponseMessageExtensions
{
	extension(HttpResponseMessage response)
	{
		/// <summary>
		/// Reads the response as a validation problem when it is one: an unsuccessful response whose JSON body carries
		/// <c>errors</c> or <c>issues</c>. Trim- and AOT-safe (source-generated JSON metadata).
		/// </summary>
		/// <param name="cancellationToken">A token that cancels reading.</param>
		/// <returns>The problem, or <see langword="null"/> when the response is not a validation problem.</returns>
		/// <example>
		/// <code>
		/// var response = await http.PostAsJsonAsync("/orders", order);
		/// if (await response.TryReadZodValidationProblemAsync() is { } problem)
		///     editContext.AddZodProblemDetails(problem);
		/// </code>
		/// </example>
		public async Task<ZodValidationProblemDetails?> TryReadZodValidationProblemAsync(
			CancellationToken cancellationToken = default
		)
		{
			ArgumentNullException.ThrowIfNull(response);

			if (response.IsSuccessStatusCode || response.Content is null)
				return null;

			var mediaType = response.Content.Headers.ContentType?.MediaType;
			if (
				mediaType is null
				|| !(
					mediaType.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase)
					|| mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
					|| mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
				)
			)
				return null;

			ZodValidationProblemDetails? problem;
			try
			{
				problem = await response
					.Content.ReadFromJsonAsync(
						ZodBlazorJsonContext.Default.ZodValidationProblemDetails,
						cancellationToken
					)
					.ConfigureAwait(false);
			}
			catch (JsonException)
			{
				return null;
			}

			if (problem is null || (problem.Errors is null && problem.Issues is null))
				return null;

			problem.Status ??= (int)response.StatusCode;
			return problem;
		}
	}
}
