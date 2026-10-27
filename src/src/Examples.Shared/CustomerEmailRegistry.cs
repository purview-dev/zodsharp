using System.Collections.Concurrent;

namespace ZodSharp.Examples;

/// <summary>
/// Looks up whether an email address is already registered. The asynchronous uniqueness rule on
/// <see cref="RegisterCustomerRequest"/> resolves it from the caller's services.
/// </summary>
public interface ICustomerEmailRegistry
{
	/// <summary>
	/// Gets whether <paramref name="email"/> is already registered.
	/// </summary>
	/// <param name="email">The email address.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> when the address is taken.</returns>
	ValueTask<bool> IsRegisteredAsync(string email, CancellationToken cancellationToken);

	/// <summary>
	/// Registers <paramref name="email"/>.
	/// </summary>
	/// <param name="email">The email address.</param>
	/// <returns><see langword="false"/> when the address was already registered.</returns>
	bool TryRegister(string email);
}

/// <summary>
/// An in-memory <see cref="ICustomerEmailRegistry"/>, case-insensitive, seeded with <c>taken@example.com</c> so the
/// samples can show the uniqueness rule failing.
/// </summary>
public sealed class InMemoryCustomerEmailRegistry : ICustomerEmailRegistry
{
	/// <summary>
	/// The address the registry starts with.
	/// </summary>
	public const string SeededEmail = "taken@example.com";

	readonly ConcurrentDictionary<string, byte> _emails = new(StringComparer.OrdinalIgnoreCase) { [SeededEmail] = 0 };

	/// <inheritdoc/>
	public async ValueTask<bool> IsRegisteredAsync(string email, CancellationToken cancellationToken)
	{
		// Stands in for a database or directory lookup.
		await Task.Yield();
		cancellationToken.ThrowIfCancellationRequested();

		return _emails.ContainsKey(email);
	}

	/// <inheritdoc/>
	public bool TryRegister(string email) => _emails.TryAdd(email, 0);
}
