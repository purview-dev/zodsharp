namespace ZodSharp.SourceGenerators.Helpers;

static class CodeGenHelpers
{
	public static string GetMemberFieldName(string propertyName) => $"Member_{propertyName}";

	public static string GetMemberMethodName(string propertyName) => $"ValidateMember_{propertyName}";

	public static string GetMemberAsyncMethodName(string propertyName) => $"ValidateMemberAsync_{propertyName}";

	public static string GetAsyncAttributeFieldName(string propertyName, int index) =>
		$"AsyncAttribute_{propertyName}_{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

	public static string GetLocalIdentifier(string propertyName, string suffix) =>
		string.IsNullOrEmpty(propertyName)
			? suffix
			: char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1) + suffix;

	public static string QuoteChar(char value)
	{
		return value switch
		{
			'\'' => "'\\''",
			'\\' => "'\\\\'",
			'\0' => "'\\0'",
			'\a' => "'\\a'",
			'\b' => "'\\b'",
			'\f' => "'\\f'",
			'\n' => "'\\n'",
			'\r' => "'\\r'",
			'\t' => "'\\t'",
			'\v' => "'\\v'",
			_ => $"'{value}'",
		};
	}
}
