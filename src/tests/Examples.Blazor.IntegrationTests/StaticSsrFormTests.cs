namespace ZodSharp.Examples.Blazor;

/// <summary>
/// The sample's static-SSR form (<c>/returns</c>): the client-validation payload, and server validation of the posted
/// form with each message next to its input.
/// </summary>
public class StaticSsrFormTests
{
	const string Path = "/returns";
	const string FormName = "return";

	static KeyValuePair<string, string>[] ValidFields() =>
		[
			new("Model.Reference", "RMA-1001"),
			new("Model.OrderId", "3f2504e0-4f89-41d3-9a0c-0305e82c3301"),
			new("Model.ReturnAddress.Street", "1 Main Street"),
			new("Model.ReturnAddress.City", "Leeds"),
			new("Model.ReturnAddress.PostCode", "LS1 1AA"),
			new("Model.Items[0].Sku", "ABC-1234"),
			new("Model.Items[0].Quantity", "1"),
			new("Model.Items[1].Sku", "DEF-5678"),
			new("Model.Items[1].Quantity", "2"),
		];

	[Test]
	public async Task GetReturns_GivenAddZodSharpClientValidation_RendersZodSharpRulesInTheClientRules(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();

		// Act
		var document = await SampleAppFactory.GetDocumentAsync(client, Path, cancellationToken);
		var rules = SampleAppFactory.ReadClientRules(document);

		// Assert
		var reference = rules["Model.Reference"];
		await Assert.That(reference.Select(static r => r.Name)).Contains("required");
		var startsWith = reference.Single(static r => r.Name == "zod-startswith");
		await Assert.That(startsWith.Parameters["prefix"]).IsEqualTo(ReturnRequest.ReferencePrefix);
		await Assert.That(rules["Model.OrderId"].Select(static r => r.Name)).Contains("zod-uuid");
		await Assert.That(rules["Model.ReturnAddress.City"].Select(static r => r.Name)).Contains("required");
		await Assert.That(rules["Model.Items[1].Quantity"].Select(static r => r.Name)).Contains("range");
	}

	[Test]
	public async Task PostReturns_GivenInvalidNestedAndCollectionFields_RendersMessagesNextToTheRightInputs(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();
		var page = await SampleAppFactory.GetDocumentAsync(client, Path, cancellationToken);
		var fields = ValidFields()
			.Select(static f =>
				f.Key switch
				{
					"Model.Reference" => new(f.Key, "RET-1001"),
					"Model.ReturnAddress.City" => new(f.Key, ""),
					"Model.Items[1].Quantity" => new(f.Key, "0"),
					_ => f,
				}
			)
			.ToArray();

		// Act
		var document = await SampleAppFactory.PostFormAsync(client, Path, page, FormName, fields, cancellationToken);

		// Assert
		await Assert.That(document.QuerySelector("#submitted")).IsNull();
		await Assert.That(SampleAppFactory.MessageAfter(document, "Model.Reference")).Contains("RMA-");
		await Assert.That(SampleAppFactory.MessageAfter(document, "Model.ReturnAddress.City")).IsNotEmpty();
		await Assert.That(SampleAppFactory.MessageAfter(document, "Model.Items[1].Quantity")).IsNotEmpty();
		await Assert.That(SampleAppFactory.MessageAfter(document, "Model.Items[0].Quantity")).IsEmpty();
		await Assert.That(SampleAppFactory.MessageAfter(document, "Model.ReturnAddress.Street")).IsEmpty();
		await Assert.That(SampleAppFactory.MessageAfter(document, "Model.OrderId")).IsEmpty();
		// The re-rendered form carries the client rules again.
		await Assert.That(SampleAppFactory.ReadClientRules(document).Keys).Contains("Model.Reference");
	}

	[Test]
	public async Task PostReturns_GivenValidForm_Submits(CancellationToken cancellationToken)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();
		var page = await SampleAppFactory.GetDocumentAsync(client, Path, cancellationToken);

		// Act
		var document = await SampleAppFactory.PostFormAsync(
			client,
			Path,
			page,
			FormName,
			ValidFields(),
			cancellationToken
		);

		// Assert
		await Assert
			.That(document.QuerySelector("#submitted"))
			.IsNotNull()
			.Because(
				string.Join(" | ", document.QuerySelectorAll(".validation-message").Select(static m => m.TextContent))
			);
	}

	[Test]
	public async Task GetOrders_GivenInteractiveServerPage_PrerendersTheNestedCollectionAndDictionaryForm(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using SampleAppFactory factory = new();
		using var client = factory.CreateClient();

		// Act
		var document = await SampleAppFactory.GetDocumentAsync(client, "/orders", cancellationToken);

		// Assert
		await Assert.That(document.QuerySelector("#shipping-city")).IsNotNull();
		await Assert.That(document.QuerySelector("#sku-0")).IsNotNull();
		await Assert
			.That(document.QuerySelectorAll("button").Select(static b => b.TextContent))
			.Contains("Add a billing address");
	}
}
