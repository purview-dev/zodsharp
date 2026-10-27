using System.Globalization;

namespace ZodSharp.Core;

/// <summary>
/// Identifies what a <see cref="ZodPathSegment"/> addresses.
/// </summary>
public enum ZodPathSegmentKind
{
	/// <summary>A named member: a property, field or object key that names a member.</summary>
	Member,

	/// <summary>A zero-based position in a sequence (array, list, tuple).</summary>
	Index,

	/// <summary>A dictionary or record key.</summary>
	Key,
}

/// <summary>
/// A single, typed step in a <see cref="ZodPath"/>. Keeping the kind alongside the value is what lets an
/// index (<c>Items[0]</c>), a dictionary key (<c>Map["0"]</c>) and a member be told apart after the fact, so
/// every host renders the same location in the same way.
/// </summary>
public readonly record struct ZodPathSegment
{
	ZodPathSegment(ZodPathSegmentKind kind, string? name, int index)
	{
		Kind = kind;
		Name = name;
		Index = index;
	}

	/// <summary>
	/// Gets what this segment addresses.
	/// </summary>
	public ZodPathSegmentKind Kind { get; }

	/// <summary>
	/// Gets the member name (<see cref="ZodPathSegmentKind.Member"/>) or the invariant-rendered key
	/// (<see cref="ZodPathSegmentKind.Key"/>); <see langword="null"/> for an index.
	/// </summary>
	public string? Name { get; }

	/// <summary>
	/// Gets the zero-based position for an <see cref="ZodPathSegmentKind.Index"/> segment; <c>0</c> otherwise.
	/// </summary>
	public int Index { get; }

	/// <summary>
	/// Creates a member segment.
	/// </summary>
	/// <param name="name">The member name.</param>
	/// <returns>The segment.</returns>
	public static ZodPathSegment Member(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		return new(ZodPathSegmentKind.Member, name, 0);
	}

	/// <summary>
	/// Creates an index segment.
	/// </summary>
	/// <param name="index">The zero-based position.</param>
	/// <returns>The segment.</returns>
	public static ZodPathSegment At(int index)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(index);
		return new(ZodPathSegmentKind.Index, null, index);
	}

	/// <summary>
	/// Creates a key segment from an already-rendered key.
	/// </summary>
	/// <param name="key">The key.</param>
	/// <returns>The segment.</returns>
	public static ZodPathSegment Key(string key)
	{
		ArgumentNullException.ThrowIfNull(key);
		return new(ZodPathSegmentKind.Key, key, 0);
	}

	/// <summary>
	/// Creates a key segment, rendering the key with the invariant culture so a path never depends on the
	/// thread's culture.
	/// </summary>
	/// <typeparam name="TKey">The key type.</typeparam>
	/// <param name="key">The key.</param>
	/// <returns>The segment.</returns>
	public static ZodPathSegment Key<TKey>(TKey key) => Key(FormatKey(key));

	/// <summary>
	/// Renders the segment as the string used by the legacy <see cref="ValidationError.Path"/> projection:
	/// the member name or key as-is, and an index as its invariant digits (Zod's convention).
	/// </summary>
	/// <returns>The legacy segment string.</returns>
	public override string ToString() =>
		Kind == ZodPathSegmentKind.Index ? Index.ToString(CultureInfo.InvariantCulture) : Name ?? string.Empty;

	internal static string FormatKey<TKey>(TKey key) =>
		key switch
		{
			null => string.Empty,
			string text => text,
			IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
			_ => key.ToString() ?? string.Empty,
		};
}
