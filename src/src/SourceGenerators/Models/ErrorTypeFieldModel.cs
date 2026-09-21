namespace ZodSharp.SourceGenerators.Models;

/// <summary>
/// Value-equatable pipeline model describing a static <c>ErrorType</c> field whose containing
/// type is a partial class. All Roslyn objects are removed before this point.
/// </summary>
sealed record ErrorTypeFieldModel(
	string Namespace,
	string ClassName,
	TypeDeclarationAccessibility Accessibility,
	bool IsStatic,
	bool IsAbstract,
	bool IsSealed,
	string FieldName,
	EquatableArray<ErrorTypeParameterModel> Parameters
)
{
	/// <summary>
	/// Deterministic, per-field hint name.
	/// </summary>
	public string HintName =>
		Namespace.Length == 0 ? $"{ClassName}.{FieldName}.g.cs" : $"{Namespace}.{ClassName}.{FieldName}.g.cs";
}
