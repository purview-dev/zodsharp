namespace ZodSharp.Core;

public class ZodPathTests
{
	[Test]
	public async Task ToDottedString_GivenMembersAndIndexes_RendersBracketedIndexes()
	{
		// Arrange
		var path = ZodPath.From(ZodPathSegment.Member("Items"), ZodPathSegment.At(0), ZodPathSegment.Member("Name"));

		// Act
		var dotted = path.ToDottedString();

		// Assert
		await Assert.That(dotted).IsEqualTo("Items[0].Name");
	}

	[Test]
	[Arguments("en", "Map[en]")]
	[Arguments("0", "Map[\"0\"]")]
	[Arguments("a.b", "Map[\"a.b\"]")]
	[Arguments("say \"hi\"", "Map[\"say \\\"hi\\\"\"]")]
	public async Task ToDottedString_GivenKey_QuotesOnlyWhenAmbiguous(string key, string expected)
	{
		// Arrange
		var path = ZodPath.From(ZodPathSegment.Member("Map"), ZodPathSegment.Key(key));

		// Act
		var dotted = path.ToDottedString();

		// Assert
		await Assert.That(dotted).IsEqualTo(expected);
	}

	[Test]
	public async Task ToDottedString_GivenRoot_ReturnsEmpty()
	{
		// Act
		var dotted = ZodPath.Root.ToDottedString();

		// Assert
		await Assert.That(dotted).IsEqualTo(string.Empty);
	}

	[Test]
	public async Task ToDottedString_GivenNamer_RenamesMembersOnly()
	{
		// Arrange
		var path = ZodPath.From(ZodPathSegment.Member("Items"), ZodPathSegment.At(2), ZodPathSegment.Key("Key"));
		var namer = ZodPathNamer.FromFunc(static name => name.ToLowerInvariant());

		// Act
		var dotted = path.ToDottedString(namer);

		// Assert
		await Assert.That(dotted).IsEqualTo("items[2][Key]");
	}

	[Test]
	[Arguments("Items[0].Name")]
	[Arguments("Map[en].Value")]
	[Arguments("Map[\"0\"]")]
	[Arguments("Map[\"a.b\"].Value")]
	[Arguments("Customer.ShippingAddress.Street")]
	public async Task ParseDotted_GivenDottedString_RoundTrips(string dotted)
	{
		// Act
		var path = ZodPath.ParseDotted(dotted);

		// Assert
		await Assert.That(path.ToDottedString()).IsEqualTo(dotted);
	}

	[Test]
	public async Task ParseDotted_GivenDigitsInBrackets_ProducesIndexSegment()
	{
		// Act
		var path = ZodPath.ParseDotted("Items[3].Name");

		// Assert
		await Assert.That(path[1]).IsEqualTo(ZodPathSegment.At(3));
	}

	[Test]
	[Arguments("$.items[0].name", "items[0].name")]
	[Arguments("$['a.b'].c", "[\"a.b\"].c")]
	[Arguments("items[0].name", "items[0].name")]
	[Arguments("$", "")]
	public async Task ParseJsonPath_GivenSystemTextJsonOrNewtonsoftPath_ParsesSegments(
		string jsonPath,
		string expectedDotted
	)
	{
		// Act
		var path = ZodPath.ParseJsonPath(jsonPath);

		// Assert
		await Assert.That(path.ToDottedString()).IsEqualTo(expectedDotted);
	}

	[Test]
	public async Task ToJsonPointer_GivenSpecialCharacters_EscapesPerRfc6901()
	{
		// Arrange
		var path = ZodPath.From(ZodPathSegment.Key("a/b~c"), ZodPathSegment.At(1));

		// Act
		var pointer = path.ToJsonPointer();

		// Assert
		await Assert.That(pointer).IsEqualTo("/a~1b~0c/1");
	}

	[Test]
	public async Task ToJsonPath_GivenMembersAndIndex_RendersSystemTextJsonForm()
	{
		// Arrange
		var path = ZodPath.From(ZodPathSegment.Member("items"), ZodPathSegment.At(0), ZodPathSegment.Key("a.b"));

		// Act
		var jsonPath = path.ToJsonPath();

		// Assert
		await Assert.That(jsonPath).IsEqualTo("$.items[0]['a.b']");
	}

	[Test]
	public async Task ToConfigurationKey_GivenPath_JoinsWithColons()
	{
		// Arrange
		var path = ZodPath.From(ZodPathSegment.Member("Servers"), ZodPathSegment.At(0), ZodPathSegment.Member("Host"));

		// Act
		var key = path.ToConfigurationKey();

		// Assert
		await Assert.That(key).IsEqualTo("Servers:0:Host");
	}

	[Test]
	public async Task ToZodPath_GivenIndex_ReturnsIntegerSegment()
	{
		// Arrange
		var path = ZodPath.From(ZodPathSegment.Member("items"), ZodPathSegment.At(4));

		// Act
		var zodPath = path.ToZodPath();

		// Assert
		await Assert.That(zodPath[0]).IsEqualTo("items");
		await Assert.That(zodPath[1]).IsEqualTo(4);
	}

	[Test]
	[Arguments("[2]")]
	[Arguments("2")]
	public async Task FromLegacy_GivenIndexSegment_ClassifiesAsIndex(string segment)
	{
		// Act
		var path = ZodPath.FromLegacy(["Items", segment]);

		// Assert
		await Assert.That(path[1]).IsEqualTo(ZodPathSegment.At(2));
		await Assert.That(path.LegacySegments[1]).IsEqualTo("2");
	}

	[Test]
	public async Task Prepend_GivenSegment_UpdatesSegmentsAndLegacyProjection()
	{
		// Arrange
		var path = ZodPath.Member("Name");

		// Act
		var prefixed = path.Prepend(ZodPathSegment.At(1)).Prepend(ZodPathSegment.Member("Items"));

		// Assert
		await Assert.That(prefixed.ToDottedString()).IsEqualTo("Items[1].Name");
		await Assert.That(prefixed.LegacySegments.SequenceEqual(["Items", "1", "Name"])).IsTrue();
	}

	[Test]
	public async Task Equals_GivenSameSegments_AreEqual()
	{
		// Arrange
		var first = ZodPath.ParseDotted("Items[0].Name");
		var second = ZodPath.From(ZodPathSegment.Member("Items"), ZodPathSegment.At(0), ZodPathSegment.Member("Name"));

		// Assert
		await Assert.That(first == second).IsTrue();
		await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
	}

	[Test]
	public async Task Key_GivenFormattableKey_RendersInvariantCulture()
	{
		// Arrange
		var previous = System.Globalization.CultureInfo.CurrentCulture;
		System.Globalization.CultureInfo.CurrentCulture = new("de-DE");
		try
		{
			// Act
			var segment = ZodPathSegment.Key(1.5);

			// Assert
			await Assert.That(segment.Name).IsEqualTo("1.5");
		}
		finally
		{
			System.Globalization.CultureInfo.CurrentCulture = previous;
		}
	}
}
