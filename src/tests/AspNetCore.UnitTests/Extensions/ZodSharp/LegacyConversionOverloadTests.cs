// The deprecated 2.x conversion overloads are exercised here on purpose: their behaviour is kept for compatibility.
#pragma warning disable CS0618

using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.AspNetCore;
using ZodSharp.Core;

namespace ZodSharp;

/// <summary>
/// The 2.x ProblemDetails conversion overloads take neither options nor a request, so they cannot apply the options an
/// application registers. They are obsolete, keep their 2.x output, and that output is exactly what the documented
/// equivalent <see cref="ZodProblemDetailsOptions"/> produce.
/// </summary>
public class LegacyConversionOverloadTests
{
	static readonly ErrorType Conflict = new(
		Code: "email_taken",
		Category: "invalid_value",
		Description: "The email is taken.",
		HttpStatus: StatusCodes.Status409Conflict,
		MessageFormat: "'{email}' is already registered"
	)
	{
		Parameters = [new ErrorTypeParameter("email", typeof(string))],
	};

	[Test]
	[Arguments(typeof(ProblemDetailsExtensions))]
	[Arguments(typeof(ZodExceptionExtensions))]
	public async Task LegacyOverloads_GivenExtensionClass_AreObsoleteAndTheOptionOverloadsAreNot(
		Type extensions,
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var methods = extensions
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(static method =>
				method.Name is "ToHttpValidationProblemDetails" or "ToValidationProblemDetails" or "ToValidationProblem"
			)
			.ToArray();

		// Act
		var obsolete = methods.Where(static method => method.IsDefined(typeof(ObsoleteAttribute))).ToArray();
		var current = methods.Except(obsolete).ToArray();

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		await Assert.That(obsolete).IsNotEmpty();
		await Assert.That(obsolete.All(static method => !TakesOptionsOrRequest(method))).IsTrue();
		await Assert.That(current).IsNotEmpty();
		await Assert.That(current.All(TakesOptionsOrRequest)).IsTrue();
	}

	[Test]
	public async Task ToHttpValidationProblemDetails_GivenRegisteredOptions_OnlyTheHttpContextOverloadAppliesThem(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var result = ValidationResult<string>.Failure(
			ValidationError.Create(
				"email_taken",
				"taken",
				ZodPath.ParseDotted("Email"),
				parameters: new Dictionary<string, object?> { ["email"] = "ada@example.com" }
			)
		);
		ServiceCollection services = new();
		services.AddLogging();
		services.Configure<ZodProblemDetailsOptions>(options =>
		{
			options.IncludeIssues = false;
			options.Registry = new ErrorTypeRegistry();
			options.Registry.Register(Conflict);
		});
		await using var provider = services.BuildServiceProvider();
		DefaultHttpContext httpContext = new() { RequestServices = provider };

		// Act
		var legacy = result.ToHttpValidationProblemDetails();
		var configured = result.ToHttpValidationProblemDetails(httpContext);

		// Assert - the legacy overload keeps its 2.x output: no ErrorType, the raw message, the issues projection.
		cancellationToken.ThrowIfCancellationRequested();
		await Assert.That(legacy.Status).IsEqualTo(StatusCodes.Status400BadRequest);
		await Assert.That(legacy.Errors["Email"]).IsEquivalentTo(["taken"]);
		await Assert.That(legacy.Extensions.ContainsKey("issues")).IsTrue();

		await Assert.That(configured.Status).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert.That(configured.Errors["Email"]).IsEquivalentTo(["'ada@example.com' is already registered"]);
		await Assert.That(configured.Extensions.ContainsKey("issues")).IsFalse();
	}

	[Test]
	public async Task LegacyOverloads_GivenTheDocumentedEquivalentOptions_ProduceTheSameBody(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		ZodException exception = new([
			ValidationError.Create(
				"email_taken",
				"taken",
				ZodPath.ParseDotted("Email"),
				parameters: new Dictionary<string, object?> { ["email"] = "ada@example.com" }
			),
			ValidationError.Create("too_small", "Name is too short", ZodPath.ParseDotted("Name"), minimum: 2),
		]);
		ErrorTypeRegistry registry = new();
		registry.Register(Conflict);
		ErrorType? Lookup(string code) => registry.TryGet(code, out var errorType) ? errorType : null;

		// Act
		(string Legacy, string Equivalent)[] pairs =
		[
			(
				Serialize(exception.ToHttpValidationProblemDetails(422)),
				Serialize(
					exception.ToHttpValidationProblemDetails(
						new ZodProblemDetailsOptions
						{
							Registry = new(),
							FormatMessages = false,
							DefaultStatusCode = 422,
						}
					)
				)
			),
			(
				Serialize(exception.ToHttpValidationProblemDetails(registry)),
				Serialize(
					exception.ToHttpValidationProblemDetails(new ZodProblemDetailsOptions { Registry = registry })
				)
			),
			(
				Serialize(exception.ToHttpValidationProblemDetails(Lookup)),
				Serialize(
					exception.ToHttpValidationProblemDetails(
						new ZodProblemDetailsOptions { ErrorTypeResolver = Lookup }
					)
				)
			),
		];

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		foreach (var (legacy, equivalent) in pairs)
		{
			await Assert.That(legacy).Contains("Email|email_taken|");
			await Assert.That(legacy).IsEqualTo(equivalent);
		}
	}

	static bool TakesOptionsOrRequest(MethodInfo method) =>
		method
			.GetParameters()
			.Any(static parameter =>
				parameter.ParameterType == typeof(HttpContext)
				|| parameter.ParameterType == typeof(ZodProblemDetailsOptions)
			);

	static string Serialize(HttpValidationProblemDetails details)
	{
		var issues =
			details.Extensions.TryGetValue("issues", out var value) && value is IEnumerable<ValidationIssue> list
				? string.Join(
					";",
					list.Select(static issue =>
						$"{issue.DottedPath}|{issue.Code}|{issue.Category}|{issue.Message}|{issue.Minimum}"
					)
				)
				: null;
		return JsonSerializer.Serialize(
			new
			{
				details.Status,
				details.Title,
				details.Type,
				details.Errors,
				Issues = issues,
			}
		);
	}
}
