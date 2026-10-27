using System.Collections;

namespace ZodSharp.Rules;

/// <summary>
/// Counts a collection without reflection: through <see cref="ICollection"/> when it is implemented, otherwise by
/// enumerating it.
/// </summary>
static class CollectionCount
{
	public static int Of(IEnumerable? value)
	{
		switch (value)
		{
			case null:
				return 0;
			case ICollection collection:
				return collection.Count;
			default:
				var count = 0;
				var enumerator = value.GetEnumerator();
				try
				{
					while (enumerator.MoveNext())
						count++;
				}
				finally
				{
					(enumerator as IDisposable)?.Dispose();
				}

				return count;
		}
	}
}
