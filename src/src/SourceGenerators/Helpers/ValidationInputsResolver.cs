using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators.Helpers;

/// <summary>
/// Resolves the validation inputs that are not DataAnnotations attribute values: the .NET 11 async
/// DataAnnotations contracts, <c>IValidatableObject</c>, localized display names, C# 15 union and
/// closed-hierarchy members, and conflicting hand-authored options validators. Every result is a value
/// (strings, flags, identities), so nothing from the compilation outlives the transform.
/// </summary>
static class ValidationInputsResolver
{
	public const string DataAnnotationsNamespace = "System.ComponentModel.DataAnnotations";

	public const string ValidationNamespace = "Microsoft.Extensions.Validation";

	const string AsyncValidationAttributeName = "AsyncValidationAttribute";

	const string ValidatableObjectName = "IValidatableObject";

	const string AsyncValidatableObjectName = "IAsyncValidatableObject";

	/// <summary>
	/// Determines whether <paramref name="type"/> implements the DataAnnotations
	/// <c>IValidatableObject</c> or (.NET 11) <c>IAsyncValidatableObject</c> interface.
	/// </summary>
	public static (bool Sync, bool Async) GetValidatableObjectKinds(INamedTypeSymbol type)
	{
		var sync = false;
		var isAsync = false;
		foreach (var iface in type.AllInterfaces)
		{
			if (iface.ContainingNamespace?.ToDisplayString() != DataAnnotationsNamespace)
				continue;

			if (iface.MetadataName == ValidatableObjectName)
				sync = true;
			else if (iface.MetadataName == AsyncValidatableObjectName)
				isAsync = true;
		}

		return (sync, isAsync);
	}

	/// <summary>
	/// Renders a construction expression for every <c>AsyncValidationAttribute</c> declared on a member, so the
	/// generated validator can hold an identical instance in a static field and await it.
	/// </summary>
	/// <param name="attributes">The member's effective attributes.</param>
	/// <returns>The construction expressions, in declaration order.</returns>
	public static EquatableArray<string> GetAsyncValidationAttributes(ImmutableArray<AttributeData> attributes)
	{
		ImmutableArray<string>.Builder? expressions = null;
		foreach (var attribute in attributes)
		{
			if (attribute.AttributeClass is not { } attributeClass || !InheritsAsyncValidationAttribute(attributeClass))
				continue;

			if (TryRenderAttributeConstruction(attribute, out var expression))
				(expressions ??= ImmutableArray.CreateBuilder<string>()).Add(expression);
		}

		return expressions is null ? new([]) : new(expressions.ToImmutable());
	}

