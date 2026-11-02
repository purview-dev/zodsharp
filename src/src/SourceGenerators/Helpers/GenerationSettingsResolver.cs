using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators.Helpers;

/// <summary>
/// Reads the assembly-level MSBuild switches that shape generation (<c>ZodSharpAutoGenerateOptionsValidators</c>,
/// <c>ZodSharpGenerateJsonSchemas</c>, <c>ZodSharpEnumSerialization</c>, <c>ZodSharpEnumNamingPolicy</c>) and
/// applies them to the collected schema descriptors.
/// </summary>
static class GenerationSettingsResolver
{
	internal readonly record struct AutoDetectSettings(
		bool AutoGenerate,
		EquatableArray<string> Suffixes,
		bool GenerateJsonSchemas,
		bool EnumAsString,
		EnumJsonNamingPolicy EnumNamingPolicy,
		bool AllowNulls,
		DateTimeOffsetSerialization DateTimeOffsetSerialization,
		DefaultImmutableArrayBehavior DefaultImmutableArrayBehavior = DefaultImmutableArrayBehavior.ValidationError
	);

	/// <summary>
	/// The assembly-level schema defaults declared by <c>[assembly: ZodSharpSchemaDefaults(...)]</c>. A null
	/// value defers to the corresponding MSBuild property.
	/// </summary>
	internal readonly record struct AssemblyDefaults(
		bool? AllowNulls,
		DateTimeOffsetSerialization? DateTimeOffsetSerialization,
		DefaultImmutableArrayBehavior? DefaultImmutableArrayBehavior = null
	);

	public static AutoDetectSettings Read(AnalyzerConfigOptionsProvider provider)
	{
		// Auto-detection is on by default; only an explicit "false" disables it.
		var autoGenerate =
			!provider.GlobalOptions.TryGetValue(
				PropertyLibrary.BuildPropertyKey(PropertyLibrary.AutoGenerateIValidateOptionsProperty),
				out var autoGenerateValue
			)
			|| !bool.TryParse(autoGenerateValue, out var autoGenerateEnabled)
			|| autoGenerateEnabled;

		var suffixes = ParseAutoGenerateIValidateOptionsSuffixes(
			provider.GlobalOptions.TryGetValue(
				PropertyLibrary.BuildPropertyKey(PropertyLibrary.AutoGenerateIValidateOptionsSuffixesProperty),
				out var suffixValue
			)
				? suffixValue
				: null
		);

		// JSON Schema export is on by default; the assembly-level switch disables it for every type.
		var generateJsonSchemas =
			!provider.GlobalOptions.TryGetValue(
				PropertyLibrary.BuildPropertyKey(PropertyLibrary.GenerateJsonSchemasProperty),
				out var generateJsonSchemasValue
			)
			|| !bool.TryParse(generateJsonSchemasValue, out var generateJsonSchemasEnabled)
			|| generateJsonSchemasEnabled;

		// Enums serialise as numbers by default; the assembly-level switch matches a global
		// JsonStringEnumConverter, and the naming policy matches its JsonNamingPolicy.
		var enumAsString = ReadEnumSerializationAsString(provider);
		var enumNamingPolicy = ReadEnumNamingPolicy(provider);

		// Null collection elements are rejected by default. The assembly-level switch opts in to accepting
		// them; only an explicit true enables it, so an unparsable value keeps the default.
		var allowNulls =
			provider.GlobalOptions.TryGetValue(
				PropertyLibrary.BuildPropertyKey(PropertyLibrary.AllowNullsProperty),
				out var allowNullsValue
			)
			&& bool.TryParse(allowNullsValue, out var allowNullsEnabled)
			&& allowNullsEnabled;

		// Offsets are preserved by default. Utc is the only opt-in value; anything else keeps the default.
		var dateTimeOffsetSerialization = ReadDateTimeOffsetSerialization(provider);

		return new(
			autoGenerate,
			suffixes,
			generateJsonSchemas,
			enumAsString,
			enumNamingPolicy,
			allowNulls,
			dateTimeOffsetSerialization,
			provider.GlobalOptions.TryGetValue(
				PropertyLibrary.BuildPropertyKey(PropertyLibrary.DefaultImmutableArrayBehaviorProperty),
				out var defaultArrayBehavior
			) && string.Equals(defaultArrayBehavior?.Trim(), "TreatAsEmpty", StringComparison.OrdinalIgnoreCase)
				? DefaultImmutableArrayBehavior.TreatAsEmpty
				: DefaultImmutableArrayBehavior.ValidationError
		);
	}

