using ZodSharp.Http.Fixtures;
using ZodSharp.Rules;

namespace ZodSharp.Http;

/// <summary>
/// <c>PostAsJsonValidatedAsync</c>, <c>PutAsJsonValidatedAsync</c> and <c>PatchAsJsonValidatedAsync</c>.
/// </summary>
public class ZodHttpClientExtensionsTests
{
	[Test]
	public async Task PostAsJsonValidatedAsync_GivenValidValue_SendsTheValidatedJson(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using RecordingHandler handler = new();
		using HttpClient client = new(handler) { BaseAddress = new("https://api.test/") };
		Order order = new() { Reference = "ABC-1", Quantity = 2 };

		// Act
		var result = await client.PostAsJsonValidatedAsync(
			"orders",
			order,
			new OrderSchemaValidator(),
			HttpTestJsonContext.Default.Order,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.IsSuccessStatusCode).IsTrue();
		var request = handler.Requests.Single();
		await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
		await Assert.That(request.Uri).IsEqualTo(new Uri("https://api.test/orders"));
		await Assert.That(request.MediaType).IsEqualTo("application/json");
		await Assert
			.That(request.Body)
			.IsEqualTo( /*lang=json,strict*/
				"""{"reference":"ABC-1","quantity":2}"""
			);
	}

	[Test]
	public async Task PostAsJsonValidatedAsync_GivenInvalidValue_ReturnsErrorsAndSendsNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using RecordingHandler handler = new();
		using HttpClient client = new(handler) { BaseAddress = new("https://api.test/") };
		Order order = new() { Reference = "A", Quantity = 0 };

		// Act
		var result = await client.PostAsJsonValidatedAsync(
			new Uri("orders", UriKind.Relative),
			order,
			new OrderSchemaValidator(),
			HttpTestJsonContext.Default.Order,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert
			.That(result.Errors.Select(static error => error.FullPath.ToDottedString()))
			.IsEquivalentTo(["Reference", "Quantity"]);
		await Assert
			.That(result.Errors.Select(static error => error.Code))
			.IsEquivalentTo([MinLengthRule.ErrorCode, MinValueRule<int>.ErrorCode]);
		await Assert.That(handler.Requests).IsEmpty();
	}

	[Test]
	public async Task PutAsJsonValidatedAsync_GivenValidValue_SendsAPut(CancellationToken cancellationToken)
	{
		// Arrange
		using RecordingHandler handler = new();
		using HttpClient client = new(handler) { BaseAddress = new("https://api.test/") };

		// Act
		var result = await client.PutAsJsonValidatedAsync(
			"orders/1",
			new Order { Reference = "ABC-1", Quantity = 1 },
			new OrderSchemaValidator(),
			HttpTestJsonContext.Default.Order,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(handler.Requests.Single().Method).IsEqualTo(HttpMethod.Put);
	}

	[Test]
	public async Task PatchAsJsonValidatedAsync_GivenInvalidValue_SendsNothing(CancellationToken cancellationToken)
	{
		// Arrange
		using RecordingHandler handler = new();
		using HttpClient client = new(handler) { BaseAddress = new("https://api.test/") };

		// Act
		var result = await client.PatchAsJsonValidatedAsync(
			"orders/1",
			new Order { Reference = "ABC-1", Quantity = 0 },
			new OrderSchemaValidator(),
			HttpTestJsonContext.Default.Order,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo(MinValueRule<int>.ErrorCode);
		await Assert.That(handler.Requests).IsEmpty();
	}
}
