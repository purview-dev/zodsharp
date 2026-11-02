#if NET11_0_OR_GREATER
using Microsoft.Extensions.Logging;

namespace ZodSharp.AspNetCore.Fixtures;

/// <summary>
/// The marker type for the <c>ValidationMessages</c> resources used by the localization tests.
/// </summary>
public sealed class ValidationMessages;

/// <summary>
/// Collects log messages so a test can assert what was logged.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
	readonly Lock _lock = new();
	readonly List<(string Category, LogLevel Level, string Message)> _messages = [];

	public IReadOnlyList<(string Category, LogLevel Level, string Message)> Messages
	{
		get
		{
			lock (_lock)
				return [.. _messages];
		}
	}

	public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

	public void Dispose() { }

	void Add(string category, LogLevel level, string message)
	{
		lock (_lock)
			_messages.Add((category, level, message));
	}

	sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
	{
		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter
		) => provider.Add(category, logLevel, formatter(state, exception));
	}
}
#endif
