using System.Collections.Immutable;
using ZodValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// The full-fidelity ZodSharp errors produced during one Microsoft.Extensions.Validation run. The
/// Microsoft.Extensions.Validation error carries only a name, path, message and container, so codes,
/// categories, origins, bounds and parameters travel here instead.
/// </summary>
/// <remarks>
/// Error paths are canonical: each error's <see cref="ZodValidationError.FullPath"/> already includes the
/// <c>ValidateContext.CurrentValidationPath</c> it was reported under, so it renders to the same key as the
/// matching <c>Microsoft.Extensions.Validation.ValidationError.Path</c>.
/// </remarks>
public sealed class ZodValidationReport
{
	readonly Lock _lock = new();
	ImmutableArray<ZodValidationError> _errors = [];
	ImmutableHashSet<string> _keys = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal);

	/// <summary>
	/// Creates an empty report.
	/// </summary>
	public ZodValidationReport() { }

	/// <summary>
	/// Creates a report holding <paramref name="errors"/>.
	/// </summary>
	/// <param name="errors">The errors.</param>
	public ZodValidationReport(IEnumerable<ZodValidationError> errors)
	{
		ArgumentNullException.ThrowIfNull(errors);
		_errors = [.. errors];
	}

	/// <summary>
	/// Gets the errors, in the order they were reported.
	/// </summary>
	public IReadOnlyList<ZodValidationError> Errors => _errors;

	/// <summary>
	/// Gets whether any error was reported.
	/// </summary>
	public bool HasErrors => !_errors.IsEmpty;

	internal ImmutableHashSet<string> Keys => _keys;

	internal void Add(ImmutableArray<ZodValidationError> errors, IEnumerable<string> keys)
	{
		lock (_lock)
		{
			_errors = _errors.AddRange(errors);
			_keys = _keys.Union(keys);
		}
	}
}
