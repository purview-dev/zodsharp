using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Http.Fixtures;

namespace ZodSharp.Http;

/// <summary>
/// The <see cref="IServiceProvider"/> overloads: service-aware rules run before a body is sent and as one is read.
/// </summary>
public class ServiceAwareValidationTests
{
	[Test]
	public async Task PostAsJsonValidatedAsync_GivenServicesAndATakenReference_SendsNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using RecordingHandler handler = new();
		using HttpClient client = new(handler) { BaseAddress = new("https://api.test/") };
		await using var services = CreateServices("TAKEN-1");

		// Act
		var result = await client.PostAsJsonValidatedAsync(
			"bookings",
			new Booking { Reference = "TAKEN-1" },
			new BookingSchemaValidator(),
			HttpTestJsonContext.Default.Booking,
			services,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo(Booking.ReferenceTakenCode);
		await Assert.That(result.Errors.Single().FullPath).IsEqualTo(BookingSchema.Paths.Reference);
		await Assert.That(handler.Requests).IsEmpty();
	}

	[Test]
	public async Task PostAsJsonValidatedAsync_GivenServicesAndAFreeReference_Sends(CancellationToken cancellationToken)
	{
		// Arrange
		using RecordingHandler handler = new();
		using HttpClient client = new(handler) { BaseAddress = new("https://api.test/") };
		await using var services = CreateServices("TAKEN-1");

		// Act
		var result = await client.PostAsJsonValidatedAsync(
			new Uri("bookings", UriKind.Relative),
			new Booking { Reference = "FREE-1" },
			new BookingSchemaValidator(),
			HttpTestJsonContext.Default.Booking,
			services,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsTrue();
		await Assert
			.That(handler.Requests.Single().Body)
			.IsEqualTo( /*lang=json,strict*/
				"""{"reference":"FREE-1"}"""
			);
	}

	[Test]
	public async Task PutAndPatchAsJsonValidatedAsync_GivenServicesAndATakenReference_SendNothing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using RecordingHandler handler = new();
		using HttpClient client = new(handler) { BaseAddress = new("https://api.test/") };
		await using var services = CreateServices("TAKEN-1");
		Booking booking = new() { Reference = "TAKEN-1" };
		BookingSchemaValidator validator = new();

		// Act
		var put = await client.PutAsJsonValidatedAsync(
			"bookings/1",
			booking,
			validator,
			HttpTestJsonContext.Default.Booking,
			services,
			cancellationToken
		);
		var patch = await client.PatchAsJsonValidatedAsync(
			"bookings/1",
			booking,
			validator,
			HttpTestJsonContext.Default.Booking,
			services,
			cancellationToken
		);

		// Assert
		await Assert.That(put.IsSuccess).IsFalse();
		await Assert.That(patch.IsSuccess).IsFalse();
		await Assert.That(handler.Requests).IsEmpty();
	}

	[Test]
	public async Task ReadFromJsonAndValidateAsync_GivenServicesAndATakenReference_ReturnsTheRuleError(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var services = CreateServices("TAKEN-1");
		using StringContent content = new(
			/*lang=json,strict*/"""{"reference":"TAKEN-1"}""",
			Encoding.UTF8,
			"application/json"
		);

		// Act
		var result = await content.ReadFromJsonAndValidateAsync(
			new BookingSchemaValidator(),
			HttpTestJsonContext.Default.Booking,
			services,
			cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo(Booking.ReferenceTakenCode);
	}

	[Test]
	public async Task ReadFromJsonAndValidateAsync_GivenServicesAndMalformedJson_ReturnsInvalidJson(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var services = CreateServices();
		using StringContent content = new("{not json", Encoding.UTF8, "application/json");

		// Act
		var result = await content.ReadFromJsonAndValidateAsync(
			new BookingSchemaValidator(),
			HttpTestJsonContext.Default.Booking,
			services,
			cancellationToken
		);

		// Assert
		await Assert.That(result.Errors.Single().Code).IsEqualTo(SystemTextJsonExtensions.InvalidJsonCode);
	}

	[Test]
	public async Task CreateAsync_GivenServicesAndATakenReference_ReturnsTheRuleError(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		await using var services = CreateServices("TAKEN-1");

		// Act
		var result = await ZodJsonContent.CreateAsync(
			new BookingSchemaValidator(),
			new Booking { Reference = "TAKEN-1" },
			HttpTestJsonContext.Default.Booking,
			services,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors.Single().Code).IsEqualTo(Booking.ReferenceTakenCode);
	}

	static ServiceProvider CreateServices(params string[] taken) =>
		new ServiceCollection().AddSingleton<IReferenceRegistry>(new ReferenceRegistry(taken)).BuildServiceProvider();
}
