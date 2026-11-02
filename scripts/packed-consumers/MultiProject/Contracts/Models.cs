// Packed-consumer gate, consumer (d): the class library the multi-project API references.
using System.ComponentModel.DataAnnotations;
using ZodSharp;
using ZodSharp.Core;

namespace PackedContracts;

/// <summary>A request to register a customer.</summary>
[ZodSchema]
public sealed class RegisterCustomer
{
	/// <summary>The customer's email address, used to sign in.</summary>
	[Required]
	[EmailAddress]
	public string? Email { get; set; }

	/// <summary>The name shown on the customer's account.</summary>
	[Required]
	[StringLength(40, MinimumLength = 2)]
	public string? DisplayName { get; set; }

	/// <summary>Where the customer's orders are delivered.</summary>
	[Required]
	public DeliveryAddress? Address { get; set; }
}

/// <summary>A delivery address.</summary>
[ZodSchema]
public sealed class DeliveryAddress
{
	/// <summary>The postal code, five digits.</summary>
	[Required]
	[RegularExpression("^[0-9]{5}$")]
	public string? PostalCode { get; set; }
}

/// <summary>The errors the customer contracts raise.</summary>
public static partial class CustomerErrors
{
	/// <summary>A customer with the same email address is already registered.</summary>
	[ErrorType]
	public static readonly ErrorType CustomerExists = new(
		Code: "customer_exists",
		Description: "A customer with this email address is already registered.",
		HttpStatus: 409,
		MessageFormat: "A customer with the email address '{Email}' is already registered",
		Parameters: [new ErrorTypeParameter("Email", typeof(string))]
	);
}
