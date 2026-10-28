namespace ZodSharp.Extensions.Validation.Fixtures;

/// <summary>
/// A union with no validator of its own, whose <see cref="Order"/> case is validated by ZodSharp.
/// </summary>
public readonly union Shipment(Order, Address);
