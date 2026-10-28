using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	const string SegmentKindType = "global::ZodSharp.Core.ZodPathSegmentKind";

	/// <summary>
	/// Emits the <c>JsonNames</c> table: the explicit <c>[JsonPropertyName]</c> of each member, recorded at
	/// compile time so a host can render JSON-named paths without reflection (defect G3).
	/// </summary>
	static void GenerateJsonNamesClass(
		CodeWriter writer,
		ZodSchemaDescriptor schema,
		CancellationToken cancellationToken
	)
	{
		List<(string Member, string JsonName)> names = [];
		foreach (var property in schema.Properties)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (property.ShouldProcess && property.Value.JsonPropertyName is { Length: > 0 } jsonName)
				names.Add((property.Value.Name, jsonName));
		}

		writer.XmlSummary(
			$"The explicit JSON names ([JsonPropertyName]) of {CodeWriter.XmlSee(schema.TargetType.Name)}'s members."
		);
		using (
			writer.ClassScope(
				new TypeDeclarationOptions("JsonNames", TypeDeclarationAccessibility.Public) { IsStatic = true }
			)
		)
		{
			writer.XmlSummary(
				"Returns the explicit JSON name of a member, or null when it has none (a naming policy then applies)."
			);
			writer.XmlParam("memberName", "The C# member name.");
			writer.XmlReturn("The JSON name, or null.");

			if (names.Count == 0)
			{
				writer.Line("public static string? Get(string memberName) => null;");
			}
			else
			{
				using (writer.OpenBlockScope("public static string? Get(string memberName) => memberName switch"))
				{
					foreach (var (member, jsonName) in names)
						writer.Line($"{member.StringLiteral()} => {jsonName.StringLiteral()},");
					writer.Line("_ => null,");
				}

				// The switch expression is closed by the block scope; terminate the expression-bodied member.
				writer.Line(";");
			}
		}

		writer.NewLine();
	}

	/// <summary>
	/// Emits <c>TryResolveMember</c> (Change 3) and <c>ToJsonNamedPath</c>: reflection-free walks of an error
	/// path through the generated schemas, used by Blazor to find a field's container and by hosts that
	/// render JSON names.
	/// </summary>
	static void GenerateResolutionMethods(
		CodeWriter writer,
		SchemaGenerationOutputContext outputContext,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var schema = outputContext.ZodSchema;
		var targetType = schema.TargetType.AsTypeReference();
		List<ZodPropertyDescriptor> walked = [];
		foreach (var property in schema.Properties)
		{
			if ((property.ShouldProcess && HasNestedValidation(property.Value)) || IsCollection(property))
				walked.Add(property.Value);
		}

		writer.XmlSummary(
			"Resolves the object that owns the last member of an error path, without reflection. A path that ends",
			"in an index or key resolves to the owner of the collection member; a path that cannot be followed",
			"resolves to the nearest object reached."
		);
		writer.XmlParam("value", "The root value.");
		writer.XmlParam("path", "The error path, relative to the value.");
		writer.XmlParam("container", "The owning object.");
		writer.XmlParam("memberName", "The member name on the container; empty for a model-level error.");
		writer.XmlReturn("True when the full path was followed.");
		writer.Line(
			$"public static bool TryResolveMember({targetType} value, {ZodPathType} path, out object? container, out string memberName) => TryResolveMember(value, path, 0, out container, out memberName);"
		);
		writer.NewLine();

		writer.XmlSummary("Resolves an error path from a segment offset; see the overload without an offset.");
		writer.XmlParam("value", "The value the path is relative to at the offset.");
		writer.XmlParam("path", "The error path.");
		writer.XmlParam("offset", "The first segment that applies to the value.");
		writer.XmlParam("container", "The owning object.");
		writer.XmlParam("memberName", "The member name on the container.");
		writer.XmlReturn("True when the full path was followed.");
		writer.Line(
			"[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]"
		);
		using (
			writer.OpenBlockScope(
				$"public static bool TryResolveMember({targetType} value, {ZodPathType} path, int offset, out object? container, out string memberName)"
			)
		)
		{
			writer.Assignment("container", "value");
			writer.Assignment("memberName", "string.Empty");
			if (schema.TargetCanBeNull)
				writer.IfBlock("value is null", body => body.Return("false"));

			writer.IfBlock("offset >= path.Length", body => body.Return("true"));
			writer.Assignment("var", "segment", "path[offset]");
			writer.IfBlock($"segment.Kind != {SegmentKindType}.Member", body => body.Return("false"));
			writer.Assignment("memberName", "segment.Name!");
			writer.Assignment("var", "last", "offset + 1 >= path.Length");

			if (walked.Count > 0)
			{
				using (writer.OpenBlockScope("switch (memberName)"))
				{
					foreach (var property in walked)
					{
						writer.Line($"case {property.Name.StringLiteral()}:");
						using (writer.OpenBlockScope())
						{
							writer.IfBlock("last", body => body.Return("true"));
							WriteResolveMember(writer, property);
						}
					}
				}
			}

			writer.Return("last");
		}

		writer.NewLine();

		writer.XmlSummary(
			"Returns the path with every member segment renamed to its JSON name: the member's [JsonPropertyName],",
			"otherwise the naming policy (or the C# name when none is given). Index and key segments are unchanged."
		);
		writer.XmlParam("path", "The error path, relative to the target type.");
		writer.XmlParam("namingPolicy", "The naming policy for members without an explicit JSON name.");
		writer.XmlReturn("The renamed path.");
		using (
			writer.OpenBlockScope(
				$"public static {ZodPathType} ToJsonNamedPath({ZodPathType} path, global::System.Func<string, string>? namingPolicy = null)"
			)
		)
		{
			writer.IfBlock("path.IsRoot", body => body.Return("path"));
			writer.Assignment(
				"var",
				"segments",
				$"global::System.Collections.Immutable.ImmutableArray.CreateBuilder<{ZodPathSegmentType}>(path.Length)"
			);
			writer.MethodCall("AppendJsonNamedPath", "path", "0", "namingPolicy", "segments");
			writer.Return($"{ZodPathType}.From(segments.ToImmutable())");
		}

		writer.NewLine();

		writer.XmlSummary("Appends the JSON-named segments of a path from an offset; see ToJsonNamedPath.");
		writer.XmlParam("path", "The error path.");
		writer.XmlParam("offset", "The first segment that applies to the target type.");
		writer.XmlParam("namingPolicy", "The naming policy for members without an explicit JSON name.");
		writer.XmlParam("segments", "The segments being built.");
		writer.Line(
			"[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]"
		);
		using (
			writer.OpenBlockScope(
				$"public static void AppendJsonNamedPath({ZodPathType} path, int offset, global::System.Func<string, string>? namingPolicy, global::System.Collections.Immutable.ImmutableArray<{ZodPathSegmentType}>.Builder segments)"
			)
		)
		{
			writer.IfBlock("offset >= path.Length", body => body.Return());
			writer.Assignment("var", "segment", "path[offset]");
			writer.IfBlock($"segment.Kind != {SegmentKindType}.Member", body =>
			{
				body.MethodCall($"{GeneratedValidation}.AppendRemaining", "path", "offset", "namingPolicy", "segments");
				body.Return();
			});
			writer.Assignment("var", "name", "segment.Name!");
			writer.MethodCallOn(
				"segments",
				"Add",
				$"{ZodPathSegmentType}.Member(JsonNames.Get(name) ?? (namingPolicy is null ? name : namingPolicy(name)))"
			);

			var renamed = walked.Where(static property => GetNestedSchemaForNaming(property) is not null).ToList();
			if (renamed.Count > 0)
			{
				using (writer.OpenBlockScope("switch (name)"))
				{
					foreach (var property in renamed)
					{
						var nested = GetNestedSchemaForNaming(property)!.Value;
						writer.Line($"case {property.Name.StringLiteral()}:");
						using (writer.OpenBlockScope())
						{
							if (property.ValidationKind == PropertyValidationKind.Collection)
							{
								using (
									writer.IfBlockScope(
										$"offset + 1 < path.Length && path[offset + 1].Kind != {SegmentKindType}.Member"
									)
								)
								{
									writer.MethodCallOn("segments", "Add", "path[offset + 1]");
									writer.MethodCall(
										nested.StaticMember("AppendJsonNamedPath"),
										"path",
										"offset + 2",
										"namingPolicy",
										"segments"
									);
									writer.Return();
								}

								writer.Line("break;");
							}
							else
							{
								writer.MethodCall(
									nested.StaticMember("AppendJsonNamedPath"),
									"path",
									"offset + 1",
									"namingPolicy",
									"segments"
								);
								writer.Return();
							}
						}
					}
				}
			}

			writer.MethodCall(
				$"{GeneratedValidation}.AppendRemaining",
				"path",
				"offset + 1",
				"namingPolicy",
				"segments"
			);
		}

		writer.NewLine();
	}

	static bool IsCollection(GeneratorResult<ZodPropertyDescriptor> property) =>
		property.ShouldProcess && property.Value.ValidationKind == PropertyValidationKind.Collection;

	/// <summary>
	/// The nested schema a member's remaining path is renamed by, when it is a modern generated schema. A
	/// union or closed hierarchy cannot be resolved without a value, so its remaining segments only get the
	/// naming policy.
	/// </summary>
	static TypeIdentity? GetNestedSchemaForNaming(ZodPropertyDescriptor property) =>
		property.DispatchKind == DispatchKind.None
		&& property.NestedSchemaType is { } nested
		&& property.NestedSchemaIsModern
			? nested
			: null;

	static void WriteResolveMember(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var valueName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Value");

		if (property.DispatchKind != DispatchKind.None)
		{
			using (writer.OpenBlockScope($"switch ({DispatchValueExpression(property)})"))
			{
				for (var i = 0; i < property.DispatchCases.Count; i++)
				{
					var dispatchCase = property.DispatchCases[i];
					if (!dispatchCase.IsModern)
						continue;

					var caseName = CodeGenHelpers.GetLocalIdentifier(property.Name, $"Case{i}");
					writer.Line($"case {dispatchCase.CaseType.AsTypeReference()} {caseName}:");
					writer.Return(
						ReferenceOwned(
							dispatchCase.SchemaType.StaticMember(
								$"TryResolveMember({caseName}, path, offset + 1, out container, out memberName)"
							),
							property
						)
					);
				}
			}

			writer.Return("false");
			return;
		}

		if (property.ValidationKind == PropertyValidationKind.Collection)
		{
			// An error on an element itself (a null element) belongs to the collection member.
			writer.IfBlock("offset + 2 >= path.Length", body =>
				body.Return($"path[offset + 1].Kind != {SegmentKindType}.Member")
			);

			if (property.NestedSchemaType is not { } elementSchema || !property.NestedSchemaIsModern)
			{
				writer.Return("false");
				return;
			}

			var elementName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Element");
			var lookup = property.IsMap ? "TryGetEntryValue" : "TryGetElement";
			writer.IfBlock(
				$"!{GeneratedValidation}.{lookup}(value.{property.Name}, path[offset + 1], out var {elementName})",
				body => body.Return("false")
			);

			var elementExpression = elementName;
			if (property.ElementTypeIsNullableValueType)
			{
				writer.IfBlock($"{elementName} is not {{ }} {elementName}Value", body => body.Return("false"));
				elementExpression = $"{elementName}Value";
			}
			else if (property.ElementTypeCanBeNull)
			{
				writer.IfBlock($"{elementName} is null", body => body.Return("false"));
			}

			var elementCall = elementSchema.StaticMember(
				$"TryResolveMember({elementExpression}, path, offset + 2, out container, out memberName)"
			);
			writer.Return(
				property.ElementTypeIsNullableValueType || !property.ElementTypeCanBeNull
					? ReferenceOwned(elementCall, property)
					: elementCall
			);
			return;
		}

		if (property.NestedSchemaType is not { } schemaType || !property.NestedSchemaIsModern)
		{
			writer.Return("false");
			return;
		}

		if (property.IsNullableValueType)
		{
			writer.IfBlock($"value.{property.Name} is not {{ }} {valueName}", body => body.Return("false"));
		}
		else
		{
			writer.Assignment("var", valueName, $"value.{property.Name}");
			if (property.CanBeNull)
				writer.IfBlock($"{valueName} is null", body => body.Return("false"));
		}

		var call = schemaType.StaticMember(
			$"TryResolveMember({valueName}, path, offset + 1, out container, out memberName)"
		);
		writer.Return(property.IsNullableValueType || !property.CanBeNull ? ReferenceOwned(call, property) : call);
	}

	/// <summary>
	/// Wraps a nested resolution step whose value can be a struct, so its result is attributed to this object and
	/// member rather than to a boxed copy of the struct, which cannot own a field.
	/// </summary>
	static string ReferenceOwned(string nestedCall, ZodPropertyDescriptor property) =>
		$"{GeneratedValidation}.ResolveToReferenceOwner({nestedCall}, value, {property.Name.StringLiteral()}, ref container, ref memberName)";
}
