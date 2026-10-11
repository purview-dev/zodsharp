using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ZodSharp.JsonSchema.NewtonsoftJson;

/// <summary>
/// Names the JSON Schema keyword properties with their specification names (<c>$schema</c>, <c>$id</c>,
/// <c>$ref</c> and <c>$defs</c>) and camelCase for every other member.
/// </summary>
sealed class JsonSchemaNamingStrategy : CamelCaseNamingStrategy
{
	static readonly Dictionary<string, string> KeywordNames = new(StringComparer.Ordinal)
	{
		["Schema"] = "$schema",
		["Id"] = "$id",
		["Ref"] = "$ref",
		["Defs"] = "$defs",
		["UnsupportedRules"] = "x-zodsharp-unsupported",
	};

	public override string GetPropertyName(string name, bool hasSpecifiedName) =>
		KeywordNames.TryGetValue(name, out var keywordName)
			? keywordName
			: base.GetPropertyName(name, hasSpecifiedName);
}

/// <summary>
/// Options for JSON serialization/deserialization of JSON Schema.
/// </summary>
public static class JsonSchemaSerializerOptions
{
	/// <summary>
	/// Default settings for JSON Schema serialization.
	/// Writes the JSON Schema keyword names (<c>$schema</c>, <c>$id</c>, <c>$ref</c>, <c>$defs</c>) and
	/// camelCase for every other member, and ignores null values.
	/// </summary>
	public static readonly JsonSerializerSettings Default = new()
	{
		NullValueHandling = NullValueHandling.Ignore,
		Formatting = Formatting.Indented,
		MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
		ContractResolver = new DefaultContractResolver { NamingStrategy = new JsonSchemaNamingStrategy() },
	};

	/// <summary>
	/// Settings for reading JSON Schema with flexible property matching.
	/// </summary>
	public static readonly JsonSerializerSettings Reading = new()
	{
		NullValueHandling = NullValueHandling.Ignore,
		MissingMemberHandling = MissingMemberHandling.Ignore,
		MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
		ContractResolver = new DefaultContractResolver { NamingStrategy = new JsonSchemaNamingStrategy() },
	};
}
