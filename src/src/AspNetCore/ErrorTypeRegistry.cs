using System.Collections.Concurrent;

namespace ZodSharp.AspNetCore;

/// <summary>
/// A code-keyed registry of <see cref="ErrorType"/> definitions that allows users to register
/// error types once and resolve them by <see cref="Core.ValidationError.Code"/> when
/// building ProblemDetails responses.
/// </summary>
public sealed class ErrorTypeRegistry
{
	/// <summary>
	/// The process-wide default registry. Register application error types here to make them
	/// available to <see cref="ZodProblemDetailsOptions"/> by default.
	/// </summary>
	public static ErrorTypeRegistry Default { get; } = new();

	readonly ConcurrentDictionary<string, ErrorType> _byCode = new(StringComparer.Ordinal);

	/// <summary>
	/// Registers an <see cref="ErrorType"/> keyed by its <see cref="ErrorType.Code"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when an error type with the same code is already registered.
	/// </exception>
	public void Register(ErrorType errorType)
	{
		ArgumentNullException.ThrowIfNull(errorType);

		if (!_byCode.TryAdd(errorType.Code, errorType))
			throw new InvalidOperationException($"An error type with code '{errorType.Code}' is already registered.");
	}

	/// <summary>
	/// Registers a sequence of <see cref="ErrorType"/> definitions.
	/// </summary>
	public void Register(IEnumerable<ErrorType> errorTypes)
	{
		ArgumentNullException.ThrowIfNull(errorTypes);

		foreach (var errorType in errorTypes)
			Register(errorType);
	}

	/// <summary>
	/// Attempts to resolve the <see cref="ErrorType"/> registered for the given code.
	/// </summary>
	public bool TryGet(string code, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ErrorType? errorType) =>
		_byCode.TryGetValue(code, out errorType);

	/// <summary>
	/// Removes the <see cref="ErrorType"/> registered for the given code.
	/// </summary>
	public bool Remove(string code) => _byCode.TryRemove(code, out _);

	/// <summary>
	/// All registered <see cref="ErrorType"/> definitions.
	/// </summary>
	public ICollection<ErrorType> All => _byCode.Values;
}