	/// <summary>
	/// Determines whether <paramref name="type"/> derives from the .NET 11 <c>AsyncValidationAttribute</c>.
	/// </summary>
	public static bool InheritsAsyncValidationAttribute(INamedTypeSymbol type)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			if (
				current.MetadataName == AsyncValidationAttributeName
				&& current.ContainingNamespace?.ToDisplayString() == DataAnnotationsNamespace
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets the DataAnnotations validation attributes the generator has no dedicated rule for, so the generated
	/// validator can run them through their own <c>GetValidationResult</c>/<c>GetValidationResultAsync</c>.
	/// Mapped attributes (a Zod rule) and attributes already handled as a specific rule are excluded.
	/// </summary>
	/// <param name="attributes">The member's or type's effective attributes.</param>
	/// <param name="isTypeLevel">
	/// Whether these are class-level attributes. A class-level mapped DataAnnotations attribute is not handled by
	/// a generated type rule, so it is included here; only a <c>[ZodRule]</c>-mapped attribute is excluded.
	/// </param>
	/// <returns>The construction expressions for the synchronous and asynchronous attributes.</returns>
	public static (EquatableArray<string> Sync, EquatableArray<string> Async) GetFallbackValidationAttributes(
		ImmutableArray<AttributeData> attributes,
		bool isTypeLevel
	)
	{
		ImmutableArray<string>.Builder? sync = null;
		ImmutableArray<string>.Builder? async = null;
		foreach (var attribute in attributes)
		{
			if (attribute.AttributeClass is not { } attributeClass)
				continue;

			var isAsync = InheritsAsyncValidationAttribute(attributeClass);
			if (!isAsync)
			{
				if (!InheritsValidationAttribute(attributeClass) || CustomRuleResolver.IsRuleMapped(attributeClass))
					continue;

				// A mapped DataAnnotations attribute is already emitted as a Zod rule. [DataType] itself has no
				// validation semantics; its subclasses that are not mapped (EnumDataType, FileExtensions, a
				// consumer's own) fall through to their own implementation below.
				if (
					!isTypeLevel
					&& (IsMappedDataAnnotationAttribute(attributeClass) || IsDataTypeAttribute(attributeClass))
				)
				{
					continue;
				}
			}

			if (!TryRenderAttributeConstruction(attribute, out var expression))
				continue;

			if (isAsync)
				(async ??= ImmutableArray.CreateBuilder<string>()).Add(expression);
			else
				(sync ??= ImmutableArray.CreateBuilder<string>()).Add(expression);
		}

		return (sync is null ? new([]) : new(sync.ToImmutable()), async is null ? new([]) : new(async.ToImmutable()));
	}

	/// <summary>
	/// Determines whether <paramref name="type"/> derives from the DataAnnotations
	/// <c>ValidationAttribute</c>.
	/// </summary>
	public static bool InheritsValidationAttribute(INamedTypeSymbol type)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			if (
				current.MetadataName == "ValidationAttribute"
				&& current.ContainingNamespace?.ToDisplayString() == DataAnnotationsNamespace
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether <paramref name="type"/> is a DataAnnotations attribute the generator maps to a Zod rule.
	/// </summary>
	static bool IsMappedDataAnnotationAttribute(INamedTypeSymbol type) =>
		type.ContainingNamespace?.ToDisplayString() == DataAnnotationsNamespace
		&& type.MetadataName
			is "RequiredAttribute"
				or "CompareAttribute"
				or "EmailAddressAttribute"
				or "CreditCardAttribute"
				or "PhoneAttribute"
				or "UrlAttribute"
				or "StringLengthAttribute"
				or "MinLengthAttribute"
				or "MaxLengthAttribute"
				or "RegularExpressionAttribute"
				or "Base64StringAttribute"
				or "DeniedValuesAttribute"
				or "AllowedValuesAttribute"
				or "LengthAttribute"
				or "RangeAttribute";

	/// <summary>
	/// Determines whether <paramref name="type"/> is <c>DataTypeAttribute</c> itself, which has no validation
	/// semantics. Its subclasses are validated (mapped ones by a Zod rule, the rest through their own
	/// implementation).
	/// </summary>
	static bool IsDataTypeAttribute(INamedTypeSymbol type) =>
		type.MetadataName == "DataTypeAttribute"
		&& type.ContainingNamespace?.ToDisplayString() == DataAnnotationsNamespace;

	/// <summary>
	/// Determines whether <paramref name="member"/> is excluded from Microsoft.Extensions.Validation with
	/// <c>[SkipValidation]</c>.
	/// </summary>
	public static bool IsSkipValidation(ISymbol member) =>
		member
			.GetAttributes()
			.Any(attribute =>
				attribute.AttributeClass is { MetadataName: "SkipValidationAttribute" } attributeClass
				&& attributeClass.ContainingNamespace?.ToDisplayString() == ValidationNamespace
			);

	/// <summary>
	/// Collects the class-level DataAnnotations validation attributes that apply to <paramref name="type"/>,
	/// following the base chain because <c>ValidationAttribute</c> is inherited. A non-multiple attribute is
	/// taken from its most derived declaration; a multiple one (for example <c>[CustomValidation]</c>) keeps
	/// every declaration.
	/// </summary>
	public static ImmutableArray<AttributeData> GetTypeValidationAttributes(INamedTypeSymbol type)
	{
		var attributes = ImmutableArray.CreateBuilder<AttributeData>();
		HashSet<INamedTypeSymbol> seen = [with(SymbolEqualityComparer.Default)];
		for (var current = type; current is not null; current = current.BaseType)
		{
			foreach (var attribute in current.GetAttributes())
			{
				if (attribute.AttributeClass is not { } attributeClass || !InheritsValidationAttribute(attributeClass))
					continue;

				if (!AllowsMultiple(attributeClass) && !seen.Add(attributeClass))
					continue;

				attributes.Add(attribute);
			}
		}

		return attributes.ToImmutable();
	}

	static bool AllowsMultiple(INamedTypeSymbol attributeClass)
	{
		foreach (var attribute in attributeClass.GetAttributes())
		{
			if (attribute.AttributeClass is not { MetadataName: "AttributeUsageAttribute" })
				continue;

			foreach (var argument in attribute.NamedArguments)
			{
				if (argument.Key == "AllowMultiple" && argument.Value.Value is true)
					return true;
			}

			return false;
		}

		return false;
	}

	/// <summary>
	/// Resolves the localized display name expression for <c>[Display(Name = ..., ResourceType = ...)]</c>: the
	/// resource type's public static string property named by <c>Name</c>, read directly so the lookup is
	/// reflection-free and follows the current UI culture.
	/// </summary>
	/// <param name="attributes">The member's effective attributes.</param>
	/// <returns>The expression, or <see langword="null"/> when no resource is declared or it cannot be resolved.</returns>
	public static string? GetDisplayNameResourceExpression(ImmutableArray<AttributeData> attributes)
	{
		foreach (var attribute in attributes)
		{
			if (
				attribute.AttributeClass is not { Name: "DisplayAttribute" } attributeClass
				|| attributeClass.ContainingNamespace?.ToDisplayString() != DataAnnotationsNamespace
			)
			{
				continue;
			}

			ITypeSymbol? resourceType = null;
			string? name = null;
			foreach (var argument in attribute.NamedArguments)
			{
				if (argument.Key == "ResourceType" && argument.Value.Value is ITypeSymbol type)
					resourceType = type;
				else if (argument.Key == "Name" && argument.Value.Value is string text)
					name = text;
			}

			if (resourceType is null || string.IsNullOrEmpty(name))
				return null;

			foreach (var member in resourceType.GetMembers(name!))
			{
				if (
					member is IPropertySymbol
					{
						IsStatic: true,
						DeclaredAccessibility: Accessibility.Public or Accessibility.Internal,
						Type.SpecialType: SpecialType.System_String,
						GetMethod: not null,
					}
				)
				{
					return $"{new TypeIdentity(resourceType).RenderFullName}.{name}";
				}
			}

			return null;
		}

		return null;
	}

	/// <summary>
	/// Resolves how a member typed as a C# 15 union or a closed class dispatches to case schemas.
	/// </summary>
	/// <param name="memberType">The member type, with a nullable wrapper removed.</param>
	/// <param name="externalSchemas">The resolver deciding schema ownership.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The dispatch kind and cases; <see cref="DispatchKind.None"/> when the member does not dispatch.</returns>
	public static (DispatchKind Kind, EquatableArray<DispatchCase> Cases) ResolveDispatch(
		ITypeSymbol memberType,
		ExternalSchemaResolver? externalSchemas,
		CancellationToken cancellationToken = default
	)
	{
		if (memberType is not INamedTypeSymbol namedType)
			return (DispatchKind.None, new([]));

		if (RoslynFeatures.IsUnion(namedType))
		{
			var cases = ResolveCases(
				GetUnionCaseTypes(namedType),
				externalSchemas,
				requireSchemaAttribute: false,
				unionType: namedType
			);
			return cases.Count == 0 ? (DispatchKind.None, cases) : (DispatchKind.Union, cases);
		}

		if (namedType.TypeKind == TypeKind.Class && RoslynFeatures.IsClosed(namedType))
		{
			var cases = ResolveCases(
				RoslynFeatures.GetClosedDerivedTypes(namedType, cancellationToken),
				externalSchemas,
				requireSchemaAttribute: true,
				closedType: namedType
			);
			return cases.Count == 0 ? (DispatchKind.None, cases) : (DispatchKind.ClosedHierarchy, cases);
		}

		return (DispatchKind.None, new([]));
	}

	/// <summary>
	/// Resolves the case types of a union that is itself a <c>[ZodSchema]</c> target, so its schema dispatches to
	/// each case's schema. Also reports whether the union exposes <c>HasValue</c> (the non-boxing access pattern).
	/// </summary>
	/// <param name="union">The union type.</param>
	/// <param name="externalSchemas">The resolver deciding schema ownership.</param>
	/// <returns>The dispatch cases, in declaration order, and whether the union has a <c>HasValue</c> property.</returns>
	public static (EquatableArray<DispatchCase> Cases, bool HasValue) ResolveUnionTarget(
		INamedTypeSymbol union,
		ExternalSchemaResolver externalSchemas
	)
	{
		var cases = ResolveCases(
			GetUnionCaseTypes(union),
			externalSchemas,
			requireSchemaAttribute: false,
			unionType: union
		);
		return (cases, HasHasValue(union));
	}

	/// <summary>
	/// Gets the case types of a union that neither this compilation generates a schema for nor a referenced
	/// assembly exposes a validator for, so the analyzer can warn that values of that case are not validated.
	/// </summary>
	/// <param name="union">The union type.</param>
	/// <param name="externalSchemas">The resolver deciding schema ownership.</param>
	/// <returns>The unresolvable case types, in declaration order.</returns>
	public static ImmutableArray<INamedTypeSymbol> GetUnionCasesWithoutSchema(
		INamedTypeSymbol union,
		ExternalSchemaResolver externalSchemas
	)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		HashSet<TypeIdentity> seen = [];
		foreach (var caseType in GetUnionCaseTypes(union))
		{
			if (!TypeIdentity.TryCreate(caseType, out var identity) || !seen.Add(identity))
				continue;

			if (externalSchemas.IsSchemaOwnedByThisCompilation(caseType))
				continue;

			if (externalSchemas.TryGetExistingSchema(caseType, out _))
				continue;

			builder.Add(caseType);
		}

		return builder.ToImmutable();
	}

	/// <summary>
	/// Whether <paramref name="union"/> exposes a <c>TryGetValue</c> overload that reads
	/// <paramref name="caseType"/> without boxing (the language's non-boxing access pattern).
	/// </summary>
	public static bool HasTryGetValue(INamedTypeSymbol union, INamedTypeSymbol caseType)
	{
		foreach (var member in union.GetMembers("TryGetValue"))
		{
			if (
				member is IMethodSymbol { IsStatic: false, Parameters.Length: 1 } method
				&& method.Parameters[0].RefKind == RefKind.Out
				&& SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, caseType)
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Whether <paramref name="union"/> exposes a public <c>HasValue</c> property.</summary>
	public static bool HasHasValue(INamedTypeSymbol union)
	{
		foreach (var member in union.GetMembers("HasValue"))
		{
			if (
				member is IPropertySymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } property
				&& property.Type.SpecialType == SpecialType.System_Boolean
				&& property.GetMethod is not null
			)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Gets the case types of a C# 15 union: the parameter types of its single-parameter case constructors.
	/// </summary>
	public static ImmutableArray<INamedTypeSymbol> GetUnionCaseTypes(INamedTypeSymbol union)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		foreach (var constructor in union.InstanceConstructors)
		{
			if (
				constructor.DeclaredAccessibility != Accessibility.Public
				|| constructor.Parameters.Length != 1
				|| constructor.Parameters[0].Type is not INamedTypeSymbol caseType
				|| SymbolEqualityComparer.Default.Equals(caseType, union)
			)
			{
				continue;
			}

			builder.Add(caseType);
		}

		return builder.ToImmutable();
	}

	/// <summary>
	/// Gets the direct derived types of a closed class that have no <c>[ZodSchema]</c>, for the analyzer.
	/// </summary>
	public static ImmutableArray<INamedTypeSymbol> GetClosedDerivedTypesWithoutSchema(
		INamedTypeSymbol closedType,
		CancellationToken cancellationToken = default
	)
	{
		var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		foreach (var derived in RoslynFeatures.GetClosedDerivedTypes(closedType, cancellationToken))
		{
			if (!ZodSchemaAttributeData.FromAttributeData(derived, out _).Exists && !derived.IsAbstract)
				builder.Add(derived);
		}

		return builder.ToImmutable();
	}

	/// <summary>
	/// Determines whether a hand-authored <c>[OptionsValidator]</c> validator named <c>{Type}Validator</c>
	/// exists alongside <paramref name="type"/> (or <paramref name="type"/> itself carries the attribute), which
	/// the generated options validator would conflict with (SYSLIB1205).
	/// </summary>
	public static bool HasOptionsValidatorConflict(INamedTypeSymbol type)
	{
		if (HasOptionsValidatorAttribute(type))
			return true;

		var validatorName = $"{type.Name}Validator";
		var siblings = type.ContainingType is { } containing
			? containing.GetTypeMembers(validatorName)
			: type.ContainingNamespace.GetTypeMembers(validatorName);

		foreach (var sibling in siblings)
		{
			if (HasOptionsValidatorAttribute(sibling))
				return true;
		}

		return false;
	}

	static bool HasOptionsValidatorAttribute(INamedTypeSymbol type)
	{
		foreach (var attribute in type.GetAttributes())
		{
			if (
				attribute.AttributeClass is { Name: "OptionsValidatorAttribute" } attributeClass
				&& attributeClass.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Options"
			)
			{
				return true;
			}
		}

		return false;
	}

	static EquatableArray<DispatchCase> ResolveCases(
		ImmutableArray<INamedTypeSymbol> caseTypes,
		ExternalSchemaResolver? externalSchemas,
		bool requireSchemaAttribute,
		INamedTypeSymbol? unionType = null,
		INamedTypeSymbol? closedType = null
	)
	{
		var builder = ImmutableArray.CreateBuilder<DispatchCase>();
		HashSet<TypeIdentity> seen = [];
		var (polymorphismEnabled, discriminatorPropertyName) = closedType is null
			? (false, string.Empty)
			: ReadPolymorphism(closedType);
		foreach (var caseType in caseTypes)
		{
			if (!TypeIdentity.TryCreate(caseType, out var caseIdentity) || !seen.Add(caseIdentity))
				continue;

			string? discriminator = null;
			var discriminatorIsNumber = false;
			if (polymorphismEnabled)
			{
				var (configured, configuredIsNumber) = GetDiscriminatorValue(closedType!, caseType);
				discriminator = configured ?? caseType.Name;
				discriminatorIsNumber = configured is not null && configuredIsNumber;
			}

			var nonBoxing = unionType is not null && HasTryGetValue(unionType, caseType);
			var owned = externalSchemas?.IsSchemaOwnedByThisCompilation(caseType) ?? IsInSource(caseType);
			if (owned)
			{
				var attributeData = ZodSchemaAttributeData.FromAttributeData(caseType, out _);
				if (requireSchemaAttribute && !attributeData.Exists)
					continue;

				builder.Add(
					new(
						caseIdentity,
						caseIdentity with
						{
							Name = $"{caseIdentity.Name}Schema",
						},
						true,
						nonBoxing,
						!attributeData.Exists || attributeData.GenerateJsonSchema,
						polymorphismEnabled ? discriminatorPropertyName : null,
						discriminator,
						discriminatorIsNumber
					)
				);
				continue;
			}

			if (externalSchemas is not null && externalSchemas.TryGetExistingSchema(caseType, out var existing))
			{
				var (isModern, _) = externalSchemas.GetExistingSchemaFeatures(caseType);
				builder.Add(
					new(
						caseIdentity,
						existing,
						isModern,
						nonBoxing,
						true,
						polymorphismEnabled ? discriminatorPropertyName : null,
						discriminator,
						discriminatorIsNumber
					)
				);
			}
		}

		return new(builder.ToImmutable());
	}

	/// <summary>
	/// Reads whether a closed hierarchy opts into System.Text.Json polymorphism and the discriminator
	/// property name it uses. A hierarchy that does not opt in has no discriminator in its wire form; a
	/// process-wide <c>JsonSerializerOptions.InferClosedTypePolymorphism</c> is not visible to the generator,
	/// so the type-level attribute is what the exported schema follows.
	/// </summary>
	static (bool Enabled, string PropertyName) ReadPolymorphism(INamedTypeSymbol closedType)
	{
		var enabled = false;
		var propertyName = "$type";
		foreach (var attribute in closedType.GetAttributes())
		{
			var attributeType = attribute.AttributeClass?.ToDisplayString();
			if (attributeType == "System.Text.Json.Serialization.JsonPolymorphicAttribute")
			{
				foreach (var argument in attribute.NamedArguments)
				{
					if (argument.Key == "TypeDiscriminatorPropertyName" && argument.Value.Value is string configured)
						propertyName = configured;

					if (argument.Key == "InferClosedTypePolymorphism" && argument.Value.Value is true)
						enabled = true;
				}
			}
			else if (attributeType == "System.Text.Json.Serialization.JsonDerivedTypeAttribute")
			{
				// Explicit registrations make the type polymorphic without inference.
				enabled = true;
			}
		}

		return (enabled, propertyName);
	}

	/// <summary>
	/// Gets the discriminator System.Text.Json writes for <paramref name="caseType"/>, from an explicit
	/// <c>[JsonDerivedType]</c> registration, and whether it is written as a JSON number. Inferred
	/// registrations use the simple type name, which the caller supplies when this returns no value.
	/// </summary>
	static (string? Value, bool IsNumber) GetDiscriminatorValue(INamedTypeSymbol closedType, INamedTypeSymbol caseType)
	{
		foreach (var attribute in closedType.GetAttributes())
		{
			if (
				attribute.AttributeClass?.ToDisplayString() != "System.Text.Json.Serialization.JsonDerivedTypeAttribute"
			)
				continue;

			if (attribute.ConstructorArguments.Length < 2)
				continue;

			if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol derived)
				continue;

			if (!SymbolEqualityComparer.Default.Equals(derived, caseType))
				continue;

			return attribute.ConstructorArguments[1].Value switch
			{
				string value => (value, false),
				int value => (value.ToString(CultureInfo.InvariantCulture), true),
				_ => (null, false),
			};
		}

		return (null, false);
	}

	static bool IsInSource(INamedTypeSymbol type) =>
		type.Locations.Any(static location => location.IsInSource)
		&& type.TypeKind is TypeKind.Class or TypeKind.Struct
		&& !type.IsAbstract
		&& !type.IsStatic;

	static bool TryRenderAttributeConstruction(AttributeData attribute, out string expression)
	{
		expression = string.Empty;
		if (attribute.AttributeClass is not { } attributeClass || attribute.AttributeConstructor is not { } constructor)
			return false;

		List<string> arguments = [];
		for (var i = 0; i < attribute.ConstructorArguments.Length && i < constructor.Parameters.Length; i++)
		{
			if (
				!CustomRuleResolver.TryConvertConstant(
					attribute.ConstructorArguments[i],
					constructor.Parameters[i].Type,
					out var argument
				)
			)
			{
				return false;
			}

			arguments.Add(argument);
		}

		List<string> initializers = [];
		foreach (var named in attribute.NamedArguments)
		{
			var memberType = FindMemberType(attributeClass, named.Key);
			if (memberType is null || !CustomRuleResolver.TryConvertConstant(named.Value, memberType, out var value))
				return false;

			initializers.Add($"{named.Key} = {value}");
		}

		var typeName = new TypeIdentity(attributeClass).RenderFullName;
		expression =
			initializers.Count == 0
				? $"new {typeName}({string.Join(", ", arguments)})"
				: $"new {typeName}({string.Join(", ", arguments)}) {{ {string.Join(", ", initializers)} }}";
		return true;
	}

	static ITypeSymbol? FindMemberType(INamedTypeSymbol type, string name)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			foreach (var member in current.GetMembers(name))
			{
				switch (member)
				{
					case IPropertySymbol property:
						return property.Type;
					case IFieldSymbol field:
						return field.Type;
					default:
						break;
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Reads the C# 15 symbol APIs behind a feature check. The generator is compiled against Roslyn 5.9, but a
	/// compiler host without those members throws <see cref="MissingMemberException"/> when the calling method
	/// is JIT-compiled; isolating each call in a non-inlined method turns that into "not supported".
	/// </summary>
	internal static class RoslynFeatures
	{
		static bool IsUnavailable;

		public static bool IsUnion(ITypeSymbol type)
		{
			if (IsUnavailable)
				return false;

			try
			{
				return IsUnionCore(type);
			}
			catch (MissingMemberException)
			{
				IsUnavailable = true;
				return false;
			}
		}

		public static bool IsClosed(ITypeSymbol type)
		{
			if (IsUnavailable)
				return false;

			try
			{
				return IsClosedCore(type);
			}
			catch (MissingMemberException)
			{
				IsUnavailable = true;
				return false;
			}
		}

		public static ImmutableArray<INamedTypeSymbol> GetClosedDerivedTypes(
			ITypeSymbol type,
			CancellationToken cancellationToken
		)
		{
			if (IsUnavailable)
				return [];

			try
			{
				return GetClosedDerivedTypesCore(type, cancellationToken);
			}
			catch (MissingMemberException)
			{
				IsUnavailable = true;
				return [];
			}
			catch (InvalidOperationException)
			{
				return [];
			}
		}

#pragma warning disable RSEXPERIMENTAL006 // C# 15 union and closed-type symbol APIs are experimental in Roslyn 5.9; isolated here.
		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool IsUnionCore(ITypeSymbol type) => type.IsUnion;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static bool IsClosedCore(ITypeSymbol type) => type.IsClosed;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static ImmutableArray<INamedTypeSymbol> GetClosedDerivedTypesCore(
			ITypeSymbol type,
			CancellationToken cancellationToken
		)
		{
			var info = type.GetClosedDerivedTypeInfo(cancellationToken);
			var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
			foreach (var derived in info.ClosedDerivedTypes)
			{
				if (derived is INamedTypeSymbol named)
					builder.Add(named);
			}

			return builder.ToImmutable();
		}
#pragma warning restore RSEXPERIMENTAL006
	}
}
