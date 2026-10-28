using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZodSharp.Core;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace ZodSharp.AspNetCore;

/// <summary>
/// The one implementation that turns ZodSharp <see cref="ValidationError"/>s into an
/// <see cref="HttpValidationProblemDetails"/>. Every path uses it - the endpoint filter, <see cref="ZodExceptionHandler"/>,
/// <see cref="ZodTypedResults"/> and the <c>ToHttpValidationProblemDetails</c>/<c>ToValidationProblem</c> extensions -
/// so they all produce the same body.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Keys</b> are <see cref="ZodPath.ToDottedString"/> of <see cref="ValidationError.FullPath"/>
/// (<c>Items[0].Name</c>), named by <see cref="ZodProblemDetailsOptions.PathNaming"/>.</item>
/// <item><b>Messages</b> come from the registered <see cref="IZodMessageFormatter"/> in the request culture (or
/// <see cref="ValidationError.Message"/> when none is registered). A matched <see cref="ErrorType.MessageFormat"/>
/// replaces the message only when <see cref="ZodProblemDetailsOptions.FormatMessages"/> is on and every placeholder
/// resolves.</item>
/// <item><b>Status</b> is the highest matched <see cref="ErrorType.HttpStatus"/>, then
/// <see cref="ZodProblemDetailsOptions.StatusCodeSelector"/> and <see cref="ZodProblemDetailsOptions.StatusCodeOverride"/>,
/// which run last and win.</item>
/// <item><b>Extensions</b>: <c>issues</c> (safe <see cref="ValidationIssue"/>s), optionally the merged parameters,
/// and <c>traceId</c>. Reserved ProblemDetails member names are never overwritten.</item>
/// </list>
/// </remarks>
public sealed class ZodProblemDetailsWriter
{
	/// <summary>
	/// The issue code given to <c>errors</c> entries that did not come from ZodSharp (see
	/// <see cref="Apply(HttpValidationProblemDetails, ReadOnlySpan{ValidationError}, HttpContext?)"/>).
	/// </summary>
	public const string DataAnnotationsCode = "dataannotations";

	/// <summary>
	/// The title used when no matched <see cref="ErrorType"/> supplies one.
	/// </summary>
	public const string DefaultTitle = "One or more validation errors occurred.";

	/// <summary>
	/// The prefix given to a parameter merged into the extensions whose name collides with a reserved member.
	/// </summary>
	public const string ReservedParameterPrefix = "param_";

	const int MaxUnionDepth = 8;

	static readonly HashSet<string> ReservedNames =
	[
		with(StringComparer.OrdinalIgnoreCase),
		"type",
		"title",
		"status",
		"detail",
		"instance",
		"errors",
		"issues",
		"traceId",
	];

	static readonly Action<ILogger, string, string, Exception?> LogReservedParameter = LoggerMessage.Define<
		string,
		string
	>(
		LogLevel.Warning,
		new EventId(1, "ZodReservedParameter"),
		"The validation error parameter '{Parameter}' collides with a reserved ProblemDetails member and was written as '{Renamed}'."
	);
	readonly IZodMessageFormatter? _formatter;
	readonly ILogger? _logger;
	JsonNamingPolicy? _cachedPolicy;
	IZodPathNamer? _cachedNamer;
	int _collisionLogged;

	/// <summary>
	/// Initializes a writer from the configured options.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <param name="formatter">The message formatter; when <see langword="null"/>, messages are used as produced.</param>
	/// <param name="logger">An optional logger, used once to report a reserved-name parameter collision.</param>
	public ZodProblemDetailsWriter(
		IOptions<ZodProblemDetailsOptions> options,
		IZodMessageFormatter? formatter = null,
		ILogger<ZodProblemDetailsWriter>? logger = null
	)
		: this(options?.Value ?? throw new ArgumentNullException(nameof(options)), formatter, logger) { }

