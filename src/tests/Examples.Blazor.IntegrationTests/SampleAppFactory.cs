using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using ZodSharp.Examples.Blazor.Components;

namespace ZodSharp.Examples.Blazor;

/// <summary>
/// Hosts the Blazor sample in memory - the same <c>Program.cs</c>, Razor components, static web assets and API - on a
/// <c>TestServer</c>.
/// </summary>
sealed class SampleAppFactory : WebApplicationFactory<App>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder) =>
		builder.UseEnvironment("Development").ConfigureLogging(static logging => logging.ClearProviders());

	/// <summary>
	/// Gets <paramref name="path"/> and parses the HTML.
	/// </summary>
	public static async Task<IHtmlDocument> GetDocumentAsync(
		HttpClient client,
		string path,
		CancellationToken cancellationToken
	)
	{
		using var response = await client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
		response.EnsureSuccessStatusCode();
		return await ParseAsync(response, cancellationToken);
	}

	/// <summary>
	/// Posts a static-SSR form the way a browser does: the fields, the form name and the antiforgery token rendered
	/// into <paramref name="page"/> (its cookie is kept by <paramref name="client"/>).
	/// </summary>
	public static async Task<IHtmlDocument> PostFormAsync(
		HttpClient client,
		string path,
		IHtmlDocument page,
		string formName,
		IEnumerable<KeyValuePair<string, string>> fields,
		CancellationToken cancellationToken
	)
	{
		var token =
			page.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value")
			?? throw new InvalidOperationException("The form rendered no antiforgery token.");

		using FormUrlEncodedContent content = new([
			new("_handler", formName),
			new("__RequestVerificationToken", token),
			.. fields,
		]);
		using var response = await client.PostAsync(new Uri(path, UriKind.Relative), content, cancellationToken);
		response.EnsureSuccessStatusCode();
		return await ParseAsync(response, cancellationToken);
	}

	static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		var html = await response.Content.ReadAsStringAsync(cancellationToken);
		return await new HtmlParser().ParseDocumentAsync(html, cancellationToken);
	}

	/// <summary>
	/// The text of the validation messages rendered directly after the input named <paramref name="name"/>.
	/// </summary>
	public static string MessageAfter(IDocument document, string name)
	{
		var input =
			document.QuerySelector($"[name='{name}']") ?? throw new InvalidOperationException($"No input '{name}'.");
		var text = string.Empty;
		for (
			var sibling = input.NextElementSibling;
			sibling is not null && sibling.ClassList.Contains("validation-message");
			sibling = sibling.NextElementSibling
		)
			text += sibling.TextContent;

		return text.Trim();
	}

	/// <summary>
	/// One client-side rule from the <c>data-rules</c> payload.
	/// </summary>
	public sealed record ClientRule(string Name, string Message, IReadOnlyDictionary<string, string> Parameters);

	/// <summary>
	/// Reads the <c>data-rules</c> payload of the form - the contract between the server and the browser's validators -
	/// keyed by form field name.
	/// </summary>
	public static Dictionary<string, List<ClientRule>> ReadClientRules(IDocument document)
	{
		var element =
			document.QuerySelector("blazor-client-validation-data")
			?? throw new InvalidOperationException("No client validation payload was rendered.");
		using var json = JsonDocument.Parse(element.GetAttribute("data-rules") ?? "{}");

		Dictionary<string, List<ClientRule>> fields = [with(StringComparer.Ordinal)];
		foreach (var field in json.RootElement.GetProperty("fields").EnumerateArray())
		{
			List<ClientRule> rules = [];
			foreach (var rule in field.GetProperty("rules").EnumerateArray())
			{
				Dictionary<string, string> parameters = [with(StringComparer.Ordinal)];
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
