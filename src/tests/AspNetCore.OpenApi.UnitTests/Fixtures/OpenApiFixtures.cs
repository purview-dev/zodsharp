using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ZodSharp.Rules;

namespace ZodSharp.AspNetCore.OpenApi.Fixtures;

/// <summary>Every string rule row of the keyword mapping.</summary>
[ZodSchema]
public partial class StringRulesRequest
{
	[MinLengthZod(2)]
	[MaxLengthZod(10)]
	public string Name { get; set; } = string.Empty;

	[Email]
	public string? Email { get; set; }

	/// <summary>The platform does not map <c>[EmailAddress]</c>; ZodSharp fills the gap.</summary>
	[EmailAddress]
	public string? LegacyEmail { get; set; }

	[UrlZod]
	public string? Website { get; set; }

	[Regex("^[a-z]+$")]
	public string? Slug { get; set; }

	[StartsWith("ab")]
	public string? Prefixed { get; set; }

	[EndsWith("yz")]
	public string? Suffixed { get; set; }

	[Includes("mid")]
	public string? Containing { get; set; }

	/// <summary>Two pattern rules on one member promote to <c>allOf</c>.</summary>
	[StartsWith("ab")]
	[EndsWith("yz")]
	public string? Bracketed { get; set; }

	[UUID(UuidVersion.V4)]
	public string? Identifier { get; set; }

	[IPAddress(IPAddressRuleType.IPv4)]
	public string? AddressV4 { get; set; }

	[IPAddress(IPAddressRuleType.IPv6)]
	public string? AddressV6 { get; set; }

	[Cidr]
	public string? Network { get; set; }

	[DatetimeString]
	public string? Timestamp { get; set; }

	[DateString]
	public string? Day { get; set; }

	[TimeString]
	public string? TimeOfDay { get; set; }

	[Duration]
	public string? Period { get; set; }

	[Base64StringZod]
	public string? Payload { get; set; }

	[JWT]
	public string? Token { get; set; }

	[AllowedValuesZod("red", "green")]
	public string? Colour { get; set; }

	[DeniedValuesZod("admin")]
	public string? Username { get; set; }

	[Display(Description = "The display description.")]
	[MaxLengthZod(50)]
	public string? Described { get; set; }
}

/// <summary>Every numeric rule row of the keyword mapping.</summary>
[ZodSchema]
public partial class NumberRulesRequest
{
	[MinValue(1)]
	[MaxValue(10)]
	public int Count { get; set; }

	[GreaterThan(0d)]
	[LessThan(100d)]
	public double Ratio { get; set; }

	[GreaterThanOrEqual(5)]
	[LessThanOrEqual(6)]
	public int Bounded { get; set; }

	[MultipleOf(5)]
	public int Step { get; set; }

	[Int]
	public double Whole { get; set; }

	[SafeInteger]
	public double Safe { get; set; }
}

public enum Shade
{
	Light,
	Dark,
}

/// <summary>Collections, maps, nullability, enums, temporal values and nested objects.</summary>
[ZodSchema]
public partial class ShapesRequest
{
	[MinLength(1)]
	[MaxLength(3)]
	public List<string> Tags { get; init; } = [];

	[MaxLength(2)]
	public Dictionary<string, int> Scores { get; init; } = [];

	public string? Nickname { get; set; }

	/// <summary>Nullable in C#, but ZodSharp rejects null: the platform's null type is removed.</summary>
	[RequiredZod]
	public string? Reference { get; set; }

	public string Title { get; set; } = string.Empty;

	public int Quantity { get; set; }

	public Shade Shade { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateOnly Birthday { get; set; }

	public TimeSpan Elapsed { get; set; }

	public Address Home { get; set; } = new();

	public List<Address> Others { get; init; } = [];
}

[ZodSchema]
public partial class Address
{
	[MinLengthZod(1)]
	public string Street { get; set; } = string.Empty;

	[Regex("^[0-9]{5}$")]
	public string? PostCode { get; set; }
}

/// <summary>DataAnnotations the platform maps, where ZodSharp agrees or disagrees.</summary>
[ZodSchema]
public partial class ConflictRequest
{
	/// <summary>Both say <c>maxLength: 10</c>: left untouched.</summary>
	[StringLength(10)]
	public string? Same { get; set; }

	/// <summary>The platform says <c>maximum: 100</c>, ZodSharp enforces 50: ZodSharp wins.</summary>
	[Range(1, 100)]
	[MaxValue(50)]
	public int Different { get; set; }
}

/// <summary>Bound from the query string through <c>[AsParameters]</c>.</summary>
[ZodSchema]
public partial class SearchQuery
{
	[MinLengthZod(3)]
	public string? Term { get; set; }

	[MaxValue(50)]
	public int Take { get; set; }
}

/// <summary>A type ZodSharp does not know: the platform's schema is left alone.</summary>
public class PlainRequest
{
	[Range(1, 5)]
	public int Stars { get; set; }

	[MaxLength(20)]
	public string? Comment { get; set; }
}

[ZodSchema]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CircleShape), "circle")]
[JsonDerivedType(typeof(SquareShape), "square")]
public abstract partial class ShapeBase
{
	public string? Label { get; set; }
}

public sealed class CircleShape : ShapeBase
{
	public double Radius { get; set; }
}

public sealed class SquareShape : ShapeBase
{
	public double Side { get; set; }
}

/// <summary>
/// A value object ZodSharp knows only through a runtime schema registered in <c>IZodSchemaFactory</c>.
/// </summary>
[JsonConverter(typeof(PercentageConverter))]
public readonly record struct Percentage(double Value);

public sealed class PercentageConverter : JsonConverter<Percentage>
{
	public override Percentage Read(
		ref System.Text.Json.Utf8JsonReader reader,
		Type typeToConvert,
		System.Text.Json.JsonSerializerOptions options
	) => new(reader.GetDouble());

	public override void Write(
		System.Text.Json.Utf8JsonWriter writer,
		Percentage value,
		System.Text.Json.JsonSerializerOptions options
	)
	{
		ArgumentNullException.ThrowIfNull(writer);
		writer.WriteNumberValue(value.Value);
	}
}

/// <summary>A hand-written runtime schema that describes its own JSON Schema.</summary>
public sealed class PercentageSchema : Core.ZodType<Percentage>, JsonSchema.IJsonSchemaProvider
{
	public JsonSchema.JsonSchemaDefinition BuildJsonSchema() =>
		new()
		{
			Type = "number",
			Minimum = 0,
			Maximum = 100,
		};

	protected override Core.ValidationResult<Percentage> ParseInternal(Percentage value) =>
		value.Value is >= 0 and <= 100
			? Core.ValidationResult<Percentage>.Success(value)
			: Core.ValidationResult<Percentage>.Failure(new Core.ValidationError("too_big", "Out of range", []));
}

[ZodSchema]
public partial class CatCase
{
	[MinLengthZod(2)]
	public string Name { get; set; } = string.Empty;
}

[ZodSchema]
public partial class DogCase
{
	[MinValue(0)]
	public int Barks { get; set; }
}

/// <summary>An enum that asks System.Text.Json for member names itself, through its own converter.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Tone>))]
public enum Tone
{
	Warm,
	Cool,
}

/// <summary>Enum members: one serialized by the options (numbers unless a global converter says otherwise), one by
/// its type's own converter.</summary>
[ZodSchema]
public partial class EnumsRequest
{
	public Shade Shade { get; set; }

	public Tone Tone { get; set; }
}
