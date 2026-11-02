using System.Collections.Immutable;
using ZodSharp.Core;
using ValidationError = ZodSharp.Core.ValidationError;

namespace ZodSharp.TrimSafety;

/// <summary>An object-typed option that accepts anything, so it carries no CLR type for the union to read.</summary>
sealed class AcceptAll : IZodSchema<object, object>
{
	public ValidationResult<object> Validate(object value) => ValidationResult<object>.Success(value);

	public ValueTask<ValidationResult<object>> ValidateAsync(
		object value,
		CancellationToken cancellationToken = default
	) => new(Validate(value));
}

/// <summary>
/// Collects check results. A check that throws is a failure too, so an exception from trimmed-away metadata or
/// missing AOT code is reported rather than ending the run.
/// </summary>
sealed class Harness
{
	readonly List<string> _failures = [];
	string _section = string.Empty;
	int _passed;

	public void Run(string section, Action<Harness> check)
	{
		_section = section;
		try
		{
			check(this);
		}
		catch (Exception ex)
		{
			Fail($"threw {ex.GetType().Name}: {ex.Message}");
		}
	}

	public async Task RunAsync(string section, Func<Harness, Task> check)
	{
		_section = section;
		try
		{
			await check(this);
		}
		catch (Exception ex)
		{
			Fail($"threw {ex.GetType().Name}: {ex.Message}");
		}
	}

	public void Expect(bool condition, string description)
	{
		if (condition)
		{
			_passed++;
			Console.WriteLine($"  pass  [{_section}] {description}");
		}
		else
		{
			Fail(description);
		}
	}

	public void ExpectPaths(ImmutableArray<ValidationError> errors, string[] expected, string description)
	{
		var actual = errors.IsDefault
			? []
			: errors.Select(static e => e.FullPath.ToDottedString()).Distinct().ToArray();
		var missing = expected.Except(actual).ToArray();
		var unexpected = actual.Except(expected).ToArray();
		Expect(
			missing.Length == 0 && unexpected.Length == 0,
			missing.Length == 0 && unexpected.Length == 0
				? description
				: $"{description} (missing: [{string.Join(", ", missing)}], unexpected: [{string.Join(", ", unexpected)}])"
		);
	}

	public void Fail(string description)
	{
		_failures.Add($"[{_section}] {description}");
		Console.WriteLine($"  FAIL  [{_section}] {description}");
	}

	public int Complete()
	{
		Console.WriteLine();
		if (_failures.Count == 0)
		{
			Console.WriteLine($"Trim and AOT verification passed: {_passed} checks.");
			return 0;
		}

		Console.WriteLine($"Trim and AOT verification FAILED: {_failures.Count} failure(s), {_passed} passed.");
		foreach (var failure in _failures)
			Console.WriteLine($"  {failure}");

		return 1;
	}
}