	/// <summary>
	/// Reads the assembly-level schema defaults from <c>[assembly: ZodSharpSchemaDefaults(...)]</c>. The
	/// source-level attribute overrides the <c>ZodSharpAllowNulls</c> MSBuild property, so a null value here
	/// defers to that property.
	/// </summary>
	/// <param name="compilation">The compilation whose assembly is inspected.</param>
	/// <returns>The declared defaults, with null for anything left unset.</returns>
	public static AssemblyDefaults ReadAssemblyDefaults(Compilation compilation)
	{
		foreach (var attribute in compilation.Assembly.GetAttributes())
		{
			if (!TypeLibrary.ZodSharp.ZodSharpSchemaDefaultsAttribute.Equals(attribute.AttributeClass))
				continue;

			var allowNulls = attribute.TryGetNamedArgument<bool>(
				AttributeGenHelper.AllowNullsPropertyName,
				out var allow
			)
				? allow
				: (bool?)null;

			DateTimeOffsetSerialization? dateTimeOffsetSerialization = null;
			DefaultImmutableArrayBehavior? defaultImmutableArrayBehavior = null;
			foreach (var argument in attribute.NamedArguments)
			{
				if (argument.Key == "DefaultImmutableArrayBehavior" && argument.Value.Value is int behavior)
					defaultImmutableArrayBehavior = (DefaultImmutableArrayBehavior)behavior;

				if (
					argument.Key == AttributeGenHelper.DateTimeOffsetSerializationPropertyName
					&& argument.Value.Value is int serialization
				)
				{
					dateTimeOffsetSerialization = (DateTimeOffsetSerialization)serialization;
				}
			}

			return new(allowNulls, dateTimeOffsetSerialization, defaultImmutableArrayBehavior);
		}

		return new(null, null);
	}

	static DateTimeOffsetSerialization ReadDateTimeOffsetSerialization(AnalyzerConfigOptionsProvider provider)
	{
		var value = provider.GlobalOptions.TryGetValue(
			PropertyLibrary.BuildPropertyKey(PropertyLibrary.DateTimeOffsetSerializationProperty),
			out var configured
		)
			? configured?.Trim()
			: null;

		return string.Equals(value, nameof(DateTimeOffsetSerialization.Utc), StringComparison.OrdinalIgnoreCase)
			? DateTimeOffsetSerialization.Utc
			: DateTimeOffsetSerialization.PreserveOffset;
	}

	static bool ReadEnumSerializationAsString(AnalyzerConfigOptionsProvider provider) =>
		provider.GlobalOptions.TryGetValue(
			PropertyLibrary.BuildPropertyKey(PropertyLibrary.EnumSerializationProperty),
			out var value
		) && string.Equals(value?.Trim(), "String", StringComparison.OrdinalIgnoreCase);

	static EnumJsonNamingPolicy ReadEnumNamingPolicy(AnalyzerConfigOptionsProvider provider) =>
		provider.GlobalOptions.TryGetValue(
			PropertyLibrary.BuildPropertyKey(PropertyLibrary.EnumNamingPolicyProperty),
			out var value
		) && string.Equals(value?.Trim(), "CamelCase", StringComparison.OrdinalIgnoreCase)
			? EnumJsonNamingPolicy.CamelCase
			: EnumJsonNamingPolicy.Unchanged;

	static EquatableArray<string> ParseAutoGenerateIValidateOptionsSuffixes(string? value)
	{
		var raw = string.IsNullOrWhiteSpace(value) ? "Options;Settings" : value!;
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (var segment in raw.Split(';', ','))
		{
			var suffix = segment.Trim();
			if (suffix.Length > 0)
				builder.Add(suffix);
		}

		return new(builder.ToImmutable());
	}

	public static SchemaGenerationModel Apply(
		SchemaGenerationModel model,
		AutoDetectSettings settings,
		AssemblyDefaults assemblyDefaults
	)
	{
		var builder = ImmutableArray.CreateBuilder<GeneratorResult<ZodSchemaDescriptor>>(model.ZodSchemas.Count);
		foreach (var result in model.ZodSchemas)
		{
			if (!result.ShouldProcess)
			{
				builder.Add(result);
				continue;
			}

			var schema = result.Value;
			var changed = false;

			// The assembly-level switch disables JSON Schema export for every type in the compilation.
			if (!settings.GenerateJsonSchemas && schema.GenerateJsonSchema)
			{
				schema = schema with { GenerateJsonSchema = false };
				changed = true;
			}

			// The assembly-level enum convention applies to every enum in the compilation (a global
			// JsonStringEnumConverter / naming policy is not visible to the generator).
			if (settings.EnumAsString != schema.EnumAsString)
			{
				schema = schema with { EnumAsString = settings.EnumAsString };
				changed = true;
			}

			if (settings.EnumNamingPolicy != schema.EnumNamingPolicy)
			{
				schema = schema with { EnumNamingPolicy = settings.EnumNamingPolicy };
				changed = true;
			}

			// Precedence, highest first: the type-level [DateTimeOffsetBehavior], the assembly-level
			// ZodSharpSchemaDefaults, the MSBuild property, then PreserveOffset.
			var dateTimeOffsetSerialization =
				schema.DateTimeOffsetSerialization
				?? assemblyDefaults.DateTimeOffsetSerialization
				?? settings.DateTimeOffsetSerialization;

			if (schema.DateTimeOffsetSerialization != dateTimeOffsetSerialization)
			{
				schema = schema with { DateTimeOffsetSerialization = dateTimeOffsetSerialization };
				changed = true;
			}

			if (
				settings.AutoGenerate
				&& settings.Suffixes.Count > 0
				&& schema.GenerateIValidateOptions is null
				&& schema.IsPrimary
				&& !schema.IsValueType
				&& settings.Suffixes.Any(suffix => schema.TargetType.Name.EndsWith(suffix, StringComparison.Ordinal))
			)
			{
				schema = schema with { GenerateIValidateOptions = true };
				changed = true;
			}

			var resolvedProperties = ResolveCollectionBehavior(schema.Properties, settings, assemblyDefaults);
			if (resolvedProperties is { } properties)
			{
				schema = schema with { Properties = properties };
				changed = true;
			}

			var resolvedDateTimeOffset = ResolveDateTimeOffsetSerialization(
				schema.Properties,
				dateTimeOffsetSerialization
			);
			if (resolvedDateTimeOffset is { } dateTimeOffsetProperties)
			{
				schema = schema with { Properties = dateTimeOffsetProperties };
				changed = true;
			}

			builder.Add(changed ? GeneratorResult<ZodSchemaDescriptor>.Create(schema) : result);
		}

		return model with
		{
			ZodSchemas = new(builder.ToImmutable()),
		};
	}

