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
		EnumJsonNamingPolicy EnumNamingPolicy
	);

	public static AutoDetectSettings Read(AnalyzerConfigOptionsProvider provider)
	{
		// Auto-detection is on by default; only an explicit "false" disables it.
		var autoGenerate =
			!provider.GlobalOptions.TryGetValue(
				$"build_property:{PropertyLibrary.AutoGenerateIValidateOptionsProperty}",
				out var autoGenerateValue
			)
			|| !bool.TryParse(autoGenerateValue, out var autoGenerateEnabled)
			|| autoGenerateEnabled;

		var suffixes = ParseAutoGenerateIValidateOptionsSuffixes(
			provider.GlobalOptions.TryGetValue(
				$"build_property:{PropertyLibrary.AutoGenerateIValidateOptionsSuffixesProperty}",
				out var suffixValue
			)
				? suffixValue
				: null
		);

		// JSON Schema export is on by default; the assembly-level switch disables it for every type.
		var generateJsonSchemas =
			!provider.GlobalOptions.TryGetValue(
				$"build_property:{PropertyLibrary.GenerateJsonSchemasProperty}",
				out var generateJsonSchemasValue
			)
			|| !bool.TryParse(generateJsonSchemasValue, out var generateJsonSchemasEnabled)
			|| generateJsonSchemasEnabled;

		// Enums serialise as numbers by default; the assembly-level switch matches a global
		// JsonStringEnumConverter, and the naming policy matches its JsonNamingPolicy.
		var enumAsString = ReadEnumSerializationAsString(provider);
		var enumNamingPolicy = ReadEnumNamingPolicy(provider);

		return new(autoGenerate, suffixes, generateJsonSchemas, enumAsString, enumNamingPolicy);
	}

	static bool ReadEnumSerializationAsString(AnalyzerConfigOptionsProvider provider) =>
		provider.GlobalOptions.TryGetValue($"build_property:{PropertyLibrary.EnumSerializationProperty}", out var value)
		&& string.Equals(value?.Trim(), "String", StringComparison.OrdinalIgnoreCase);

	static EnumJsonNamingPolicy ReadEnumNamingPolicy(AnalyzerConfigOptionsProvider provider) =>
		provider.GlobalOptions.TryGetValue($"build_property:{PropertyLibrary.EnumNamingPolicyProperty}", out var value)
		&& string.Equals(value?.Trim(), "CamelCase", StringComparison.OrdinalIgnoreCase)
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

	public static SchemaGenerationModel Apply((SchemaGenerationModel Model, AutoDetectSettings Settings) input)
	{
		var (model, settings) = input;

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

			builder.Add(changed ? GeneratorResult<ZodSchemaDescriptor>.Create(schema) : result);
		}

		return model with
		{
			ZodSchemas = new(builder.ToImmutable()),
		};
	}
}
