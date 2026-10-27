using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace ZodSharp.Core;

/// <summary>
/// Maps a member name in a <see cref="ZodPath"/> to the name rendered for a host, for example a JSON naming
/// policy. Index and key segments are never renamed.
/// </summary>
public interface IZodPathNamer
{
	/// <summary>
	/// Returns the rendered name for <paramref name="memberName"/>.
	/// </summary>
	/// <param name="memberName">The member name recorded in the path.</param>
	/// <returns>The name to render.</returns>
	string GetName(string memberName);
}

/// <summary>
/// Creates <see cref="IZodPathNamer"/> instances.
/// </summary>
public static class ZodPathNamer
{
	/// <summary>
	/// Creates a namer from a conversion function, for example <c>JsonNamingPolicy.CamelCase.ConvertName</c>.
	/// </summary>
	/// <param name="convert">The conversion applied to each member name.</param>
	/// <returns>The namer.</returns>
	public static IZodPathNamer FromFunc(Func<string, string> convert)
	{
		ArgumentNullException.ThrowIfNull(convert);
		return new FuncNamer(convert);
	}

	sealed class FuncNamer(Func<string, string> convert) : IZodPathNamer
	{
		public string GetName(string memberName) => convert(memberName);
	}
}

/// <summary>
/// The location of a validation issue: an immutable sequence of typed <see cref="ZodPathSegment"/>s plus the
/// legacy string projection exposed by <see cref="ValidationError.Path"/>. This is the one place paths are
/// rendered, so ProblemDetails keys, Blazor field mapping, log output and options failures always agree.
/// </summary>
/// <remarks>
/// The dotted rendering (<see cref="ToDottedString"/>) matches Microsoft.Extensions.Validation: members are
/// joined with <c>.</c> and indexes are bracketed (<c>Items[0].Name</c>). Keys are bracketed too, and quoted
/// when they would otherwise be ambiguous (<c>Map[en]</c>, <c>Map["0"]</c>, <c>Map["a.b"]</c>).
/// </remarks>
public readonly struct ZodPath : IEquatable<ZodPath>
{
	readonly ImmutableArray<ZodPathSegment> _segments;
	readonly ImmutableArray<string> _legacy;

	ZodPath(ImmutableArray<ZodPathSegment> segments, ImmutableArray<string> legacy)
	{
		_segments = segments;
		_legacy = legacy;
	}

	/// <summary>
	/// Gets the empty (root) path.
	/// </summary>
	public static ZodPath Root => default;

	/// <summary>
	/// Gets the typed segments, outermost first.
	/// </summary>
	public ImmutableArray<ZodPathSegment> Segments => _segments.IsDefault ? [] : _segments;

	/// <summary>
	/// Gets the number of segments.
	/// </summary>
	public int Length => _segments.IsDefault ? 0 : _segments.Length;

	/// <summary>
	/// Gets whether this is the root path.
	/// </summary>
	public bool IsRoot => Length == 0;

	/// <summary>
	/// Gets the segment at <paramref name="index"/>.
	/// </summary>
	/// <param name="index">The segment position.</param>
	public ZodPathSegment this[int index] => Segments[index];

	/// <summary>
	/// Gets the legacy string projection: members and keys as-is, indexes as invariant digits.
	/// </summary>
	public ImmutableArray<string> LegacySegments => _legacy.IsDefault ? [] : _legacy;

	/// <summary>
	/// Creates a path from typed segments.
	/// </summary>
	/// <param name="segments">The segments, outermost first.</param>
	/// <returns>The path.</returns>
	public static ZodPath From(params ReadOnlySpan<ZodPathSegment> segments) =>
		segments.IsEmpty ? Root : Create(ImmutableArray.Create(segments));

	/// <summary>
	/// Creates a path from typed segments.
	/// </summary>
	/// <param name="segments">The segments, outermost first.</param>
	/// <returns>The path.</returns>
	public static ZodPath From(ImmutableArray<ZodPathSegment> segments) =>
		segments.IsDefaultOrEmpty ? Root : Create(segments);

	/// <summary>
	/// Creates a single-member path.
	/// </summary>
	/// <param name="name">The member name.</param>
	/// <returns>The path.</returns>
	public static ZodPath Member(string name) => Create([ZodPathSegment.Member(name)]);

	/// <summary>
	/// Classifies legacy string segments. A segment written as <c>[n]</c> or consisting only of ASCII digits is
	/// an index; anything else is a member. This keeps paths supplied as plain strings (refinements, existing
	/// callers) working.
	/// </summary>
	/// <param name="segments">The legacy segments.</param>
	/// <returns>The path.</returns>
	public static ZodPath FromLegacy(IEnumerable<string>? segments)
	{
		if (segments is null)
			return Root;

		var builder = ImmutableArray.CreateBuilder<ZodPathSegment>();
		foreach (var segment in segments)
			builder.Add(ClassifyLegacy(segment));

		return builder.Count == 0 ? Root : Create(builder.ToImmutable());
	}

	/// <summary>
	/// Classifies legacy string segments; see <see cref="FromLegacy(IEnumerable{string})"/>.
	/// </summary>
	/// <param name="segments">The legacy segments.</param>
	/// <returns>The path.</returns>
	public static ZodPath FromLegacy(ImmutableArray<string> segments)
	{
		if (segments.IsDefaultOrEmpty)
			return Root;

		var builder = ImmutableArray.CreateBuilder<ZodPathSegment>(segments.Length);
		foreach (var segment in segments)
			builder.Add(ClassifyLegacy(segment));

		return Create(builder.MoveToImmutable());
	}

	/// <summary>
	/// Returns a new path with <paramref name="segment"/> placed before the existing segments.
	/// </summary>
	/// <param name="segment">The outer segment.</param>
	/// <returns>The new path.</returns>
	public ZodPath Prepend(ZodPathSegment segment) =>
		new(Segments.Insert(0, segment), LegacySegments.Insert(0, segment.ToString()));

	/// <summary>
	/// Returns a new path with <paramref name="prefix"/> placed before the existing segments.
	/// </summary>
	/// <param name="prefix">The outer path.</param>
	/// <returns>The new path.</returns>
	public ZodPath Prepend(ZodPath prefix) =>
		prefix.IsRoot ? this
		: IsRoot ? prefix
		: new(prefix.Segments.AddRange(Segments), prefix.LegacySegments.AddRange(LegacySegments));

	/// <summary>
	/// Returns a new path with <paramref name="segment"/> added after the existing segments.
	/// </summary>
	/// <param name="segment">The inner segment.</param>
	/// <returns>The new path.</returns>
	public ZodPath Append(ZodPathSegment segment) => new(Segments.Add(segment), LegacySegments.Add(segment.ToString()));

	/// <summary>
	/// Returns a new path with <paramref name="suffix"/> added after the existing segments.
	/// </summary>
	/// <param name="suffix">The inner path.</param>
	/// <returns>The new path.</returns>
	public ZodPath Append(ZodPath suffix) => suffix.Prepend(this);

	/// <summary>
	/// Renders the path with members joined by <c>.</c> and indexes and keys bracketed:
	/// <c>Items[0].Name</c>, <c>Map[en].Value</c>, <c>Map["a.b"]</c>. The root renders as an empty string.
	/// </summary>
	/// <param name="namer">Optional member renaming (for example a JSON naming policy).</param>
	/// <returns>The dotted path.</returns>
	public string ToDottedString(IZodPathNamer? namer = null)
	{
		if (IsRoot)
			return string.Empty;

		StringBuilder builder = new();
		foreach (var segment in Segments)
		{
			switch (segment.Kind)
			{
				case ZodPathSegmentKind.Index:
					builder.Append('[').Append(segment.Index.ToString(CultureInfo.InvariantCulture)).Append(']');
					break;
				case ZodPathSegmentKind.Key:
					AppendBracketedKey(builder, segment.Name!);
					break;
				case ZodPathSegmentKind.Member:
				default:
					var name = namer?.GetName(segment.Name!) ?? segment.Name!;
					if (IsPlainMemberName(name))
					{
						if (builder.Length > 0)
							builder.Append('.');
						builder.Append(name);
					}
					else
					{
						AppendQuoted(builder, name);
					}

					break;
			}
		}

		return builder.ToString();
	}

	/// <summary>
	/// Renders the path as an RFC 6901 JSON Pointer (<c>/items/0/name</c>). The root renders as an empty
	/// string.
	/// </summary>
	/// <param name="namer">Optional member renaming.</param>
	/// <returns>The JSON Pointer.</returns>
	public string ToJsonPointer(IZodPathNamer? namer = null)
	{
		if (IsRoot)
			return string.Empty;

		StringBuilder builder = new();
		foreach (var segment in Segments)
		{
			builder.Append('/');
			var text = segment.Kind switch
			{
				ZodPathSegmentKind.Index => segment.Index.ToString(CultureInfo.InvariantCulture),
				ZodPathSegmentKind.Member => namer?.GetName(segment.Name!) ?? segment.Name!,
				_ => segment.Name!,
			};
			builder.Append(
				text.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)
			);
		}

		return builder.ToString();
	}

	/// <summary>
	/// Renders the path as a JSONPath expression in the form System.Text.Json reports
	/// (<c>$.items[0].name</c>, <c>$['a.b']</c>).
	/// </summary>
	/// <param name="namer">Optional member renaming.</param>
	/// <returns>The JSONPath.</returns>
	public string ToJsonPath(IZodPathNamer? namer = null)
	{
		StringBuilder builder = new("$");
		foreach (var segment in Segments)
		{
			switch (segment.Kind)
			{
				case ZodPathSegmentKind.Index:
					builder.Append('[').Append(segment.Index.ToString(CultureInfo.InvariantCulture)).Append(']');
					break;
				case ZodPathSegmentKind.Member:
				case ZodPathSegmentKind.Key:
				default:
					var name =
						segment.Kind == ZodPathSegmentKind.Member
							? namer?.GetName(segment.Name!) ?? segment.Name!
							: segment.Name!;
					if (IsPlainMemberName(name))
						builder.Append('.').Append(name);
					else
						builder.Append("['").Append(name.Replace("'", "\\'", StringComparison.Ordinal)).Append("']");
					break;
			}
		}

		return builder.ToString();
	}

	/// <summary>
	/// Renders the path as a <c>Microsoft.Extensions.Configuration</c> key (<c>Servers:0:Host</c>).
	/// </summary>
	/// <returns>The configuration key.</returns>
	public string ToConfigurationKey() => string.Join(':', LegacySegments);

	/// <summary>
	/// Returns the path in Zod's issue shape: strings for members and keys, integers for indexes.
	/// </summary>
	/// <returns>The Zod path.</returns>
	public IReadOnlyList<object> ToZodPath()
	{
		if (IsRoot)
			return [];

		var result = new object[Length];
		for (var i = 0; i < result.Length; i++)
		{
			var segment = Segments[i];
			result[i] = segment.Kind == ZodPathSegmentKind.Index ? segment.Index : segment.Name!;
		}

		return result;
	}

	/// <summary>
	/// Parses a JSONPath as reported by System.Text.Json (<c>$.a[0]['b.c']</c>) or Newtonsoft.Json
	/// (<c>a[0].b</c>, <c>['b.c']</c>). Property names become member segments.
	/// </summary>
	/// <param name="jsonPath">The JSONPath; <see langword="null"/> or empty yields the root.</param>
	/// <returns>The parsed path.</returns>
	public static ZodPath ParseJsonPath(string? jsonPath)
	{
		if (string.IsNullOrEmpty(jsonPath))
			return Root;

		var span = jsonPath.AsSpan();
		if (span[0] == '$')
			span = span[1..];

		return Parse(span, quotedIsKey: false);
	}

	/// <summary>
	/// Parses the output of <see cref="ToDottedString"/> (and plain <c>Microsoft.Extensions.Validation</c>
	/// keys such as <c>Items[0].Name</c>). A bracketed all-digit value is an index; any other bracketed value is
	/// a key.
	/// </summary>
	/// <param name="dotted">The dotted path; <see langword="null"/> or empty yields the root.</param>
	/// <returns>The parsed path.</returns>
	public static ZodPath ParseDotted(string? dotted) =>
		string.IsNullOrEmpty(dotted) ? Root : Parse(dotted.AsSpan(), quotedIsKey: true);

	/// <inheritdoc/>
	public bool Equals(ZodPath other) => Segments.AsSpan().SequenceEqual(other.Segments.AsSpan());

	/// <inheritdoc/>
	public override bool Equals(object? obj) => obj is ZodPath other && Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		HashCode hash = new();
		foreach (var segment in Segments)
			hash.Add(segment);
		return hash.ToHashCode();
	}

	/// <summary>
	/// Returns <see cref="ToDottedString"/>.
	/// </summary>
	/// <returns>The dotted path.</returns>
	public override string ToString() => ToDottedString();

	/// <summary>Equality operator.</summary>
	public static bool operator ==(ZodPath left, ZodPath right) => left.Equals(right);

	/// <summary>Inequality operator.</summary>
	public static bool operator !=(ZodPath left, ZodPath right) => !left.Equals(right);

	static ZodPath Create(ImmutableArray<ZodPathSegment> segments)
	{
		var legacy = ImmutableArray.CreateBuilder<string>(segments.Length);
		foreach (var segment in segments)
			legacy.Add(segment.ToString());

		return new(segments, legacy.MoveToImmutable());
	}

	static ZodPathSegment ClassifyLegacy(string? segment)
	{
		if (string.IsNullOrEmpty(segment))
			return ZodPathSegment.Member(segment ?? string.Empty);

		var span = segment.AsSpan();
		if (span.Length > 2 && span[0] == '[' && span[^1] == ']' && TryParseIndex(span[1..^1], out var bracketed))
			return ZodPathSegment.At(bracketed);

		return TryParseIndex(span, out var index) ? ZodPathSegment.At(index) : ZodPathSegment.Member(segment);
	}

	static bool TryParseIndex(ReadOnlySpan<char> span, out int index)
	{
		index = 0;
		if (span.IsEmpty)
			return false;

		foreach (var c in span)
		{
			if (c is < '0' or > '9')
				return false;
		}

		return int.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out index);
	}

	static bool IsPlainMemberName(string name)
	{
		if (name.Length == 0)
			return false;

		foreach (var c in name)
		{
			if (c is '.' or '[' or ']' or '"' or '\'' || char.IsWhiteSpace(c))
				return false;
		}

		return true;
	}

	static bool IsPlainKey(string key)
	{
		if (key.Length == 0)
			return false;

		var allDigits = true;
		foreach (var c in key)
		{
			if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
				return false;
			allDigits &= char.IsAsciiDigit(c);
		}

		return !allDigits;
	}

	static void AppendBracketedKey(StringBuilder builder, string key)
	{
		// An all-digit key is quoted so it cannot be read back as an index.
		if (IsPlainKey(key))
			builder.Append('[').Append(key).Append(']');
		else
			AppendQuoted(builder, key);
	}

	static void AppendQuoted(StringBuilder builder, string value)
	{
		builder.Append("[\"");
		foreach (var c in value)
		{
			if (c is '"' or '\\')
				builder.Append('\\');
			builder.Append(c);
		}

		builder.Append("\"]");
	}

	static ZodPath Parse(ReadOnlySpan<char> span, bool quotedIsKey)
	{
		var builder = ImmutableArray.CreateBuilder<ZodPathSegment>();
		var i = 0;
		while (i < span.Length)
		{
			var c = span[i];
			if (c == '.')
			{
				i++;
				continue;
			}

			if (c == '[')
			{
				i++;
				if (i < span.Length && span[i] is '"' or '\'')
				{
					var quote = span[i++];
					StringBuilder value = new();
					while (i < span.Length && span[i] != quote)
					{
						if (span[i] == '\\' && i + 1 < span.Length)
							i++;
						value.Append(span[i++]);
					}

					i++; // closing quote
					if (i < span.Length && span[i] == ']')
						i++;

					builder.Add(
						quotedIsKey ? ZodPathSegment.Key(value.ToString()) : ZodPathSegment.Member(value.ToString())
					);
					continue;
				}

				var start = i;
				while (i < span.Length && span[i] != ']')
					i++;

				var content = span[start..i];
				if (i < span.Length)
					i++; // closing bracket

				if (TryParseIndex(content, out var index))
					builder.Add(ZodPathSegment.At(index));
				else
					builder.Add(ZodPathSegment.Key(content.ToString()));

				continue;
			}

			var nameStart = i;
			while (i < span.Length && span[i] is not ('.' or '['))
				i++;

			builder.Add(ZodPathSegment.Member(span[nameStart..i].ToString()));
		}

		return builder.Count == 0 ? Root : Create(builder.ToImmutable());
	}
}
