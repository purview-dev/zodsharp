using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

// ZODSASP001: the templates deliberately use undeclared or built-in placeholders (path, minimum, code).
#pragma warning disable ZODSASP001

namespace ZodSharp.AspNetCore;

public class ZodProblemDetailsWriterTests
{
	static ZodProblemDetailsWriter CreateWriter(Action<ZodProblemDetailsOptions>? configure = null)
	{
		ZodProblemDetailsOptions options = new() { Registry = new ErrorTypeRegistry() };
		configure?.Invoke(options);
		return new ZodProblemDetailsWriter(options);
	}

	static ValidationIssue[] Issues(HttpValidationProblemDetails details) =>
		(ValidationIssue[])details.Extensions["issues"]!;

	static JsonElement Serialize(HttpValidationProblemDetails details) =>
		JsonSerializer.SerializeToElement(details, ZodProblemDetailsJsonContext.Default.HttpValidationProblemDetails);

	[Test]
	public async Task Create_GivenIndexKeyAndRootPaths_RendersDottedKeys()
	{
		// Defect D1: keys were built by joining segments (Items.0.Name), with no escaping.

		// Arrange
		ValidationError[] errors =
		[
			ValidationError.Create(
				"invalid",
				"Bad name.",
				ZodPath.From(ZodPathSegment.Member("Items"), ZodPathSegment.At(0), ZodPathSegment.Member("Name"))
			),
			ValidationError.Create(
				"invalid",
				"Bad key.",
				ZodPath.From(ZodPathSegment.Member("Map"), ZodPathSegment.Key("a.b"))
			),
			ValidationError.Create("invalid", "Bad root.", ZodPath.Root),
		];

		// Act
		var details = CreateWriter().Create(errors);

		// Assert
		await Assert.That(details.Errors.Keys).IsEquivalentTo(["Items[0].Name", "Map[\"a.b\"]", string.Empty]);
	}

	[Test]
	public async Task Create_GivenErrorTypeTemplateWithUnresolvedPlaceholder_KeepsTheErrorMessage()
	{
		// Defect D2: the template replaced the message even when placeholders could not be resolved.

		// Arrange
		var writer = CreateWriter(options =>
			options.MapErrorType(new ErrorType("save_failed", MessageFormat: "Save of '{AggregateId}' failed."))
		);

		// Act
		var details = writer.Create([ValidationError.Create("save_failed", "Save failed.", ZodPath.Root)]);

		// Assert
		await Assert.That(details.Errors[string.Empty]).IsEquivalentTo(["Save failed."]);
	}

	[Test]
	public async Task Create_GivenErrorTypeTemplateWithBuiltInPlaceholders_FormatsTheMessage()
	{
		// Arrange
		var writer = CreateWriter(options =>
			options.MapErrorType(new ErrorType("too_small", MessageFormat: "{path} must be at least {minimum}."))
		);
		var error = ValidationError.Create(
			"too_small",
			"Too small.",
			ZodPath.Member("Age"),
			minimum: 18,
			inclusive: true
		);

		// Act
		var details = writer.Create([error]);

		// Assert
		await Assert.That(details.Errors["Age"]).IsEquivalentTo(["Age must be at least 18."]);
	}

	[Test]
	public async Task ToHttpValidationProblemDetails_GivenMergeEnabledAndNoRegistry_StillMergesParameters()
	{
		// Defect D3: parameters were merged into the extensions only by the registry/lookup overloads.

		// Arrange
		var result = ValidationResult<string>.Failure(
			ValidationError.Create(
				"locked",
				"Locked.",
				["Id"],
				parameters: new Dictionary<string, object?> { ["ResourceId"] = "res-1" }
			)
		);

		// Act
		var details = result.ToHttpValidationProblemDetails(
			new ZodProblemDetailsOptions { MergeParametersIntoExtensions = true },
			httpContext: null
		);

		// Assert
		await Assert.That(details.Extensions["resourceId"]).IsEqualTo("res-1");
	}

