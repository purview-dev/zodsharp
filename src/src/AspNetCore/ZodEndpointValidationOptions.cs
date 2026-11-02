namespace ZodSharp.AspNetCore;

/// <summary>
/// Options for <c>WithZodSharpValidation()</c>.
/// </summary>
public sealed class ZodEndpointValidationOptions
{
	/// <summary>
	/// When <c>true</c>, building the endpoint fails if a complex (non-scalar) bound parameter has no registered
	/// validator. Defaults to <c>false</c>: parameters without a validator are not validated.
	/// </summary>
	public bool ThrowIfNoValidator { get; set; }

	/// <summary>
	/// What to do when parameter binding has already failed. .NET 11 runs endpoint filters after a route, query,
	/// header or form binding failure, with the response status already set to 400 and the failed argument left at
	/// its default; a malformed JSON body is answered by ASP.NET Core itself and never reaches a filter. Defaults to
	/// <see cref="ZodBindingFailureBehavior.PassThrough"/>. Has no effect before .NET 11, where filters do not run
	/// after a binding failure.
	/// </summary>
	public ZodBindingFailureBehavior OnBindingFailure { get; set; } = ZodBindingFailureBehavior.PassThrough;

	/// <summary>
	/// The status code advertised in the endpoint's response metadata (and therefore in OpenAPI) for a validation
	/// failure. Defaults to <c>400</c>; set it when a mapped <c>ErrorType</c> changes the status.
	/// </summary>
	public int ProducesStatusCode { get; set; } = 400;
}

/// <summary>
/// How the ZodSharp endpoint filter behaves when parameter binding has already failed.
/// </summary>
public enum ZodBindingFailureBehavior
{
	/// <summary>
	/// Do not validate; let ASP.NET Core's own binding-failure response stand.
	/// </summary>
	PassThrough,

	/// <summary>
	/// Write the ZodSharp ProblemDetails contract instead of ASP.NET Core's empty 400: an <c>invalid_type</c> issue
	/// (<c>invalid_json</c> for a body) for each validated parameter left unbound, or a single root
	/// <c>invalid_type</c> issue when the failed parameter cannot be identified.
	/// </summary>
	ConvertToProblemDetails,
}
