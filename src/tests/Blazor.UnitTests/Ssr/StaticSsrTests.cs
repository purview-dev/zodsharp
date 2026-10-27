using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.DependencyInjection;

namespace ZodSharp.Blazor.Ssr;

/// <summary>
/// Static server-side rendering: server validation of posted forms, and the client-validation payload.
/// </summary>
public class StaticSsrTests
{
	[Test]
	public async Task Get_GivenZodSharpClientValidation_RendersZodRulesBesideTheBuiltInRules(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await StartAsync(cancellationToken);

		// Act
		var document = await host.GetAsync("/order/data-annotations", cancellationToken);

		// Assert
		var rules = ReadRules(document);
		var reference = rules["Model.Reference"];
		await Assert.That(reference.Select(static r => r.Name)).Contains("required");
		await Assert.That(reference.Select(static r => r.Name)).Contains("zod-startswith");
		var startsWith = reference.Single(static r => r.Name == "zod-startswith");
		await Assert.That(startsWith.Parameters["prefix"]).IsEqualTo("ORD-");
		await Assert.That(startsWith.Parameters["comparison"]).IsEqualTo("ordinal");
		await Assert.That(startsWith.Message).IsEqualTo("String must start with 'ORD-'");
	}

	[Test]
	public async Task Get_GivenNestedAndCollectionFields_RendersRulesUnderTheFormFieldNames(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await StartAsync(cancellationToken);

		// Act
		var document = await host.GetAsync("/order/data-annotations", cancellationToken);

		// Assert
		var rules = ReadRules(document);
		await Assert.That(rules.Keys).Contains("Model.Customer.Email");
		await Assert.That(rules["Model.Lines[1].Quantity"].Select(static r => r.Name)).Contains("range");
		await Assert.That(document.QuerySelector("input[name='Model.Lines[1].Quantity']")).IsNotNull();
	}

	[Test]
	public async Task Get_GivenZodValidator_RendersTheClientValidationPayloadToo(CancellationToken cancellationToken)
	{
		// Arrange
		await using var host = await StartAsync(cancellationToken);

		// Act
		var document = await host.GetAsync("/order/zod", cancellationToken);

		// Assert
		var rules = ReadRules(document);
		await Assert.That(rules["Model.Reference"].Select(static r => r.Name)).Contains("zod-startswith");
	}

	[Test]
	public async Task Get_GivenNoZodSharpClientValidation_RendersOnlyTheBuiltInRules(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await StaticSsrHost.StartAsync(
			static services =>
			{
				services.AddRazorComponents();
				services.AddZodSharpFactory(static factory =>
					ZodSharp.Generated.PurviewZodSharpBlazorUnitTestsZodSharpSchemas.Register(factory)
				);
				services.AddZodSharpBlazor();
			},
			cancellationToken: cancellationToken
		);

		// Act
		var document = await host.GetAsync("/order/data-annotations", cancellationToken);

		// Assert
		var rules = ReadRules(document);
		await Assert.That(rules["Model.Reference"].Select(static r => r.Name)).DoesNotContain("zod-startswith");
		await Assert.That(rules["Model.Reference"].Select(static r => r.Name)).Contains("required");
	}

	[Test]
	public async Task Post_GivenInvalidNestedAndCollectionFields_RendersMessagesNextToTheRightInputs(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var host = await StartAsync(cancellationToken);

		// Act
		var document = await host.PostAsync(
			"/order/data-annotations",
			[
				new("_handler", "order"),
				new("Model.Reference", "ORD-1"),
				new("Model.Customer.Email", "not-an-email"),
				new("Model.Lines[0].Sku", "A"),
				new("Model.Lines[0].Quantity", "5"),
				new("Model.Lines[1].Sku", "B"),
				new("Model.Lines[1].Quantity", "0"),
			],
			cancellationToken
		);

		// Assert
		await Assert.That(document.QuerySelector("#submitted")).IsNull();
		await Assert.That(MessageAfter(document, "Model.Customer.Email")).IsNotEmpty();
		await Assert.That(MessageAfter(document, "Model.Lines[1].Quantity")).IsNotEmpty();
		await Assert.That(MessageAfter(document, "Model.Lines[0].Quantity")).IsEmpty();
		await Assert.That(MessageAfter(document, "Model.Reference")).IsEmpty();
		await Assert.That(ReadRules(document).Keys).Contains("Model.Reference");
	}

