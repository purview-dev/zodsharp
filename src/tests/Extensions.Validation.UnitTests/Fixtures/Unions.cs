namespace ZodSharp.Extensions.Validation.Fixtures;

/// <summary>
/// A union with no validator of its own, whose <see cref="Order"/> case is validated by ZodSharp.
/// </summary>
public union Shipment(Order, Address);
