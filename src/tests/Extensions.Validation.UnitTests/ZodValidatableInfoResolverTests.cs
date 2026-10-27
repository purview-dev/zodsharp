using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Validation;
using ZodSharp.Core;
using ZodSharp.Extensions.Validation.Fixtures;
using static ZodSharp.Extensions.Validation.Fixtures.TestHelpers;

namespace ZodSharp.Extensions.Validation;

public class ZodValidatableInfoResolverTests
{
	[Test]
	public async Task TryGetValidatableTypeInfo_GivenRegisteredType_ReturnsZodTypeInfo()
	{
		// Arrange
		ZodSchemaFactory factory = new();
		factory.Register(new ScriptedValidator<Order>());
		ZodValidatableInfoResolver resolver = new(factory);

		// Act
		var found = resolver.TryGetValidatableTypeInfo(typeof(Order), out var info);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(info).IsTypeOf<ZodValidatableTypeInfo>();
		await Assert.That(((ZodValidatableTypeInfo)info!).ValidatedType).IsEqualTo(typeof(Order));
	}

	[Test]
	public async Task TryGetValidatableTypeInfo_GivenUnregisteredType_ReturnsFalse()
	{
		// Arrange
		ZodValidatableInfoResolver resolver = new(new ZodSchemaFactory());

		// Act
		var found = resolver.TryGetValidatableTypeInfo(typeof(Order), out var info);

		// Assert
		await Assert.That(found).IsFalse();
		await Assert.That(info).IsNull();
	}

	[Test]
	public async Task TryGetValidatableTypeInfo_GivenRepeatedLookups_ReturnsCachedInstance()
	{
		// Arrange
		ZodSchemaFactory factory = new();
		factory.Register(new ScriptedValidator<Order>());
		ZodValidatableInfoResolver resolver = new(factory);

		// Act
		resolver.TryGetValidatableTypeInfo(typeof(Order), out var first);
		resolver.TryGetValidatableTypeInfo(typeof(Order), out var second);

		// Assert
		await Assert.That(second).IsSameReferenceAs(first);
	}

