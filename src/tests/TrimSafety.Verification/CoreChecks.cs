using ZodSharp.Core;

namespace ZodSharp.TrimSafety;

/// <summary>Runtime schemas, discriminated unions and generated registration in Purview.ZodSharp.</summary>
static class CoreChecks
{
	public static void RuntimeSchemas(Harness h)
	{
		var email = Z.String().Min(3).Email();
		h.Expect(email.Validate("someone@example.com").IsSuccess, "a valid email passes");
		h.Expect(!email.Validate("x").IsSuccess, "an invalid email fails");

		var person = Z.Object().Field("name", Z.String().Min(2)).Field("age", Z.Number().Min(0).Int()).Build();
		var valid = person.Validate(new Dictionary<string, object?> { ["name"] = "Ada", ["age"] = 36d });
		h.Expect(valid.IsSuccess, "a valid object passes");

		var invalid = person.Validate(new Dictionary<string, object?> { ["name"] = "A", ["age"] = -1d });
		h.ExpectPaths(invalid.Errors, ["name", "age"], "an invalid object reports each field");

		var pets = Z.DiscriminatedUnion("kind")
			.Option("cat", Z.Object().Field("kind", Z.String()).Field("lives", Z.Number().Max(9)).Build())
			.Option("dog", Z.Object().Field("kind", Z.String()).Field("bark", Z.String().Min(1)).Build())
			.Build();
		h.Expect(
			pets.Validate(new Dictionary<string, object?> { ["kind"] = "cat", ["lives"] = 9d }).IsSuccess,
			"a discriminated union over a dictionary selects its option"
		);
		h.Expect(
			!pets.Validate(new Dictionary<string, object?> { ["kind"] = "cow" }).IsSuccess,
			"an unknown discriminator fails"
		);

		var stringOrNumber = Z.String().Min(2).Or(Z.Number().Min(0));
		h.Expect(stringOrNumber.Validate("ab").IsSuccess, "a typed union accepts its first option");
		h.Expect(stringOrNumber.Validate(3d).IsSuccess, "a typed union accepts its second option");
		h.Expect(!stringOrNumber.Validate("a").IsSuccess, "a typed union rejects a value no option accepts");

		var native = Z.NativeUnion(Z.String(), Z.Number());
		h.Expect(native.Validate("x").IsSuccess, "a native union accepts an option");

		var compiled = ZodSharp.Expressions.CompiledValidator.Compile(email);
		h.Expect(compiled("someone@example.com").IsSuccess, "CompiledValidator binds the schema without codegen");
	}

	public static void PocoDiscriminatedUnion(Harness h)
	{
		var shapes = Z.DiscriminatedUnion("kind")
			.Option("circle", new CircleShapeSchemaValidator())
			.Option("square", new SquareShapeSchemaValidator())
			.Build();

		h.Expect(shapes.Validate(new CircleShape { Radius = 3 }).IsSuccess, "Option<T> reads a POCO discriminator");
		h.ExpectPaths(
			shapes.Validate(new SquareShape { Side = 0 }).Errors,
			["Side"],
			"the POCO is validated by its option's schema"
		);

		// A POCO of a type the union was not told about needs the reflection fallback, which trimming removes
		// (ZodSharp.Reflection.IsSupported is a feature guard): published, the value is reported as missing its
		// discriminator rather than read through metadata that may be gone.
		var untyped = Z.DiscriminatedUnion("kind").Option("circle", new AcceptAll()).Build();
		var result = untyped.Validate(new UnregisteredShape());

		// A native image compiles nothing at run time (dotnet run of a PublishAot project reports dynamic code as
		// unsupported too, so RuntimeFeature cannot tell the two apart).
		var isNative = System.Runtime.JitInfo.GetCompiledMethodCount() == 0;
		h.Expect(
			!isNative ? result.IsSuccess : !result.IsSuccess,
			!isNative
				? "untrimmed, the reflection fallback reads an unregistered POCO's discriminator"
				: "trimmed, the reflection fallback is removed and the union fails closed"
		);
	}

	public static void Registration(Harness h)
	{
		ZodSchemaFactory generated = new();
		ZodSharp.Generated.ZodSharpTrimSafetyVerificationZodSharpSchemas.Register(generated);
		h.Expect(generated.Resolve<Order>() is not null, "the generated Register names every validator");

		ZodSchemaFactory scanned = new();
		scanned.RegisterFromAssembly(typeof(Order).Assembly);
		h.Expect(
			scanned.Resolve<Order>() is not null && scanned.TryGetUntypedValidator(typeof(Address), out _),
			"RegisterFromAssembly reads [ZodSchemaGenerated] metadata, which the trimmer keeps"
		);
	}
}
