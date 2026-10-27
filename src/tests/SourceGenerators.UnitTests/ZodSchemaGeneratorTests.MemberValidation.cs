using ZodSharp.Core;
using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Member validation (Change 2), member resolution (Change 3), the generated per-assembly registration and
/// the untyped validator contract (Change 5).
/// </summary>
partial class ZodSchemaGeneratorTests
{
	const string FlatMembersSource = """
		using System.ComponentModel.DataAnnotations;
		using ZodSharp;

		namespace Testing
		{
			[ZodSchema]
			public class Registration
			{
				[Required]
				[StringLength(20, MinimumLength = 3)]
				public string? UserName { get; set; }

				[EmailAddress]
				public string? Email { get; set; }

				[Range(18, 120)]
				public int Age { get; set; }

				public string? Password { get; set; }

				[Compare(nameof(Password))]
				public string? ConfirmPassword { get; set; }

				public string? Notes { get; set; }
			}
		}
		""";

	[Test]
	public async Task ValidateMember_GivenEveryMember_MatchesValidateFilteredToThatMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			FlatMembersSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var registration = Create(
			assembly,
			"Testing.Registration",
			("UserName", "ab"),
			("Email", "not-an-email"),
			("Age", 12),
			("Password", "secret"),
			("ConfirmPassword", "different")
		);
		var all = RunValidate(assembly, "Testing.RegistrationSchema", registration);
		string[] members = ["UserName", "Email", "Age", "Password", "ConfirmPassword", "Notes"];

		foreach (var member in members)
		{
			// Act
			var memberErrors = RunValidateMember(assembly, "Testing.RegistrationSchema", registration, member);

			// Assert — property-based parity: the member's errors are exactly Validate's errors at that member.
			var expected = all.Where(error => error.FullPath[0].Name == member).ToArray();
			await Assert.That(memberErrors.ToArray()).IsEquivalentTo(expected).Because(member);
		}

