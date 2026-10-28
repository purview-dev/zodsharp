using ZodSharp.SourceGenerators.Infra;
using static ZodSharp.SourceGenerators.Infra.GeneratedSchemaRuntime;

namespace ZodSharp.SourceGenerators;

/// <summary>
/// Service-aware custom validation (Change 9), [ZodAsyncMember] targeting, the synchronous fast path and the
/// async depth guard.
/// </summary>
partial class ZodSchemaGeneratorTests
{
	const string ServiceAwareSource = """
		using System;
		using System.ComponentModel.DataAnnotations;
		using System.Threading;
		using System.Threading.Tasks;
		using ZodSharp;
		using ZodSharp.Core;

		namespace Testing
		{
			[ZodSchema]
			public class Account
			{
				[Required]
				public string? Handle { get; set; }

				[MinLength(2)]
				public string? DisplayName { get; set; }

				[ZodAsyncMember(nameof(Handle))]
				internal static ValueTask<ValidationResult<Account>> CustomValidationAsync(
					Account value,
					IServiceProvider services,
					CancellationToken ct
				)
				{
					var taken = services.GetService(typeof(string)) as string;
					return ValueTask.FromResult(
						value.Handle == taken
							? ValidationResult<Account>.Failure(
								new[]
								{
									ValidationError.Create("handle_taken", "Handle is taken", AccountSchema.Paths.Handle),
									ValidationError.Create("audit", "Logged", ZodPath.Root),
								}
							)
							: ValidationResult<Account>.Success(value)
					);
				}
			}
		}
		""";

	[Test]
	public async Task ValidateAsync_GivenServiceAwareMethod_HandsTheCallersServicesToIt(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			ServiceAwareSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var account = Create(assembly, "Testing.Account", ("Handle", "ada"), ("DisplayName", "Ada"));
		StubServiceProvider services = new("ada");

		// Act
		var errors = await RunValidationAsync(assembly, "Testing.AccountSchema", account, services, cancellationToken);

		// Assert
		await Assert
			.That(errors.Select(static error => error.Code).ToArray())
			.IsEquivalentTo(["handle_taken", "audit"]);
	}

	[Test]
	public async Task ValidateAsync_GivenServiceAwareMethodAndNoServices_ThrowsNamingTheType(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			ServiceAwareSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var account = Create(assembly, "Testing.Account", ("Handle", "ada"));

		// Act
		async Task Act() =>
			await RunValidationAsync(assembly, "Testing.AccountSchema", account, null, cancellationToken);

		// Assert
		await Assert.That(Act).Throws<InvalidOperationException>().WithMessageContaining("Testing.Account");
	}

	[Test]
	public async Task ValidateMemberAsync_GivenZodAsyncMember_RunsTheCustomMethodOnlyForThatMemberAndKeepsItsErrors(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var result = await GenerateAsync(
			ServiceAwareSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var account = Create(assembly, "Testing.Account", ("Handle", "ada"), ("DisplayName", "A"));
		StubServiceProvider services = new("ada");

		// Act
		var handle = await RunMemberValidationAsync(
			assembly,
			"Testing.AccountSchema",
			account,
			"Handle",
			services,
			cancellationToken
		);
		var displayName = await RunMemberValidationAsync(
			assembly,
			"Testing.AccountSchema",
			account,
			"DisplayName",
			services,
			cancellationToken
		);

		// Assert — the root-level "audit" error is not the Handle field's, so it is not reported for it.
		await Assert.That(handle.Select(static error => error.Code).ToArray()).IsEquivalentTo(["handle_taken"]);
		await Assert.That(displayName.Select(static error => error.Code).ToArray()).IsEquivalentTo(["too_small"]);
		await Assert.That(services.Requests).IsEqualTo(1);
	}

	[Test]
	public async Task ValidateAsync_GivenNoAsynchronousRules_CompletesSynchronously(CancellationToken cancellationToken)
	{
		// Arrange
		using var result = await GenerateAsync(
			FlatMembersSource,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		var registration = Create(assembly, "Testing.Registration", ("UserName", "valid"));

		// Act
		var pending = GetStaticMethod(assembly, "Testing.RegistrationSchema", "ValidateAsync", 3)
			.Invoke(null, [registration, null, cancellationToken])!;
		var completed = (bool)pending.GetType().GetProperty("IsCompletedSuccessfully")!.GetValue(pending)!;
		var requiresAsync = (bool)GetStaticField(assembly, "Testing.RegistrationSchema", "RequiresAsyncValidation")!;

		// Assert
		await Assert.That(completed).IsTrue();
		await Assert.That(requiresAsync).IsFalse();
	}

	[Test]
	[Arguments("FlatMembers", "RegistrationSchema", "so this call never blocks", "a synchronous call cannot")]
	[Arguments("ServiceAware", "AccountSchema", "a synchronous call cannot", "so this call never blocks")]
	public async Task Validate_GivenRequiresAsyncValidation_DocumentsWhichCallIsRightInAsyncCode(
		string sourceName,
		string schemaName,
		string expected,
		string unexpected,
		CancellationToken cancellationToken
	)
	{
		// Arrange - CA1849 pairs Validate with ValidateAsync; ZodSharp's analyzer suppresses it for both, so the
		// generated remark states which call the type actually needs.
		var source = sourceName == "FlatMembers" ? FlatMembersSource : ServiceAwareSource;

		// Act
		using var result = await GenerateAsync(source, cancellationToken);
		var generated = result.GetSource(schemaName);

		// Assert
		await Assert.That(generated).Contains(expected);
		await Assert.That(generated).DoesNotContain(unexpected);
	}

	[Test]
	public async Task ValidateAsync_GivenCyclicGraph_ThrowsAtTheMaximumDepth(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;
			using ZodSharp;
			using ZodSharp.Core;

			namespace Testing
			{
				[ZodSchema]
				public class Link
				{
					public Link? Next { get; set; }

					internal static ValueTask<ValidationResult<Link>> CustomValidationAsync(Link value, CancellationToken ct) =>
						ValueTask.FromResult(ValidationResult<Link>.Success(value));
				}
			}
			""";

		using var result = await GenerateAsync(
			source,
			new ZodSourceGeneratorTestOptions().Compile(),
			cancellationToken
		);
		var assembly = result.CompilationResult.Assembly!;
		dynamic link = Create(assembly, "Testing.Link");
		link.Next = link;
		object cyclic = link;

		// Act
		async Task Act() => await RunValidationAsync(assembly, "Testing.LinkSchema", cyclic, null, cancellationToken);

		// Assert
		await Assert.That(Act).Throws<InvalidOperationException>().WithMessageContaining("Maximum validation depth");
	}

	sealed class StubServiceProvider(string taken) : IServiceProvider
	{
		public int Requests { get; private set; }

		public object? GetService(Type serviceType)
		{
			Requests++;
			return serviceType == typeof(string) ? taken : null;
		}
	}
}
