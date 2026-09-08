namespace ZodSharp.SourceGenerators.Helpers;

static class CodeGenHelpers
{
	public static CodeWriter Rule(
		this CodeWriter writer,
		string propertyName,
		string comparison,
		string errorCode,
		string errorMessage
	)
	{
		using (writer.IfBlockScope(comparison))
		{
			writer.IfBlock(
				"errors is null",
				ifBody =>
					ifBody.Assignment(
						"errors",
						$"new {TypeLibrary.System.Collections.Generic.List.MakeGeneric(TypeLibrary.ZodSharp.Core.ValidationError)}()"
					)
			);

			writer.OpenDelimitedBlock(
				$"errors.Add(new {TypeLibrary.ZodSharp.Core.ValidationError}",
				"(",
				"));",
				bodyWriter =>
				{
					bodyWriter.Write(errorCode.Surround()).Line(",");
					bodyWriter.Write(errorMessage.StringLiteral()).Line(",");
					bodyWriter.Line($"new[] {{ \"{propertyName}\" }}");
				}
			);
		}

		return writer.NewLine();
	}

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