	[Test]
	public async Task Create_GivenParameters_CamelCasesThemPerIssueAndHonoursTheMergeOption()
	{
		// Defect D4: parameters were flattened into the top-level extensions only, and issue parameters kept
		// their raw keys while the extensions were camel-cased.

		// Arrange
		var writer = CreateWriter(static options => options.MergeParametersIntoExtensions = false);
		var error = ValidationError.Create(
			"locked",
			"Locked.",
			["Id"],
			parameters: new Dictionary<string, object?> { ["ResourceId"] = "res-1" }
		);

		// Act
		var details = writer.Create([error]);
		var issue = Serialize(details).GetProperty("issues")[0];

		// Assert
		await Assert.That(details.Extensions.ContainsKey("resourceId")).IsFalse();
		await Assert.That(issue.GetProperty("parameters").GetProperty("resourceId").GetString()).IsEqualTo("res-1");
	}

	[Test]
	public async Task Create_GivenParameterNamedLikeAReservedMember_DoesNotOverwriteIt()
	{
		// Defect D5: a parameter named "errors" (as ZodUnion used) wrote a second "errors" JSON member.

		// Arrange
		var error = ValidationError.Create(
			"invalid",
			"Invalid.",
			["Id"],
			parameters: new Dictionary<string, object?>
			{
				["errors"] = "x",
				["Status"] = 7,
				["traceId"] = "fake",
			}
		);

		// Act
		var details = CreateWriter(static options => options.MergeParametersIntoExtensions = true)
			.Create([error], new DefaultHttpContext { TraceIdentifier = "real-trace" });
		var json = Serialize(details);

		// Assert
		await Assert.That(details.Extensions["param_errors"]).IsEqualTo("x");
		await Assert.That(details.Extensions["param_status"]).IsEqualTo(7L);
		await Assert.That(details.Extensions["param_traceId"]).IsEqualTo("fake");
		await Assert.That(json.GetProperty("traceId").GetString()).IsNotEqualTo("fake");
		await Assert.That(json.GetProperty("status").GetInt32()).IsEqualTo(400);
		await Assert.That(json.GetProperty("errors").ValueKind).IsEqualTo(JsonValueKind.Object);
	}

	[Test]
	public async Task TryHandleAsync_GivenFormatMessagesDisabled_KeepsTheErrorMessage(
		CancellationToken cancellationToken
	)
	{
		// Defect D7: ZodProblemDetailsOptions.FormatMessages was never read.

		// Arrange
		ZodProblemDetailsOptions options = new() { Registry = new ErrorTypeRegistry(), FormatMessages = false };
		options.MapErrorType(new ErrorType("conflict", MessageFormat: "Templated {code}."));
		ZodExceptionHandler handler = new(Microsoft.Extensions.Options.Options.Create(options));
		DefaultHttpContext httpContext = new();
		httpContext.Response.Body = new MemoryStream();

		// Act
		await handler.TryHandleAsync(
			httpContext,
			new ZodException([ValidationError.Create("conflict", "Original.", ZodPath.Root)]),
			cancellationToken
		);
		httpContext.Response.Body.Position = 0;
		using var document = await JsonDocument.ParseAsync(
			httpContext.Response.Body,
			cancellationToken: cancellationToken
		);

		// Assert
		await Assert
			.That(document.RootElement.GetProperty("errors").GetProperty("")[0].GetString())
			.IsEqualTo("Original.");
	}

	[Test]
	public async Task Create_GivenStatusCodeSelectorAndMappedErrorType_SelectorWins()
	{
		// Defect D8: the selector's status was overridden by ErrorType.HttpStatus.

		// Arrange
		var writer = CreateWriter(static options =>
		{
			options.MapErrorType(new ErrorType("conflict", HttpStatus: StatusCodes.Status409Conflict));
			options.StatusCodeSelector = static _ => StatusCodes.Status503ServiceUnavailable;
		});

		// Act
		var details = writer.Create([ValidationError.Create("conflict", "Conflict.", ZodPath.Root)]);

		// Assert
		await Assert.That(details.Status).IsEqualTo(StatusCodes.Status503ServiceUnavailable);
	}

