using ZodSharp.Core;

namespace ZodSharp.Schemas;

public class ZodDiscriminatedUnionTests
{
	static readonly string[] ReadPermissions = ["read"];

	[Test]
	public async Task DiscriminatedUnion_GivenUserObject_RoutesToUserSchema()
	{
		var union = CreateUnion();

		var result = union.Validate(new UnionUser("user", "John"));

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task DiscriminatedUnion_GivenAdminObject_RoutesToAdminSchema()
	{
		var union = CreateUnion();

		var result = union.Validate(new UnionAdmin("admin", "Admin", ["read"]));

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task DiscriminatedUnion_GivenDictionaryInput_RoutesToMatchingSchema()
	{
		var union = CreateUnion();

		var result = union.Validate(new Dictionary<string, object?> { ["type"] = "admin", ["name"] = "Admin" });

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task DiscriminatedUnion_GivenZodObjectOptions_ValidatesDictionaryInput()
	{
		var userSchema = Z.Object().Field("type", Z.String()).Field("name", Z.String()).Build();
		var adminSchema = Z.Object()
			.Field("type", Z.String())
			.Field("name", Z.String())
			.Field("permissions", Z.Array(Z.String()))
			.Build();
		var union = Z.DiscriminatedUnion("type").Option("user", userSchema).Option("admin", adminSchema).Build();

		var result = union.Validate(
			new Dictionary<string, object?>
			{
				["type"] = "admin",
				["name"] = "Admin",
				["permissions"] = ReadPermissions,
			}
		);

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task DiscriminatedUnion_GivenMissingDiscriminator_ReturnsFailure()
	{
		var union = CreateUnion();

		var result = union.Validate(new { name = "John" });

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("missing_discriminator");
	}

	[Test]
	public async Task DiscriminatedUnion_GivenUnknownDiscriminator_ReturnsFailure()
	{
		var union = CreateUnion();

		var result = union.Validate(new UnionUser("guest", "John"));

		await Assert.That(result.IsSuccess).IsFalse();
		await Assert.That(result.Errors[0].Code).IsEqualTo("invalid_discriminator");
	}

	[Test]
	public async Task DiscriminatedUnion_GivenNonStringDiscriminator_RoutesToMatchingSchema()
	{
		var union = Z.DiscriminatedUnion("kind").Option("1", new ObjectPassThroughSchema()).Build();

		var result = union.Validate(new { kind = 1 });

		await Assert.That(result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task DiscriminatedUnion_GivenRepeatedValidation_KeepsRoutingToMatchingSchema()
	{
		// Exercises the compiled discriminator accessor cache on repeat calls.
		var union = CreateUnion();
		UnionUser user = new("user", "John");

		var first = union.Validate(user);
		var second = union.Validate(user);

		await Assert.That(first.IsSuccess).IsTrue();
		await Assert.That(second.IsSuccess).IsTrue();
	}

	static ZodDiscriminatedUnion CreateUnion()
	{
		ObjectPassThroughSchema userSchema = new();
		ObjectPassThroughSchema adminSchema = new();

		return Z.DiscriminatedUnion("type").Option("user", userSchema).Option("admin", adminSchema).Build();
	}

	sealed record UnionUser(string Type, string Name);

	sealed record UnionAdmin(string Type, string Name, string[] Permissions);

	sealed class ObjectPassThroughSchema : IZodSchema<object, object>
	{
		public ValidationResult<object> Validate(object value) => ValidationResult<object>.Success(value);

		public ValueTask<ValidationResult<object>> ValidateAsync(
			object value,
			CancellationToken cancellationToken = default
		) => new(Validate(value));
	}
}
