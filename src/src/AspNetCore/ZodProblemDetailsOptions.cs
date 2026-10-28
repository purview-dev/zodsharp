using System.Collections.Immutable;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Configuration for <see cref="ZodProblemDetailsWriter"/>, and therefore for every path that turns ZodSharp
/// errors into ProblemDetails: the endpoint filter, <see cref="ZodExceptionHandler"/>, <see cref="ZodTypedResults"/>
/// and the <c>ToHttpValidationProblemDetails</c>/<c>ToValidationProblem</c> extensions.
/// </summary>
public sealed class ZodProblemDetailsOptions
{
	/// <summary>
	/// The <see cref="ErrorTypeRegistry"/> used to resolve error codes to <see cref="ErrorType"/>s.
	/// Defaults to <see cref="ErrorTypeRegistry.Default"/>.
	/// </summary>
	public ErrorTypeRegistry Registry { get; set; } = ErrorTypeRegistry.Default;

	/// <summary>
	/// An optional resolver used instead of <see cref="Registry"/> to map an error code to an
	/// <see cref="ErrorType"/>.
	/// </summary>
	public Func<string, ErrorType?>? ErrorTypeResolver { get; set; }

	/// <summary>
	/// When <c>true</c>, an error's message is formatted from the matched <see cref="ErrorType.MessageFormat"/>.
	/// The template only replaces the error's message when every placeholder resolves, from the error's
	/// <see cref="ValidationError.Parameters"/> or its built-in fields (<c>path</c>, <c>origin</c>,
	/// <c>minimum</c>, <c>maximum</c>, <c>displayName</c>, …). Defaults to <c>true</c>.
	/// </summary>
	public bool FormatMessages { get; set; } = true;

	/// <summary>
	/// How member names are rendered in <c>errors</c> keys and <c>issues[].path</c>. Defaults to
	/// <see cref="ZodPathNaming.MemberName"/>, which matches Microsoft.Extensions.Validation and MVC model state.
	/// <see cref="ZodPathNaming.Json"/> applies the naming policy from the minimal-API <c>JsonOptions</c>.
	/// </summary>
	public ZodPathNaming PathNaming { get; set; } = ZodPathNaming.MemberName;

	/// <summary>
	/// When <c>true</c>, the <c>issues</c> extension (structured <see cref="ValidationIssue"/>s) is included.
	/// Defaults to <c>true</c>.
	/// </summary>
	public bool IncludeIssues { get; set; } = true;

	/// <summary>
	/// When <c>true</c>, every error's parameters are also merged (camel-cased) into the top-level extensions,
	/// where the last error wins. Defaults to <c>false</c> (it was the 2.4 behaviour): the parameters are always
	/// present per issue in <c>issues[].parameters</c>, which is the reliable place to read them. Enable only for
	/// clients that still read the top-level copy. Reserved member names (<c>type</c>, <c>title</c>,
	/// <c>status</c>, <c>detail</c>, <c>instance</c>, <c>errors</c>, <c>issues</c>, <c>traceId</c>) are never
	/// overwritten; a colliding parameter is written as <c>param_{name}</c>.
	/// </summary>
	public bool MergeParametersIntoExtensions { get; set; }

	/// <summary>
	/// When <c>true</c>, the diagnostic parameters generated validators attach to every error - <c>declaringType</c>
	/// (the full CLR name of the type that declares the failing member) and <c>member</c> (its C# name) - are written
	/// to <c>issues[].parameters</c> (and, with <see cref="MergeParametersIntoExtensions"/>, the top-level extensions).
	/// Defaults to <c>false</c>: they describe the server's implementation, not the request, so they are kept on
	/// <see cref="ValidationError.Parameters"/> for localization and logging but not sent to API clients. The error's
	/// path already names the member in the request's terms.
	/// </summary>
	public bool IncludeDiagnosticParameters { get; set; }

	/// <summary>
	/// When <c>true</c>, <c>errors</c> entries that did not come from ZodSharp (for example DataAnnotations errors
	/// produced by Microsoft.Extensions.Validation and merged by <see cref="ZodProblemDetailsWriter.Apply(Microsoft.AspNetCore.Http.HttpValidationProblemDetails, ReadOnlySpan{ZodSharp.Core.ValidationError}, Microsoft.AspNetCore.Http.HttpContext?)"/>) also get an
	/// <c>issues</c> entry, with code <see cref="ZodProblemDetailsWriter.DataAnnotationsCode"/>. Defaults to
	/// <c>true</c>.
	/// </summary>
	public bool ProjectDataAnnotationsIssues { get; set; } = true;

	/// <summary>
	/// The status code used when no matched <see cref="ErrorType"/> supplies one. Defaults to <c>400</c>.
	/// </summary>
	public int DefaultStatusCode { get; set; } = 400;

	/// <summary>
	/// An optional escape hatch that takes complete control of the response status code. It runs after the
	/// highest matched <see cref="ErrorType.HttpStatus"/> has been computed, and its result wins.
	/// </summary>
	public Func<ImmutableArray<ValidationError>, int>? StatusCodeSelector { get; set; }

	/// <summary>
	/// Like <see cref="StatusCodeSelector"/>, but also receives the computed status (the highest matched
	/// <see cref="ErrorType.HttpStatus"/>, or <see cref="DefaultStatusCode"/>). Runs last and wins.
	/// </summary>
	public Func<ImmutableArray<ValidationError>, int, int>? StatusCodeOverride { get; set; }

	/// <summary>
	/// Registers an <see cref="ErrorType"/> with the configured <see cref="Registry"/>.
	/// </summary>
	public ZodProblemDetailsOptions MapErrorType(ErrorType errorType)
	{
		Registry.Register(errorType);
		return this;
	}

	internal ZodProblemDetailsOptions Clone() =>
		new()
		{
			Registry = Registry,
			ErrorTypeResolver = ErrorTypeResolver,
			FormatMessages = FormatMessages,
			PathNaming = PathNaming,
			IncludeIssues = IncludeIssues,
			MergeParametersIntoExtensions = MergeParametersIntoExtensions,
			ProjectDataAnnotationsIssues = ProjectDataAnnotationsIssues,
			DefaultStatusCode = DefaultStatusCode,
			StatusCodeSelector = StatusCodeSelector,
			StatusCodeOverride = StatusCodeOverride,
		};
}
