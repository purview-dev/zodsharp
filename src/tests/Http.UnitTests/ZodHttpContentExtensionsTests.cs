using System.Text;
using ZodSharp.Http.Fixtures;
using ZodSharp.Rules;

namespace ZodSharp.Http;

/// <summary>
/// <c>HttpContent.ReadFromJsonAndValidateAsync</c> and <see cref="ZodJsonContent"/>.
/// </summary>
public class ZodHttpContentExtensionsTests
{
	[Test]
	public async Task ReadFromJsonAndValidateAsync_GivenValidBody_ReturnsTheValue(CancellationToken cancellationToken)
	{
		// Arrange
		using StringContent content = new(
			/*lang=json,strict*/"""{"reference":"ABC-1","quantity":2}""",
			Encoding.UTF8,
			"application/json"
		);

		// Act
		var result = await content.ReadFromJsonAndValidateAsync(
			new OrderSchemaValidator(),
			HttpTestJsonContext.Default.Order,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert.That(result.Value!.Reference).IsEqualTo("ABC-1");
		await Assert.That(result.Value!.Quantity).IsEqualTo(2);
	}

	[Test]
	public async Task ReadFromJsonAndValidateAsync_GivenInvalidValue_ReturnsTheSchemaErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using StringContent content = new(
			/*lang=json,strict*/"""{"reference":"ABC-1","quantity":0}""",
			Encoding.UTF8,
			"application/json"
		);

		// Act
		var result = await content.ReadFromJsonAndValidateAsync(
			new OrderSchemaValidator(),
			HttpTestJsonContext.Default.Order,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		var error = result.Errors.Single();
		await Assert.That(error.Code).IsEqualTo(MinValueRule<int>.ErrorCode);
		await Assert.That(error.FullPath.ToDottedString()).IsEqualTo("Quantity");
	}

	[Test]
	[Arguments("{not json")]
	[Arguments("null")]
	[Arguments( /*lang=json,strict*/
		"""{"reference":"ABC-1","quantity":"two"}"""
	)]
	public async Task ReadFromJsonAndValidateAsync_GivenUnreadableBody_ReturnsInvalidJson(
		string body,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using StringContent content = new(body, Encoding.UTF8, "application/json");

		// Act
		var result = await content.ReadFromJsonAndValidateAsync(
			new OrderSchemaValidator(),
			HttpTestJsonContext.Default.Order,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
	}

	[Test]
	public async Task Create_GivenValidValue_ReturnsJsonContent(CancellationToken cancellationToken)
	{
		// Arrange
		Order order = new() { Reference = "ABC-1", Quantity = 2 };

		// Act
		var result = ZodJsonContent.Create(new OrderSchemaValidator(), order, HttpTestJsonContext.Default.Order);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		using var content = result.Value!;
		await Assert
			.That(await content.ReadAsStringAsync(cancellationToken))
			.IsEqualTo( /*lang=json,strict*/
				"""{"reference":"ABC-1","quantity":2}"""
			);
	}

	[Test]
	public async Task CreateAsync_GivenInvalidValue_ReturnsTheErrors(CancellationToken cancellationToken)
	{
		// Arrange
		Order order = new() { Reference = "A", Quantity = 2 };

		// Act
		var result = await ZodJsonContent.CreateAsync(
			new OrderSchemaValidator(),
			order,
			HttpTestJsonContext.Default.Order,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo(MinLengthRule.ErrorCode);
	}
}
