using ZodSharp.Core;

Console.WriteLine("=== Basic ZodSharp Examples ===\n");

var nameSchema = Z.String().Min(3).Max(50);
var nameResult = nameSchema.Validate("John");
Console.WriteLine($"Name validation: {(nameResult.IsSuccess ? "Success" : "Failed")}");

var ageSchema = Z.Number().Min(0).Max(120).Int();
var ageResult = ageSchema.Validate(25.0);
Console.WriteLine($"Age validation: {(ageResult.IsSuccess ? "Success" : "Failed")}");

var emailSchema = Z.String().Email();
var emailResult = emailSchema.Validate("user@example.com");
Console.WriteLine($"Email validation: {(emailResult.IsSuccess ? "Success" : "Failed")}");

var numbersSchema = Z.Array(Z.Number()).Min(1).Max(10);
var numbersResult = numbersSchema.Validate([1.0, 2.0, 3.0]);
Console.WriteLine($"Numbers validation: {(numbersResult.IsSuccess ? "Success" : "Failed")}");

var userSchema = Z.Object()
	.Field("name", Z.String().Min(1))
	.Field("age", Z.Number().Min(0).Max(120))
	.Field("email", Z.String().Email())
	.Build();

var userData = new Dictionary<string, object?>
{
	{ "name", "John Doe" },
	{ "age", 30.0 },
	{ "email", "john@example.com" },
};

var userResult = userSchema.Validate(userData);
if (userResult.IsSuccess)
{
	Console.WriteLine("User validation: Success");
	Console.WriteLine(
		$"Validated user: {string.Join(", ", userResult.Value!.Select(static kvp => $"{kvp.Key}={kvp.Value}"))}"
	);
}
else
{
	Console.WriteLine("User validation: Failed");
	foreach (var error in userResult.Errors)
	{
		Console.WriteLine($"  - {string.Join(".", error.Path)}: {error.Message}");
	}
}

var optionalSchema = Z.Optional(Z.String());
var optionalResult1 = optionalSchema.Validate(null);
var optionalResult2 = optionalSchema.Validate("value");
Console.WriteLine($"Optional null: {(optionalResult1.IsSuccess ? "Success" : "Failed")}");
Console.WriteLine($"Optional value: {(optionalResult2.IsSuccess ? "Success" : "Failed")}");

try
{
	var invalidResult = nameSchema.Parse("AB");
}
catch (ZodException ex)
{
	Console.WriteLine($"Validation error: {ex.Message}");
	foreach (var error in ex.Errors)
	{
		Console.WriteLine($"  - {error.Message}");
	}
}

Console.WriteLine("\n");
AdvancedExamples.RunAll();

Console.WriteLine("\n");
DependencyInjectionExamples.RunAll();

Console.WriteLine("\n");
JsonSchemaExamples.RunAll();