	/// <summary>
	/// Initializes a writer from an options instance.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <param name="formatter">The message formatter; when <see langword="null"/>, messages are used as produced.</param>
	public ZodProblemDetailsWriter(ZodProblemDetailsOptions options, IZodMessageFormatter? formatter = null)
		: this(options, formatter, logger: null) { }

	ZodProblemDetailsWriter(ZodProblemDetailsOptions options, IZodMessageFormatter? formatter, ILogger? logger)
	{
		ArgumentNullException.ThrowIfNull(options);

		Options = options;
		_formatter = formatter;
		_logger = logger;
	}

	/// <summary>
	/// Gets the options this writer applies.
	/// </summary>
	public ZodProblemDetailsOptions Options { get; }

	/// <summary>
	/// Returns the writer registered in <paramref name="services"/>, or creates one from the registered
	/// <see cref="ZodProblemDetailsOptions"/> and <see cref="IZodMessageFormatter"/> (or the defaults).
	/// </summary>
	/// <param name="services">The services, normally <see cref="HttpContext.RequestServices"/>.</param>
	/// <returns>The writer.</returns>
	public static ZodProblemDetailsWriter GetOrCreate(IServiceProvider? services) =>
		services?.GetService<ZodProblemDetailsWriter>()
		?? new(
			services?.GetService<IOptions<ZodProblemDetailsOptions>>()?.Value ?? new ZodProblemDetailsOptions(),
			services?.GetService<IZodMessageFormatter>(),
			services?.GetService<ILogger<ZodProblemDetailsWriter>>()
		);

	/// <summary>
	/// Creates the ProblemDetails for <paramref name="errors"/>.
	/// </summary>
	/// <param name="errors">The errors.</param>
	/// <param name="httpContext">The request, used for the trace id, JSON naming and services; optional.</param>
	/// <returns>The ProblemDetails.</returns>
	public HttpValidationProblemDetails Create(ReadOnlySpan<ValidationError> errors, HttpContext? httpContext = null)
	{
		HttpValidationProblemDetails details = new();
		Populate(details, errors, httpContext, mergeExistingErrors: false);
		return details;
	}

	/// <inheritdoc cref="Create(ReadOnlySpan{ValidationError}, HttpContext?)"/>
	public HttpValidationProblemDetails Create(IEnumerable<ValidationError> errors, HttpContext? httpContext = null)
	{
		ArgumentNullException.ThrowIfNull(errors);

		if (errors is ImmutableArray<ValidationError> immutable)
			return Create(immutable.IsDefault ? [] : immutable.AsSpan(), httpContext);

		ValidationError[] materialized = [.. errors];
		return Create(materialized.AsSpan(), httpContext);
	}

	/// <summary>
	/// Rewrites an existing <paramref name="problemDetails"/> (for example one produced by
	/// Microsoft.Extensions.Validation) with the ZodSharp contract for <paramref name="errors"/>. Existing
	/// <c>errors</c> entries whose key ZodSharp also produces are replaced; the others are kept and, when
	/// <see cref="ZodProblemDetailsOptions.ProjectDataAnnotationsIssues"/> is on, get an <c>issues</c> entry with
	/// code <see cref="DataAnnotationsCode"/>. Status, title, type and detail are recomputed.
	/// </summary>
	/// <param name="problemDetails">The ProblemDetails to rewrite in place.</param>
	/// <param name="errors">The ZodSharp errors.</param>
	/// <param name="httpContext">The request; optional.</param>
	public void Apply(
		HttpValidationProblemDetails problemDetails,
		ReadOnlySpan<ValidationError> errors,
		HttpContext? httpContext = null
	)
	{
		ArgumentNullException.ThrowIfNull(problemDetails);
		Populate(problemDetails, errors, httpContext, mergeExistingErrors: true);
	}

