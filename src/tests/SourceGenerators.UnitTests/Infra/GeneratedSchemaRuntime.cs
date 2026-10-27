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

	public static async Task<ImmutableArray<ValidationError>> RunMemberValidationAsync(
		Assembly assembly,
		string schemaTypeName,
		object value,
		string memberName,
		IServiceProvider? services = null,
		CancellationToken cancellationToken = default
	)
	{
		var pending = GetStaticMethod(assembly, schemaTypeName, "ValidateMemberAsync", 4)
			.Invoke(null, [value, memberName, services, cancellationToken])!;
		var task = (Task)pending.GetType().GetMethod("AsTask")!.Invoke(pending, null)!;
		await task.ConfigureAwait(false);
		var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
		return GetErrors(result);
	}

	public static bool RunTryResolveMember(
		Assembly assembly,
		string schemaTypeName,
		object value,
		ZodPath path,
		out object? container,
		out string memberName
	)
	{
		object?[] arguments = [value, path, null, null];
		var resolved = (bool)GetStaticMethod(assembly, schemaTypeName, "TryResolveMember", 4).Invoke(null, arguments)!;
		container = arguments[2];
		memberName = (string)arguments[3]!;
		return resolved;
	}

	public static T InvokeStatic<T>(Assembly assembly, string typeName, string name, params object?[] arguments) =>
		(T)GetStaticMethod(assembly, typeName, name, arguments.Length).Invoke(null, arguments)!;

	public static object? GetStaticField(Assembly assembly, string typeName, string name) =>
		assembly
			.GetType(typeName, throwOnError: true)!
			.GetField(name, BindingFlags.Public | BindingFlags.Static)!
			.GetValue(null);

	/// <summary>Unwraps the exception a reflected call threw.</summary>
	public static Exception? Unwrap(Action action)
	{
		try
		{
			action();
			return null;
		}
		catch (TargetInvocationException ex)
		{
			return ex.InnerException;
		}
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