	[Test]
	public async Task Create_GivenStatusCodeOverride_ReceivesTheComputedStatus()
	{
		// Arrange
		var received = 0;
		var writer = CreateWriter(options =>
		{
			options.MapErrorType(new ErrorType("conflict", HttpStatus: StatusCodes.Status409Conflict));
			options.StatusCodeOverride = (_, computed) =>
			{
				received = computed;
				return computed + 1;
			};
		});

		// Act
		var details = writer.Create([ValidationError.Create("conflict", "Conflict.", ZodPath.Root)]);

		// Assert
		await Assert.That(received).IsEqualTo(StatusCodes.Status409Conflict);
		await Assert.That(details.Status).IsEqualTo(410);
	}

	[Test]
	public async Task ToValidationProblemDetails_CopiesEveryExtension()
	{
		// Defect D9: the MVC copy kept only "issues".

		// Arrange
		ZodException exception = new([
			ValidationError.Create(
				"locked",
				"Locked.",
				["Id"],
				parameters: new Dictionary<string, object?> { ["ResourceId"] = "res-1" }
			),
		]);

		// Act
		var details = exception.ToValidationProblemDetails(
			new ZodProblemDetailsOptions { MergeParametersIntoExtensions = true },
			httpContext: null
		);

		// Assert
		await Assert.That(details.Extensions.ContainsKey("issues")).IsTrue();
		await Assert.That(details.Extensions["resourceId"]).IsEqualTo("res-1");
	}

