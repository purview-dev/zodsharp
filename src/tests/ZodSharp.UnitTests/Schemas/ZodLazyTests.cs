namespace ZodSharp.Schemas;

public class ZodLazyTests
{
	[Test]
	public async Task Lazy_GivenNestedCategory_ReturnsSuccess()
	{
		ZodLazy<Dictionary<string, object?>>? categorySchema = null;
		categorySchema = Z.Lazy(() =>
			Z.Object().Field("name", Z.String()).Field("subcategories", Z.Array(categorySchema!)).Build()
		);
		Dictionary<string, object?> data = new()
		{
			["name"] = "Electronics",
			["subcategories"] = new[]
			{
				new Dictionary<string, object?>
				{
					["name"] = "Phones",
					["subcategories"] = Array.Empty<Dictionary<string, object?>>(),
				},
			},
		};

		var result = categorySchema.Validate(data);

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Lazy_SchemaGetter_IsInvokedOnlyOnce()
	{
		var invocations = 0;
		var schema = Z.Lazy(() =>
		{
			invocations++;
			return Z.String();
		});

		schema.Validate("one");
		schema.Validate("two");

		await Assert.That(invocations).IsEqualTo(1);
	}
}
