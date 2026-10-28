using ZodSharp.SourceGenerators.Models;

namespace ZodSharp.SourceGenerators.Helpers;

static class AttributeGenHelper
{
	/// <summary>
	/// The name of the optional <c>AllowNulls</c> property on the <c>[ZodSchema]</c> attribute (<c>ZodSharp.ZodSchemaAttribute</c>, defined in Purview.ZodSharp).
	/// </summary>
	/// <remarks>
	/// The property is read from the raw <c>AttributeData</c> rather than through
	/// <see cref="ZodSchemaAttributeData"/>: the framework's attribute-data-model generator collapses a named
	/// argument to a plain value, so an unset <c>bool</c> is indistinguishable from an explicit <c>false</c>.
	/// Reading the raw attribute preserves the unset state the precedence chain needs. A nullable
	/// <c>bool?</c> cannot be used instead, because <see cref="Nullable{T}"/> is not a legal attribute
	/// parameter type.
	/// </remarks>
	internal const string AllowNullsPropertyName = "AllowNulls";

	/// <summary>
	/// The name of the <c>DateTimeOffsetSerialization</c> property on
	/// <c>[assembly: ZodSharpSchemaDefaults(...)]</c>.
	/// </summary>
	internal const string DateTimeOffsetSerializationPropertyName = "DateTimeOffsetSerialization";
}
