namespace ZodSharp;

/// <summary>Controls how generated validation handles an uninitialized immutable array.</summary>
public enum DefaultImmutableArrayBehavior
{
	/// <summary>
	/// Treat the default array as absent: Required reports a missing field, a nullable member accepts it,
	/// and a non-nullable member reports an invalid type.
	/// </summary>
	ValidationError,

	/// <summary>Treat the default array as empty, including when evaluating collection count rules.</summary>
	TreatAsEmpty,
}
