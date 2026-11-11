using ZodSharp.Core;
using ZodSharp.JsonSchema;
using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// A generated schema validates inherited members too: a derived type is the value the caller passes, and
/// validation, paths, field resolution, JSON Schema export and closed-hierarchy dispatch all read the same
/// member list. A property the derived type overrides or hides is taken once, from the most derived
/// declaration.
/// </summary>
partial class ZodSchemaGeneratorTests
{
	[Test]
	public async Task Validate_GivenInheritedRequiredProperty_ReportsTheBaseMember(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public class BaseEntity
				{
					[Required]
					public string? Id { get; set; }
				}

				[ZodSchema]
				public sealed class Order : BaseEntity
				{
					[Range(1, 10)]
					public int Quantity { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var order = Create(assembly, "Testing.Order", ("Id", null), ("Quantity", 5));

		// Act
		var errors = RunValidate(assembly, "Testing.OrderSchema", order);
		var memberErrors = RunValidateMember(assembly, "Testing.OrderSchema", order, "Id");
		var resolved = RunTryResolveMember(
			assembly,
			"Testing.OrderSchema",
			order,
			ZodPath.From(ZodPathSegment.Member("Id")),
			out var container,
			out var memberName
		);

		// Assert — the base member is validated on the derived value, under its own name; the member and
		// resolution APIs see it too, so a host's field-level validation and error placement work for an
		// inherited field.
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("missing_field");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Id");
		await Assert.That(memberErrors.Length).IsEqualTo(1);
		await Assert.That(memberErrors[0].FullPath.ToDottedString()).IsEqualTo("Id");
		await Assert.That(resolved).IsTrue();
		await Assert.That(ReferenceEquals(container, order)).IsTrue();
		await Assert.That(memberName).IsEqualTo("Id");
	}

	[Test]
	public async Task JsonSchema_GivenInheritedProperty_ExportsItOnTheDerivedSchema(CancellationToken cancellationToken)
	{
		// Arrange — the base type does not need its own [ZodSchema]: the derived schema validates the value
		// and describes the members it reads.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using System.Text.Json.Serialization;
			using ZodSharp;

			namespace Testing
			{
				public class BaseEntity
				{
					[Required]
					[JsonPropertyName("entity_id")]
					public string? Id { get; set; }
				}

				[ZodSchema]
				public sealed class Order : BaseEntity
				{
					public int Quantity { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var schemaType = assembly.GetType("Testing.OrderSchema")!;

		// Act
		var definition = (JsonSchemaDefinition)schemaType.GetMethod("BuildJsonSchema")!.Invoke(null, null)!;

		// Assert — an inherited property's attributes (here [JsonPropertyName]) are read from the base
		// declaration, so its wire name and requiredness match a declared member.
		await Assert.That(definition.Properties!.ContainsKey("entity_id")).IsTrue();
		await Assert.That(definition.Properties!.ContainsKey("quantity")).IsTrue();
		await Assert.That(definition.Properties!["entity_id"].Type).IsEqualTo("string");
		await Assert.That(definition.Required!).Contains("entity_id");
		await Assert.That(definition.Required!).Contains("quantity");
	}

	[Test]
	public async Task Validate_GivenOverriddenBaseProperty_ValidatesItOnce(CancellationToken cancellationToken)
	{
		// Arrange — the override is the member `value.Name` binds, so the base declaration must not add a
		// second rule (and must not be validated under the base property's attributes).
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public class BaseEntity
				{
					public virtual string? Name { get; set; }
				}

				[ZodSchema]
				public sealed class Person : BaseEntity
				{
					[MinLength(3)]
					public override string? Name { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var person = Create(assembly, "Testing.Person", ("Name", "ab"));

		// Act
		var errors = RunValidate(assembly, "Testing.PersonSchema", person);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("too_small");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Name");
	}

	[Test]
	public async Task Validate_GivenHiddenBaseProperty_UsesTheDerivedPropertyOnly(CancellationToken cancellationToken)
	{
		// Arrange — the base member's rule must not be applied to the new member that hides it.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public class BaseEntity
				{
					[MinLength(10)]
					public string? Name { get; set; }
				}

				[ZodSchema]
				public sealed class Alias : BaseEntity
				{
					public new string? Name { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var alias = Create(assembly, "Testing.Alias", ("Name", "short"));

		// Act
		var errors = RunValidate(assembly, "Testing.AliasSchema", alias);

		// Assert
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Validate_GivenGenericBaseProperty_UsesTheSubstitutedType(CancellationToken cancellationToken)
	{
		// Arrange — BaseValue<string> exposes a string Value, so the string rule applies.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public class BaseValue<T>
				{
					[MinLength(3)]
					public T Value { get; set; } = default!;
				}

				[ZodSchema]
				public sealed class TextValue : BaseValue<string>
				{
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var value = Create(assembly, "Testing.TextValue", ("Value", "ab"));

		// Act
		var errors = RunValidate(assembly, "Testing.TextValueSchema", value);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("too_small");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Value");
	}

	[Test]
	public async Task Validate_GivenInheritedNestedProperty_GeneratesAndValidatesTheNestedSchema(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the nested type has no [ZodSchema]; the inherited property must make the generator queue
		// and emit its secondary schema, just as a declared property would.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public class BaseEnvelope
				{
					public Address Address { get; set; } = new();
				}

				public class Address
				{
					[Required]
					public string? City { get; set; }
				}

				[ZodSchema]
				public sealed class Envelope : BaseEnvelope
				{
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var address = Create(assembly, "Testing.Address", ("City", null));
		var envelope = Create(assembly, "Testing.Envelope", ("Address", address));

		// Act
		var errors = RunValidate(assembly, "Testing.EnvelopeSchema", envelope);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].Code).IsEqualTo("missing_field");
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("Address.City");
	}

	[Test]
	public async Task Validate_GivenCompareToInheritedProperty_ComparesAgainstTheBaseMember(
		CancellationToken cancellationToken
	)
	{
		// Arrange — the referenced property is inherited, so the Compare target search has to follow the base
		// chain; the generated code reads value.Password from the base type.
		const string source = """
			using System.ComponentModel.DataAnnotations;
			using ZodSharp;

			namespace Testing
			{
				public class Credentials
				{
					public string? Password { get; set; }
				}

				[ZodSchema]
				public sealed class Signup : Credentials
				{
					[Compare(nameof(Password))]
					public string? ConfirmPassword { get; set; }
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var signup = Create(assembly, "Testing.Signup", ("Password", "secret"), ("ConfirmPassword", "different"));

		// Act
		var errors = RunValidate(assembly, "Testing.SignupSchema", signup);

		// Assert
		await Assert.That(errors.Length).IsEqualTo(1);
		await Assert.That(errors[0].FullPath.ToDottedString()).IsEqualTo("ConfirmPassword");
	}
}
