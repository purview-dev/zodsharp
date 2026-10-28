// Stand-ins for the Purview.ValueObjects contracts the ZodSharp generator recognizes, so this consumer depends on
// nothing but the packed ZodSharp packages. In a real consumer the value-object generator emits the adapter.
namespace Purview.ValueObjects.Serialization
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class ScalarAttribute(string propertyName = "Value") : Attribute
	{
		public string PropertyName { get; } = propertyName;
	}
}

namespace Purview.ValueObjects
{
	public interface IScalarValueObject<TSelf, TValue>
		where TSelf : IScalarValueObject<TSelf, TValue>
	{
		TValue Value { get; }
	}

	public readonly record struct ScalarRuleAdapter<TSelf, TValue, TRule>(TRule Rule)
		: ZodSharp.Core.IValidationRule<TSelf>
		where TSelf : IScalarValueObject<TSelf, TValue>
		where TRule : ZodSharp.Core.IValidationRule<TValue>
	{
		// The validation-rule conventions (ZODSGEN042) apply to every rule declared in a compilation.
		public const string ErrorCode = "validation_failed";

		public const string MessageFormat = "{displayName} is invalid.";

		public bool IsValid(in TSelf value) => Rule.IsValid(value.Value);

		public string GetErrorMessage(in TSelf value) => Rule.GetErrorMessage(value.Value);
	}
}
