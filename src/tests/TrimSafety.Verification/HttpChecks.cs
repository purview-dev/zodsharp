using System.Net;
using System.Text;

namespace ZodSharp.TrimSafety;

/// <summary>Purview.ZodSharp.Http: validated request and response bodies, and reading validation problems.</summary>
static class HttpChecks
{
	public static async Task HttpClient(Harness h)
	{
		using EchoHandler handler = new();
		using HttpClient client = new(handler) { BaseAddress = new("https://harness.test/") };
		CustomerSchemaValidator schema = new();
		var typeInfo = HarnessJsonContext.Default.Customer;

		var sent = await client.PostAsJsonValidatedAsync(
			"customers",
			new Customer { Name = "Grace", Age = 85 },
			schema,
			typeInfo
		);
		h.Expect(
			sent.IsSuccess && handler.Requests == 1,
			"a valid value is sent through the source-generated contract"
		);

		using var response = sent.Value!;
		var read = await response.Content.ReadFromJsonAndValidateAsync(schema, typeInfo);
		h.Expect(read.IsSuccess && read.Value!.Name == "Grace", "the response body reads back and validates");

		var rejected = await client.PostAsJsonValidatedAsync(
			"customers",
			new Customer { Name = "A", Age = 200 },
			schema,
			typeInfo
		);
		h.Expect(!rejected.IsSuccess && handler.Requests == 1, "an invalid value is never sent");

		using HttpResponseMessage problemResponse = new(HttpStatusCode.BadRequest)
		{
			Content = new StringContent(
				/*lang=json,strict*/"""
				{"status":400,"issues":[{"path":"Name","segments":["Name"],"code":"too_small","message":"Too short",
				"minimum":2,"inclusive":true}]}
				""",
				Encoding.UTF8,
				"application/problem+json"
			),
		};
		var problem = await problemResponse.TryReadZodValidationProblemAsync();
		var errors = problem?.ToValidationErrors() ?? [];
		h.Expect(
			errors.Length == 1 && errors[0].Code == "too_small" && errors[0].Lower?.Value is 2L,
			"a validation problem reads through the package's source-generated context"
		);
	}

	/// <summary>Answers every request with its own body, so a round trip needs no network.</summary>
	sealed class EchoHandler : HttpMessageHandler
	{
		public int Requests { get; private set; }

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken
		)
		{
			Requests++;
			var body = request.Content is null
				? string.Empty
				: await request.Content.ReadAsStringAsync(cancellationToken);
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			};
		}
	}
}
