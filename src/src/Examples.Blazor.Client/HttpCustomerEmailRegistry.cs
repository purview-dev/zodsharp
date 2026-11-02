namespace ZodSharp.Examples.Blazor.Client;

/// <summary>
/// The browser's <see cref="ICustomerEmailRegistry"/>: asks the server whether an address is taken. The service-aware
/// async rule on <see cref="RegisterCustomerRequest"/> resolves it from the client's services, so the same generated
/// validator runs unchanged in WebAssembly and on the server.
/// </summary>
/// <param name="http">The client for the sample's server.</param>
sealed class HttpCustomerEmailRegistry(HttpClient http) : ICustomerEmailRegistry
{
	/// <inheritdoc/>
	public async ValueTask<bool> IsRegisteredAsync(string email, CancellationToken cancellationToken)
	{
		Uri uri = new($"api/customers/registered?email={Uri.EscapeDataString(email)}", UriKind.Relative);
		var status = await http.GetFromJsonAsync(uri, ClientJsonContext.Default.EmailStatus, cancellationToken);
		return status?.Registered ?? false;
	}

	/// <inheritdoc/>
	/// <remarks>Registration happens on the server, when the form is posted.</remarks>
	public bool TryRegister(string email) => throw new NotSupportedException("Customers register through the server.");
}
