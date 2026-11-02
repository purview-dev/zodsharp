using ZodSharp.Core;

namespace ZodSharp.TrimSafety;

/// <summary>The code the [ZodSchema] generator emits into a trimmed application.</summary>
static class GeneratedChecks
{
	public static Order ValidOrder() =>
		new()
		{
			CustomerEmail = "buyer@example.com",
			ShippingAddress = new() { Street = "1 Main St", City = "Springfield" },
			Lines = [new() { Sku = "ABC-1234", Quantity = 2 }],
			AdditionalAddresses = new()
			{
				["billing"] = new() { Street = "2 Side St", City = "Shelbyville" },
			},
			Pet = new Cat(),
			Status = OrderStatus.Placed,
		};

	public static Order InvalidOrder() =>
		new()
		{
			CustomerEmail = "not-an-email",
			ShippingAddress = new() { Street = "", City = "Springfield" },
			Lines = [new() { Sku = "abc", Quantity = 0 }],
			AdditionalAddresses = new()
			{
				["billing"] = new() { Street = "2 Side St", City = null },
			},
			Pet = new Cat { Name = "T" },
			Status = (OrderStatus)42,
			Notes = "forbidden",
		};

	public static void GeneratedSchema(Harness h)
	{
		h.Expect(OrderSchema.Validate(ValidOrder()).IsSuccess, "a valid order passes");

		var result = OrderSchema.Validate(InvalidOrder());
		h.ExpectPaths(
			result.Errors,
			[
				"CustomerEmail",
				"ShippingAddress.Street",
				"Lines[0].Sku",
				"Lines[0].Quantity",
				"AdditionalAddresses[billing].City",
				"Pet.Name",
				"Status",
			],
			"an invalid order reports nested, collection, dictionary, union and enum errors"
		);

		// IValidatableObject runs once the member rules pass, as it does under DataAnnotations.
		var forbidden = ValidOrder();
		forbidden.Notes = "forbidden";
		h.ExpectPaths(
			OrderSchema.Validate(forbidden).Errors,
			["Notes"],
			"IValidatableObject.Validate runs through the generated schema"
		);

		var street = result.Errors.FirstOrDefault(static e => e.FullPath.ToDottedString() == "ShippingAddress.Street");
		h.Expect(
			street.Message?.Contains("Street line", StringComparison.Ordinal) == true,
			$"the display name comes from DisplayAttribute.ResourceType (got '{street.Message}')"
		);
	}

	public static void MemberValidation(Harness h)
	{
		var order = InvalidOrder();

		var member = OrderSchema.ValidateMember(order, nameof(Order.CustomerEmail));
		h.ExpectPaths(member.Errors, ["CustomerEmail"], "ValidateMember reports only the member");

		IZodUntypedValidator validator = new OrderSchemaValidator();
		var errors = validator.Validate(order);
		var city = errors.First(static e => e.FullPath.ToDottedString() == "AdditionalAddresses[billing].City");
		var resolved = validator.TryResolveMember(order, city.FullPath, out var container, out var memberName);
		h.Expect(
			resolved && ReferenceEquals(container, order.AdditionalAddresses["billing"]) && memberName == "City",
			"TryResolveMember resolves the owning object without reflection"
		);
	}

	public static void CollectionCounts(Harness h)
	{
		var empty = ValidOrder();
		empty.Lines = [];
		var tooFew = OrderSchema.Validate(empty).Errors.Single();
		h.Expect(
			tooFew.Code == "too_small" && tooFew.FullPath.ToDottedString() == "Lines" && tooFew.Minimum == 1,
			$"[MinCount] reports too_small at the member (got {tooFew.Code} at {tooFew.FullPath.ToDottedString()})"
		);

		var full = ValidOrder();
		full.Lines = [.. Enumerable.Range(0, 4).Select(static _ => new OrderLine { Sku = "ABC-1234" })];
		var tooMany = OrderSchema.Validate(full).Errors.Single();
		h.Expect(tooMany.Code == "too_big" && tooMany.Maximum == 3, "[MaxCount] reports too_big with the maximum");

		var crowded = ValidOrder();
		for (var i = 0; i < 3; i++)
			crowded.AdditionalAddresses[$"extra{i}"] = new() { Street = "s", City = "c" };
		var range = OrderSchema.Validate(crowded).Errors.Single();
		h.Expect(
			range.Code == "too_big" && range.Maximum == 2 && range.FullPath.ToDottedString() == "AdditionalAddresses",
			$"[CountRange] counts dictionary entries (got {range.Code}/{range.Origin} at {range.FullPath.ToDottedString()})"
		);
	}
}
