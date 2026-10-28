using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Helpers;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators;

partial class ZodSchemaGenerator
{
	const string ErrorsListType = "global::System.Collections.Generic.List<global::ZodSharp.Core.ValidationError>";

	const string CustomValidatorFieldName = "CustomValidator";

	const string CustomValidationBridgeName = "InvokeZodCustomValidationAsync";

	static TypeReference ErrorsListReference(CodeWriter writer) =>
		TypeLibrary
			.System.Collections.Generic.List.MakeGeneric(TypeLibrary.ZodSharp.Core.ValidationError)
			.AsTypeReference()
			.Nullable(writer);

	/// <summary>
	/// Emits the public <c>Paths</c> class: one <c>ZodPath</c> constant per member, so refinement hooks and
	/// hosts can place or match an issue without hand-writing member names (defect G13).
	/// </summary>
	static void GeneratePathsClass(CodeWriter writer, ZodSchemaDescriptor schema, CancellationToken cancellationToken)
	{
		writer.XmlSummary(
			$"The canonical error paths of {CodeWriter.XmlSee(schema.TargetType.Name)}'s members.",
			"Use them to place an issue on a member (RefineCtx.AddIssue) or to match an error's FullPath."
		);

		using (
			writer.ClassScope(
				new TypeDeclarationOptions("Paths", TypeDeclarationAccessibility.Public) { IsStatic = true }
			)
		)
		{
			TypeReference zodPathReference = new(
				new TypeIdentity("ZodPath", TypeLibraryGenerator.ZodSharpCoreNamespace)
			);

			foreach (var property in schema.Properties)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (!property.ShouldProcess)
					continue;

				var name = property.Value.Name;
				writer.XmlSummary($"The path of {CodeWriter.XmlSee(name)}.");
				if (IsInheritedStaticMemberName(name))
				{
					// FieldDeclarationOptions has no `new` modifier, and this member must hide object.Equals
					// or object.GetType, so the structured API cannot express it.
#pragma warning disable PSGFR18 // Prefer a structured CodeWriter declaration API
					writer.Line(
						$"public static new readonly {ZodPathType} {GetPathMemberName(name)} = {ZodPathType}.Member({name.StringLiteral()});"
					);
#pragma warning restore PSGFR18
				}
				else
				{
					writer.Field(
						new FieldDeclarationOptions(GetPathMemberName(name), zodPathReference)
						{
							Accessibility = TypeDeclarationAccessibility.Public,
							IsStatic = true,
							IsReadOnly = true,
							Initializer = $"{ZodPathType}.Member({name.StringLiteral()})",
						}
					);
				}
			}
		}