	/// <summary>
	/// Resolves each property's collection behavior overrides against the assembly and MSBuild defaults.
	/// </summary>
	/// <param name="properties">The schema's properties.</param>
	/// <param name="settings">The assembly-level MSBuild settings.</param>
	/// <param name="assemblyDefaults">The source-level assembly defaults.</param>
	/// <returns>The rewritten properties, or <see langword="null"/> when no property changed.</returns>
	static EquatableArray<GeneratorResult<ZodPropertyDescriptor>>? ResolveCollectionBehavior(
		EquatableArray<GeneratorResult<ZodPropertyDescriptor>> properties,
		AutoDetectSettings settings,
		AssemblyDefaults assemblyDefaults
	)
	{
		if (properties.Count == 0)
			return null;

		var changed = false;
		var builder = ImmutableArray.CreateBuilder<GeneratorResult<ZodPropertyDescriptor>>(properties.Count);
		foreach (var propertyResult in properties)
		{
			if (!propertyResult.ShouldProcess)
			{
				builder.Add(propertyResult);
				continue;
			}

			var property = propertyResult.Value;
			var resolved = property.AllowNulls ?? assemblyDefaults.AllowNulls ?? settings.AllowNulls;
			var arrayBehavior =
				property.DefaultImmutableArrayBehavior
				?? assemblyDefaults.DefaultImmutableArrayBehavior
				?? settings.DefaultImmutableArrayBehavior;
			if (property.AllowNulls == resolved && property.DefaultImmutableArrayBehavior == arrayBehavior)
			{
				builder.Add(propertyResult);
				continue;
			}

			builder.Add(
				GeneratorResult<ZodPropertyDescriptor>.Create(
					property with
					{
						AllowNulls = resolved,
						DefaultImmutableArrayBehavior = arrayBehavior,
					}
				)
			);
			changed = true;
		}

		return changed ? new(builder.ToImmutable()) : null;
	}

	/// <summary>
	/// Resolves each property's DateTimeOffset wire form against the containing type's resolved setting.
	/// </summary>
	/// <param name="properties">The schema's properties.</param>
	/// <param name="schemaSerialization">The type/assembly/MSBuild setting already resolved for the schema.</param>
	/// <returns>The rewritten properties, or <see langword="null"/> when no property changed.</returns>
	static EquatableArray<GeneratorResult<ZodPropertyDescriptor>>? ResolveDateTimeOffsetSerialization(
		EquatableArray<GeneratorResult<ZodPropertyDescriptor>> properties,
		DateTimeOffsetSerialization schemaSerialization
	)
	{
		if (properties.Count == 0)
			return null;

		var changed = false;
		var builder = ImmutableArray.CreateBuilder<GeneratorResult<ZodPropertyDescriptor>>(properties.Count);
		foreach (var propertyResult in properties)
		{
			if (!propertyResult.ShouldProcess)
			{
				builder.Add(propertyResult);
				continue;
			}

			var property = propertyResult.Value;
			var resolved = property.DateTimeOffsetSerialization ?? schemaSerialization;
			if (property.DateTimeOffsetSerialization == resolved)
			{
				builder.Add(propertyResult);
				continue;
			}

			builder.Add(
				GeneratorResult<ZodPropertyDescriptor>.Create(property with { DateTimeOffsetSerialization = resolved })
			);
			changed = true;
		}

		return changed ? new(builder.ToImmutable()) : null;
	}
}
