using Microsoft.AspNetCore.Http;

namespace ZodSharp.AspNetCore;

public class ErrorTypeRegistryTests
{
	[Test]
	public async Task Register_ThenTryGet_ReturnsRegisteredType()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		ErrorType errorType = new("aggregate_save_failed", HttpStatus: StatusCodes.Status409Conflict);

		// Act
		registry.Register(errorType);
		var found = registry.TryGet("aggregate_save_failed", out var resolved);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(resolved).IsSameReferenceAs(errorType);
	}

	[Test]
	public async Task Register_DuplicateCode_Throws()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(new ErrorType("duplicate"));

		// Act
		void IAct() => registry.Register(new ErrorType("duplicate"));

		// Assert
		await Assert.That(IAct).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task Register_MultipleTypes_AllReturnsThem()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		ErrorType first = new("first");
		ErrorType second = new("second", HttpStatus: StatusCodes.Status409Conflict);

		// Act
		registry.Register([first, second]);

		// Assert
		await Assert.That(registry.All).Contains(first);
		await Assert.That(registry.All).Contains(second);
	}

	[Test]
	public async Task Remove_RemovesRegisteredType()
	{
		// Arrange
		ErrorTypeRegistry registry = new();
		registry.Register(new ErrorType("stale"));

		// Act
		var removed = registry.Remove("stale");

		// Assert
		await Assert.That(removed).IsTrue();
		await Assert.That(registry.TryGet("stale", out _)).IsFalse();
	}

	[Test]
	public async Task Default_IsUsableSharedInstance()
	{
		// Arrange / Act
		var first = ErrorTypeRegistry.Default;
		var second = ErrorTypeRegistry.Default;

		// Assert
		await Assert.That(first).IsSameReferenceAs(second);
	}
}