	[Test]
	public async Task TryGetValidatableTypeInfo_GivenRegistrationAfterNegativeLookup_KeepsNegativeResult()
	{
		// Arrange: registering into the factory after the first validation is documented as unsupported.
		ZodSchemaFactory factory = new();
		ZodValidatableInfoResolver resolver = new(factory);
		resolver.TryGetValidatableTypeInfo(typeof(Order), out _);
		factory.Register(new ScriptedValidator<Order>());

		// Act
		var found = resolver.TryGetValidatableTypeInfo(typeof(Order), out _);

		// Assert
		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TryGetValidatableTypeInfo_GivenNullableValueType_ResolvesUnderlyingType()
	{
		// Arrange
		ZodSchemaFactory factory = new();
		factory.Register(new ScriptedValidator<Point>());
		ZodValidatableInfoResolver resolver = new(factory);

		// Act
		var found = resolver.TryGetValidatableTypeInfo(typeof(Point?), out var info);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(((ZodValidatableTypeInfo)info!).ValidatedType).IsEqualTo(typeof(Point));
	}

	[Test]
	[Arguments(typeof(int))]
	[Arguments(typeof(string))]
	[Arguments(typeof(Guid))]
	[Arguments(typeof(DateTimeOffset))]
	public async Task TryGetValidatableTypeInfo_GivenPrimitiveTypeEvenWhenRegistered_ReturnsFalse(Type type)
	{
		// Arrange
		ZodSchemaFactory factory = new();
		factory.Register(new ScriptedValidator<int>());
		factory.Register(new ScriptedValidator<string>());
		ZodValidatableInfoResolver resolver = new(factory);

		// Act
		var found = resolver.TryGetValidatableTypeInfo(type, out _);

		// Assert
		await Assert.That(found).IsFalse();
	}

	[Test]
	public async Task TryGetValidatableTypeInfo_GivenSource_PrefersSourceOverFactory()
	{
		// Arrange
		ZodSchemaFactory factory = new();
		factory.Register(new ScriptedValidator<Order>());
		ZodUntypedValidator<Order> fromSource = new(new ScriptedValidator<Order>());
		ZodValidationOptions options = new();
		options.Sources.Add(new SingleSource(typeof(Order), fromSource));
		ZodValidatableInfoResolver resolver = new(factory, options);

		// Act
		resolver.TryGetValidatableTypeInfo(typeof(Order), out var info);

		// Assert
		await Assert.That(((ZodValidatableTypeInfo)info!).Validator).IsSameReferenceAs(fromSource);
	}

	[Test]
	public async Task TryGetValidatableTypeInfo_GivenNoFactory_UsesSourcesOnly()
	{
		// Arrange
		ZodValidationOptions options = new();
		options.Sources.Add(
			new SingleSource(typeof(Order), new ZodUntypedValidator<Order>(new ScriptedValidator<Order>()))
		);
		ZodValidatableInfoResolver resolver = new(null, options);

		// Act
		var found = resolver.TryGetValidatableTypeInfo(typeof(Order), out _);

		// Assert
		await Assert.That(found).IsTrue();
	}

	[Test]
	public async Task TryGetValidatableParameterInfo_GivenComplexParameter_ReturnsParameterInfo()
	{
		// Arrange
		var resolver = CreateOrderResolver();

		// Act
		var found = resolver.TryGetValidatableParameterInfo(Parameter("order"), out var info);

		// Assert
		await Assert.That(found).IsTrue();
		var parameter = (ZodValidatableParameterInfo)info!;
		await Assert.That(parameter.Name).IsEqualTo("order");
		await Assert.That(parameter.IsCollection).IsFalse();
		await Assert.That(parameter.AllowsNull).IsFalse();
	}

	[Test]
	public async Task TryGetValidatableParameterInfo_GivenCollectionOfRegisteredElements_ReturnsCollectionInfo()
	{
		// Arrange
		var resolver = CreateOrderResolver();

		// Act
		var found = resolver.TryGetValidatableParameterInfo(Parameter("orders"), out var info);

		// Assert
		await Assert.That(found).IsTrue();
		await Assert.That(((ZodValidatableParameterInfo)info!).IsCollection).IsTrue();
	}

	[Test]
	public async Task TryGetValidatableParameterInfo_GivenNullableOrOptionalParameter_AllowsNull()
	{
		// Arrange
		var resolver = CreateOrderResolver();

		// Act
		resolver.TryGetValidatableParameterInfo(Parameter("optional"), out var optional);
		resolver.TryGetValidatableParameterInfo(Parameter("nullable"), out var nullable);

		// Assert
		await Assert.That(((ZodValidatableParameterInfo)optional!).AllowsNull).IsTrue();
		await Assert.That(((ZodValidatableParameterInfo)nullable!).AllowsNull).IsTrue();
	}

	[Test]
	[Arguments("skipped")]
	[Arguments("service")]
	[Arguments("count")]
	[Arguments("unregistered")]
	public async Task TryGetValidatableParameterInfo_GivenExcludedParameter_ReturnsFalse(string name)
	{
		// Arrange
		var resolver = CreateOrderResolver();

		// Act
		var found = resolver.TryGetValidatableParameterInfo(Parameter(name), out _);

		// Assert
		await Assert.That(found).IsFalse();
	}

	static ZodValidatableInfoResolver CreateOrderResolver()
	{
		ZodSchemaFactory factory = new();
		factory.Register(new ScriptedValidator<Order>());
		return new(factory);
	}

	static ParameterInfo Parameter(string name) =>
		typeof(ZodValidatableInfoResolverTests)
			.GetMethod(nameof(Handler), BindingFlags.NonPublic | BindingFlags.Static)!
			.GetParameters()
			.Single(p => p.Name == name);

	[SuppressMessage("Usage", "IDE0060:Remove unused parameter", Justification = "Parameter metadata fixture.")]
	[SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "Read by reflection.")]
	static void Handler(
		Order order,
		List<Order> orders,
		[SkipValidation] Order skipped,
		[FromServices] Order service,
		int count,
		Customer unregistered,
		Order? nullable,
		Order? optional = null
	) { }

	sealed class SingleSource(Type type, IZodUntypedValidator validator) : IZodValidatableInfoSource
	{
		public bool TryGetValidator(Type requested, out IZodUntypedValidator? found)
		{
			found = requested == type ? validator : null;
			return found is not null;
		}
	}

	/// <summary>Stands in for ASP.NET Core's attribute, which this package detects by name.</summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	sealed class FromServicesAttribute : Attribute;
}
