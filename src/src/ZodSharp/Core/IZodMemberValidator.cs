namespace ZodSharp.Core;

/// <summary>
/// Implemented by validators that can validate a single member in isolation — the member's own rules only,
/// without recursing into a nested schema and without type-level rules or refinements. Hosts use it for
/// field-level validation (a Blazor field change), where every error produced is attributed to the field
/// that changed.
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
public interface IZodMemberValidator<T>
{
	/// <summary>
	/// Gets whether <paramref name="memberName"/> has rules that <see cref="ValidateMember"/> runs.
	/// </summary>
	/// <param name="memberName">The C# member name.</param>
	/// <returns><see langword="true"/> when the member is individually validatable.</returns>
	bool IsValidatableMember(string memberName);

	/// <summary>
	/// Validates one member of <paramref name="value"/>. An unknown member succeeds.
	/// </summary>
	/// <param name="value">The containing object.</param>
	/// <param name="memberName">The C# member name.</param>
	/// <returns>The member's errors, with paths relative to <paramref name="value"/>.</returns>
	ValidationResult<T> ValidateMember(T value, string memberName);

	/// <summary>
	/// Validates one member of <paramref name="value"/> asynchronously, including the member's async and
	/// service-aware rules.
	/// </summary>
	/// <param name="value">The containing object.</param>
	/// <param name="memberName">The C# member name.</param>
	/// <param name="services">The caller's services, if any.</param>
	/// <param name="cancellationToken">A token that cancels the validation.</param>
	/// <returns>The member's errors, with paths relative to <paramref name="value"/>.</returns>
	ValueTask<ValidationResult<T>> ValidateMemberAsync(
		T value,
		string memberName,
		IServiceProvider? services = null,
		CancellationToken cancellationToken = default
	) => new(ValidateMember(value, memberName));
}

/// <summary>
/// Implemented by validators that can walk a value along an error path to the object that owns the failing
/// member, without reflection. Blazor needs this to attach a message to the right field
/// (<c>FieldIdentifier(container, member)</c>).
/// </summary>
/// <typeparam name="T">The validated type.</typeparam>
public interface IZodMemberResolver<T>
{
	/// <summary>
	/// Resolves the object that owns the last member of <paramref name="path"/>. A path that ends in an index or
	/// key resolves to the owner of the collection member. A path that cannot be followed resolves to the
	/// nearest object reached.
	/// </summary>
	/// <param name="value">The root value.</param>
	/// <param name="path">The error path, relative to <paramref name="value"/>.</param>
	/// <param name="container">The owning object.</param>
	/// <param name="memberName">The member name on <paramref name="container"/>; empty for a model-level error.</param>
	/// <returns><see langword="true"/> when the full path was followed.</returns>
	bool TryResolveMember(T value, ZodPath path, out object? container, out string memberName);
}
