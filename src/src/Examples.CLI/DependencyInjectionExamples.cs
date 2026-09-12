using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp.Examples.CLI;

/// <summary>
/// Advanced <c>IServiceProvider</c> examples: composes source-generated validators
/// (discovered via <see cref="ZodSchemaGeneratedAttribute"/>) with hand-built
/// <see cref="ZodObject"/> schemas in a single DI container, then consumes them
/// through <see cref="IZodSchemaFactory"/> resolved from the provider.
/// </summary>
static class DependencyInjectionExamples
{
	public static void RunAll()
	{
		Console.WriteLine("=== Dependency Injection / IServiceProvider Examples ===\n");

		BasicFactoryExample();
		ServiceProviderWithHandBuiltSchemaExample();
		ConsumerServiceInjectionExample();
		ScopedValidationExample();
		MixedResolutionExample();
	}

	static void BasicFactoryExample()
	{
		Console.WriteLine("--- Basic Factory with Source-Generated Validator ---");

		// The source generator emitted [assembly: ZodSchemaGenerated(typeof(User))]
		// and a UserSchemaValidator adapter. RegisterFromAssembly discovers both.
		ZodSchemaFactory factory = new();
		factory.RegisterFromAssembly(typeof(User).Assembly);

		Console.WriteLine($"User registered: {factory.IsRegistered<User>()}");

		var result = factory.Validate(
			new User
			{
				Name = "Alice",
				Age = 30,
				Email = "alice@example.com",
			}
		);
		Console.WriteLine($"Source-gen'd User validation: {result.IsSuccess}");

		Console.WriteLine();
	}

	static void ServiceProviderWithHandBuiltSchemaExample()
	{
		Console.WriteLine("--- IServiceProvider: Hand-Built (Non-Source-Gen'd) Schema ---");

		// Product has no [ZodSchema] attribute, so nothing is generated for it.
		// Build the schema by hand with the fluent Z.Object() API, then wrap it
		// in a ZodSchemaValidator<T> so it satisfies IZodSchemaValidator<T> for DI.
		var productSchema = Z.Object()
			.Field("sku", Z.String().Min(1).Max(20))
			.Field("price", Z.Number().Min(0))
			.Field("stock", Z.Number().Min(0).Int())
			.Build();

		ServiceCollection services = new();
		services.AddSingleton<IZodSchemaFactory>(sp =>
		{
			ZodSchemaFactory factory = new();
			// Source-gen'd: auto-discovered from the assembly attribute.
			factory.RegisterFromAssembly(typeof(User).Assembly);
			// Non-source-gen'd: wrap the hand-built ZodObject in a validator adapter.
			factory.Register(new ZodSchemaValidator<Dictionary<string, object?>>(productSchema));
			return factory;
		});

		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IZodSchemaFactory>();

		Console.WriteLine(
			$"User registered: {resolved.IsRegistered<User>()}, "
				+ $"Product (Dictionary) registered: {resolved.IsRegistered<Dictionary<string, object?>>()}"
		);

		Console.WriteLine();
	}

	static void ConsumerServiceInjectionExample()
	{
		Console.WriteLine("--- Consumer Service with Constructor Injection ---");

		var productSchema = BuildProductSchema();
		ServiceCollection services = new();

		// Register the factory as a singleton, wiring both validator kinds once.
		services.AddSingleton<IZodSchemaFactory>(sp =>
		{
			ZodSchemaFactory factory = new();
			factory.RegisterFromAssembly(typeof(User).Assembly);
			factory.Register(new ZodSchemaValidator<Dictionary<string, object?>>(productSchema));
			return factory;
		});

		// A consumer service that depends only on the factory abstraction -
		// it never knows whether a given type was source-gen'd or hand-built.
		services.AddSingleton<OrderService>();

		using var provider = services.BuildServiceProvider();
		var orderService = provider.GetRequiredService<OrderService>();

		var userResult = orderService.ValidateUser(
			new User
			{
				Name = "Bob",
				Age = 42,
				Email = "bob@example.com",
			}
		);
		Console.WriteLine($"OrderService.ValidateUser: {userResult.IsSuccess}");

		var productResult = orderService.ValidateProduct(
			new Dictionary<string, object?>
			{
				{ "sku", "WIDGET-01" },
				{ "price", 9.99 },
				{ "stock", 100.0 },
			}
		);
		Console.WriteLine($"OrderService.ValidateProduct: {productResult.IsSuccess}");

		Console.WriteLine();
	}

