using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.AspNetCore.Fixtures;

namespace ZodSharp.AspNetCore;

public class ZodValidationFilterTests
{
	const string NextResult = "next-ran";

	[Test]
	public async Task InvokeAsync_GivenValidRequest_InvokesNext()
	{
		var context = CreateContext(CreateProvider(), new ValidatedRequest { Name = "abc" });

		var result = await new ZodValidationFilter<ValidatedRequest>().InvokeAsync(context, static _ =>
			ValueTask.FromResult<object?>(NextResult)
		);

		await Assert.That(result).IsEqualTo(NextResult);
	}

	[Test]
	public async Task InvokeAsync_GivenInvalidRequest_ReturnsValidationProblem()
	{
		var context = CreateContext(CreateProvider(), new ValidatedRequest { Name = "a" });

		var result = await new ZodValidationFilter<ValidatedRequest>().InvokeAsync(context, static _ =>
			ValueTask.FromResult<object?>(NextResult)
		);

		await Assert.That(result is IResult).IsTrue();
	}

	[Test]
	public async Task InvokeAsync_GivenNoFactoryRegistered_Throws()
	{
		ServiceCollection services = new();
		var context = CreateContext(services.BuildServiceProvider(), new ValidatedRequest { Name = "abc" });

		InvalidOperationException? exception = null;
		try
		{
			await new ZodValidationFilter<ValidatedRequest>().InvokeAsync(context, static _ =>
				ValueTask.FromResult<object?>(NextResult)
			);
		}
		catch (InvalidOperationException ex)
		{
			exception = ex;
		}

		await Assert.That(exception).IsNotNull();
	}

	[Test]
	public async Task InvokeAsync_GivenNoValidatorForType_Throws()
	{
		ServiceCollection services = new();
		services.AddZodSharp();
		var context = CreateContext(services.BuildServiceProvider(), new ValidatedRequest { Name = "abc" });

		InvalidOperationException? exception = null;
		try
		{
			await new ZodValidationFilter<ValidatedRequest>().InvokeAsync(context, static _ =>
				ValueTask.FromResult<object?>(NextResult)
			);
		}
		catch (InvalidOperationException ex)
		{
			exception = ex;
		}

		await Assert.That(exception).IsNotNull();
	}

	static ServiceProvider CreateProvider()
	{
		ServiceCollection services = new();
		services.AddZodSharp(static opts => opts.ScanAssemblies.Add(typeof(ValidatedRequest).Assembly));
		return services.BuildServiceProvider();
	}

	static EndpointFilterInvocationContext CreateContext(IServiceProvider provider, object? argument)
	{
		DefaultHttpContext httpContext = new() { RequestServices = provider };
		return EndpointFilterInvocationContext.Create(httpContext, argument);
	}
}
