namespace ZodSharp.Examples.MinimalApi;

/// <summary>
/// The response to an accepted order.
/// </summary>
/// <param name="Id">The order id.</param>
/// <param name="Lines">The number of order lines.</param>
sealed record OrderAccepted(string Id, int Lines)
{
	public static OrderAccepted For(CreateOrderRequest order) =>
		new(Guid.CreateVersion7().ToString("N"), order.Lines.Count);
}

/// <summary>
/// The response to a registered customer.
/// </summary>
/// <param name="Email">The registered email address.</param>
sealed record CustomerRegistered(string Email);