	static void ScopedValidationExample()
	{
		Console.WriteLine("--- Scoped Validation with Failure Path ---");

		var productSchema = BuildProductSchema();
		ServiceCollection services = new();
		services.AddSingleton<IZodSchemaFactory>(sp =>
		{
			ZodSchemaFactory factory = new();
			factory.RegisterFromAssembly(typeof(User).Assembly);
			factory.Register(new ZodSchemaValidator<Dictionary<string, object?>>(productSchema));
			return factory;
		});

		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Intentionally invalid: missing sku, negative price, fractional stock.
		Dictionary<string, object?> badProduct = new()
		{
			{ "sku", "" },
			{ "price", -5.0 },
			{ "stock", 3.5 },
		};
		var result = factory.Validate(badProduct);
		Console.WriteLine($"Invalid product validation: {result.IsSuccess}");
		foreach (var error in result.Errors)
			Console.WriteLine($"  - {string.Join(".", error.Path)}: {error.Message}");

		Console.WriteLine();
	}

	static void MixedResolutionExample()
	{
		Console.WriteLine("--- Mixed Resolution: Validate + Parse ---");

		var productSchema = BuildProductSchema();
		ServiceCollection services = new();
		services.AddSingleton<IZodSchemaFactory>(sp =>
		{
			ZodSchemaFactory factory = new();
			factory.RegisterFromAssembly(typeof(User).Assembly);
			factory.Register(new ZodSchemaValidator<Dictionary<string, object?>>(productSchema));
			return factory;
		});

		using var provider = services.BuildServiceProvider();
		var factory = provider.GetRequiredService<IZodSchemaFactory>();

		// Non-generic resolve path (IZodSchemaValidator) via the marker interface.
		if (factory.Resolve<User>() is { } userValidator)
		{
			var ok = userValidator.Validate(new User { Name = "Carol", Age = 25 });
			Console.WriteLine($"Resolved user validator via Resolve<User>(): {ok.IsSuccess}");
		}

		// ResolveRequired throws if no validator is registered.
		try
		{
			factory.ResolveRequired<Product>();
		}
		catch (InvalidOperationException ex)
		{
			Console.WriteLine($"ResolveRequired<Product> threw (expected): {ex.Message}");
		}

		// Validate<T> is the convenience entry point on the factory itself.
		var productResult = factory.Validate(
			new Dictionary<string, object?>
			{
				{ "sku", "SKU-1" },
				{ "price", 1.0 },
				{ "stock", 1.0 },
			}
		);
		Console.WriteLine($"factory.Validate<Dictionary> (hand-built): {productResult.IsSuccess}");

		Console.WriteLine();
	}

	static ZodObject BuildProductSchema() =>
		Z.Object()
			.Field("sku", Z.String().Min(1).Max(20))
			.Field("price", Z.Number().Min(0))
			.Field("stock", Z.Number().Min(0).Int())
			.Build();
}

/// <summary>
/// Example consumer service that validates both source-gen'd (<see cref="User"/>)
/// and hand-built (Product as <see cref="Dictionary{TKey, TValue}"/>) payloads
/// through a single injected <see cref="IZodSchemaFactory"/>.
/// </summary>
sealed class OrderService(IZodSchemaFactory schemaFactory)
{
	readonly IZodSchemaFactory _schemaFactory = schemaFactory;

	public ValidationResult<User> ValidateUser(User user) => _schemaFactory.Validate(user);

	public ValidationResult<Dictionary<string, object?>> ValidateProduct(Dictionary<string, object?> product) =>
		_schemaFactory.Validate(product);
}
