#if NET11_0_OR_GREATER
using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Validation;
using ZodSharp.Extensions.Validation;

namespace ZodSharp.AspNetCore;

/// <summary>
/// The double-validation guard: tells the ZodSharp endpoint filter which parameters the built-in
/// Microsoft.Extensions.Validation pipeline already validates with ZodSharp, so <c>WithZodSharpValidation()</c> on
/// an endpoint the pipeline also covers does not validate them twice.
/// </summary>
/// <remarks>
/// The pipeline's filter is outermost and short-circuits on failure, so a parameter it covers has already passed
/// ZodSharp validation by the time the ZodSharp filter would run.
/// </remarks>
sealed class ZodValidationPipelineGuard
{
	static readonly Action<ILogger, string, string, Exception?> LogCoveredParameters = LoggerMessage.Define<
		string,
		string
	>(
		LogLevel.Debug,
		new EventId(2, "ZodValidationCoveredByPipeline"),
		"WithZodSharpValidation on endpoint handler '{Handler}' skips parameter(s) {Parameters}: the "
			+ "Microsoft.Extensions.Validation pipeline already validates them with ZodSharp (AddZodSharpValidation)."
	);

	readonly ValidationOptions _options;
	readonly Assembly _bridge;
	readonly IServiceProviderIsService? _isService;
	readonly ILogger? _logger;

	ZodValidationPipelineGuard(ValidationOptions options, IServiceProviderIsService? isService, ILogger? logger)
	{
		_options = options;
		_bridge = typeof(ZodValidatableInfoResolver).Assembly;
		_isService = isService;
		_logger = logger;
	}

	/// <summary>
	/// Returns the guard for an endpoint, or <see langword="null"/> when the pipeline does not validate it with
	/// ZodSharp: the bridge resolver is not registered, or the endpoint opted out with <c>DisableValidation()</c>.
	/// </summary>
	public static ZodValidationPipelineGuard? Create(IServiceProvider services, IList<object> endpointMetadata)
	{
		if (services.GetService<ZodValidatableInfoResolver>() is not { } resolver)
			return null;

		foreach (var item in endpointMetadata)
		{
			if (item is IDisableValidationMetadata)
				return null;
		}

		var options = services.GetService<IOptions<ValidationOptions>>()?.Value;
		if (options is null || !options.Resolvers.Contains(resolver))
			return null;

		return new(
			options,
			services.GetService<IServiceProviderIsService>(),
			services.GetService<ILoggerFactory>()?.CreateLogger("ZodSharp.AspNetCore.ZodEndpointValidationFilter")
		);
	}

	/// <summary>
	/// Whether the pipeline validates <paramref name="parameter"/> with ZodSharp.
	/// </summary>
	public bool Covers(ParameterInfo parameter)
	{
		// The pipeline never validates a parameter the container resolves.
		if (_isService?.IsService(parameter.ParameterType) == true)
			return false;

		return _options.TryGetValidatableParameterInfo(parameter, out var info) && info.GetType().Assembly == _bridge;
	}

	/// <summary>
	/// Logs, once per endpoint (when it is built), the parameters left to the pipeline.
	/// </summary>
	public void LogCovered(string handler, List<string> parameters)
	{
		if (_logger is not null && _logger.IsEnabled(LogLevel.Debug))
			LogCoveredParameters(_logger, handler, "'" + string.Join("', '", parameters) + "'", null);
	}
}
#endif