	/// <inheritdoc cref="Apply(HttpValidationProblemDetails, ReadOnlySpan{ValidationError}, HttpContext?)"/>
	/// <remarks>
	/// Accepts any sequence, such as a <see cref="ValidationResult{T}.Errors"/> <see cref="ImmutableArray{T}"/> or a
	/// <see cref="List{T}"/>; an <see cref="ImmutableArray{T}"/> is read without copying.
	/// </remarks>
	public void Apply(
		HttpValidationProblemDetails problemDetails,
		IEnumerable<ValidationError> errors,
		HttpContext? httpContext = null
	)
	{
		ArgumentNullException.ThrowIfNull(problemDetails);
		ArgumentNullException.ThrowIfNull(errors);

		if (errors is ImmutableArray<ValidationError> immutable)
		{
			Apply(problemDetails, immutable.IsDefault ? [] : immutable.AsSpan(), httpContext);
			return;
		}

		ValidationError[] materialized = [.. errors];
		Apply(problemDetails, materialized.AsSpan(), httpContext);
	}

	/// <summary>
	/// Adds an <c>issues</c> entry (code <see cref="DataAnnotationsCode"/>) for every existing <c>errors</c> message,
	/// leaving the rest of <paramref name="problemDetails"/> untouched. Does nothing when
	/// <see cref="ZodProblemDetailsOptions.IncludeIssues"/> or <see cref="ZodProblemDetailsOptions.ProjectDataAnnotationsIssues"/>
	/// is off, when there are no errors, or when the payload already has an <c>issues</c> extension.
	/// </summary>
	internal void ProjectExternalIssues(HttpValidationProblemDetails problemDetails)
	{
		if (
			!Options.IncludeIssues
			|| !Options.ProjectDataAnnotationsIssues
			|| problemDetails.Errors.Count == 0
			|| problemDetails.Extensions.ContainsKey("issues")
		)
		{
			return;
		}

		List<ValidationIssue> issues = [];
		foreach (var pair in problemDetails.Errors)
		{
			foreach (var message in pair.Value ?? [])
				issues.Add(CreateExternalIssue(pair.Key, message));
		}

		// Ahead of traceId, matching the order every ZodSharp payload uses.
		var hasTraceId = problemDetails.Extensions.TryGetValue("traceId", out var traceId);
		problemDetails.Extensions.Remove("traceId");
		problemDetails.Extensions["issues"] = issues.ToArray();
		if (hasTraceId)
			problemDetails.Extensions["traceId"] = traceId;
	}

