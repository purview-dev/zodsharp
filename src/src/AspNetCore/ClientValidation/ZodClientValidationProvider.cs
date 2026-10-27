#if NET11_0_OR_GREATER
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using ZodSharp.Core;

namespace ZodSharp.AspNetCore.ClientValidation;

/// <summary>
/// The <see cref="ClientValidationProvider"/> registered by <c>AddZodSharpClientValidation</c>: renders the rules of
/// the provider it decorates (by default ASP.NET Core's DataAnnotations provider) plus client rules for ZodSharp rule
/// attributes and registered runtime schemas, in the same <c>&lt;blazor-client-validation-data data-rules&gt;</c>
/// payload.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core has no public API to add rules to another provider's payload, so this provider renders the decorated
/// provider's fragment, reads its <c>data-rules</c> attribute and merges its own rules into the same JSON
/// (<c>{"fields":[{"name":…,"rules":[{"name":…,"message":…,"params":{…}}]}]}</c>). That format is pinned by contract
/// tests; a change to it in ASP.NET Core fails them rather than silently dropping rules.
/// </para>
/// <para>
/// Client rules only improve the user experience: the server always validates the posted form.
/// </para>
/// </remarks>
public sealed class ZodClientValidationProvider : ClientValidationProvider
{
	const string ElementName = "blazor-client-validation-data";
	const string RulesAttribute = "data-rules";

	static readonly ConcurrentDictionary<(Type Container, string Field), List<ZodClientRule>> AttributeRules = new();

	readonly ClientValidationProvider? _inner;
	readonly ZodClientValidationOptions _options;
	readonly IZodSchemaFactory? _factory;

	/// <summary>
	/// Creates the provider.
	/// </summary>
	/// <param name="inner">The provider whose rules are kept; <see langword="null"/> for ZodSharp rules only.</param>
	/// <param name="options">The options.</param>
	/// <param name="factory">The schema factory, used to tell which nested model types ZodSharp validates.</param>
	public ZodClientValidationProvider(
		ClientValidationProvider? inner,
		ZodClientValidationOptions options,
		IZodSchemaFactory? factory = null
	)
	{
		ArgumentNullException.ThrowIfNull(options);

		_inner = inner is ZodClientValidationProvider { _inner: var unwrapped } ? unwrapped : inner;
		_options = options;
		_factory = factory;
	}

	/// <summary>
	/// Gets the decorated provider.
	/// </summary>
	public ClientValidationProvider? Inner => _inner;

	/// <inheritdoc/>
	public override RenderFragment? RenderClientValidationRules(
		EditContext editContext,
		IReadOnlyDictionary<FieldIdentifier, string> renderedFields
	)
	{
		ArgumentNullException.ThrowIfNull(editContext);
		ArgumentNullException.ThrowIfNull(renderedFields);

		var inner = _inner?.RenderClientValidationRules(editContext, renderedFields);
		var own = CollectRules(editContext, renderedFields);
		if (own.Count == 0)
			return inner;

		var innerJson = inner is null ? null : ReadRules(inner);
		if (inner is not null && innerJson is null)
			return inner; // The decorated provider rendered something this provider does not understand: keep it.

		var json = Merge(innerJson, own);
		return builder =>
		{
			builder.OpenElement(0, ElementName);
			builder.AddAttribute(1, RulesAttribute, json);
			builder.CloseElement();
		};
	}

	List<(string Name, IReadOnlyList<ZodClientRule> Rules)> CollectRules(
		EditContext editContext,
		IReadOnlyDictionary<FieldIdentifier, string> renderedFields
	)
	{
		List<(string, IReadOnlyList<ZodClientRule>)> fields = [];
		var formType = editContext.Model.GetType();
		foreach (var (field, renderedName) in renderedFields)
		{
			if (string.IsNullOrEmpty(field.FieldName) || string.IsNullOrEmpty(renderedName))
				continue;

			var containerType = field.Model.GetType();
			List<ZodClientRule> rules = [];

			if (_options.Schemas.TryGetValue(containerType, out var schema))
			{
				if (schema.Properties?.TryGetValue(field.FieldName, out var property) == true)
					rules.AddRange(
						ZodClientValidationRules.FromJsonSchema(
							property,
							schema.Required?.Contains(field.FieldName, StringComparer.Ordinal) == true
						)
					);
			}
			else if (containerType == formType || IsZodValidated(containerType))
			{
				rules.AddRange(
					AttributeRules.GetOrAdd(
						(containerType, field.FieldName),
						static key => BuildRules(key.Container, key.Field)
					)
				);
			}

			if (rules.Count > 0)
				fields.Add((renderedName, rules));
		}

		return fields;
	}

