using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation;

namespace ZodSharp.Blazor.Fixtures;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when the test advances it, so debounce delays are
/// deterministic.
/// </summary>
sealed class ManualTimeProvider : TimeProvider
{
	readonly Lock _lock = new();
	readonly List<ManualTimer> _timers = [];
	DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	public override DateTimeOffset GetUtcNow() => _now;

	public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
	{
		ManualTimer timer = new(this, callback, state);
		timer.Change(dueTime, period);
		return timer;
	}

	public int PendingTimers
	{
		get
		{
			lock (_lock)
				return _timers.Count(static t => t.Due is not null);
		}
	}

	public void Advance(TimeSpan by)
	{
		List<ManualTimer> due;
		lock (_lock)
		{
			_now += by;
			due = [.. _timers.Where(t => t.Due is { } at && at <= _now)];
			foreach (var timer in due)
				timer.Due = null;
		}

		foreach (var timer in due)
			timer.Fire();
	}

	void Track(ManualTimer timer)
	{
		lock (_lock)
		{
			if (!_timers.Contains(timer))
				_timers.Add(timer);
		}
	}

	sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
	{
		public DateTimeOffset? Due { get; set; }

		public bool Change(TimeSpan dueTime, TimeSpan period)
		{
			Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
			owner.Track(this);
			return true;
		}

		public void Fire() => callback(state);

		public void Dispose() => Due = null;

		public ValueTask DisposeAsync()
		{
			Dispose();
			return ValueTask.CompletedTask;
		}
	}
}

/// <summary>
/// Captures log entries.
/// </summary>
sealed class CapturingLoggerProvider : ILoggerProvider
{
	public ConcurrentQueue<(string Category, LogLevel Level, string Message, Exception? Exception)> Entries { get; } =
		new();

	public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

	public void Dispose() { }

	sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
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
		) => owner.Entries.Enqueue((category, logLevel, formatter(state, exception), exception));
	}
}

sealed class FakeStringLocalizerFactory(Dictionary<string, string> resources) : IStringLocalizerFactory
{
	public IStringLocalizer Create(Type resourceSource) => new FakeStringLocalizer(resources);

	public IStringLocalizer Create(string baseName, string location) => new FakeStringLocalizer(resources);
}

sealed class FakeStringLocalizer(Dictionary<string, string> resources) : IStringLocalizer
{
	public LocalizedString this[string name]
	{
		get
		{
			var culture = CultureInfo.CurrentUICulture.Name;
			if (resources.TryGetValue($"{culture}:{name}", out var specific))
				return new(name, specific, resourceNotFound: false);

			return resources.TryGetValue(name, out var value)
				? new(name, value, resourceNotFound: false)
				: new(name, name, resourceNotFound: true);
		}
	}

	public LocalizedString this[string name, params object[] arguments] => this[name];

	public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
		resources.Select(static pair => new LocalizedString(pair.Key, pair.Value));
}

/// <summary>
/// Records what the bridge reports; must stay empty for Blazor validations through <see cref="ZodValidator"/>.
/// </summary>
sealed class RecordingSink : IZodValidationReportSink
{
	public List<Type> Calls { get; } = [];

	public void Add(
		Type validatedType,
		string pathPrefix,
		System.Collections.Immutable.ImmutableArray<ValidationError> errors
	) => Calls.Add(validatedType);
}

static class FormHost
{
	/// <summary>
	/// Creates a bUnit context with ZodSharp for Blazor registered.
	/// </summary>
	public static BunitContext CreateContext(
		Action<IZodSchemaFactory>? register = null,
		Action<IServiceCollection>? configure = null,
		Action<ZodBlazorOptions>? options = null
	)
	{
		BunitContext context = new();
		context.Services.AddZodSharpFactory(register);
		context.Services.AddZodSharpBlazor(configure: options);
		configure?.Invoke(context.Services);
		return context;
	}

	/// <summary>
	/// Renders <paramref name="content"/> inside a cascading <paramref name="editContext"/>, as an
	/// <see cref="EditForm"/> would.
	/// </summary>
	public static IRenderedComponent<CascadingValue<EditContext>> Render(
		this BunitContext context,
		EditContext editContext,
		RenderFragment content
	) =>
		context.Render<CascadingValue<EditContext>>(parameters =>
			parameters.Add(static p => p.Value, editContext).Add(static p => p.IsFixed, true).AddChildContent(content)
		);

	public static RenderFragment DataAnnotations(bool disableClientValidation = false) =>
		builder =>
		{
			builder.OpenComponent<DataAnnotationsValidator>(0);
			builder.AddComponentParameter(
				1,
				nameof(DataAnnotationsValidator.DisableClientValidation),
				disableClientValidation
			);
			builder.CloseComponent();
		};

	public static RenderFragment Zod(
		ZodValidationTrigger trigger = ZodValidationTrigger.Change,
		int debounceMilliseconds = 0,
		IZodUntypedValidator? schema = null,
		bool validateNestedOnFieldChange = false,
		bool enableClientValidation = true
	) =>
		builder =>
		{
			builder.OpenComponent<ZodValidator>(0);
			builder.AddComponentParameter(1, nameof(ZodValidator.ValidateOn), trigger);
			builder.AddComponentParameter(2, nameof(ZodValidator.DebounceMilliseconds), debounceMilliseconds);
			builder.AddComponentParameter(3, nameof(ZodValidator.Schema), schema);
			builder.AddComponentParameter(
				4,
				nameof(ZodValidator.ValidateNestedOnFieldChange),
				validateNestedOnFieldChange
			);
			builder.AddComponentParameter(5, nameof(ZodValidator.EnableClientValidation), enableClientValidation);
			builder.CloseComponent();
		};

	public static RenderFragment Both(params RenderFragment[] fragments) =>
		builder =>
		{
			foreach (var fragment in fragments)
				fragment(builder);
		};

	/// <summary>
	/// Polls until <paramref name="condition"/> holds: platform validation tasks complete on the renderer's
	/// dispatcher, which does not necessarily re-render anything bUnit could wait on.
	/// </summary>
	public static async Task WaitUntilAsync(Func<bool> condition, string because)
	{
		var deadline = DateTime.UtcNow.AddSeconds(10);
		while (!condition())
		{
			if (DateTime.UtcNow > deadline)
				throw new TimeoutException(because);

			await Task.Delay(5);
		}
	}

	public static Task<bool> ValidateAsync(
		this IRenderedComponent<CascadingValue<EditContext>> form,
		EditContext editContext
	) => form.InvokeAsync(() => editContext.ValidateAsync());

	public static Task ChangeAsync(
		this IRenderedComponent<CascadingValue<EditContext>> form,
		EditContext editContext,
		FieldIdentifier field
	) => form.InvokeAsync(() => editContext.NotifyFieldChanged(field));
}
