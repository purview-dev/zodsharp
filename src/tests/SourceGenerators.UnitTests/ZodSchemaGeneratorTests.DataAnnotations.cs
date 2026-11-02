using System.Reflection;
using ZodSharp.SourceGenerators.Infra;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGeneratorTests
{
	static readonly int[] SingleInventoryValue = [1];

	[Test]
	public async Task GeneratedValidate_GivenUserWithValidData_ReturnsSuccess(CancellationToken cancellationToken)
	{
		using var driverResult = await GenerateAsync(
			UserSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var user = CreateUser(assembly, "John Doe", 30, "john@example.com");

		var result = InvokeValidate(assembly, user);

		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsTrue();
	}

	[Test]
	[Arguments(null, 30, "john@example.com", "Name")]
	[Arguments("AB", 30, "john@example.com", "Name")]
	[Arguments(
		"This name is far too long for the generated StringLength validation rule",
		30,
		"john@example.com",
		"Name"
	)]
	[Arguments("John Doe", -1, "john@example.com", "Age")]
	[Arguments("John Doe", 121, "john@example.com", "Age")]
	[Arguments("John Doe", 30, "not-an-email", "Email")]
	public async Task GeneratedValidate_GivenInvalidUser_ReturnsFailureWithExpectedPath(
		string? name,
		int age,
		string? email,
		string expectedPath,
		CancellationToken cancellationToken
	)
	{
		using var driverResult = await GenerateAsync(
			UserSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var user = CreateUser(assembly, name, age, email);

		var result = InvokeValidate(assembly, user);
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors.Any(error => error.Path.Contains(expectedPath))).IsTrue();
	}

	[Test]
	public async Task GeneratedParse_GivenInvalidUser_ThrowsZodException(CancellationToken cancellationToken)
	{
		using var driverResult = await GenerateAsync(
			UserSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var user = CreateUser(assembly, "AB", 30, "john@example.com");
		var schemaType = assembly.GetType("Testing.UserSchema")!;

		var exception = Assert.Throws<TargetInvocationException>(() =>
			schemaType.GetMethod("Parse")!.Invoke(null, [user])
		);

		await Assert.That(exception).IsNotNull();
		await Assert.That(exception!.InnerException).IsTypeOf<Core.ZodException>();
	}

	[Test]
	public async Task Generate_GivenDataAnnotations_GeneratedSourceContainsExpectedValidationChecks(
		CancellationToken cancellationToken
	)
	{
		using var driverResult = await GenerateAsync(UserSource, cancellationToken);

		var generatedSource = driverResult.GetSource("UserSchema");

		await Assert.That(generatedSource).ContainsGeneratedCode("string.IsNullOrWhiteSpace(value.Name)");
		await Assert.That(generatedSource).ContainsGeneratedCode("var nameValue = value.Name;");
		await Assert.That(generatedSource).ContainsGeneratedCode("var nameLength = nameValue.Length;");
		await Assert.That(generatedSource).ContainsGeneratedCode("nameLength < 3");
		await Assert.That(generatedSource).ContainsGeneratedCode("nameLength > 50");
		await Assert.That(generatedSource).ContainsGeneratedCode("static readonly int RangeMinimum_Age = 0;");
		await Assert.That(generatedSource).ContainsGeneratedCode("static readonly int RangeMaximum_Age = 120;");
		await Assert.That(generatedSource).ContainsGeneratedCode("ageValue < RangeMinimum_Age");
		await Assert.That(generatedSource).ContainsGeneratedCode("EmailRegex.IsMatch(emailValue)");
	}

	[Test]
	public async Task Generate_GivenCollectionLengthAttributes_UsesEfficientAccessors(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class LengthExamples
	{
		[Length(2, 5)]
		public string? Name { get; set; }

		[Length(2, 5)]
		public int[]? Values { get; set; }

		[Length(2, 5)]
		public List<int>? Items { get; set; }

		[Length(2, 5)]
		public IEnumerable<int>? Sequence { get; set; }

		[Length(2, 5)]
		public IEnumerable? UntypedSequence { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(source, cancellationToken);

		var generatedSource = driverResult.GetSource("LengthExamplesSchema");

		await Assert.That(generatedSource).ContainsGeneratedCode("propertyValue.Length");
		await Assert.That(generatedSource).ContainsGeneratedCode("propertyValue.Count");
		await Assert.That(generatedSource).ContainsGeneratedCode("CollectionCountHelper.GetCount(propertyValue)");
		await Assert.That(generatedSource).ContainsGeneratedCode("else if");
	}

	[Test]
	public async Task Generate_GivenAdditionalDataAnnotations_UsesExpectedGeneratedShapes(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System;
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class AttributeExamples
	{
		[RegularExpression(""^[A-Z]{2}$"")]
		public string? CountryCode { get; set; }

		[AllowedValues(""open"", ""closed"")]
		public string? Status { get; set; }

		[DeniedValues(13, 99)]
		public int Code { get; set; }

		[Range(typeof(decimal), ""1.5"", ""3.5"")]
		public decimal Price { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generatedSource = driverResult.GetSource("AttributeExamplesSchema");

		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode("static readonly global::System.Text.RegularExpressions.Regex Regex_CountryCode");
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode("EqualityComparer<string?>.Default.Equals(statusValue, \"open\")");
		await Assert.That(generatedSource).ContainsGeneratedCode("EqualityComparer<int>.Default.Equals(codeValue, 13)");
		await Assert.That(generatedSource).ContainsGeneratedCode("static readonly decimal RangeMinimum_Price");
		await Assert.That(generatedSource).ContainsGeneratedCode("Decimal.Parse(\"1.5\"");
	}

	[Test]
	public async Task Generate_GivenRegularExpressionWithBackslashes_EscapesEmittedStringLiteral(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class BackslashPatternModel
	{
		[RegularExpression(@""^[\w\-.]+$"")]
		public string? Identifier { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var generatedSource = driverResult.GetSource("BackslashPatternModelSchema");

		// The emitted Regex carries a match timeout: the pattern is author-supplied but runs against
		// untrusted request input, and RegularExpressionAttribute (which this mirrors) defaults to one.
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode(
				@"new(""^[\\w\\-.]+$"", global::System.Text.RegularExpressions.RegexOptions.CultureInvariant, global::System.TimeSpan.FromMilliseconds(2000))"
			);
	}

	[Test]
	public async Task GeneratedValidate_GivenDisplayAndResourceBackedDataAnnotations_UsesResolvedMessages(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	public static class ValidationMessages
	{
		public static string PasswordTooShort => ""{0} must be at least {2} characters long."";
		public static string PasswordPattern => ""{0} must include a symbol."";
	}

	[ZodSchema]
	public sealed class ResourceModel
	{
		[Display(Name = ""Password field"")]
		[StringLength(12, MinimumLength = 5, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PasswordTooShort))]
		[RegularExpression(@""^.*[!@#$%^&*()].*$"", ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.PasswordPattern))]
		public string? Password { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.ResourceModel")!;
		var model = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Password")!.SetValue(model, "abcd");

		var result = InvokeValidate(assembly, model, "Testing.ResourceModelSchema");
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert
			.That(errors.Any(static error => error.Message == "Password field must be at least 5 characters long."))
			.IsTrue();

		modelType.GetProperty("Password")!.SetValue(model, "abcde");
		result = InvokeValidate(assembly, model, "Testing.ResourceModelSchema");
		errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert
			.That(errors.Any(static error => error.Message == "Password field must include a symbol."))
			.IsTrue();
	}

	[Test]
	public async Task GeneratedValidate_GivenLengthAnnotatedCollections_ProducesStructuredSizeIssues(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class Inventory
	{
		[Length(2, 3)]
		public int[]? Values { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var inventoryType = assembly.GetType("Testing.Inventory")!;
		var inventory = Activator.CreateInstance(inventoryType)!;
		inventoryType.GetProperty("Values")!.SetValue(inventory, SingleInventoryValue);

		var result = InvokeValidate(assembly, inventory, "Testing.InventorySchema");
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors).HasSingleItem();
		await Assert.That(errors[0].Code).IsEqualTo("too_small");
		await Assert.That(errors[0].Origin).IsEqualTo("array");
		await Assert.That(errors[0].Minimum).IsEqualTo(2);
		await Assert.That(errors[0].Inclusive).IsTrue();
	}

	[Test]
	public async Task GeneratedValidate_GivenAdditionalDataAnnotations_ProducesExpectedValidationResults(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class AttributeRuntimeModel
	{
		[RegularExpression(""^[A-Z]{2}$"")]
		public string? CountryCode { get; set; }

		[AllowedValues(""open"", ""closed"")]
		public string? Status { get; set; }

		[DeniedValues(13, 99)]
		public int Code { get; set; }

		[Range(typeof(decimal), ""1.5"", ""3.5"")]
		public decimal Price { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.AttributeRuntimeModel")!;
		var model = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("CountryCode")!.SetValue(model, string.Empty);
		modelType.GetProperty("Status")!.SetValue(model, null);
		modelType.GetProperty("Code")!.SetValue(model, 13);
		modelType.GetProperty("Price")!.SetValue(model, 4.0m);

		var result = InvokeValidate(assembly, model, "Testing.AttributeRuntimeModelSchema");
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert
			.That(errors.Any(static error => error.Path.Contains("Status") && error.Code == "invalid_value"))
			.IsTrue();
		await Assert
			.That(errors.Any(static error => error.Path.Contains("Code") && error.Code == "invalid_value"))
			.IsTrue();
		await Assert
			.That(errors.Any(static error => error.Path.Contains("Price") && error.Code == "invalid_range"))
			.IsTrue();
		await Assert.That(errors.Any(static error => error.Path.Contains("CountryCode"))).IsFalse();
	}

	[Test]
	public async Task GeneratedValidate_GivenEverySupportedSourceGeneratorAttribute_ProducesExpectedFailures(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class FullAttributeCoverageModel
	{
		[Required]
		public string? Name { get; set; }

		[StringLength(5, MinimumLength = 2)]
		public string Value { get; set; } = string.Empty;

		[MinLength(2)]
		public string MinOnly { get; set; } = string.Empty;

		[MaxLength(3)]
		public string MaxOnly { get; set; } = string.Empty;

		[Length(1, 2)]
		public int[]? Items { get; set; }

		[EmailAddress]
		public string? Email { get; set; }

		[RegularExpression(""^[A-Z]{2}$"")]
		public string? CountryCode { get; set; }

		[AllowedValues(""open"", ""closed"")]
		public string? Status { get; set; }

		[DeniedValues(13, 99)]
		public int Code { get; set; }

		[Range(1, 3)]
		public int Quantity { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.FullAttributeCoverageModel")!;
		var model = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Name")!.SetValue(model, null);
		modelType.GetProperty("Value")!.SetValue(model, "A");
		modelType.GetProperty("MinOnly")!.SetValue(model, "A");
		modelType.GetProperty("MaxOnly")!.SetValue(model, "ABCD");
		modelType.GetProperty("Items")!.SetValue(model, Array.Empty<int>());
		modelType.GetProperty("Email")!.SetValue(model, "bad");
		modelType.GetProperty("CountryCode")!.SetValue(model, "abc");
		modelType.GetProperty("Status")!.SetValue(model, "pending");
		modelType.GetProperty("Code")!.SetValue(model, 13);
		modelType.GetProperty("Quantity")!.SetValue(model, 4);

		var result = InvokeValidate(assembly, model, "Testing.FullAttributeCoverageModelSchema");
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;
		var codesByPath = errors.ToDictionary(static error => error.Path[0], static error => error.Code);

		await Assert.That(codesByPath["Name"]).IsEqualTo("missing_field");
		// Defect B3: [StringLength] reports the runtime StringLengthRule's code; the direction is the category.
		await Assert.That(codesByPath["Value"]).IsEqualTo("invalid_length");
		await Assert.That(codesByPath["MinOnly"]).IsEqualTo("too_small");
		await Assert.That(codesByPath["MaxOnly"]).IsEqualTo("too_big");
		await Assert.That(codesByPath["Items"]).IsEqualTo("too_small");
		await Assert.That(codesByPath["Email"]).IsEqualTo("invalid_string");
		await Assert.That(codesByPath["CountryCode"]).IsEqualTo("invalid_string");
		await Assert.That(codesByPath["Status"]).IsEqualTo("invalid_value");
		await Assert.That(codesByPath["Code"]).IsEqualTo("invalid_value");
		await Assert.That(codesByPath["Quantity"]).IsEqualTo("invalid_range");
	}

	[Test]
	public async Task Generate_GivenStringFormatDataAnnotations_UsesExpectedGeneratedShapes(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class StringFormatModel
	{
		[Url]
		public string? Website { get; set; }

		[Phone]
		public string? PhoneNumber { get; set; }

		[CreditCard]
		public string? CardNumber { get; set; }

		[Base64String]
		public string? Encoded { get; set; }

		[Compare(nameof(Password))]
		public string? ConfirmPassword { get; set; }

		public string? Password { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(source, cancellationToken);
		var generatedSource = driverResult.GetSource("StringFormatModelSchema");

		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.UrlRule().IsValid(websiteValue)");
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.PhoneRule().IsValid(phoneNumberValue)");
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.CreditCardRule().IsValid(cardNumberValue)");
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode("new global::ZodSharp.Rules.Base64StringRule().IsValid(encodedValue)");
		await Assert
			.That(generatedSource)
			.ContainsGeneratedCode("EqualityComparer<object>.Default.Equals(value.ConfirmPassword, value.Password)");
	}

	[Test]
	public async Task GeneratedValidate_GivenStringFormatDataAnnotations_ProducesExpectedValidationResults(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class StringFormatRuntimeModel
	{
		[Url]
		public string? Website { get; set; }

		[Phone]
		public string? PhoneNumber { get; set; }

		[CreditCard]
		public string? CardNumber { get; set; }

		[Base64String]
		public string? Encoded { get; set; }

		[Compare(nameof(Password))]
		public string? ConfirmPassword { get; set; }

		public string? Password { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.StringFormatRuntimeModel")!;
		var model = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Website")!.SetValue(model, "not-a-url");
		modelType.GetProperty("PhoneNumber")!.SetValue(model, "not-a-phone");
		modelType.GetProperty("CardNumber")!.SetValue(model, "not-a-card");
		modelType.GetProperty("Encoded")!.SetValue(model, "not-base64!");
		modelType.GetProperty("Password")!.SetValue(model, "secret");
		modelType.GetProperty("ConfirmPassword")!.SetValue(model, "different");

		var result = InvokeValidate(assembly, model, "Testing.StringFormatRuntimeModelSchema");
		var errors = (System.Collections.Immutable.ImmutableArray<Core.ValidationError>)
			result.GetType().GetProperty("Errors")!.GetValue(result)!;

		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsFalse();
		await Assert.That(errors.Any(e => e.Path.Contains("Website"))).IsTrue();
		await Assert.That(errors.Any(e => e.Path.Contains("PhoneNumber"))).IsTrue();
		await Assert.That(errors.Any(e => e.Path.Contains("CardNumber"))).IsTrue();
		await Assert.That(errors.Any(e => e.Path.Contains("Encoded"))).IsTrue();
		await Assert.That(errors.Any(e => e.Path.Contains("ConfirmPassword"))).IsTrue();
	}

	[Test]
	public async Task GeneratedValidate_GivenStringFormatDataAnnotationsWithValidValues_ReturnsSuccess(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class StringFormatValidModel
	{
		[Url]
		public string? Website { get; set; }

		[Phone]
		public string? PhoneNumber { get; set; }

		[CreditCard]
		public string? CardNumber { get; set; }

		[Base64String]
		public string? Encoded { get; set; }

		[Compare(nameof(Password))]
		public string? ConfirmPassword { get; set; }

		public string? Password { get; set; }
	}
}
";

		using var driverResult = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = await Assert.That(driverResult.CompilationResult.Assembly).IsNotNull();

		var modelType = assembly.GetType("Testing.StringFormatValidModel")!;
		var model = Activator.CreateInstance(modelType)!;
		modelType.GetProperty("Website")!.SetValue(model, "https://example.com");
		modelType.GetProperty("PhoneNumber")!.SetValue(model, "+1 555 123 4567");
		modelType.GetProperty("CardNumber")!.SetValue(model, "4532015112830366");
		modelType.GetProperty("Encoded")!.SetValue(model, "SGVsbG8gV29ybGQ=");
		modelType.GetProperty("Password")!.SetValue(model, "secret");
		modelType.GetProperty("ConfirmPassword")!.SetValue(model, "secret");

		var result = InvokeValidate(assembly, model, "Testing.StringFormatValidModelSchema");
		await Assert.That((bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!).IsTrue();
	}

	const string UserSource =
		@"
using System.ComponentModel.DataAnnotations;

namespace Testing
{
	[ZodSchema]
	public sealed class User
	{
		[Required]
		[StringLength(50, MinimumLength = 3)]
		public string? Name { get; set; }

		[Range(0, 120)]
		public int Age { get; set; }

		[EmailAddress]
		public string? Email { get; set; }
	}
}
";

	static object CreateUser(Assembly assembly, string? name, int age, string? email)
	{
		var userType = assembly.GetType("Testing.User")!;
		var user = Activator.CreateInstance(userType)!;
		userType.GetProperty("Name")!.SetValue(user, name);
		userType.GetProperty("Age")!.SetValue(user, age);
		userType.GetProperty("Email")!.SetValue(user, email);
		return user;
	}

	static object InvokeValidate(Assembly assembly, object user, string schemaTypeName = "Testing.UserSchema")
	{
		var schemaType = assembly.GetType(schemaTypeName)!;
		return schemaType.GetMethod("Validate")!.Invoke(null, [user])!;
	}
}