	[Test]
	public async Task Post_GivenValidForm_Submits(CancellationToken cancellationToken)
	{
		// Arrange
		await using var host = await StartAsync(cancellationToken);

		// Act
		var document = await host.PostAsync(
			"/order/data-annotations",
			[
				new("_handler", "order"),
				new("Model.Reference", "ORD-1"),
				new("Model.Customer.Email", "someone@example.com"),
				new("Model.Lines[0].Sku", "A"),
				new("Model.Lines[0].Quantity", "5"),
			],
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
	public async Task Post_GivenZodValidatorForm_RendersCodesOnTheMessages(CancellationToken cancellationToken)
	{
		// Arrange
		await using var host = await StartAsync(cancellationToken);

		// Act
		var document = await host.PostAsync(
			"/order/zod",
			[
				new("_handler", "order"),
				new("Model.Reference", "bad"),
				new("Model.Lines[0].Sku", "A"),
				new("Model.Lines[0].Quantity", "500"),
			],
			cancellationToken
		);

		// Assert
		var quantity = document.QuerySelector("div.validation-message[data-valmsg-for='Model.Lines[0].Quantity']");
		await Assert.That(quantity).IsNotNull();
		await Assert.That(quantity!.GetAttribute("data-category")).IsEqualTo("too_big");
		var reference = document.QuerySelector("div.validation-message[data-valmsg-for='Model.Reference']");
		await Assert.That(reference!.GetAttribute("data-code")).IsEqualTo("invalid_string");
	}

	static Task<StaticSsrHost> StartAsync(CancellationToken cancellationToken) =>
		StaticSsrHost.StartAsync(
			static services =>
			{
				services.AddZodSharpClientValidation();
				services.AddRazorComponents();
				services.AddZodSharpFactory(static factory =>
					ZodSharp.Generated.PurviewZodSharpBlazorUnitTestsZodSharpSchemas.Register(factory)
				);
				services.AddZodSharpBlazor();
			},
			cancellationToken: cancellationToken
		);

	/// <summary>
	/// The text of the validation message rendered after the input named <paramref name="name"/>.
	/// </summary>
	static string MessageAfter(IHtmlDocument document, string name)
	{
		var input =
			document.QuerySelector($"[name='{name}']") ?? throw new InvalidOperationException($"No input '{name}'.");
		var sibling = input.NextElementSibling;
		var text = string.Empty;
		while (sibling is not null && sibling.ClassList.Contains("validation-message"))
		{
			text += sibling.TextContent;
			sibling = sibling.NextElementSibling;
		}

		return text.Trim();
	}

	internal sealed record Rule(string Name, string Message, Dictionary<string, string> Parameters);

	/// <summary>
	/// Reads the <c>data-rules</c> payload: the wire contract between the server and the browser's validators.
	/// </summary>
	internal static Dictionary<string, List<Rule>> ReadRules(IDocument document)
	{
		var element =
			document.QuerySelector("blazor-client-validation-data")
			?? throw new InvalidOperationException("No client validation payload was rendered.");
		using var json = JsonDocument.Parse(element.GetAttribute("data-rules") ?? "{}");

		Dictionary<string, List<Rule>> fields = new(StringComparer.Ordinal);
		foreach (var field in json.RootElement.GetProperty("fields").EnumerateArray())
		{
			List<Rule> rules = [];
			foreach (var rule in field.GetProperty("rules").EnumerateArray())
			{
				Dictionary<string, string> parameters = [];
				if (rule.TryGetProperty("params", out var values))
				{
					foreach (var value in values.EnumerateObject())
						parameters[value.Name] = value.Value.GetString() ?? string.Empty;
				}

				rules.Add(
					new(rule.GetProperty("name").GetString()!, rule.GetProperty("message").GetString()!, parameters)
				);
			}

			fields[field.GetProperty("name").GetString()!] = rules;
		}

		return fields;
	}
}
