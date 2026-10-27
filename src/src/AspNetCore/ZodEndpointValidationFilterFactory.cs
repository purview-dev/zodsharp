using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore;

/// <summary>
/// Builds the ZodSharp endpoint filter. All per-endpoint work - choosing the parameters to validate and
/// resolving their validators - happens once, when the endpoint is built; the per-request delegate only
/// validates the selected arguments.
/// </summary>
static class ZodEndpointValidationFilterFactory
{
	internal const string InvalidJsonCode = "invalid_json";

	/// <summary>
	/// Adds the filter factory to every endpoint <paramref name="builder"/> builds. The factory sees the endpoint's
	/// final metadata (it runs after every convention), which the double-validation guard needs.
	/// </summary>
	public static void AddTo<TBuilder>(
		TBuilder builder,
		ZodEndpointValidationOptions options,
		Type? requiredType,
		Func<IZodSchemaFactory, IZodUntypedValidator?>? requiredTypeResolver
	)
		where TBuilder : IEndpointConventionBuilder =>
		builder.Add(endpoint =>
			endpoint.FilterFactories.Add(
				(context, next) => Build(context, next, options, requiredType, requiredTypeResolver, endpoint.Metadata)
			)
		);

	static EndpointFilterDelegate Build(
		EndpointFilterFactoryContext context,
		EndpointFilterDelegate next,
		ZodEndpointValidationOptions options,
		Type? requiredType,
		Func<IZodSchemaFactory, IZodUntypedValidator?>? requiredTypeResolver,
		IList<object> endpointMetadata
	)
	{
		var services = context.ApplicationServices;
		var factory = services.GetService<IZodSchemaFactory>();
		var isService = services.GetService<IServiceProviderIsService>();
		var method = context.MethodInfo;
		var parameters = method.GetParameters();
#if NET11_0_OR_GREATER
		var pipeline = ZodValidationPipelineGuard.Create(services, endpointMetadata);
		List<string>? coveredByPipeline = null;
#else
		_ = endpointMetadata;
#endif

		if (factory is null && (requiredType is not null || options.ThrowIfNoValidator))
		{
			throw new InvalidOperationException(
				$"No IZodSchemaFactory is registered, so endpoint handler '{DescribeHandler(method)}' cannot be "
					+ "validated. Call AddZodSharp (or AddZodSharpFactory) before applying WithZodSharpValidation."
			);
		}

		List<ValidationTarget> targets = [];
		for (var index = 0; index < parameters.Length; index++)
		{
			var parameter = parameters[index];
			var parameterType = parameter.ParameterType;
			var validatedType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

#if NET11_0_OR_GREATER
			// The built-in pipeline already validates this parameter with ZodSharp (outermost filter, before this one).
			if (pipeline?.Covers(parameter) == true)
			{
				(coveredByPipeline ??= []).Add(
					parameter.Name ?? index.ToString(System.Globalization.CultureInfo.InvariantCulture)
				);
				continue;
			}
#endif

			IZodUntypedValidator? validator = null;
			if (requiredType is not null)
			{
				if (!requiredType.IsAssignableFrom(validatedType))
					continue;

				if (!factory!.TryGetUntypedValidator(requiredType, out validator) || validator is null)
					validator = requiredTypeResolver?.Invoke(factory);

				if (validator is null)
					throw MissingValidator(method, parameter, requiredType);
			}
			else
			{
				if (IsSkipped(parameter, isService))
					continue;

				validator = factory is null ? null : ResolveUntyped(factory, parameterType, validatedType);
				if (validator is null)
				{
					if (options.ThrowIfNoValidator && !IsScalar(validatedType))
						throw MissingValidator(method, parameter, validatedType);

					continue;
				}
			}

			targets.Add(
				new ValidationTarget(
					index,
					parameter.Name ?? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"arg{index}"),
					validator,
					PrefixAlways: IsSequence(validatedType),
					IsBody: IsBody(parameter, validatedType, isService)
				)
			);
		}

#if NET11_0_OR_GREATER
		if (coveredByPipeline is not null)
			pipeline!.LogCovered(DescribeHandler(method), coveredByPipeline);
#endif

		if (targets.Count == 0)
			return next;

		var writer = ZodProblemDetailsWriter.GetOrCreate(services);
		ValidationTarget[] selected = [.. targets];