		await Assert.That(all.Length).IsEqualTo(4);
	}

	[Test]
	public async Task IsValidatableMember_GivenMembers_IsTrueOnlyForMembersWithRules(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			FlatMembersSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;

		// Act
		bool IsValidatable(string member) =>
			InvokeStatic<bool>(assembly, "Testing.RegistrationSchema", "IsValidatableMember", member);

		// Assert — matching is ordinal on the C# member name.
		await Assert.That(IsValidatable("UserName")).IsTrue();
		await Assert.That(IsValidatable("ConfirmPassword")).IsTrue();
		await Assert.That(IsValidatable("Notes")).IsFalse();
		await Assert.That(IsValidatable("username")).IsFalse();
		await Assert.That(IsValidatable("Unknown")).IsFalse();
	}

	[Test]
	public async Task ValidateMember_GivenNestedAndCollectionMembers_DoesNotRecurseButKeepsNullElementChecks(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			NestedCollectionsSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var order = CreateInvalidOrder(assembly, out _);

		// Act
		var customer = RunValidateMember(assembly, "Testing.OrderSchema", order, "Customer");
		var lines = RunValidateMember(assembly, "Testing.OrderSchema", order, "Lines");
		var unknown = RunValidateMember(assembly, "Testing.OrderSchema", order, "DoesNotExist");

		// Assert — Blazor attributes field-level errors to the changed field, so a nested type's own errors are
		// left to its own field changes; the null element belongs to the collection member.
		await Assert.That(customer).IsEmpty();
		await Assert
			.That(lines.Select(static error => error.FullPath.ToDottedString()).ToArray())
			.IsEquivalentTo(["Lines[2]"]);
		await Assert.That(unknown).IsEmpty();
	}

	[Test]
	public async Task TryResolveMember_GivenEveryValidateError_ResolvesTheOwningObjectAndMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			NestedCollectionsSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var order = CreateInvalidOrder(assembly, out var typed);
		var errors = RunValidate(assembly, "Testing.OrderSchema", order);
		Dictionary<string, (object Container, string Member)> expected = new(StringComparer.Ordinal)
		{
			["Number"] = (order, "Number"),
			["Customer.Name"] = ((object)typed.Customer, "Name"),
			["Lines[1].Quantity"] = ((object)typed.Lines[1], "Quantity"),
			["Lines[2]"] = (order, "Lines"),
			["ByCode[\"a.b\"].Quantity"] = ((object)typed.ByCode["a.b"], "Quantity"),
		};

		foreach (var error in errors)
		{
			// Act
			var resolved = RunTryResolveMember(
				assembly,
				"Testing.OrderSchema",
				order,
				error.FullPath,
				out var container,
				out var memberName
			);

			// Assert — round trip, by object identity.
			var (expectedContainer, expectedMember) = expected[error.FullPath.ToDottedString()];
			await Assert.That(resolved).IsTrue();
			await Assert
				.That(ReferenceEquals(container, expectedContainer))
				.IsTrue()
				.Because(error.FullPath.ToDottedString());
			await Assert.That(memberName).IsEqualTo(expectedMember);
		}

		await Assert.That(errors.Length).IsEqualTo(expected.Count);
	}

	[Test]
	public async Task TryResolveMember_GivenPathThatCannotBeFollowed_ResolvesTheNearestObject(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			NestedCollectionsSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var order = CreateInvalidOrder(assembly, out _);
		var missingElement = ZodPath.From(
			ZodPathSegment.Member("Lines"),
			ZodPathSegment.At(9),
			ZodPathSegment.Member("Quantity")
		);

		// Act
		var resolved = RunTryResolveMember(
			assembly,
			"Testing.OrderSchema",
			order,
			missingElement,
			out var container,
			out var member
		);
		var root = RunTryResolveMember(
			assembly,
			"Testing.OrderSchema",
			order,
			ZodPath.Root,
			out var rootContainer,
			out var rootMember
		);

		// Assert
		await Assert.That(resolved).IsFalse();
		await Assert.That(ReferenceEquals(container, order)).IsTrue();
		await Assert.That(member).IsEqualTo("Lines");
		await Assert.That(root).IsTrue();
		await Assert.That(ReferenceEquals(rootContainer, order)).IsTrue();
		await Assert.That(rootMember).IsEqualTo(string.Empty);
	}

	[Test]
	public async Task Registration_GivenGeneratedSchemas_RegistersUntypedValidatorsWithoutReflection(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			NestedCollectionsSource,
			ZodSourceGeneratorTestOptions.CompileWithDependencyInjection,
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		ZodSchemaFactory factory = new();
		var orderType = assembly.GetType("Testing.Order")!;

		// Act
		InvokeStatic<IZodSchemaFactory>(
			assembly,
			"ZodSharp.Generated.TestAssemblyZodSharpSchemas",
			"Register",
			factory
		);
		var found = factory.TryGetUntypedValidator(orderType, out var untyped);
		var errors = untyped!.Validate(CreateInvalidOrder(assembly, out _));

		// Assert — the generated adapter is the untyped validator itself, not a reflection-built wrapper.
		await Assert.That(found).IsTrue();
		await Assert.That(untyped.GetType().Name).IsEqualTo("OrderSchemaValidator");
		await Assert.That(untyped.ValidatedType).IsEqualTo(orderType);
		await Assert.That(untyped.IsValidatableMember("Number")).IsTrue();
		await Assert.That(errors.Length).IsEqualTo(5);
		await Assert.That(factory.TryGetUntypedValidator(assembly.GetType("Testing.Line")!, out _)).IsTrue();
	}

	[Test]
	public async Task Registration_GivenDependencyInjection_AddsADeferredFactoryRegistration(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			NestedCollectionsSource,
			ZodSourceGeneratorTestOptions.CompileWithDependencyInjection,
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		TestServiceCollection services = [];

		// Act
		InvokeStatic<Microsoft.Extensions.DependencyInjection.IServiceCollection>(
			assembly,
			"Microsoft.Extensions.DependencyInjection.TestAssemblyZodSharpServiceCollectionExtensions",
			"AddZodSharpTestAssemblySchemas",
			services
		);
		var registration = (ZodSchemaRegistration)
			services
				.Single(static descriptor => descriptor.ServiceType == typeof(ZodSchemaRegistration))
				.ImplementationInstance!;
		ZodSchemaFactory factory = new();
		registration.Register(factory);

		// Assert
		await Assert
			.That(services.Any(static descriptor => descriptor.ServiceType == typeof(IZodSchemaFactory)))
			.IsTrue();
		await Assert.That(factory.TryGetUntypedValidator(assembly.GetType("Testing.Order")!, out _)).IsTrue();
	}

	[Test]
	public async Task Registration_GivenSchemas_EmitsOneFactoryRegistrationPerAssembly(
		CancellationToken cancellationToken
	)
	{
		// Act
		using var result = await GenerateAsync(NestedCollectionsSource, cancellationToken);
		var generated = result.GetSource("ZodSharpAssemblyRegistration")!;

		// Assert
		await Assert.That(generated).ContainsGeneratedCode("public static partial class TestAssemblyZodSharpSchemas");
		await Assert
			.That(generated)
			.ContainsGeneratedCode(
				"factory.Register<global::Testing.Order>(new global::Testing.OrderSchemaValidator());"
			);
	}

	[Test]
	[Arguments("Contoso.Orders.Api", "ContosoOrdersApi")]
	[Arguments("my-app", "MyApp")]
	[Arguments("1st.assembly", "_1stAssembly")]
	[Arguments("", "Assembly")]
	public async Task GetAssemblyIdentifier_GivenAssemblyName_ReturnsPascalCaseIdentifier(
		string assemblyName,
		string expected
	)
	{
		// Act
		var identifier = ZodSchemaGenerator.GetAssemblyIdentifier(assemblyName);

		// Assert
		await Assert.That(identifier).IsEqualTo(expected);
	}

	sealed class TestServiceCollection
		: List<Microsoft.Extensions.DependencyInjection.ServiceDescriptor>,
			Microsoft.Extensions.DependencyInjection.IServiceCollection;
}
