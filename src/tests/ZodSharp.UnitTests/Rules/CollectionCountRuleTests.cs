using System.Collections;
using ZodSharp.Core;

namespace ZodSharp.Rules;

public class CollectionCountRuleTests
{
	static IEnumerable Lazy(int count)
	{
		for (var i = 0; i < count; i++)
			yield return i;
	}

	[Test]
	[Arguments(1, true)]
	[Arguments(2, true)]
	[Arguments(0, false)]
	public async Task MinCount_IsValid_GivenCollection_ComparesTheCount(
		int count,
		bool expected,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		MinCountRule rule = new(1);
		List<int> values = [.. Enumerable.Range(0, count)];

		// Act
		var isValid = rule.IsValid(values);

		// Assert
		await Assert.That(isValid).IsEqualTo(expected);
	}

	[Test]
	public async Task MaxCount_IsValid_GivenNonCollectionEnumerable_CountsByEnumerating(
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		MaxCountRule rule = new(2);

		// Act
		var withinBound = rule.IsValid(Lazy(2));
		var aboveBound = rule.IsValid(Lazy(3));

		// Assert
		await Assert.That(withinBound).IsTrue();
		await Assert.That(aboveBound).IsFalse();
	}

	[Test]
	[Arguments(0, false)]
	[Arguments(1, true)]
	[Arguments(3, true)]
	[Arguments(4, false)]
	public async Task CountRange_IsValid_GivenDictionary_CountsEntries(
		int count,
		bool expected,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		CountRangeRule rule = new(1, 3);
		var entries = Enumerable
			.Range(0, count)
			.ToDictionary(static i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));

		// Act
		var isValid = rule.IsValid(entries);

		// Assert
		await Assert.That(isValid).IsEqualTo(expected);
	}

	[Test]
	public async Task MinCount_IsValid_GivenNull_ReturnsTrue(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange — null is [Required]'s concern, as for every size rule.
		MinCountRule rule = new(1);

		// Act
		var isValid = rule.IsValid(null!);

		// Assert
		await Assert.That(isValid).IsTrue();
	}

	[Test]
	public async Task MinCount_GetErrorMessage_GivenFailure_FormatsTheBoundAndCount(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		MinCountRule rule = new(2);

		// Act
		var message = rule.GetErrorMessage(new List<int> { 1 });

		// Assert
		await Assert.That(message).IsEqualTo("Collection must contain at least 2 element(s), but got 1");
	}

	[Test]
	public async Task CountRules_Identity_MatchesTheCollectionSizeErrors(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		// Arrange
		IZodRule min = new MinCountRule(1);
		IZodRule max = new MaxCountRule(3);
		IZodRule range = new CountRangeRule(1, 3);

		// Act / Assert
		await Assert.That(min.Code).IsEqualTo(MinCountRule.ErrorCode);
		await Assert.That(min.Category).IsEqualTo(ZodIssueCategories.TooSmall);
		await Assert.That(min.Origin).IsEqualTo(ZodTypeNames.Array);
		await Assert.That(max.Code).IsEqualTo(MaxCountRule.ErrorCode);
		await Assert.That(max.Category).IsEqualTo(ZodIssueCategories.TooBig);
		await Assert.That(range.Lower).IsEqualTo(ErrorBound.Including(1));
		await Assert.That(range.Upper).IsEqualTo(ErrorBound.Including(3));
	}
}
