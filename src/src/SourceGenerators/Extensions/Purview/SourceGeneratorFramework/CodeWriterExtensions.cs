#pragma warning disable CS1591
using System.ComponentModel;

namespace Purview.SourceGeneratorFramework;

[EditorBrowsable(EditorBrowsableState.Never)]
static class CodeWriterExtensions
{
	extension(CodeWriter writer)
	{
		public CodeWriter Rule(string propertyName, string comparison, string errorCode, string errorMessage)
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
	}
}
