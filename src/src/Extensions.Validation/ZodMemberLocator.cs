using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZodSharp.Core;

namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Turns a ZodSharp error path into the <c>(Container, Name)</c> pair Blazor uses to place a message on a
/// field. Validators that implement <see cref="IZodMemberResolver{T}"/> (generated ones do) resolve without
/// reflection; anything else falls back to a property walker. Only runs on the failure path and never throws.
/// </summary>
static class ZodMemberLocator
{
	static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> Properties = new();

	/// <summary>
	/// Gets whether <paramref name="value"/> is a key/value bag (a runtime <c>Z.Object</c> over a dictionary or
	/// JSON) rather than a CLR object, so members have no owning object a field can be bound to.
	/// </summary>
	public static bool IsDictionaryBacked(object? value) =>
		value is IDictionary or IReadOnlyDictionary<string, object?> or JsonElement or JsonNode;

	public static void Locate(
		IZodUntypedValidator validator,
		object value,
		ZodPath path,
		out object? container,
		out string memberName
	)
	{
		object? resolvedContainer;
		string resolvedMember;
		try
		{
			if (validator.TryResolveMember(value, path, out resolvedContainer, out resolvedMember))
			{
				container = resolvedContainer;
				memberName = resolvedMember ?? string.Empty;
				return;
			}
		}
		catch (Exception)
		{
			resolvedContainer = null;
			resolvedMember = string.Empty;
		}

		if (Walk(value, path, out container, out memberName))
			return;

		// The validator followed part of the path itself: its nearest ancestor is better informed than ours.
		if (!string.IsNullOrEmpty(resolvedMember))
		{
			container = resolvedContainer;
			memberName = resolvedMember;
		}
	}

	/// <summary>
	/// Walks public instance properties along <paramref name="path"/>. Indexes step into lists, arrays and other
	/// sequences; keys step into dictionaries by their invariant-rendered key. A path that ends in an index or a
	/// key resolves to the owner of the collection member. A path that cannot be followed stops at the nearest
	/// object reached.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2075:DynamicallyAccessedMembers",
		Justification = "Failure-path fallback for runtime schemas over CLR types. When the trimmer removed a "
			+ "property, the walk stops at the nearest object reached, which only degrades field placement; it "
			+ "never throws. Generated validators resolve members without reflection and never reach this."
	)]
	public static bool Walk(object root, ZodPath path, out object? container, out string memberName)
	{
		container = root;
		memberName = string.Empty;

		var current = root;
		foreach (var segment in path.Segments)
		{
			switch (segment.Kind)
			{
				case ZodPathSegmentKind.Member:
					if (current is null)
						return false;

					container = current;
					memberName = segment.Name ?? string.Empty;

					var property = Properties.GetOrAdd(
						(current.GetType(), memberName),
						static key => FindProperty(key.Type, key.Name)
					);
					if (property is null || !TryGetValue(property, current, out current))
						return false;

					break;

				case ZodPathSegmentKind.Index:
					if (!TryGetElement(current, segment.Index, out current))
						return false;

					break;

				case ZodPathSegmentKind.Key:
				default:
					if (!TryGetEntry(current, segment.Name ?? string.Empty, out current))
						return false;

					break;
			}
		}

		return true;
	}

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2070:DynamicallyAccessedMembers",
		Justification = "See Walk: a trimmed property only stops the walk early."
	)]
	static PropertyInfo? FindProperty(Type type, string name)
	{
		try
		{
			var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
			return property is { CanRead: true } && property.GetIndexParameters().Length == 0 ? property : null;
		}
		catch (AmbiguousMatchException)
		{
			// A property hidden with 'new': take the most derived declaration.
			foreach (var candidate in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (
					string.Equals(candidate.Name, name, StringComparison.Ordinal)
					&& candidate.CanRead
					&& candidate.GetIndexParameters().Length == 0
				)
					return candidate;
			}

			return null;
		}
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
				if (index >= list.Count)
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

	static bool TryGetEntry(object? map, string key, out object? value)
	{
		value = null;
		if (map is not IDictionary dictionary)
			return false;

		foreach (DictionaryEntry entry in dictionary)
		{
			if (string.Equals(ZodPathSegment.Key(entry.Key).Name, key, StringComparison.Ordinal))
			{
				value = entry.Value;
				return true;
			}
		}

		return false;
	}
}
