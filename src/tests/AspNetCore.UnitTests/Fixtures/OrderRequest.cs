using ZodSharp.Core;

namespace ZodSharp.AspNetCore.Fixtures;

public sealed class OrderRequest
{
	public string? Email { get; set; }

	public List<OrderLine> Items { get; set; } = [];
}

public sealed class OrderLine
{
	public int Quantity { get; set; }
}

public sealed class CustomerQuery
{
	public string? Name { get; set; }
}

/// <summary>
/// A hand-written validator for <see cref="OrderRequest"/> producing the errors the ProblemDetails contract
/// documents, so the tests do not depend on generated message wording.
/// </summary>
public sealed class OrderRequestValidator : IZodSchemaValidator<OrderRequest>
{
	public int Calls { get; private set; }

	public static ValidationError InvalidEmail =>
		ValidationError.Create(
			"invalid_email",
			"Email must be a valid email address",
			ZodPath.Member("Email"),
			origin: "string",
			category: ZodIssueCategories.InvalidFormat
		);

	public static ValidationError QuantityTooSmall(int index) =>
		ValidationError.Create(
			"too_small",
			"Quantity must be at least 1",
			ZodPath.From(ZodPathSegment.Member("Items"), ZodPathSegment.At(index), ZodPathSegment.Member("Quantity")),
			origin: "number",
			minimum: 1,
			inclusive: true,
			category: ZodIssueCategories.TooSmall
		);

	public static IEnumerable<ValidationError> Check(OrderRequest value)
	{
		if (value.Email is null || !value.Email.Contains('@', StringComparison.Ordinal))
			yield return InvalidEmail;

		for (var i = 0; i < value.Items.Count; i++)
		{
			if (value.Items[i].Quantity < 1)
				yield return QuantityTooSmall(i);
		}
	}

	public ValidationResult<OrderRequest> Validate(OrderRequest value)
	{
		Calls++;
		ValidationError[] errors = [.. Check(value)];
		return errors.Length == 0
			? ValidationResult<OrderRequest>.Success(value)
			: ValidationResult<OrderRequest>.Failure(errors);
	}

	public ValueTask<ValidationResult<OrderRequest>> ValidateAsync(
		OrderRequest value,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult(Validate(value));
}

/// <summary>
/// A validator for <see cref="CustomerQuery"/> that reports a root-level error when the name is missing.
/// </summary>
public sealed class CustomerQueryValidator : IZodSchemaValidator<CustomerQuery>
{
	public ValidationResult<CustomerQuery> Validate(CustomerQuery value) =>
		string.IsNullOrEmpty(value.Name)
			? ValidationResult<CustomerQuery>.Failure(
				ValidationError.Create("required", "Name is required", ZodPath.Member("Name"))
			)
			: ValidationResult<CustomerQuery>.Success(value);

	public ValueTask<ValidationResult<CustomerQuery>> ValidateAsync(
		CustomerQuery value,
		CancellationToken cancellationToken = default
	) => ValueTask.FromResult(Validate(value));
}
