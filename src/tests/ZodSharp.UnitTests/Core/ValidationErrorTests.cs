using System.Collections.Immutable;

namespace ZodSharp.Core;

public class ValidationErrorTests
{
	[Test]
	public async Task Constructor_GivenCategory_PopulatesCategory()
	{
		// Arrange
		const string category = "invalid_value";

		// Act
		ValidationError error = new("invalid_tenant_id", "The tenant id is invalid.", category: category);

		// Assert
		await Assert.That(error.Code).IsEqualTo("invalid_tenant_id");
		await Assert.That(error.Category).IsEqualTo(category);
	}

	[Test]
	public async Task Create_GivenCategory_PopulatesCategory()
	{
		// Arrange
		const string category = "invalid_value";

		// Act
		var error = ValidationError.Create(
			"invalid_tenant_id",
			"The tenant id is invalid.",
			ImmutableArray<string>.Empty,
			category: category
		);

		// Assert
		await Assert.That(error.Category).IsEqualTo(category);
	}

	[Test]
	public async Task Create_GivenNoCategory_LeavesCategoryNull()
	{
		// Act
		var error = ValidationError.Create("too_small", "Too small.", ImmutableArray<string>.Empty);

		// Assert
		await Assert.That(error.Category).IsNull();
	}

	[Test]
	public async Task Equality_GivenSameCategory_AreEqual()
	{
		// Arrange
		ValidationError first = new("invalid_tenant_id", "The tenant id is invalid.", category: "invalid_value");
		ValidationError second = new("invalid_tenant_id", "The tenant id is invalid.", category: "invalid_value");
		ValidationError third = new("invalid_tenant_id", "The tenant id is invalid.", category: "invalid_type");

		// Assert
		await Assert.That(first == second).IsTrue();
		await Assert.That(first == third).IsFalse();
	}
}