		writer.NewLine();
	}

	/// <summary>
	/// Whether a static field named <paramref name="name"/> hides a member inherited from
	/// <see cref="object"/> (CS0108), so it needs the <c>new</c> modifier.
	/// </summary>
	static bool IsInheritedStaticMemberName(string name) =>
		name is "Equals" or "ReferenceEquals" or "GetHashCode" or "GetType" or "ToString" or "MemberwiseClone";

	/// <summary>
	/// Emits the member-info fields (declaring type, member, display name and path) read by the error
	/// factories, the cached async attribute instances, and the custom validator instance when the custom
	/// method lives on the adapter.
	/// </summary>
	static void GenerateMemberFields(
		CodeWriter writer,
		SchemaGenerationOutputContext outputContext,
		CancellationToken cancellationToken
	)
	{
		var schema = outputContext.ZodSchema;
		TypeReference memberTypeReference = new(
			new TypeIdentity("ZodGeneratedMember", TypeLibraryGenerator.ZodSharpCoreNamespace)
		);
		var declaringType = GetDeclaringTypeName(schema);

		writer.Field(
			new FieldDeclarationOptions(TypeMemberFieldName, memberTypeReference)
			{
				Accessibility = TypeDeclarationAccessibility.Private,
				IsStatic = true,
				IsReadOnly = true,
				Initializer =
					$"new({declaringType.StringLiteral()}, \"\", {schema.TargetType.Name.StringLiteral()}, {ZodPathType}.Root)",
			}
		);

		foreach (var propertyResult in schema.Properties)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!propertyResult.ShouldProcess)
				continue;

			var property = propertyResult.Value;
			if (!HasMemberRules(property, schema.ValidateEnumValues))
				continue;

			var resolver = property.DisplayNameResourceExpression is { } resource
				? $", static () => {resource}"
				: string.Empty;
			writer.Field(
				new FieldDeclarationOptions(MemberOf(property), memberTypeReference)
				{
					Accessibility = TypeDeclarationAccessibility.Private,
					IsStatic = true,
					IsReadOnly = true,
					Initializer =
						$"new({declaringType.StringLiteral()}, {property.Name.StringLiteral()}, {property.DisplayName.StringLiteral()}, {PathOf(property)}{resolver})",
				}
			);

			for (var i = 0; i < property.AsyncValidationAttributes.Count; i++)
			{
				writer.Field(
					new FieldDeclarationOptions(
						CodeGenHelpers.GetAsyncAttributeFieldName(property.Name, i),
						new TypeIdentity("AsyncValidationAttribute", "System.ComponentModel.DataAnnotations")
					)
					{
						Accessibility = TypeDeclarationAccessibility.Private,
						IsStatic = true,
						IsReadOnly = true,
						Initializer = property.AsyncValidationAttributes[i],
					}
				);
			}
		}

		if (
			schema.CustomValidationMethod.Value.InvocationKind
			== CustomValidationInvocationKind.DefinedOnSchemaValidator
		)
			writer.Field(
				new FieldDeclarationOptions(
					CustomValidatorFieldName,
					new TypeIdentity($"{schema.SchemaType.Name}Validator", null).AsTypeReference().Nullable(writer)
				)
				{
					Accessibility = TypeDeclarationAccessibility.Private,
					IsStatic = true,
				}
			);

		writer.NewLine();
	}

	static string GetDeclaringTypeName(ZodSchemaDescriptor schema)
	{
		var rendered = schema.TargetType.AsTypeReference().ToString();
		return rendered.StartsWith("global::", StringComparison.Ordinal) ? rendered.Substring(8) : rendered;
	}

	/// <summary>
	/// Whether the member's own rules (not counting nested recursion) or nested recursion produce anything,
	/// so it gets a per-member method and is reported by <c>IsValidatableMember</c>.
	/// </summary>
	static bool HasMemberRules(ZodPropertyDescriptor property, bool validateEnumValues)
	{
		var attributes = property.ValidationAttributes;

		if (property.CanBeNull && Exists(attributes.Required, static required => required.Exists))
			return true;

		if (property.CanBeNull && property.CustomRules.Any(static rule => rule.IsRequired))
			return true;

		if (!property.IsDeclaredNullable && property.CanBeNull)
			return true;

		// The six-part condition is the final branch of a chain of early returns; a conditional expression
		// would bury it under the checks above.
#pragma warning disable IDE0046 // Convert to conditional expression
		if (
			Exists(attributes.AllowedValues, static values => values.Exists)
			|| Exists(attributes.DeniedValues, static values => values.Exists)
			|| Exists(attributes.Compare, static compare => compare.Exists)
			|| property.CustomRules.Count > 0
			|| property.AsyncValidationAttributes.Count > 0
			|| HasNestedValidation(property)
		)
		{
			return true;
		}
#pragma warning restore IDE0046

		return HasKindRules(property, validateEnumValues);
	}

	static bool HasKindRules(ZodPropertyDescriptor property, bool validateEnumValues)
	{
		var attributes = property.ValidationAttributes;
		return property.ValidationKind switch
		{
			PropertyValidationKind.String => HasStringRules(attributes),
			PropertyValidationKind.Numeric or PropertyValidationKind.Comparable => Exists(
				attributes.Range,
				static range =>
					range.Exists
					&& !string.IsNullOrEmpty(range.MinimumExpression)
					&& !string.IsNullOrEmpty(range.MaximumExpression)
			),
			PropertyValidationKind.Collection => (
				property.LengthAccessor.IsSupported
				&& (
					Exists(attributes.Length, static length => length.Exists)
					|| Exists(attributes.MinLength, static length => length.Exists && length.Length > 0)
					|| Exists(attributes.MaxLength, static length => length.Exists && length.Length >= 0)
				)
			) || RejectsNullItems(property),
			PropertyValidationKind.Enum => IsEnumValidationEmitted(property, validateEnumValues),
			PropertyValidationKind.Complex or PropertyValidationKind.Unsupported => false,
			_ => false,
		};
	}

	static bool HasStringRules(ValidationAttributes attributes) =>
		Exists(attributes.EmailAddress, static email => email.Exists)
		|| Exists(attributes.RegularExpression, static regex => regex.Exists)
		|| Exists(attributes.Url, static url => url.Exists)
		|| Exists(attributes.Phone, static phone => phone.Exists)
		|| Exists(attributes.CreditCard, static card => card.Exists)
		|| Exists(attributes.Base64String, static base64 => base64.Exists)
		|| Exists(attributes.Length, static length => length.Exists)
		|| Exists(attributes.StringLength, static length => length.Exists)
		|| Exists(attributes.MinLength, static length => length.Exists && length.Length > 0)
		|| Exists(attributes.MaxLength, static length => length.Exists && length.Length >= 0);

	static bool Exists<T>(GeneratorResult<T> result, Func<T, bool> exists)
		where T : struct => result.ShouldProcess && exists(result.Value);

	/// <summary>Whether the member recurses into another schema (nested, element, or dispatched case).</summary>
	static bool HasNestedValidation(ZodPropertyDescriptor property) =>
		(
			property.NestedSchemaType is not null
			&& property.ValidationKind is PropertyValidationKind.Complex or PropertyValidationKind.Collection
		)
		|| property.DispatchKind != DispatchKind.None;

	static IEnumerable<ZodPropertyDescriptor> GetValidatedMembers(ZodSchemaDescriptor schema)
	{
		foreach (var property in schema.Properties)
		{
			if (property.ShouldProcess && HasMemberRules(property.Value, schema.ValidateEnumValues))
				yield return property.Value;
		}
	}

	/// <summary>Whether the synchronous <c>Validate</c> cannot run: the type has async-only rules (defect G23).</summary>
	static bool HasAsyncOnlyRules(ZodSchemaDescriptor schema) =>
		schema.ImplementsAsyncValidatableObject
		|| GetValidatedMembers(schema).Any(static property => property.AsyncValidationAttributes.Count > 0);

	static bool HasTypeValidation(ZodSchemaDescriptor schema) =>
		schema.TypeRules.Count > 0
		|| (schema.ImplementsValidatableObject && !schema.ImplementsAsyncValidatableObject)
		|| schema.RefinementHook.Value.IsImplemented;

	static void GenerateValidateMethods(
		CodeWriter writer,
		SchemaGenerationOutputContext outputContext,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var schema = outputContext.ZodSchema;
		var targetType = schema.TargetType.AsTypeReference();
		var resultType = TypeLibrary.ZodSharp.Core.ValidationResult.MakeGeneric(schema.TargetType).AsTypeReference();
		var resultTaskReference = TypeLibraryGenerator
			.ValueTask.MakeGeneric(TypeLibrary.ZodSharp.Core.ValidationResult.MakeGeneric(schema.TargetType))
			.AsTypeReference();
		var serviceProviderReference = new TypeIdentity(typeof(IServiceProvider)).AsTypeReference();
		var requiresAsync = outputContext.RequiresAsync(schema);

		writer.XmlSummary(
			"Whether some rules only run asynchronously (a custom asynchronous method, an AsyncValidationAttribute, an",
			"IAsyncValidatableObject target, or a nested schema that has them), computed when the schema is generated."
		);
		writer.Field(
			new FieldDeclarationOptions("RequiresAsyncValidation", PurviewTypeLibrary.System.Boolean)
			{
				Accessibility = TypeDeclarationAccessibility.Public,
				IsConst = true,
				Initializer = requiresAsync ? "true" : "false",
			}
		);
		writer.NewLine();

		writer.XmlSummary("Validates an instance of the target type.");
		writer.XmlParam("value", "The value to validate.");
		writer.XmlReturn("The validation result.");

		// CA1849 would report every call to this method from async code, because ValidateAsync exists. ZodSharp's
		// analyzer suppresses that for this method, so the remark states which call the type actually needs.
		if (requiresAsync)
		{
			writer.XmlRemarks(
				"Some rules in this graph only run asynchronously (RequiresAsyncValidation is true). Call ValidateAsync, which",
				"runs them; a synchronous call cannot."
			);
		}
		else
		{
			writer.XmlRemarks(
				"Every rule in this graph is synchronous (RequiresAsyncValidation is false), so this call never blocks,",
				"and CA1849 is suppressed for it. Call ValidateAsync only when you need the asynchronous pipeline."
			);
		}

		writer.MethodExpression(
			"Validate",
			resultType,
			TypeDeclarationAccessibility.Public,
			static expression => expression.Write("ValidateCore(value, 0)"),
			declaration => declaration with { IsStatic = true, Parameters = [new("value", targetType)] }
		);
		writer.NewLine();

		writer.XmlSummary(
			"Validates an instance of the target type at a nesting depth; nested schemas call this so a cyclic",
			"graph fails with InvalidOperationException at ZodValidationDepth.MaxDepth instead of overflowing the stack."
		);
		writer.XmlParam("value", "The value to validate.");
		writer.XmlParam("depth", "The nesting depth of the value (the root is 0).");
		writer.XmlReturn("The validation result.");
		writer.Line(
			"[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]"
		);
		using (writer.OpenBlockScope($"public static {resultType} ValidateCore({targetType} value, int depth)"))
		{
			WriteValidatePrologue(writer, schema, resultType);

			if (HasAsyncOnlyRules(schema))
			{
				// Defect G23/Change 8: async-only rules must not be skipped silently by a synchronous caller.
				writer.MethodCall($"{GeneratedValidation}.ThrowAsyncValidationRequired", $"typeof({targetType})");
			}

			writer.Assignment(ErrorsListReference(writer), "errors", "null");

			foreach (var property in GetValidatedMembers(schema))
				writer.MethodCall(CodeGenHelpers.GetMemberMethodName(property.Name), MemberArguments(property, "true"));

			if (HasTypeValidation(schema))
				writer.MethodCall("ValidateType", "value", "ref errors", "services: null");

			writer.Return($"{GeneratedValidation}.ToResult(value, errors)");
		}

		writer.NewLine();

		var resultTaskType = $"global::System.Threading.Tasks.ValueTask<{resultType}>";
		var customKind = schema.CustomValidationMethod.Value.InvocationKind;
		var adapterParameter =
			customKind == CustomValidationInvocationKind.DefinedOnSchemaValidator
				? $", {schema.SchemaType.Name}Validator? validator"
				: string.Empty;

		writer.XmlSummary(
			"Validates an instance of the target type asynchronously: nested schemas are awaited, and asynchronous and",
			"service-aware rules run. Completes synchronously when no rule in the graph is asynchronous."
		);
		writer.XmlParam("value", "The value to validate.");
		writer.XmlParam("services", "The caller's services, for service-aware rules.");
		writer.XmlParam("cancellationToken", "The cancellation token.");
		writer.XmlReturn("The validation result.");
		writer.MethodExpression(
			"ValidateAsync",
			resultTaskReference,
			TypeDeclarationAccessibility.Public,
			static expression => expression.Write("ValidateCoreAsync(value, services, 0, cancellationToken)"),
			declaration =>
				declaration with
				{
					IsStatic = true,
					Parameters =
					[
						new("value", targetType),
						new("services", serviceProviderReference.Nullable(writer)) { DefaultValue = "null" },
						new("cancellationToken", TypeLibraryGenerator.CancellationToken.AsTypeReference())
						{
							DefaultValue = "default",
						},
					],
				}
		);
		writer.NewLine();

		writer.XmlSummary("Validates an instance asynchronously at a nesting depth; see ValidateCore.");
		writer.XmlParam("value", "The value to validate.");
		writer.XmlParam("services", "The caller's services, for service-aware rules.");
		writer.XmlParam("depth", "The nesting depth of the value (the root is 0).");
		writer.XmlParam("cancellationToken", "The cancellation token.");
		writer.XmlReturn("The validation result.");
		writer.Line(
			"[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]"
		);

		if (!requiresAsync)
		{
			writer.MethodExpression(
				"ValidateCoreAsync",
				resultTaskReference,
				TypeDeclarationAccessibility.Public,
				static expression => expression.Write("new(ValidateCore(value, depth))"),
				declaration =>
					declaration with
					{
						IsStatic = true,
						Parameters =
						[
							new("value", targetType),
							new("services", serviceProviderReference.Nullable(writer)),
							new("depth", PurviewTypeLibrary.System.Int32),
							new("cancellationToken", TypeLibraryGenerator.CancellationToken.AsTypeReference()),
						],
					}
			);
			writer.NewLine();
		}
		else
		{
			if (adapterParameter.Length > 0)
			{
				writer.MethodExpression(
					"ValidateCoreAsync",
					resultTaskReference,
					TypeDeclarationAccessibility.Public,
					static expression =>
						expression.Write("ValidateCoreAsync(value, services, depth, cancellationToken, null)"),
					declaration =>
						declaration with
						{
							IsStatic = true,
							Parameters =
							[
								new("value", targetType),
								new("services", serviceProviderReference.Nullable(writer)),
								new("depth", PurviewTypeLibrary.System.Int32),
								new("cancellationToken", TypeLibraryGenerator.CancellationToken.AsTypeReference()),
							],
						}
				);
				writer.NewLine();
			}

			var accessibility = adapterParameter.Length > 0 ? "internal" : "public";
			using (
				writer.OpenBlockScope(
					$"{accessibility} static async {resultTaskType} ValidateCoreAsync({targetType} value, {ServiceProviderType}? services, int depth, {CancellationTokenType} cancellationToken{adapterParameter})"
				)
			)
			{
				WriteValidatePrologue(writer, schema, resultType);
				writer.MethodCall("cancellationToken.ThrowIfCancellationRequested");
				writer.Assignment(ErrorsListReference(writer), "errors", "null");

				foreach (var property in GetValidatedMembers(schema))
				{
					var nestedAsync = outputContext.NestedRequiresAsync(property);
					writer.MethodCall(
						CodeGenHelpers.GetMemberMethodName(property.Name),
						MemberArguments(property, nestedAsync ? "false" : "true")
					);

					if (nestedAsync || property.AsyncValidationAttributes.Count > 0)
					{
						writer.Assignment(
							"errors",
							$"await {CodeGenHelpers.GetMemberAsyncMethodName(property.Name)}(value, errors, services, depth, {(nestedAsync ? "true" : "false")}, cancellationToken).ConfigureAwait(false)"
						);
					}
				}

				if (HasTypeValidation(schema))
					writer.MethodCall("ValidateType", "value", "ref errors", "services");

				if (schema.ImplementsAsyncValidatableObject)
					WriteAsyncValidatableObject(writer, schema);

				if (schema.CustomValidationMethod.Value.HasCustomValidation)
				{
					writer.Assignment(
						"var",
						"customResult",
						$"await {CustomMethodCall(schema, "value")}.ConfigureAwait(false)"
					);
					writer.Assignment("errors", $"{GeneratedValidation}.WithResult(errors, customResult)");
				}

				writer.Return($"{GeneratedValidation}.ToResult(value, errors)");
			}

			writer.NewLine();
		}

		if (HasTypeValidation(schema))
			GenerateValidateTypeMethod(writer, schema);
	}

	static void WriteValidatePrologue(CodeWriter writer, ZodSchemaDescriptor schema, TypeReference resultType)
	{
		if (schema.TargetCanBeNull)
		{
			writer.IfBlock("value is null", ifBody =>
				ifBody.Return(
					$"{resultType}.Failure({GeneratedValidation}.ForMember(global::ZodSharp.Core.ZodIssues.NullValue(global::ZodSharp.Core.ZodTypeNames.Object, EmptyPath), {TypeMemberFieldName}))"
				)
			);
			writer.NewLine();
		}

		// Defect G24/Change 5: the same depth limit Microsoft.Extensions.Validation enforces.
		writer.MethodCall(
			"global::ZodSharp.Core.ZodValidationDepth.ThrowIfExceeded",
			"depth",
			$"typeof({schema.TargetType.AsTypeReference()})"
		);
	}

	/// <summary>
	/// The custom async method call: a static method on the model, or an instance method on the adapter
	/// (the calling adapter when there is one, otherwise a cached instance). A service-aware method is handed
	/// the caller's services and fails clearly when there are none.
	/// </summary>
	static string CustomMethodCall(ZodSchemaDescriptor schema, string valueExpression)
	{
		var custom = schema.CustomValidationMethod.Value;
		var services = custom.RequiresServices
			? $"services ?? throw new global::System.InvalidOperationException(\"'{GetDeclaringTypeName(schema)}' has a service-aware validation method that needs an IServiceProvider. Call ValidateAsync with the caller's services.\"), "
			: string.Empty;
		var receiver =
			custom.InvocationKind == CustomValidationInvocationKind.DefinedOnSchemaValidator
				? $"(validator ?? ({CustomValidatorFieldName} ??= new()))"
				: schema.TargetType.AsTypeReference().ToString();

		// A method on the adapter may be private, so the adapter exposes it through a generated internal bridge.
		var method =
			custom.InvocationKind == CustomValidationInvocationKind.DefinedOnSchemaValidator
				? CustomValidationBridgeName
				: custom.MethodName;

		return $"{receiver}.{method}({valueExpression}, {services}cancellationToken)";
	}

	static string[] MemberArguments(ZodPropertyDescriptor property, string recurse) =>
		HasNestedValidation(property) ? ["value", "ref errors", "depth", recurse] : ["value", "ref errors"];

	static void WriteAsyncValidatableObject(CodeWriter writer, ZodSchemaDescriptor schema)
	{
		// Microsoft.Extensions.Validation / DataAnnotations order: members, then type attributes, then the
		// validatable object, which runs only when everything before it passed.
		using (writer.IfBlockScope("errors is null"))
		{
			writer.Assignment(
				"var",
				"validationContext",
				$"{GeneratedValidation}.CreateValidationContext(value, {schema.TargetType.Name.StringLiteral()}, services)"
			);
			using (
				writer.OpenBlockScope(
					$"await foreach (var validationResult in global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(((global::System.ComponentModel.DataAnnotations.IAsyncValidatableObject)value).ValidateAsync(validationContext, cancellationToken), false))"
				)
			)
			{
				writer.Assignment(
					"errors",
					$"{GeneratedValidation}.WithDataAnnotationsResult(errors, validationResult, EmptyPath, {TypeMemberFieldName})"
				);
			}
		}
	}

	/// <summary>
	/// Emits <c>ValidateType</c>: the type-level rules, <c>IValidatableObject</c> (only when nothing before it
	/// failed, as in DataAnnotations) and the <c>OnZodValidate</c> refinement, shared by the sync and async paths.
	/// </summary>
	static void GenerateValidateTypeMethod(CodeWriter writer, ZodSchemaDescriptor schema)
	{
		var targetType = schema.TargetType.AsTypeReference();
		using (
			writer.OpenBlockScope(
				$"static void ValidateType({targetType} value, ref {ErrorsListType}? errors, {ServiceProviderType}? services)"
			)
		)
		{
			GenerateTypeRuleValidations(writer, schema);

			if (schema.ImplementsValidatableObject && !schema.ImplementsAsyncValidatableObject)
			{
				using (writer.IfBlockScope("errors is null"))
				{
					writer.Assignment(
						"var",
						"validationContext",
						$"{GeneratedValidation}.CreateValidationContext(value, {schema.TargetType.Name.StringLiteral()}, services)"
					);
					writer.Foreach(
						"var validationResult in ((global::System.ComponentModel.DataAnnotations.IValidatableObject)value).Validate(validationContext)",
						body =>
							body.MethodCall(
								$"{GeneratedValidation}.AddDataAnnotationsResult",
								"ref errors",
								"validationResult",
								"EmptyPath",
								TypeMemberFieldName
							)
					);
				}
			}

			GenerateZodRefinement(schema, writer);
		}

		writer.NewLine();
	}

	static void GenerateZodRefinement(ZodSchemaDescriptor schema, CodeWriter method)
	{
		var hook = schema.RefinementHook.Value;
		if (!hook.IsImplemented)
			return;

		// The hook runs after the attribute and type rules, so its issues merge into the same error set
		// the generated Validate returns. Nothing is emitted when the target supplies no body, so a type
		// that only leaves the generated declaration in place stays on the zero-allocation path.
		method.Assignment(
			$"global::{TypeLibraryGenerator.ZodSharpSchemasNamespace}.RefineCtx<{schema.TargetType.AsTypeReference()}>",
			"refineContext",
			"new(value, EmptyPath)"
		);
		method.MethodCall(
			$"{schema.TargetType.AsTypeReference()}.{TypeLibraryGenerator.ZodRefinementHookInvokerName}",
			"value",
			"refineContext"
		);
		using (method.IfBlockScope("refineContext.HasIssues"))
		{
			method.Foreach("var refinementIssue in refineContext.Issues", foreachBody =>
				foreachBody.MethodCall($"{GeneratedValidation}.Add", "ref errors", "refinementIssue")
			);
		}
	}

	/// <summary>
	/// Emits one method per validated member (Change 1), <c>ValidateMember</c>/<c>ValidateMemberAsync</c> and
	/// <c>IsValidatableMember</c> (Change 2), and the per-member async methods.
	/// </summary>
	static void GenerateMemberValidationMethods(
		CodeWriter writer,
		SchemaGenerationOutputContext outputContext,
		CancellationToken cancellationToken
	)
	{
		var schema = outputContext.ZodSchema;
		var targetType = schema.TargetType.AsTypeReference();
		var resultType = TypeLibrary.ZodSharp.Core.ValidationResult.MakeGeneric(schema.TargetType).AsTypeReference();
		var resultTaskType = $"global::System.Threading.Tasks.ValueTask<{resultType}>";
		var resultTaskReference = TypeLibraryGenerator
			.ValueTask.MakeGeneric(TypeLibrary.ZodSharp.Core.ValidationResult.MakeGeneric(schema.TargetType))
			.AsTypeReference();
		var members = GetValidatedMembers(schema).ToList();
		var asyncMembers = schema.CustomValidationMethod.Value.HasCustomValidation
			? schema.CustomValidationMethod.Value.AsyncMembers
			: new([]);

		foreach (var property in members)
		{
			cancellationToken.ThrowIfCancellationRequested();
			GenerateMemberMethod(writer, schema, property);

			var nestedAsync = outputContext.NestedRequiresAsync(property);
			if (nestedAsync || property.AsyncValidationAttributes.Count > 0)
				GenerateMemberAsyncMethod(writer, outputContext, property);
		}

		writer.XmlSummary(
			"Gets whether a member has rules that ValidateMember runs. Matching is ordinal, on the C# member name."
		);
		writer.XmlParam("memberName", "The C# member name.");
		writer.XmlReturn("True when the member is individually validatable.");
		writer.Line(
			members.Count == 0
				? "public static bool IsValidatableMember(string memberName) => false;"
				: $"public static bool IsValidatableMember(string memberName) => memberName is {string.Join(" or ", members.Select(static member => member.Name.StringLiteral()))};"
		);
		writer.NewLine();

		writer.XmlSummary(
			"Validates one member of the value: the member's own rules (including a collection's null-element",
			"check and its Compare rule). Nested schemas, type-level rules, IValidatableObject, the",
			"refinement hook and the custom asynchronous method are not run, so every error belongs to the member. An unknown",
			"member succeeds."
		);
		writer.XmlParam("value", "The containing object.");
		writer.XmlParam("memberName", "The C# member name.");
		writer.XmlReturn("The member's errors, with paths relative to the value.");
		using (
			writer.OpenBlockScope($"public static {resultType} ValidateMember({targetType} value, string memberName)")
		)
		{
			if (schema.TargetCanBeNull)
			{
				writer.IfBlock("value is null", ifBody =>
					ifBody.Return(
						$"{resultType}.Failure({GeneratedValidation}.ForMember(global::ZodSharp.Core.ZodIssues.NullValue(global::ZodSharp.Core.ZodTypeNames.Object, EmptyPath), {TypeMemberFieldName}))"
					)
				);
			}

			writer.Assignment(ErrorsListReference(writer), "errors", "null");
			if (members.Count > 0)
			{
				using (writer.OpenBlockScope("switch (memberName)"))
				{
					foreach (var property in members)
					{
						writer.Line($"case {property.Name.StringLiteral()}:");
						using (writer.OpenBlockScope())
						{
							if (property.AsyncValidationAttributes.Count > 0)
								writer.MethodCall(
									$"{GeneratedValidation}.ThrowAsyncValidationRequired",
									$"typeof({targetType})"
								);

							writer.MethodCall(
								CodeGenHelpers.GetMemberMethodName(property.Name),
								HasNestedValidation(property)
									? ["value", "ref errors", "0", "false"]
									: ["value", "ref errors"]
							);
							writer.Line("break;");
						}
					}
				}
			}

			writer.Return($"{GeneratedValidation}.ToResult(value, errors)");
		}

		writer.NewLine();

		var hasAsyncMemberWork =
			members.Any(static member => member.AsyncValidationAttributes.Count > 0) || asyncMembers.Count > 0;

		writer.XmlSummary(
			"Validates one member of the value asynchronously: its own rules, its AsyncValidationAttributes, and the",
			"custom asynchronous method when [ZodAsyncMember] lists the member (only that member's errors are kept)."
		);
		writer.XmlParam("value", "The containing object.");
		writer.XmlParam("memberName", "The C# member name.");
		writer.XmlParam("services", "The caller's services, for service-aware rules.");
		writer.XmlParam("cancellationToken", "The cancellation token.");
		writer.XmlReturn("The member's errors, with paths relative to the value.");

		if (!hasAsyncMemberWork)
		{
			writer.MethodExpression(
				"ValidateMemberAsync",
				resultTaskReference,
				TypeDeclarationAccessibility.Public,
				static expression => expression.Write("new(ValidateMember(value, memberName))"),
				declaration =>
					declaration with
					{
						IsStatic = true,
						Parameters =
						[
							new("value", targetType),
							new("memberName", PurviewTypeLibrary.System.String),
							new(
								"services",
								new TypeIdentity(typeof(IServiceProvider)).AsTypeReference().Nullable(writer)
							)
							{
								DefaultValue = "null",
							},
							new("cancellationToken", TypeLibraryGenerator.CancellationToken.AsTypeReference())
							{
								DefaultValue = "default",
							},
						],
					}
			);
			writer.NewLine();
			return;
		}

		var usesAdapter =
			schema.CustomValidationMethod.Value.InvocationKind
			== CustomValidationInvocationKind.DefinedOnSchemaValidator;

		using (
			writer.OpenBlockScope(
				$"public static async {resultTaskType} ValidateMemberAsync({targetType} value, string memberName, {ServiceProviderType}? services = null, {CancellationTokenType} cancellationToken = default)"
			)
		)
		{
			if (usesAdapter)
				writer.Assignment($"{schema.SchemaType.Name}Validator? validator", "null");

			HashSet<string> asyncMemberNames = new(asyncMembers, StringComparer.Ordinal);

			writer.Assignment(ErrorsListReference(writer), "errors", "null");
			using (writer.OpenBlockScope("switch (memberName)"))
			{
				foreach (var property in members)
				{
					var hasAttributes = property.AsyncValidationAttributes.Count > 0;
					var runsCustom = asyncMemberNames.Contains(property.Name);
					if (!hasAttributes && !runsCustom)
						continue;

					writer.Line($"case {property.Name.StringLiteral()}:");
					using (writer.OpenBlockScope())
					{
						writer.MethodCall(
							CodeGenHelpers.GetMemberMethodName(property.Name),
							HasNestedValidation(property)
								? ["value", "ref errors", "0", "false"]
								: ["value", "ref errors"]
						);

						if (hasAttributes)
						{
							writer.Assignment(
								"errors",
								$"await {CodeGenHelpers.GetMemberAsyncMethodName(property.Name)}(value, errors, services, 0, false, cancellationToken).ConfigureAwait(false)"
							);
						}

						if (runsCustom)
							WriteMemberCustomCall(writer, schema, property.Name);

						writer.Line("break;");
					}
				}

				// A [ZodAsyncMember] name with no rules of its own still runs the custom method.
				foreach (var name in asyncMemberNames.OrderBy(static name => name, StringComparer.Ordinal))
				{
					if (members.Any(member => member.Name == name))
						continue;

					writer.Line($"case {name.StringLiteral()}:");
					using (writer.OpenBlockScope())
					{
						WriteMemberCustomCall(writer, schema, name);
						writer.Line("break;");
					}
				}

				writer.Line("default:");
				using (writer.OpenBlockScope())
				{
					writer.Return("ValidateMember(value, memberName)");
				}
			}

			writer.Return($"{GeneratedValidation}.ToResult(value, errors)");
		}

		writer.NewLine();
	}

	static void WriteMemberCustomCall(CodeWriter writer, ZodSchemaDescriptor schema, string memberName)
	{
		writer.Assignment("var", "customResult", $"await {CustomMethodCall(schema, "value")}.ConfigureAwait(false)");
		writer.Assignment(
			"errors",
			$"{GeneratedValidation}.WithMemberErrors(errors, customResult, {memberName.StringLiteral()})"
		);
	}

	/// <summary>
	/// Emits <c>ValidateMember_X</c>: the member's own rules and, when <c>recurse</c> is true, its nested
	/// schema. <c>ValidateMember</c> passes <c>false</c> so field-level validation never re-attributes a nested
	/// type's errors to the changed field.
	/// </summary>
	static void GenerateMemberMethod(CodeWriter writer, ZodSchemaDescriptor schema, ZodPropertyDescriptor property)
	{
		var targetType = schema.TargetType.AsTypeReference();
		var nestedParameters = HasNestedValidation(property) ? ", int depth, bool recurse" : string.Empty;

		using (
			writer.OpenBlockScope(
				$"static void {CodeGenHelpers.GetMemberMethodName(property.Name)}({targetType} value, ref {ErrorsListType}? errors{nestedParameters})"
			)
		)
		{
			GeneratePropertyValidation(writer, property, schema.ValidateEnumValues);
		}

		writer.NewLine();
	}

	static void GeneratePropertyValidation(CodeWriter writer, ZodPropertyDescriptor property, bool validateEnumValues)
	{
		var propertyName = property.Name;
		var attributes = property.ValidationAttributes;
		CodeWriter.BlockScope? block = null;

		// The enum rule owns the property's value set: the values a [DeniedValues] attribute lists are
		// absorbed into the rule's disallowed set, so the standalone denied-values validation is skipped.
		var emitEnumRule = IsEnumValidationEmitted(property, validateEnumValues);

		// A rule that rejects an absent value (IRequiredRule) stands in for [Required]: it is emitted outside
		// the non-null guard, so a missing value fails instead of being skipped.
		var hasRequiredRule = TryGetRequiredRule(property, out var requiredRule);

		if (property.CanBeNull && attributes.Required.ShouldProcess && attributes.Required.Value.Exists)
		{
			var required = attributes.Required.Value;

			// RequiredAttribute semantics (defect G1): a string is missing when it is null, empty or
			// whitespace-only, unless AllowEmptyStrings is set, in which case only null is missing.
			var rejectsBlankStrings =
				property.PropertyType.SpecialType == SpecialType.System_String && !required.AllowEmptyStrings;
			var comparison = rejectsBlankStrings
				? $"string.IsNullOrWhiteSpace(value.{propertyName})"
				: $"value.{propertyName} is null";

			// The shared missing-field identity (code missing_field, category invalid_type, the runtime
			// "{displayName} is required" template); an attribute message replaces only the text.
			var missing =
				$"global::ZodSharp.Core.ZodIssues.MissingField({MemberOf(property)}.DisplayName, {PathOf(property)})";
			var errorExpression = HasConfiguredMessage(required.ValidationAttribute)
				? ForMemberMessage(
					missing,
					MemberOf(property),
					BuildMessageTemplate(required.ValidationAttribute, string.Empty)
				)
				: MemberError(missing, MemberOf(property));

			using (writer.IfBlockScope(comparison))
				WriteAddError(writer, errorExpression);

			block = writer.ElseScope();
			GenerateValueSetValidations(writer, property, emitEnumRule);
		}
		else if (hasRequiredRule)
		{
			GenerateRequiredRuleValidation(writer, property, requiredRule);
			block = writer.ElseScope();
			GenerateValueSetValidations(writer, property, emitEnumRule);
		}
		else if (!property.IsDeclaredNullable && property.CanBeNull)
		{
			// A non-nullable reference type rejects null — the exported JSON Schema marks it required — so
			// the rules below run only for a present value. This keeps null acceptance consistent with the
			// schema and makes length/count access null-safe.
			using (writer.IfBlockScope($"value.{propertyName} is null"))
			{
				WriteAddError(
					writer,
					MemberError(
						$"global::ZodSharp.Core.ZodIssues.NullValue({ExpectedTypeName(property)}, {PathOf(property)})",
						MemberOf(property)
					)
				);
			}

			block = writer.ElseScope();
			GenerateValueSetValidations(writer, property, emitEnumRule);
		}
		else
		{
			GenerateValueSetValidations(writer, property, emitEnumRule);
			if (property.IsDeclaredNullable)
				block = writer.IfBlockScope($"value.{propertyName} is not null");
		}

		GenerateTypeSpecificValidations(writer, property, emitEnumRule, skipRequiredRule: hasRequiredRule);
		block?.Dispose();

		GenerateCompareValidation(writer, property);
	}

	static string ForMemberMessage(string errorExpression, string memberExpression, MessageTemplate message) =>
		message.OrdinalNamesExpression is null
			? $"{GeneratedValidation}.ForMember({errorExpression}, {memberExpression}, messageFormat: {message.FormatExpression})"
			: $"{GeneratedValidation}.ForMember({errorExpression}, {memberExpression}, messageFormat: {message.FormatExpression}, ordinalNames: {message.OrdinalNamesExpression})";

	/// <summary>The Zod type name of the member's type, as a runtime expression.</summary>
	static string ExpectedTypeName(ZodPropertyDescriptor property) =>
		property.ValidationKind switch
		{
			PropertyValidationKind.String => "global::ZodSharp.Core.ZodTypeNames.String",
			PropertyValidationKind.Collection => property.IsMap
				? "global::ZodSharp.Core.ZodTypeNames.Record"
				: "global::ZodSharp.Core.ZodTypeNames.Array",
			PropertyValidationKind.Complex => "global::ZodSharp.Core.ZodTypeNames.Object",
			PropertyValidationKind.Numeric
			or PropertyValidationKind.Comparable
			or PropertyValidationKind.Enum
			or PropertyValidationKind.Unsupported =>
				$"global::ZodSharp.Core.ZodTypeNames.Of(typeof({property.PropertyType.AsTypeReference()}))",
			_ => "global::ZodSharp.Core.ZodTypeNames.Unknown",
		};

	/// <summary>
	/// Finds the rule that rejects an absent value (<c>IRequiredRule</c>) among the property's custom rules,
	/// when the property can be null.
	/// </summary>
	/// <param name="property">The property to inspect.</param>
	/// <param name="requiredRule">The required rule, when one is bound to the property.</param>
	/// <returns><see langword="true"/> when a required rule is bound to a nullable property.</returns>
	static bool TryGetRequiredRule(ZodPropertyDescriptor property, out CustomRuleDescriptor requiredRule)
	{
		requiredRule = default;

		if (!property.CanBeNull)
			return false;

		foreach (var rule in property.CustomRules)
		{
			if (!rule.IsRequired)
				continue;

			requiredRule = rule;
			return true;
		}

		return false;
	}

	static void GenerateTypeSpecificValidations(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		bool emitEnumRule,
		bool skipRequiredRule
	)
	{
		switch (property.ValidationKind)
		{
			case PropertyValidationKind.String:
				GenerateStringValidations(writer, property);
				break;
			case PropertyValidationKind.Numeric:
			case PropertyValidationKind.Comparable:
				GenerateRangeValidations(writer, property);
				break;
			case PropertyValidationKind.Collection:
				GenerateCollectionValidations(writer, property, recurse: HasNestedValidation(property));
				break;
			case PropertyValidationKind.Complex:
				GenerateComplexTypeValidation(writer, property);
				break;
			case PropertyValidationKind.Enum:
				if (emitEnumRule)
					GenerateEnumValidation(writer, property);
				break;
			case PropertyValidationKind.Unsupported:
				break;
			default:
				break;
		}

		if (property.DispatchKind != DispatchKind.None)
			GenerateDispatchValidation(writer, property);

		GenerateCustomRuleValidations(writer, property, skipRequiredRule);
	}

	static void GenerateCompareValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var compareAttribute = property.ValidationAttributes.Compare;
		if (!compareAttribute.ShouldProcess || !compareAttribute.Value.Exists)
			return;

		var compare = compareAttribute.Value;
		var otherDisplayName = compare.OtherPropertyDisplayName ?? compare.OtherProperty;

		// CompareAttribute formats a custom message with (name, OtherPropertyDisplayName ?? OtherProperty).
		var message = BuildMessageTemplate(
			compare.ValidationAttribute,
			"Field '{displayName}' must match '{otherDisplayName}'.",
			"otherDisplayName"
		);

		writer.IfBlock(
			$"!global::System.Collections.Generic.EqualityComparer<object>.Default.Equals(value.{property.Name}, value.{compare.OtherProperty})",
			ifBody =>
				WriteAddError(
					ifBody,
					ErrorExpression(
						"global::ZodSharp.Rules.CompareRule.ErrorCode",
						message,
						PathOf(property),
						MemberOf(property),
						category: Category("InvalidValue"),
						valuesExpression: ValuesExpression(
							("otherProperty", compare.OtherProperty.StringLiteral()),
							("otherDisplayName", otherDisplayName.StringLiteral())
						)
					)
				)
		);
	}

	static void GenerateComplexTypeValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		if (property.NestedSchemaType is null)
			return;

		var propertyName = property.Name;
		var propertyValueName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Value");
		var nestedResultName = CodeGenHelpers.GetLocalIdentifier(propertyName, "Result");

		using (writer.IfBlockScope("recurse"))
		{
			if (property.IsNullableValueType)
			{
				// A Nullable<T> property is validated through its unwrapped value; the nested schema is declared
				// for T, not Nullable<T>.
				using (writer.IfBlockScope($"value.{propertyName} is {{ }} {propertyValueName}"))
					WriteNestedValidation(writer, property, propertyValueName, nestedResultName);

				return;
			}

			writer.Assignment("var", propertyValueName, $"value.{propertyName}");
			if (property.IsDeclaredNullable)
			{
				using (writer.IfBlockScope($"{propertyValueName} is not null"))
					WriteNestedValidation(writer, property, propertyValueName, nestedResultName);
			}
			else
			{
				WriteNestedValidation(writer, property, propertyValueName, nestedResultName);
			}
		}
	}

	static void WriteNestedValidation(
		CodeWriter writer,
		ZodPropertyDescriptor property,
		string valueName,
		string resultName
	)
	{
		writer.Assignment("var", resultName, NestedValidateCall(property, valueName));
		using (writer.IfBlockScope($"!{resultName}.IsSuccess"))
		{
			if (property.NestedIsScalarValueObject)
				writer.MethodCall(
					$"{GeneratedValidation}.AddNested",
					"ref errors",
					resultName,
					PathOf(property),
					MemberOf(property)
				);
			else
				writer.MethodCall($"{GeneratedValidation}.AddNested", "ref errors", resultName, PathOf(property));
		}
	}

	/// <summary>
	/// Emits the dispatch for a member typed as a C# 15 union (on the active case) or a closed class (on the
	/// derived type): the value's runtime type selects the case schema, and the path does not include the
	/// union's <c>Value</c> (Change 7).
	/// </summary>
	static void GenerateDispatchValidation(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var dispatched = DispatchValueExpression(property);
		using (writer.IfBlockScope("recurse"))
		{
			using (writer.OpenBlockScope($"switch ({dispatched})"))
			{
				for (var i = 0; i < property.DispatchCases.Count; i++)
				{
					var dispatchCase = property.DispatchCases[i];
					var caseName = CodeGenHelpers.GetLocalIdentifier(property.Name, $"Case{i}");
					writer.Line($"case {dispatchCase.CaseType.AsTypeReference()} {caseName}:");
					using (writer.OpenBlockScope())
					{
						var resultName = CodeGenHelpers.GetLocalIdentifier(property.Name, $"Case{i}Result");
						writer.Assignment(
							"var",
							resultName,
							NestedValidateCall(dispatchCase.SchemaType, dispatchCase.IsModern, caseName)
						);
						writer.MethodCall(
							$"{GeneratedValidation}.AddNested",
							"ref errors",
							resultName,
							PathOf(property)
						);
						writer.Line("break;");
					}
				}
			}
		}
	}

	/// <summary>
	/// The expression dispatched on: a union's active case (<c>Value</c>), or the member value itself for a
	/// closed hierarchy.
	/// </summary>
	static string DispatchValueExpression(ZodPropertyDescriptor property) =>
		property.DispatchKind == DispatchKind.Union
			? property.IsNullableValueType
				? $"value.{property.Name}?.Value"
				: $"value.{property.Name}.Value"
			: $"value.{property.Name}";

	/// <summary>
	/// Emits <c>ValidateMemberAsync_X</c>: a member's async attributes and, when <c>recurse</c> is true, its
	/// nested schemas awaited through their own <c>ValidateCoreAsync</c> (defect G11), with the caller's
	/// services threaded through (Change 9).
	/// </summary>
	static void GenerateMemberAsyncMethod(
		CodeWriter writer,
		SchemaGenerationOutputContext outputContext,
		ZodPropertyDescriptor property
	)
	{
		var schema = outputContext.ZodSchema;
		var targetType = schema.TargetType.AsTypeReference();
		var nestedAsync = outputContext.NestedRequiresAsync(property);

		using (
			writer.OpenBlockScope(
				$"static async global::System.Threading.Tasks.ValueTask<{ErrorsListType}?> {CodeGenHelpers.GetMemberAsyncMethodName(property.Name)}({targetType} value, {ErrorsListType}? errors, {ServiceProviderType}? services, int depth, bool recurse, {CancellationTokenType} cancellationToken)"
			)
		)
		{
			if (property.AsyncValidationAttributes.Count > 0)
			{
				writer.Assignment(
					"var",
					"validationContext",
					$"{GeneratedValidation}.CreateValidationContext(value, {MemberOf(property)}.DisplayName, services, {property.Name.StringLiteral()})"
				);

				for (var i = 0; i < property.AsyncValidationAttributes.Count; i++)
				{
					var resultName = CodeGenHelpers.GetLocalIdentifier(property.Name, $"AsyncResult{i}");
					writer.Assignment(
						"var",
						resultName,
						$"await {CodeGenHelpers.GetAsyncAttributeFieldName(property.Name, i)}.GetValidationResultAsync(value.{property.Name}, validationContext, cancellationToken).ConfigureAwait(false)"
					);
					writer.Assignment(
						"errors",
						$"{GeneratedValidation}.WithDataAnnotationsResult(errors, {resultName}, {PathOf(property)}, {MemberOf(property)})"
					);
				}
			}

			if (nestedAsync)
			{
				using (writer.IfBlockScope("recurse"))
				{
					if (property.DispatchKind != DispatchKind.None)
						WriteAsyncDispatch(writer, outputContext, property);
					else if (property.ValidationKind == PropertyValidationKind.Collection)
						WriteAsyncCollection(writer, property);
					else
						WriteAsyncComplex(writer, property);
				}
			}

			writer.Return("errors");
		}

		writer.NewLine();
	}

	static string NestedValidateAsyncCall(TypeIdentity schemaType, string valueExpression) =>
		$"await {schemaType.StaticMember($"ValidateCoreAsync({valueExpression}, services, depth + 1, cancellationToken)")}.ConfigureAwait(false)";

	static void WriteAsyncComplex(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var valueName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Value");
		var resultName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Result");
		var schemaType = property.NestedSchemaType!.Value;

		void WriteCall(CodeWriter body)
		{
			body.Assignment("var", resultName, NestedValidateAsyncCall(schemaType, valueName));
			var owner = property.NestedIsScalarValueObject ? $", {MemberOf(property)}" : string.Empty;
			body.Assignment(
				"errors",
				$"{GeneratedValidation}.WithNested(errors, {resultName}, {PathOf(property)}{owner})"
			);
		}

		if (property.IsNullableValueType)
		{
			writer.IfBlock($"value.{property.Name} is {{ }} {valueName}", WriteCall);
			return;
		}

		writer.Assignment("var", valueName, $"value.{property.Name}");
		if (property.CanBeNull)
			writer.IfBlock($"{valueName} is not null", WriteCall);
		else
			WriteCall(writer);
	}

	static void WriteAsyncCollection(CodeWriter writer, ZodPropertyDescriptor property)
	{
		var collectionName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Collection");
		var itemName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Item");
		var resultName = CodeGenHelpers.GetLocalIdentifier(property.Name, "ItemResult");
		var schemaType = property.NestedSchemaType!.Value;
		var itemExpression = property.ElementTypeIsNullableValueType ? $"{itemName}.Value" : itemName;

		writer.Assignment("var", collectionName, $"value.{property.Name}");
		using (writer.IfBlockScope($"{collectionName} is not null"))
		{
			if (property.IsMap)
			{
				var entryName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Entry");
				using (writer.ForeachScope($"var {entryName} in {collectionName}"))
				{
					writer.Assignment("var", itemName, $"{entryName}.Value");
					if (property.ElementTypeCanBeNull)
						writer.IfBlock($"{itemName} is null", body => body.Line("continue;"));

					writer.Assignment("var", resultName, NestedValidateAsyncCall(schemaType, itemExpression));
					writer.Assignment(
						"errors",
						$"{GeneratedValidation}.WithNested(errors, {resultName}, {PathOf(property)}.Append({ZodPathSegmentType}.Key({entryName}.Key)))"
					);
				}

				return;
			}

			var indexName = CodeGenHelpers.GetLocalIdentifier(property.Name, "Index");
			writer.Assignment("var", indexName, "0");
			using (writer.ForeachScope($"var {itemName} in {collectionName}"))
			{
				if (property.ElementTypeCanBeNull)
				{
					writer.IfBlock($"{itemName} is null", body =>
					{
						body.Line($"{indexName}++;");
						body.Line("continue;");
					});
				}

				writer.Assignment("var", resultName, NestedValidateAsyncCall(schemaType, itemExpression));
				writer.Assignment(
					"errors",
					$"{GeneratedValidation}.WithNested(errors, {resultName}, {PathOf(property)}.Append({ZodPathSegmentType}.At({indexName})))"
				);
				writer.Line($"{indexName}++;");
			}
		}
	}

	static void WriteAsyncDispatch(
		CodeWriter writer,
		SchemaGenerationOutputContext outputContext,
		ZodPropertyDescriptor property
	)
	{
		using (writer.OpenBlockScope($"switch ({DispatchValueExpression(property)})"))
		{
			for (var i = 0; i < property.DispatchCases.Count; i++)
			{
				var dispatchCase = property.DispatchCases[i];
				var caseIsAsync =
					dispatchCase.IsModern && outputContext.AsyncIndex.RequiresAsync(dispatchCase.SchemaType);

				var caseName = CodeGenHelpers.GetLocalIdentifier(property.Name, $"Case{i}");
				writer.Line($"case {dispatchCase.CaseType.AsTypeReference()} {caseName}:");
				using (writer.OpenBlockScope())
				{
					var resultName = CodeGenHelpers.GetLocalIdentifier(property.Name, $"Case{i}Result");
					writer.Assignment(
						"var",
						resultName,
						caseIsAsync
							? NestedValidateAsyncCall(dispatchCase.SchemaType, caseName)
							: NestedValidateCall(dispatchCase.SchemaType, dispatchCase.IsModern, caseName)
					);
					writer.Assignment(
						"errors",
						$"{GeneratedValidation}.WithNested(errors, {resultName}, {PathOf(property)})"
					);
					writer.Line("break;");
				}
			}
		}
	}
}
