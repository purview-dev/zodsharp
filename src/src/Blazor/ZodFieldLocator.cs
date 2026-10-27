using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components.Forms;
using ZodSharp.Core;

namespace ZodSharp.Blazor;

/// <summary>
/// Turns a ZodSharp error path into the <see cref="FieldIdentifier"/> Blazor shows the message on. Validators that
/// resolve members themselves (generated ones do) are asked first; anything else falls back to a property walker.
/// Never throws.
/// </summary>
static class ZodFieldLocator
{
	static readonly ConcurrentDictionary<(Type Type, string Name, bool IgnoreCase), PropertyInfo?> Properties = new();

	/// <summary>
	/// Locates the field <paramref name="path"/> addresses below <paramref name="root"/>.
	/// </summary>
	/// <param name="root">The object the path is relative to.</param>
	/// <param name="path">The error path.</param>
	/// <param name="validator">The validator of <paramref name="root"/>, when known.</param>
	/// <param name="ignoreCase">
	/// Whether member names may differ in case from the CLR property (server paths rendered with a JSON naming policy).
	/// </param>
	/// <returns>The field; a model-level field (empty name) for a root error.</returns>
	public static FieldIdentifier Locate(object root, ZodPath path, IZodUntypedValidator? validator, bool ignoreCase)
	{
		if (path.IsRoot)
			return new(root, string.Empty);

		if (IsDictionaryBacked(root))
			return new(root, path.ToDottedString());

		object? resolvedContainer = null;
		var resolvedMember = string.Empty;
		if (validator is not null)
		{
			try
			{
				if (
					validator.ValidatedType.IsInstanceOfType(root)
					&& validator.TryResolveMember(root, path, out resolvedContainer, out resolvedMember)
					&& resolvedContainer is not null
				)
					return new(resolvedContainer, resolvedMember ?? string.Empty);
			}
			catch (Exception)
			{
				resolvedContainer = null;
				resolvedMember = string.Empty;
			}
		}

		if (Walk(root, path, ignoreCase, out var container, out var member))
			return new(container ?? root, member);

		// The validator followed part of the path itself: its nearest ancestor is better informed than ours.
		if (resolvedContainer is not null && !string.IsNullOrEmpty(resolvedMember))
			return new(resolvedContainer, resolvedMember);

		return new(container ?? root, member);
	}

	static bool IsDictionaryBacked(object value) =>
		value is IDictionary or IReadOnlyDictionary<string, object?> or JsonElement or JsonNode;

	/// <summary>
	/// Walks public instance properties along <paramref name="path"/>. Indexes step into sequences; keys - and member
	/// segments over a dictionary, which is how a server path renders a key - step into dictionaries. A path that
	/// ends in an index or a key resolves to the owner of the collection member; a path that cannot be followed stops
	/// at the nearest object reached.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:DynamicallyAccessedMembers",
		Justification = "Failure-path fallback for validators that do not resolve members themselves. When the "
			+ "trimmer removed a property, the walk stops at the nearest object reached, which only degrades field "
			+ "placement; it never throws. Form models are application types, preserved because they are rendered."
	)]
	static bool Walk(object root, ZodPath path, bool ignoreCase, out object? container, out string memberName)
	{
		container = root;
		memberName = string.Empty;

		var current = (object?)root;
		foreach (var segment in path.Segments)
		{
			switch (segment.Kind)
			{
				case ZodPathSegmentKind.Member when current is IDictionary:
				case ZodPathSegmentKind.Key:
					if (!TryGetEntry(current, segment.Name ?? string.Empty, ignoreCase, out current))
						return false;

					break;

				case ZodPathSegmentKind.Index:
					if (!TryGetElement(current, segment.Index, out current))
						return false;

					break;

				case ZodPathSegmentKind.Member:
				default:
					if (current is null)
						return false;

					var property = FindCachedProperty(current.GetType(), segment.Name ?? string.Empty, ignoreCase);
					container = current;
					memberName = property?.Name ?? segment.Name ?? string.Empty;
					if (property is null || !TryGetValue(property, current, out current))
						return false;

					break;
			}
		}

		return true;
	}

	static PropertyInfo? FindCachedProperty(Type type, string name, bool ignoreCase)
	{
		var exact = Properties.GetOrAdd((type, name, false), static key => FindProperty(key.Type, key.Name, false));
		return exact is not null || !ignoreCase
			? exact
			: Properties.GetOrAdd((type, name, true), static key => FindProperty(key.Type, key.Name, true));
	}

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:DynamicallyAccessedMembers",
		Justification = "See Walk: a trimmed property only stops the walk early."
	)]
	static PropertyInfo? FindProperty(Type type, string name, bool ignoreCase)
	{
		var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

		PropertyInfo? best = null;
		var bestDepth = -1;
		foreach (var candidate in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (
				!string.Equals(candidate.Name, name, comparison)
				|| !candidate.CanRead
				|| candidate.GetIndexParameters().Length != 0
			)
				continue;

			// Most derived wins, so a property hidden with 'new' resolves to its newest declaration.
			var depth = Depth(candidate.DeclaringType);
			if (depth > bestDepth)
			{
				best = candidate;
				bestDepth = depth;
			}
		}

		return best;
	}

	static int Depth(Type? type)
	{
		var depth = 0;
		for (var current = type; current is not null; current = current.BaseType)
			depth++;

		return depth;
	}

	static bool TryGetValue(PropertyInfo property, object instance, out object? value)
	{
		try
		{
			value = property.GetValue(instance);
			return true;
		}
		catch (Exception)
		{
			value = null;
			return false;
		}
	}

	static bool TryGetElement(object? sequence, int index, out object? element)
	{
		element = null;
		switch (sequence)
		{
			case null:
			case string:
				return false;
			case IList list:
				if (index < 0 || index >= list.Count)
					return false;

				element = list[index];
				return true;
			case IEnumerable enumerable:
				var position = 0;
				foreach (var item in enumerable)
				{
					if (position++ == index)
					{
						element = item;
						return true;
					}
				}

				return false;
			default:
				return false;
		}
	}

	static bool TryGetEntry(object? map, string key, bool ignoreCase, out object? value)
	{
		value = null;
		if (map is not IDictionary dictionary)
			return false;

		var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		foreach (DictionaryEntry entry in dictionary)
		{
			if (string.Equals(ZodPathSegment.Key(entry.Key).Name, key, comparison))
			{
				value = entry.Value;
				return true;
			}
		}

		return false;
	}
}
