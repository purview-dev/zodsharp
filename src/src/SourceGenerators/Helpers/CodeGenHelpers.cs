namespace ZodSharp.SourceGenerators.Helpers;

static class CodeGenHelpers
{
	public static string GetPathFieldName(string propertyName) => $"Path_{propertyName}";

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
