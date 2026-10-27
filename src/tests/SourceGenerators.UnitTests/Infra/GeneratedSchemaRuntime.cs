using System.Collections.Immutable;
using System.Reflection;
using ZodSharp.Core;

namespace ZodSharp.SourceGenerators.Infra;

/// <summary>
/// Invokes generated schemas compiled into a test assembly, so behavioural tests read as plain calls.
/// </summary>
static class GeneratedSchemaRuntime
{
	public static object Create(Assembly assembly, string typeName, params (string Name, object? Value)[] properties)
	{
		var type = assembly.GetType(typeName, throwOnError: true)!;
		var instance = Activator.CreateInstance(type)!;
		foreach (var (name, value) in properties)
			type.GetProperty(name)!.SetValue(instance, value);

		return instance;
	}

	public static ImmutableArray<ValidationError> RunValidate(Assembly assembly, string schemaTypeName, object? value)
	{
		var result = GetStaticMethod(assembly, schemaTypeName, "Validate", 1).Invoke(null, [value])!;
		return GetErrors(result);
	}

	public static ImmutableArray<ValidationError> RunValidateMember(
		Assembly assembly,
		string schemaTypeName,
		object value,
		string memberName
	)
	{
		var result = GetStaticMethod(assembly, schemaTypeName, "ValidateMember", 2).Invoke(null, [value, memberName])!;
		return GetErrors(result);
	}

	public static async Task<ImmutableArray<ValidationError>> RunValidationAsync(
		Assembly assembly,
		string schemaTypeName,
		object? value,
		IServiceProvider? services = null,
		CancellationToken cancellationToken = default
	)
	{
		var pending = GetStaticMethod(assembly, schemaTypeName, "ValidateAsync", 3)
			.Invoke(null, [value, services, cancellationToken])!;
		var task = (Task)pending.GetType().GetMethod("AsTask")!.Invoke(pending, null)!;
		await task.ConfigureAwait(false);
		var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
		return GetErrors(result);
	}

	public static ImmutableArray<ValidationError> GetErrors(object result)
	{
		var errors = (ImmutableArray<ValidationError>)result.GetType().GetProperty("Errors")!.GetValue(result)!;
		return errors.IsDefault ? [] : errors;
	}

	public static MethodInfo GetStaticMethod(Assembly assembly, string typeName, string name, int parameterCount)
	{
		var type = assembly.GetType(typeName, throwOnError: true)!;
		return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(method => method.Name == name && method.GetParameters().Length == parameterCount);
	}
}
