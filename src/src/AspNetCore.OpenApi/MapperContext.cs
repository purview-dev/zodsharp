using Microsoft.Extensions.Logging;

namespace ZodSharp.AspNetCore.OpenApi;

/// <summary>
/// The per-schema state the mapper needs: the options, and where to report a keyword ZodSharp overrode.
/// </summary>
/// <param name="options">The transformer options.</param>
/// <param name="logger">The logger conflicts are reported to; <see langword="null"/> outside Development.</param>
/// <param name="location">A description of the schema being transformed, for the log line.</param>
sealed class MapperContext(ZodOpenApiOptions options, ILogger? logger, string location)
{
	public ZodOpenApiOptions Options { get; } = options;

	/// <summary>
	/// Gets or sets a <c>format</c> ZodSharp's export assigns to the member's CLR type that does not describe its
	/// System.Text.Json wire form, and so is not written.
	/// </summary>
	public string? IgnoredFormat { get; set; }

	/// <summary>
	/// Reports that ZodSharp replaced a keyword value the platform had already set (for example from a
	/// DataAnnotation). Only a replaced value is reported: a keyword the platform left unset is not a conflict.
	/// </summary>
	public void Conflict(string keyword, object? current, object? value)
	{
		if (current is null || logger is null)
			return;

		ZodOpenApiLog.KeywordOverridden(logger, keyword, location, current, value);
	}
}

static partial class ZodOpenApiLog
{
	[LoggerMessage(
		EventId = 1,
		Level = LogLevel.Debug,
		Message = "ZodSharp overrode OpenAPI keyword '{Keyword}' on {Location}: '{Current}' -> '{Value}'. ZodSharp is the enforcing validator."
	)]
	public static partial void KeywordOverridden(
		ILogger logger,
		string keyword,
		string location,
		object current,
		object? value
	);
}
