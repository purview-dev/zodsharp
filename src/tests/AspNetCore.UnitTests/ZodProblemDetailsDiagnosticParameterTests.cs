using Microsoft.AspNetCore.Http;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Generated validators attach <c>declaringType</c> (a full CLR type name) and <c>member</c> to every error, for
/// localization. They describe the server's implementation, so ProblemDetails leaves them out unless asked.
/// </summary>
public class ZodProblemDetailsDiagnosticParameterTests
{
	static ValidationError CreateError() =>
		ValidationError.Create(
			"too_small",
			"Name is too short",
			ZodPath.ParseDotted("Customer.Name"),
			parameters: new Dictionary<string, object?>
			{
				[ZodIssueParameters.DisplayName] = "Name",
				[ZodIssueParameters.Member] = "Name",
				[ZodIssueParameters.DeclaringType] = "Contoso.Orders.Internal.CustomerDto",
				["minimum"] = 2,
			}
		);

	[Test]
	public async Task Create_GivenDefaultOptions_OmitsDiagnosticParametersButKeepsThemOnTheError(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var error = CreateError();
		ZodProblemDetailsWriter writer = new(new ZodProblemDetailsOptions { MergeParametersIntoExtensions = true });

		// Act
		var details = writer.Create([error]);

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		var issue = Issue(details);
		await Assert.That(issue.SafeParameters).IsNotNull();
		await Assert.That(issue.SafeParameters!.ContainsKey("declaringType")).IsFalse();
		await Assert.That(issue.SafeParameters.ContainsKey("member")).IsFalse();
		await Assert.That(issue.SafeParameters["displayName"]).IsEqualTo("Name");
		await Assert.That(issue.SafeParameters.ContainsKey("minimum")).IsTrue();

		await Assert.That(details.Extensions.ContainsKey("declaringType")).IsFalse();
		await Assert.That(details.Extensions.ContainsKey("member")).IsFalse();
		await Assert.That(details.Extensions.ContainsKey("displayName")).IsTrue();

		// Still available server-side, for localization and logging.
		await Assert
			.That(issue.Parameters![ZodIssueParameters.DeclaringType])
			.IsEqualTo("Contoso.Orders.Internal.CustomerDto");
	}

	[Test]
	public async Task Create_GivenIncludeDiagnosticParameters_WritesThem(CancellationToken cancellationToken)
	{
		// Arrange
		ZodProblemDetailsWriter writer = new(
			new ZodProblemDetailsOptions { IncludeDiagnosticParameters = true, MergeParametersIntoExtensions = true }
		);

		// Act
		var details = writer.Create([CreateError()]);

		// Assert
		cancellationToken.ThrowIfCancellationRequested();
		var issue = Issue(details);
		await Assert.That(issue.SafeParameters!["declaringType"]).IsEqualTo("Contoso.Orders.Internal.CustomerDto");
		await Assert.That(issue.SafeParameters["member"]).IsEqualTo("Name");
		await Assert.That(details.Extensions.ContainsKey("declaringType")).IsTrue();
	}

	static ValidationIssue Issue(HttpValidationProblemDetails details) =>
		((IEnumerable<ValidationIssue>)details.Extensions["issues"]!).Single();
}