	/// <summary>
	/// Creates the ProblemDetails for <paramref name="errors"/> and writes it to the response.
	/// </summary>
	/// <param name="httpContext">The request.</param>
	/// <param name="errors">The errors.</param>
	/// <returns>A task that completes when the response is written.</returns>
	public ValueTask WriteAsync(HttpContext httpContext, ReadOnlySpan<ValidationError> errors)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		return WriteAsync(httpContext, Create(errors, httpContext));
	}

	/// <summary>
	/// Writes <paramref name="problemDetails"/> to the response: sets the status code, then writes through the
	/// registered <see cref="IProblemDetailsService"/> (so <c>CustomizeProblemDetails</c> runs), falling back to
	/// <c>application/problem+json</c> with the minimal-API JSON options.
	/// </summary>
	/// <param name="httpContext">The request.</param>
	/// <param name="problemDetails">The ProblemDetails.</param>
	/// <param name="exception">The exception being handled, if any; passed to the ProblemDetails service.</param>
	/// <returns>A task that completes when the response is written.</returns>
	public async ValueTask WriteAsync(
		HttpContext httpContext,
		HttpValidationProblemDetails problemDetails,
		Exception? exception = null
	)
	{
		ArgumentNullException.ThrowIfNull(httpContext);
		ArgumentNullException.ThrowIfNull(problemDetails);

		httpContext.Response.StatusCode = problemDetails.Status ?? Options.DefaultStatusCode;

		if (httpContext.RequestServices?.GetService<IProblemDetailsService>() is { } service)
		{
			ProblemDetailsContext context = new()
			{
				HttpContext = httpContext,
				ProblemDetails = problemDetails,
				Exception = exception,
			};

			if (await service.TryWriteAsync(context).ConfigureAwait(false))
				return;
		}

		await httpContext
			.Response.WriteAsJsonAsync(
				problemDetails,
				ResolveTypeInfo(httpContext),
				"application/problem+json",
				httpContext.RequestAborted
			)
			.ConfigureAwait(false);
	}

	void Populate(
		HttpValidationProblemDetails target,
		ReadOnlySpan<ValidationError> errors,
		HttpContext? httpContext,
		bool mergeExistingErrors
	)
	{
		Dictionary<string, List<string>> grouped = [with(StringComparer.Ordinal)];
		var issues = Options.IncludeIssues ? new List<ValidationIssue>(errors.Length) : null;

		var highest = CollectErrors(errors, httpContext, grouped, issues);

		if (mergeExistingErrors)
			MergeExistingErrors(target, grouped, issues);

		target.Errors.Clear();
		foreach (var pair in grouped)
			target.Errors[pair.Key] = [.. pair.Value];

		var status = ComputeStatus(errors, highest);
		target.Status = status;
		target.Title = highest?.Title ?? DefaultTitle;
		target.Type = highest?.Type ?? GetDefaultType(status);
		target.Detail = highest?.Description ?? (mergeExistingErrors ? target.Detail : null);

		// Re-added below in a fixed order (issues, parameters, traceId), so a payload rewritten by Apply - which may
		// already carry a traceId from the ProblemDetails defaults - serializes exactly like one built by Create.
		target.Extensions.Remove("issues");
		target.Extensions.Remove("traceId");

		if (issues is not null)
			target.Extensions["issues"] = issues.ToArray();

		if (Options.MergeParametersIntoExtensions)
			MergeParameters(target, errors);

		var traceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier;
		if (traceId is not null)
			target.Extensions["traceId"] = traceId;
	}

	ErrorType? CollectErrors(
		ReadOnlySpan<ValidationError> errors,
		HttpContext? httpContext,
		Dictionary<string, List<string>> grouped,
		List<ValidationIssue>? issues
	)
	{
		var namer = ResolveNamer(httpContext);
		var culture = CultureInfo.CurrentUICulture;

		ErrorType? highest = null;
		foreach (ref readonly var error in errors)
		{
			var errorType = ResolveErrorType(error.Code);
			if (errorType is not null && errorType.HttpStatus > (highest?.HttpStatus ?? 0))
				highest = errorType;

			var key = error.FullPath.ToDottedString(namer);
			var message = ResolveMessage(error, errorType, culture);
			AddMessage(grouped, key, message);
			issues?.Add(CreateIssue(error, key, message, namer, culture, depth: 0));
		}

		return highest;
	}

	void MergeExistingErrors(
		HttpValidationProblemDetails target,
		Dictionary<string, List<string>> grouped,
		List<ValidationIssue>? issues
	)
	{
		var project = issues is not null && Options.ProjectDataAnnotationsIssues;
		foreach (var pair in target.Errors)
		{
			if (grouped.ContainsKey(pair.Key))
				continue;

			List<string> messages = [.. pair.Value ?? []];
			grouped[pair.Key] = messages;

			if (!project)
				continue;

			foreach (var message in messages)
				issues!.Add(CreateExternalIssue(pair.Key, message));
		}
	}

	int ComputeStatus(ReadOnlySpan<ValidationError> errors, ErrorType? highest)
	{
		var status = highest?.HttpStatus ?? Options.DefaultStatusCode;
		if (Options.StatusCodeSelector is null && Options.StatusCodeOverride is null)
			return status;

		var snapshot = ImmutableArray.Create(errors);
		if (Options.StatusCodeSelector is { } selector)
			status = selector(snapshot);
		if (Options.StatusCodeOverride is { } statusOverride)
			status = statusOverride(snapshot, status);

		return status;
	}

	ErrorType? ResolveErrorType(string code)
	{
		if (string.IsNullOrEmpty(code))
			return null;

		// The resolver deliberately does not fall through to the registry when it returns null, so this is
		// not a ?? and the conditional expression would be a nested ternary.
#pragma warning disable IDE0046 // Convert to conditional expression
		if (Options.ErrorTypeResolver is { } resolver)
			return resolver(code);
#pragma warning restore IDE0046

		return Options.Registry is { } registry && registry.TryGet(code, out var errorType) ? errorType : null;
	}

	string ResolveMessage(in ValidationError error, ErrorType? errorType, CultureInfo culture)
	{
		try
		{
			if (Options.FormatMessages && errorType?.MessageFormat is { Length: > 0 } template)
			{
				var templated = error with { MessageFormat = template };
				return _formatter is null
					? DefaultZodMessageFormatter.Instance.Format(templated, CultureInfo.InvariantCulture)
					: _formatter.Format(templated, culture);
			}

			return _formatter is null ? error.Message ?? string.Empty : _formatter.Format(error, culture);
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			// A formatter must never turn a validation failure into a server error.
			return error.Message ?? string.Empty;
		}
	}

	ValidationIssue CreateIssue(
		in ValidationError error,
		string key,
		string message,
		IZodPathNamer? namer,
		CultureInfo culture,
		int depth
	)
	{
		var lower = error.Lower;
		var upper = error.Upper;
		var inclusive = error.Category switch
		{
			ZodIssueCategories.TooBig => upper?.Inclusive ?? error.Inclusive,
			ZodIssueCategories.TooSmall => lower?.Inclusive ?? error.Inclusive,
			_ => lower?.Inclusive ?? upper?.Inclusive ?? error.Inclusive,
		};

		return new ValidationIssue
		{
			DottedPath = key,
			Segments = ToSegments(error.FullPath, namer),
			Code = error.Code ?? string.Empty,
			Category = error.Category,
			Origin = error.Origin,
			Lower = lower is { } l ? NormalizeBound(l.Value) : null,
			Upper = upper is { } u ? NormalizeBound(u.Value) : null,
			Inclusive = inclusive,
			Message = message,
			SafeParameters = ProjectParameters(error.Parameters),
			UnionErrors = depth < MaxUnionDepth ? ProjectUnionErrors(error, namer, culture, depth) : null,
			Path = [.. error.Path],
			Minimum = error.Minimum,
			Maximum = error.Maximum,
			Parameters = error.Parameters,
		};
	}

	static ValidationIssue CreateExternalIssue(string key, string message)
	{
		var path = ZodPath.ParseDotted(key);
		return new ValidationIssue
		{
			DottedPath = key,
			Segments = [.. path.ToZodPath()],
			Code = DataAnnotationsCode,
			Category = ZodIssueCategories.InvalidValue,
			Message = message,
			Path = [.. path.LegacySegments],
		};
	}

	ValidationIssue[][]? ProjectUnionErrors(
		in ValidationError error,
		IZodPathNamer? namer,
		CultureInfo culture,
		int depth
	)
	{
		if (error.Parameters is not { } parameters)
			return null;

		List<IEnumerable<ValidationError>>? groups = null;
		if (parameters.TryGetValue(ZodIssueParameters.UnionErrors, out var raw))
		{
			switch (raw)
			{
				case ImmutableArray<ImmutableArray<ValidationError>> immutable when !immutable.IsDefault:
					groups = [];
					foreach (var group in immutable)
						groups.Add(group.IsDefault ? [] : group);
					break;
				case IEnumerable<IEnumerable<ValidationError>> nested:
					groups = [.. nested];
					break;
				case IEnumerable<ValidationError> flat:
					groups = [flat];
					break;
				default:
					break;
			}
		}

		if (groups is null || groups.Count == 0)
			return null;

		var result = new ValidationIssue[groups.Count][];
		for (var i = 0; i < groups.Count; i++)
		{
			List<ValidationIssue> projected = [];
			foreach (var inner in groups[i])
			{
				var errorType = ResolveErrorType(inner.Code);
				var message = ResolveMessage(inner, errorType, culture);
				projected.Add(
					CreateIssue(inner, inner.FullPath.ToDottedString(namer), message, namer, culture, depth + 1)
				);
			}

			result[i] = [.. projected];
		}

		return result;
	}

	void MergeParameters(HttpValidationProblemDetails target, ReadOnlySpan<ValidationError> errors)
	{
		foreach (ref readonly var error in errors)
		{
			if (error.Parameters is not { } parameters)
				continue;

			foreach (var pair in parameters)
			{
				if (!IsClientParameter(pair.Key) || !TryNormalize(pair.Value, allowCollections: true, out var safe))
					continue;

				var name = JsonNamingPolicy.CamelCase.ConvertName(pair.Key);
				if (ReservedNames.Contains(name))
				{
					var renamed = ReservedParameterPrefix + name;
					if (_logger is not null && Interlocked.Exchange(ref _collisionLogged, 1) == 0)
						LogReservedParameter(_logger, pair.Key, renamed, null);
					name = renamed;
				}

				target.Extensions[name] = safe;
			}
		}
	}

	Dictionary<string, object?>? ProjectParameters(ErrorTypeParameters? parameters)
	{
		if (parameters is null || parameters.Count == 0)
			return null;

		Dictionary<string, object?>? result = null;
		foreach (var pair in parameters)
		{
			if (!IsClientParameter(pair.Key) || !TryNormalize(pair.Value, allowCollections: true, out var safe))
				continue;

			result ??= [with(StringComparer.Ordinal)];
			result[JsonNamingPolicy.CamelCase.ConvertName(pair.Key)] = safe;
		}

		return result;
	}

	// The union's nested errors are projected separately (unionErrors); the diagnostic parameters describe the
	// server's types, not the request, so they are written only when IncludeDiagnosticParameters asks for them.
	bool IsClientParameter(string key) =>
		!IsUnionParameter(key)
		&& (
			Options.IncludeDiagnosticParameters
			|| !(
				string.Equals(key, ZodIssueParameters.DeclaringType, StringComparison.Ordinal)
				|| string.Equals(key, ZodIssueParameters.Member, StringComparison.Ordinal)
			)
		);

	static bool IsUnionParameter(string key) =>
		string.Equals(key, ZodIssueParameters.UnionErrors, StringComparison.Ordinal);

	static object[] ToSegments(ZodPath path, IZodPathNamer? namer)
	{
		if (path.IsRoot)
			return [];

		var segments = new object[path.Length];
		for (var i = 0; i < segments.Length; i++)
		{
			var segment = path[i];
			segments[i] = segment.Kind switch
			{
				ZodPathSegmentKind.Index => segment.Index,
				ZodPathSegmentKind.Member => namer?.GetName(segment.Name ?? string.Empty)
					?? segment.Name
					?? string.Empty,
				ZodPathSegmentKind.Key or _ => segment.Name ?? string.Empty,
			};
		}

		return segments;
	}

	static object? NormalizeBound(object? value) =>
		TryNormalize(value, allowCollections: false, out var safe)
			? safe
			: Convert.ToString(value, CultureInfo.InvariantCulture);

	/// <summary>
	/// Projects a value onto the JSON-safe scalar set (string, long, double, decimal, bool, ISO-8601 strings and
	/// arrays of them). Anything else - objects, nested errors - is rejected, so it is never serialized.
	/// </summary>
	static bool TryNormalize(object? value, bool allowCollections, out object? safe)
	{
		switch (value)
		{
			case null:
				safe = null;
				return true;
			case string text:
				safe = text;
				return true;
			case bool flag:
				safe = flag;
				return true;
			case char character:
				safe = character.ToString();
				return true;
			case Enum enumValue:
				safe = enumValue.ToString();
				return true;
			case int or short or byte or sbyte or ushort or long or uint:
				safe = Convert.ToInt64(value, CultureInfo.InvariantCulture);
				return true;
			case ulong unsigned:
				safe = unsigned <= long.MaxValue ? (long)unsigned : (decimal)unsigned;
				return true;
			case float single:
				safe = float.IsFinite(single) ? (double)single : single.ToString("R", CultureInfo.InvariantCulture);
				return true;
			case double number:
				safe = double.IsFinite(number) ? number : number.ToString("R", CultureInfo.InvariantCulture);
				return true;
			case decimal money:
				safe = money;
				return true;
			case IEnumerable sequence when allowCollections:
				List<object?> items = [];
				foreach (var item in sequence)
				{
					if (!TryNormalize(item, allowCollections: false, out var safeItem))
					{
						safe = null;
						return false;
					}

					items.Add(safeItem);
				}

				safe = items.ToArray();
				return true;
			default:
				safe = TryFormatInvariant(value);
				return safe is not null;
		}
	}

	/// <summary>
	/// Renders dates, times, identifiers and other invariant-formattable scalars as strings (ISO-8601 for dates).
	/// </summary>
	static string? TryFormatInvariant(object value) =>
		value switch
		{
			DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
			DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
			DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
			TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
			TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
			Guid guid => guid.ToString("D"),
			Uri uri => uri.OriginalString,
			IFormattable formattable when value.GetType().IsPrimitive || value is Half or Int128 or UInt128 =>
				formattable.ToString(null, CultureInfo.InvariantCulture),
			_ => null,
		};

	IZodPathNamer? ResolveNamer(HttpContext? httpContext)
	{
		if (Options.PathNaming != ZodPathNaming.Json)
			return null;

		var jsonOptions = httpContext?.RequestServices?.GetService<IOptions<HttpJsonOptions>>();
		var policy = jsonOptions is null
			? JsonNamingPolicy.CamelCase
			: jsonOptions.Value.SerializerOptions.PropertyNamingPolicy;
		if (policy is null)
			return null;

		if (!ReferenceEquals(policy, _cachedPolicy) || _cachedNamer is null)
		{
			_cachedNamer = ZodPathNamer.FromFunc(policy.ConvertName);
			_cachedPolicy = policy;
		}

		return _cachedNamer;
	}

	static JsonTypeInfo<HttpValidationProblemDetails> ResolveTypeInfo(HttpContext httpContext)
	{
		var options = httpContext.RequestServices?.GetService<IOptions<HttpJsonOptions>>()?.Value.SerializerOptions;
		// The three-part pattern is clearer as a guard than as a conditional expression.
#pragma warning disable IDE0046 // Convert to conditional expression
		if (
			options is not null
			&& options.TryGetTypeInfo(typeof(HttpValidationProblemDetails), out var typeInfo)
			&& typeInfo is JsonTypeInfo<HttpValidationProblemDetails> typed
		)
		{
			return typed;
		}
#pragma warning restore IDE0046

		return ZodProblemDetailsJsonContext.Default.HttpValidationProblemDetails;
	}

	static void AddMessage(Dictionary<string, List<string>> grouped, string key, string message)
	{
		if (!grouped.TryGetValue(key, out var messages))
		{
			messages = [];
			grouped[key] = messages;
		}

		messages.Add(message);
	}

	/// <summary>
	/// The RFC 9110 <c>type</c> URI ASP.NET Core assigns by default for <paramref name="status"/>, so every path
	/// sets the same value whether or not it goes through the ProblemDetails service.
	/// </summary>
	static string? GetDefaultType(int status) =>
		status switch
		{
			400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
			401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
			403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
			404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
			405 => "https://tools.ietf.org/html/rfc9110#section-15.5.6",
			406 => "https://tools.ietf.org/html/rfc9110#section-15.5.7",
			408 => "https://tools.ietf.org/html/rfc9110#section-15.5.9",
			409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
			412 => "https://tools.ietf.org/html/rfc9110#section-15.5.13",
			415 => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
			422 => "https://tools.ietf.org/html/rfc9110#section-15.5.21",
			426 => "https://tools.ietf.org/html/rfc9110#section-15.5.22",
			500 => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
			502 => "https://tools.ietf.org/html/rfc9110#section-15.6.3",
			503 => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
			504 => "https://tools.ietf.org/html/rfc9110#section-15.6.5",
			_ => null,
		};
}