	bool IsZodValidated(Type type) =>
		_factory is not null && _factory.TryGetUntypedValidator(type, out var validator) && validator is not null;

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:DynamicallyAccessedMembers",
		Justification = "Form model types are application types, preserved because they are rendered and bound; "
			+ "ASP.NET Core's own DataAnnotations client provider reflects over them the same way."
	)]
	static List<ZodClientRule> BuildRules(Type containerType, string fieldName)
	{
		var property = containerType.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
		if (property is null)
			return [];

		List<ZodClientRule> rules = [];
		foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>(inherit: true))
		{
			// DataAnnotations attributes and attributes that describe their own client rules belong to the decorated
			// provider; emitting them here would duplicate them.
			if (attribute is IClientValidationRuleProvider || IsDataAnnotationsAttribute(attribute))
				continue;

			rules.AddRange(ZodClientValidationRules.FromAttribute(attribute));
		}

		return rules;
	}

	static bool IsDataAnnotationsAttribute(ValidationAttribute attribute) =>
		attribute.GetType().Assembly == typeof(ValidationAttribute).Assembly;

	/// <summary>
	/// Reads the <c>data-rules</c> JSON from a provider's fragment by rendering it into a scratch builder.
	/// </summary>
	[SuppressMessage(
		"Usage",
		"BL0006:Do not use RenderTree types",
		Justification = "The only public way to read another provider's rendered payload."
	)]
	static string? ReadRules(RenderFragment fragment)
	{
		using RenderTreeBuilder builder = new();
		fragment(builder);
		var frames = builder.GetFrames();
		for (var i = 0; i < frames.Count; i++)
		{
			ref var frame = ref frames.Array[i];
			if (
				frame.FrameType == RenderTreeFrameType.Attribute
				&& string.Equals(frame.AttributeName, RulesAttribute, StringComparison.Ordinal)
			)
				return frame.AttributeValue as string;
		}

		return null;
	}

	static string Merge(string? innerJson, List<(string Name, IReadOnlyList<ZodClientRule> Rules)> own)
	{
		ArrayBufferWriter<byte> buffer = new(512);
		using (Utf8JsonWriter writer = new(buffer))
		{
			writer.WriteStartObject();
			writer.WritePropertyName("fields");
			writer.WriteStartArray();

			HashSet<string> written = new(StringComparer.Ordinal);
			if (innerJson is not null)
			{
				using var document = JsonDocument.Parse(innerJson);
				if (
					document.RootElement.TryGetProperty("fields", out var fields)
					&& fields.ValueKind == JsonValueKind.Array
				)
				{
					foreach (var field in fields.EnumerateArray())
					{
						var name = field.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
						if (name is null)
						{
							field.WriteTo(writer);
							continue;
						}

						written.Add(name);
						writer.WriteStartObject();
						writer.WriteString("name", name);
						writer.WritePropertyName("rules");
						writer.WriteStartArray();
						if (field.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
						{
							foreach (var rule in rules.EnumerateArray())
								rule.WriteTo(writer);
						}

						foreach (var (ownName, ownRules) in own)
						{
							if (string.Equals(ownName, name, StringComparison.Ordinal))
								WriteRules(writer, ownRules);
						}

						writer.WriteEndArray();
						writer.WriteEndObject();
					}
				}
			}

			foreach (var (name, rules) in own)
			{
				if (!written.Add(name))
					continue;

				writer.WriteStartObject();
				writer.WriteString("name", name);
				writer.WritePropertyName("rules");
				writer.WriteStartArray();
				WriteRules(writer, rules);
				writer.WriteEndArray();
				writer.WriteEndObject();
			}

			writer.WriteEndArray();
			writer.WriteEndObject();
		}

		return Encoding.UTF8.GetString(buffer.WrittenSpan);
	}

	static void WriteRules(Utf8JsonWriter writer, IReadOnlyList<ZodClientRule> rules)
	{
		foreach (var rule in rules)
		{
			writer.WriteStartObject();
			writer.WriteString("name", rule.Name);
			writer.WriteString("message", rule.Message);
			if (rule.Parameters.Count > 0)
			{
				writer.WritePropertyName("params");
				writer.WriteStartObject();
				foreach (var (key, value) in rule.Parameters)
					writer.WriteString(key, value);

				writer.WriteEndObject();
			}

			writer.WriteEndObject();
		}
	}
}
#endif
