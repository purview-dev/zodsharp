using System.Collections.Immutable;
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
			var cases = ResolveCases(GetUnionCaseTypes(namedType), externalSchemas, requireSchemaAttribute: false);
			return cases.Count == 0 ? (DispatchKind.None, cases) : (DispatchKind.Union, cases);
		}

		if (namedType.TypeKind == TypeKind.Class && RoslynFeatures.IsClosed(namedType))
		{
			var cases = ResolveCases(
				RoslynFeatures.GetClosedDerivedTypes(namedType, cancellationToken),
				externalSchemas,
				requireSchemaAttribute: true
			);
			return cases.Count == 0 ? (DispatchKind.None, cases) : (DispatchKind.ClosedHierarchy, cases);
		}

		return (DispatchKind.None, new([]));
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
		bool requireSchemaAttribute
	)
	{
		var builder = ImmutableArray.CreateBuilder<DispatchCase>();
		HashSet<TypeIdentity> seen = [];
		foreach (var caseType in caseTypes)
		{
			if (!TypeIdentity.TryCreate(caseType, out var caseIdentity) || !seen.Add(caseIdentity))
				continue;

			var owned = externalSchemas?.IsSchemaOwnedByThisCompilation(caseType) ?? IsInSource(caseType);
			if (owned)
			{
				if (requireSchemaAttribute && !ZodSchemaAttributeData.FromAttributeData(caseType, out _).Exists)
					continue;

				builder.Add(new(caseIdentity, caseIdentity with { Name = $"{caseIdentity.Name}Schema" }, true));
				continue;
			}

			if (externalSchemas is not null && externalSchemas.TryGetExistingSchema(caseType, out var existing))
			{
				var (isModern, _) = externalSchemas.GetExistingSchemaFeatures(caseType);
				builder.Add(new(caseIdentity, existing, isModern));
			}
		}

		return new(builder.ToImmutable());
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