	[Test]
	public async Task Create_SerializesASafeIssueShape()
	{
		// Defect D10: raw parameters were serialized, including nested union errors and arbitrary objects.

		// Arrange
		var inner = ValidationError.Create("invalid_type", "Expected number.", ZodPath.Member("Value"));
		var union = ValidationError.Create(
			"invalid_union",
			"No option matched.",
			ZodPath.Member("Value"),
			parameters: new Dictionary<string, object?>
			{
				[ZodIssueParameters.UnionErrors] = ImmutableArray.Create(ImmutableArray.Create(inner)),
				["Secret"] = new Uri("https://internal").GetType().Assembly,
				["Count"] = 3,
			},
			category: ZodIssueCategories.InvalidUnion
		);
		var range = ValidationError.Create(
			"too_small",
			"Too small.",
			ZodPath.From(ZodPathSegment.Member("Items"), ZodPathSegment.At(2), ZodPathSegment.Member("Price")),
			category: ZodIssueCategories.TooSmall
		) with
		{
			Lower = ErrorBound.Excluding(0.5),
			Upper = ErrorBound.Including(9.5m),
		};
		var date = ValidationError.Create(
			"too_big",
			"Too late.",
			ZodPath.Member("When"),
			category: ZodIssueCategories.TooBig
		) with
		{
			Upper = ErrorBound.Including(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
		};

		// Act
		var issues = Serialize(CreateWriter().Create([union, range, date])).GetProperty("issues");

		// Assert
		var unionIssue = issues[0];
		await Assert.That(unionIssue.GetProperty("parameters").TryGetProperty("secret", out _)).IsFalse();
		await Assert.That(unionIssue.GetProperty("parameters").GetProperty("count").GetInt32()).IsEqualTo(3);
		await Assert.That(unionIssue.GetProperty("parameters").TryGetProperty("unionErrors", out _)).IsFalse();
		await Assert
			.That(unionIssue.GetProperty("unionErrors")[0][0].GetProperty("code").GetString())
			.IsEqualTo("invalid_type");

		var rangeIssue = issues[1];
		await Assert.That(rangeIssue.GetProperty("path").GetString()).IsEqualTo("Items[2].Price");
		await Assert.That(rangeIssue.GetProperty("segments")[1].GetInt32()).IsEqualTo(2);
		await Assert.That(rangeIssue.GetProperty("minimum").GetDouble()).IsEqualTo(0.5);
		await Assert.That(rangeIssue.GetProperty("maximum").GetDecimal()).IsEqualTo(9.5m);
		await Assert.That(rangeIssue.GetProperty("inclusive").GetBoolean()).IsFalse();

		var dateIssue = issues[2];
		await Assert.That(dateIssue.GetProperty("maximum").GetString()).IsEqualTo("2026-01-02T03:04:05.0000000Z");
		await Assert.That(dateIssue.GetProperty("inclusive").GetBoolean()).IsTrue();
	}

	[Test]
	public async Task Create_GivenHttpContext_AddsTheTraceId()
	{
		// Arrange
		DefaultHttpContext httpContext = new() { TraceIdentifier = "trace-42" };

		// Act
		var details = CreateWriter().Create([ValidationError.Create("invalid", "Invalid.", ZodPath.Root)], httpContext);

		// Assert
		await Assert
			.That(details.Extensions["traceId"])
			.IsEqualTo(System.Diagnostics.Activity.Current?.Id ?? "trace-42");
	}

	[Test]
	public async Task Create_GivenJsonPathNaming_UsesTheConfiguredNamingPolicy()
	{
		// Arrange
		ServiceCollection services = new();
		services.Configure<HttpJsonOptions>(static options =>
			options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
		);
		await using var provider = services.BuildServiceProvider();
		DefaultHttpContext httpContext = new() { RequestServices = provider };
		var writer = CreateWriter(static options => options.PathNaming = ZodPathNaming.Json);
		var error = ValidationError.Create(
			"invalid",
			"Invalid.",
			ZodPath.From(ZodPathSegment.Member("LineItems"), ZodPathSegment.At(0), ZodPathSegment.Member("UnitPrice"))
		);

		// Act
		var details = writer.Create([error], httpContext);

		// Assert
		await Assert.That(details.Errors.Keys).IsEquivalentTo(["line_items[0].unit_price"]);
		await Assert.That(Issues(details)[0].Segments).IsEquivalentTo(new object[] { "line_items", 0, "unit_price" });
	}

	[Test]
	public async Task Create_GivenAMessageFormatter_FormatsInTheCurrentUICulture()
	{
		// Arrange
		CultureInfo? seen = null;
		ZodProblemDetailsWriter writer = new(
			new ZodProblemDetailsOptions { Registry = new ErrorTypeRegistry() },
			new RecordingFormatter(culture => seen = culture)
		);
		var previous = CultureInfo.CurrentUICulture;
		CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

		try
		{
			// Act
			var details = writer.Create([ValidationError.Create("invalid", "Invalid.", ZodPath.Member("Name"))]);

			// Assert
			await Assert.That(details.Errors["Name"]).IsEquivalentTo(["localized:invalid"]);
			await Assert.That(seen?.Name).IsEqualTo("fr-FR");
			await Assert.That(Issues(details)[0].Code).IsEqualTo("invalid");
		}
		finally
		{
			CultureInfo.CurrentUICulture = previous;
		}
	}

	[Test]
	public async Task Apply_GivenExistingDataAnnotationsErrors_MergesThemAndProjectsIssues()
	{
		// Arrange
		HttpValidationProblemDetails existing = new(
			new Dictionary<string, string[]>
			{
				["Email"] = ["stale"],
				["page"] = ["The field page must be between 1 and 10."],
			}
		);

		// Act
		CreateWriter()
			.Apply(existing, [ValidationError.Create("invalid_email", "Email is invalid.", ZodPath.Member("Email"))]);
		var issues = Issues(existing);

		// Assert
		await Assert.That(existing.Errors["Email"]).IsEquivalentTo(["Email is invalid."]);
		await Assert.That(existing.Errors["page"]).IsEquivalentTo(["The field page must be between 1 and 10."]);
		await Assert.That(issues.Length).IsEqualTo(2);
		await Assert.That(issues[1].Code).IsEqualTo(ZodProblemDetailsWriter.DataAnnotationsCode);
		await Assert.That(issues[1].DottedPath).IsEqualTo("page");
	}

	sealed class RecordingFormatter(Action<CultureInfo> onFormat) : IZodMessageFormatter
	{
		public string Format(in ValidationError validationError, CultureInfo culture)
		{
			onFormat(culture);
			return "localized:" + validationError.Code;
		}
	}
}
