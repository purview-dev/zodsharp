using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ZodSharp.Core;
using ZodSharp.Schemas;

namespace ZodSharp.Blazor;

/// <summary>
/// Adapts runtime schemas to <see cref="IZodUntypedValidator"/> for <see cref="ZodValidator.Schema"/>.
/// </summary>
public static class ZodModelSchema
{
	const string ObjectModelRequirement =
		"Reads the form model and its nested models by reflection, which a trimmer cannot follow. Validate with a "
		+ "[ZodSchema] generated validator, or ZodModelSchema.For<TModel>(IZodSchema<TModel, TModel>), instead.";

	/// <summary>
	/// Validates a CLR form model with a schema of the model's own type.
	/// </summary>
	/// <typeparam name="TModel">The form model type.</typeparam>
	/// <param name="schema">The schema.</param>
	/// <returns>The validator to pass as <see cref="ZodValidator.Schema"/>.</returns>
	public static IZodUntypedValidator For<TModel>(IZodSchema<TModel, TModel> schema)
	{
		ArgumentNullException.ThrowIfNull(schema);
		return new ZodUntypedValidator<TModel>(new ZodSchemaValidator<TModel>(schema));
	}

	/// <summary>
	/// Validates a CLR form model with a runtime <c>Z.Object()</c> schema whose field names are the model's property
	/// names. The model's public properties are read into the key/value shape the object schema validates; nested
	/// objects are read the same way, so a nested <c>Z.Object()</c> field validates a nested model. Errors are placed on
	/// the CLR fields they name.
	/// </summary>
	/// <typeparam name="TModel">The form model type.</typeparam>
	/// <param name="schema">The object schema.</param>
	/// <returns>The validator to pass as <see cref="ZodValidator.Schema"/>.</returns>
	/// <remarks>
	/// <para>
	/// Collections are passed to the schema as they are (their elements are not converted), and there is no member
	/// validation, so a field change validates the whole model and keeps the changed field's errors.
	/// </para>
	/// <para>
	/// The model is read by reflection, including nested model types a trimmer cannot see, so this is annotated
	/// <see cref="RequiresUnreferencedCodeAttribute"/>. In a trimmed or WebAssembly-AOT application validate with a
	/// <c>[ZodSchema]</c> generated validator (or <see cref="For{TModel}(IZodSchema{TModel, TModel})"/>) instead.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode(ObjectModelRequirement)]
	public static IZodUntypedValidator For<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TModel
	>(ZodObject schema)
		where TModel : class
	{
		ArgumentNullException.ThrowIfNull(schema);
		return new ObjectModelValidator(typeof(TModel), schema);
	}

	[RequiresUnreferencedCode(ObjectModelRequirement)]
	sealed class ObjectModelValidator(Type modelType, ZodObject schema) : IZodUntypedValidator
	{
		static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesByType = new();

		public Type ValidatedType => modelType;

		public bool RequiresAsyncValidation => false;

		public ImmutableArray<ValidationError> Validate(object? value)
		{
			if (value is null)
				return [ZodIssues.NullValue(ZodTypeNames.Object)];

			var result = schema.Validate(Project(value, depth: 0));
			return result.IsSuccess ? [] : result.Errors;
		}

		public async ValueTask<ImmutableArray<ValidationError>> ValidateAsync(
			object? value,
			IServiceProvider? services,
			CancellationToken cancellationToken
		)
		{
			if (value is null)
				return [ZodIssues.NullValue(ZodTypeNames.Object)];

			var result = await schema.ValidateAsync(Project(value, depth: 0), cancellationToken).ConfigureAwait(false);
			return result.IsSuccess ? [] : result.Errors;
		}

		public bool IsValidatableMember(string memberName) => false;

		public ValueTask<ImmutableArray<ValidationError>> ValidateMemberAsync(
			object value,
			string memberName,
			IServiceProvider? services,
			CancellationToken cancellationToken
		) => new([]);

		public bool TryResolveMember(object value, ZodPath path, out object? container, out string memberName)
		{
			container = value;
			memberName = string.Empty;
			return false;
		}

		static Dictionary<string, object?> Project(object value, int depth)
		{
			var properties = PropertiesByType.GetOrAdd(value.GetType(), GetReadableProperties);
			Dictionary<string, object?> projected = new(properties.Length, StringComparer.Ordinal);
			foreach (var property in properties)
			{
				object? propertyValue;
				try
				{
					propertyValue = property.GetValue(value);
				}
				catch (Exception)
				{
					continue;
				}

				projected[property.Name] =
					depth < 16 && propertyValue is not null && IsComplex(propertyValue.GetType())
						? Project(propertyValue, depth + 1)
						: propertyValue;
			}

			return projected;
		}

		static bool IsComplex(Type type) =>
			!type.IsPrimitive
			&& !type.IsEnum
			&& !type.IsValueType
			&& type != typeof(string)
			&& type != typeof(Uri)
			&& !typeof(IEnumerable).IsAssignableFrom(type);

		static PropertyInfo[] GetReadableProperties(Type type) =>
			[
				.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(static property => property.CanRead && property.GetIndexParameters().Length == 0),
			];
	}
}
