using ZodSharp.Schemas;

namespace ZodSharp.Core;

public class SchemaCacheTests
{
	[Test]
	public async Task GetOrCreate_GivenMissingKey_InvokesFactory()
	{
		var key = CreateKey();
		var invocationCount = 0;

		var schema = SchemaCache.GetOrCreate(key, () =>
		{
			invocationCount++;
			return Z.String();
		});

		try
		{
			await Assert.That(schema).IsNotNull();
			await Assert.That(invocationCount).IsEqualTo(1);
		}
		finally
		{
			SchemaCache.Remove(key);
		}
	}

	[Test]
	public async Task GetOrCreate_GivenExistingKey_ReturnsCachedInstanceWithoutInvokingFactoryAgain()
	{
		var key = CreateKey();
		var invocationCount = 0;

		try
		{
			var first = SchemaCache.GetOrCreate(key, () =>
			{
				invocationCount++;
				return Z.String();
			});

			var second = SchemaCache.GetOrCreate(key, () =>
			{
				invocationCount++;
				return Z.String().Min(10);
			});

			await Assert.That(ReferenceEquals(first, second)).IsTrue();
			await Assert.That(invocationCount).IsEqualTo(1);
		}
		finally
		{
			SchemaCache.Remove(key);
		}
	}

	[Test]
	public async Task TryGet_GivenCachedKey_ReturnsTrueAndValue()
	{
		var key = CreateKey();

		try
		{
			var schema = SchemaCache.GetOrCreate(key, Z.String);

			var found = SchemaCache.TryGet<ZodString>(key, out var cachedSchema);

			await Assert.That(found).IsTrue();
			await Assert.That(ReferenceEquals(schema, cachedSchema)).IsTrue();
		}
		finally
		{
			SchemaCache.Remove(key);
		}
	}

	[Test]
	public async Task Remove_GivenCachedSchema_RemovesSchema()
	{
		var key = CreateKey();
		SchemaCache.GetOrCreate(key, Z.String);

		var removed = SchemaCache.Remove(key);
		var found = SchemaCache.TryGet<ZodString>(key, out _);

		await Assert.That(removed).IsTrue();
		await Assert.That(found).IsFalse();
	}

	static string CreateKey() => $"{nameof(SchemaCacheTests)}-{Guid.NewGuid():N}";
}