		return invocation => InvokeAsync(invocation, next, selected, writer, options);
	}

	static async ValueTask<object?> InvokeAsync(
		EndpointFilterInvocationContext invocation,
		EndpointFilterDelegate next,
		ValidationTarget[] targets,
		ZodProblemDetailsWriter writer,
		ZodEndpointValidationOptions options
	)
	{
		var httpContext = invocation.HttpContext;

#if NET11_0_OR_GREATER
		// .NET 11 runs endpoint filters after a binding failure, with the 400 already set. Validating the
		// default/null argument would overwrite the binder's response with a misleading one.
		if (httpContext.Response.StatusCode == StatusCodes.Status400BadRequest)
		{
			return options.OnBindingFailure == ZodBindingFailureBehavior.PassThrough
				? await next(invocation).ConfigureAwait(false)
				: CreateBindingFailureResult(invocation, targets, writer);
		}
#else
		_ = options;
#endif

		List<ValidationError>? errors = null;
		foreach (var target in targets)
		{
			if (target.Index >= invocation.Arguments.Count || invocation.Arguments[target.Index] is not { } argument)
				continue;

			var pending = target.Validator.ValidateAsync(
				argument,
				httpContext.RequestServices,
				httpContext.RequestAborted
			);
			var result = pending.IsCompletedSuccessfully ? pending.Result : await pending.ConfigureAwait(false);
			if (result.IsDefaultOrEmpty)
				continue;

			errors ??= [];
			foreach (var error in result)
				errors.Add(target.Rebase(error));
		}

		if (errors is not null)
			return new ZodProblemDetailsResult(writer, writer.Create(CollectionsMarshal.AsSpan(errors), httpContext));

		return await next(invocation).ConfigureAwait(false);
	}

	internal static ZodProblemDetailsResult CreateBindingFailureResult(
		EndpointFilterInvocationContext invocation,
		ValidationTarget[] targets,
		ZodProblemDetailsWriter writer
	)
	{
		List<ValidationError> errors = [];
		foreach (var target in targets)
		{
			var argument = target.Index < invocation.Arguments.Count ? invocation.Arguments[target.Index] : null;
			if (argument is not null)
				continue;

			errors.Add(CreateBindingError(target.Name, target.IsBody));
		}

		if (errors.Count == 0)
		{
			errors.Add(
				ValidationError.Create(
					ZodIssueCategories.InvalidType,
					"One or more request values could not be bound.",
					ZodPath.Root,
					category: ZodIssueCategories.InvalidType
				)
			);
		}

		return new ZodProblemDetailsResult(
			writer,
			writer.Create(CollectionsMarshal.AsSpan(errors), invocation.HttpContext)
		);
	}

	static ValidationError CreateBindingError(string? name, bool isBody)
	{
		var path = name is null ? ZodPath.Root : ZodPath.Member(name);
		return isBody
			? ValidationError.Create(
				InvalidJsonCode,
				"The request body could not be read.",
				path,
				category: ZodIssueCategories.InvalidType
			)
			: ValidationError.Create(
				ZodIssueCategories.InvalidType,
				"The value could not be bound.",
				path,
				category: ZodIssueCategories.InvalidType
			);
	}

	static IZodUntypedValidator? ResolveUntyped(IZodSchemaFactory factory, Type parameterType, Type validatedType)
	{
		if (factory.TryGetUntypedValidator(parameterType, out var validator) && validator is not null)
			return validator;

		if (factory.TryGetUntypedValidator(validatedType, out validator) && validator is not null)
			return validator;

		return AdaptRegisteredValidator(factory, validatedType);
	}

	/// <summary>
	/// Adapts a validator registered without an untyped view (<see cref="IZodSchemaFactory.Register(Type, IZodSchemaValidator)"/>
	/// with a validator that does not implement <see cref="IZodUntypedValidator"/>, as assembly scanning does for
	/// validators generated before untyped support, or a custom factory that does not implement
	/// <see cref="IZodSchemaFactory.TryGetUntypedValidator"/>). Needs dynamic code, so it is skipped under Native AOT,
	/// where such a parameter is not validated (or fails the endpoint build with <c>ThrowIfNoValidator</c>). Every
	/// validator the current generator emits implements <see cref="IZodUntypedValidator"/>, and
	/// <see cref="IZodSchemaFactory.Register{T}(IZodSchemaValidator{T})"/> adds the untyped view itself, so neither path
	/// reaches this.
	/// </summary>
	/// <remarks>
	/// The reflection is limited to closing <see cref="Adapt{T}"/>, a private method with no
	/// <c>[DynamicallyAccessedMembers]</c> requirement on <c>T</c>, which is why the IL2060 suppression is safe.
	/// </remarks>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2060:MakeGenericMethod",
		Justification = "Adapt<T> has no DynamicallyAccessedMembers requirements on T."
	)]
	static IZodUntypedValidator? AdaptRegisteredValidator(IZodSchemaFactory factory, Type validatedType)
	{
		if (!RuntimeFeature.IsDynamicCodeSupported || validatedType.ContainsGenericParameters)
			return null;

		var adapt = typeof(ZodEndpointValidationFilterFactory)
			.GetMethod(nameof(Adapt), BindingFlags.NonPublic | BindingFlags.Static)!
			.MakeGenericMethod(validatedType);

		return adapt.Invoke(null, [factory]) as IZodUntypedValidator;
	}

	static ZodUntypedValidator<T>? Adapt<T>(IZodSchemaFactory factory) =>
		factory.Resolve<T>() is { } typed ? new ZodUntypedValidator<T>(typed) : null;

	static InvalidOperationException MissingValidator(MethodInfo method, ParameterInfo parameter, Type type) =>
		new(
			$"Endpoint handler '{DescribeHandler(method)}' parameter '{parameter.Name}' requires a ZodSharp validator "
				+ $"for '{type.FullName}', but none is registered with the IZodSchemaFactory. Register the type's "
				+ "generated validator (AddZodSharpAssembly, or IZodSchemaFactory.Register) or remove the "
				+ "requirement."
		);

	static string DescribeHandler(MethodInfo method) =>
		method.DeclaringType is { } declaringType ? $"{declaringType.FullName}.{method.Name}" : method.Name;

	static bool IsSkipped(ParameterInfo parameter, IServiceProviderIsService? isService)
	{
		var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
		if (
			type == typeof(HttpContext)
			|| type == typeof(HttpRequest)
			|| type == typeof(HttpResponse)
			|| type == typeof(ClaimsPrincipal)
			|| type == typeof(CancellationToken)
			|| type == typeof(IFormFile)
			|| type == typeof(IFormFileCollection)
			|| type == typeof(IFormCollection)
			|| type == typeof(Stream)
			|| type == typeof(PipeReader)
		)
		{
			return true;
		}

		var forced = false;
		foreach (var attribute in parameter.GetCustomAttributes(inherit: true))
		{
			switch (attribute)
			{
				case IFromServiceMetadata:
				case FromKeyedServicesAttribute:
					return true;
				case IFromBodyMetadata:
				case IFromRouteMetadata:
				case IFromQueryMetadata:
				case IFromHeaderMetadata:
				case IFromFormMetadata:
					forced = true;
					break;
				default:
					// Microsoft.Extensions.Validation's [SkipValidation], or an application's own, matched by name
					// so no package reference is needed.
					if (string.Equals(attribute.GetType().Name, "SkipValidationAttribute", StringComparison.Ordinal))
						return true;
					break;
			}
		}

		return !forced && isService?.IsService(parameter.ParameterType) == true;
	}

	static bool IsBody(ParameterInfo parameter, Type validatedType, IServiceProviderIsService? isService)
	{
		foreach (var attribute in parameter.GetCustomAttributes(inherit: true))
		{
			switch (attribute)
			{
				case IFromBodyMetadata:
					return true;
				case IFromRouteMetadata:
				case IFromQueryMetadata:
				case IFromHeaderMetadata:
				case IFromFormMetadata:
					return false;
				default:
					break;
			}
		}

		return !IsScalar(validatedType) && isService?.IsService(parameter.ParameterType) != true;
	}

	static bool IsSequence(Type type) =>
		type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type) && !IsDictionary(type);

	static bool IsDictionary(Type type) =>
		typeof(IDictionary).IsAssignableFrom(type)
		|| (
			type.IsGenericType
			&& (
				type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
				|| type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
			)
		);

	static bool IsScalar(Type type) =>
		type.IsPrimitive
		|| type.IsEnum
		|| type == typeof(string)
		|| type == typeof(decimal)
		|| type == typeof(Guid)
		|| type == typeof(DateTime)
		|| type == typeof(DateTimeOffset)
		|| type == typeof(DateOnly)
		|| type == typeof(TimeOnly)
		|| type == typeof(TimeSpan)
		|| type == typeof(Uri);

	/// <summary>
	/// A parameter selected for validation at build time.
	/// </summary>
	/// <param name="Index">The argument index.</param>
	/// <param name="Name">The parameter name.</param>
	/// <param name="Validator">The resolved validator.</param>
	/// <param name="PrefixAlways">
	/// Whether every error is prefixed with the parameter name (sequence parameters: <c>items[0].Name</c>).
	/// Otherwise only errors about the value itself (an empty path) are, matching Microsoft.Extensions.Validation.
	/// </param>
	/// <param name="IsBody">Whether the parameter is (or is inferred to be) bound from the body.</param>
	internal sealed record ValidationTarget(
		int Index,
		string Name,
		IZodUntypedValidator Validator,
		bool PrefixAlways,
		bool IsBody
	)
	{
		public ValidationError Rebase(in ValidationError error) =>
			PrefixAlways || error.FullPath.IsRoot ? error.WithPathPrefix(ZodPathSegment.Member(Name)) : error;
	}
}
