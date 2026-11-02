using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZodSharp.CodeFixes;

/// <summary>
/// Code fix for <c>ZODSGEN047</c>. Removes a redundant <c>[ValidatableType]</c> from a <c>[ZodSchema]</c> type,
/// removing the whole attribute list when it was the only attribute in it.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantValidatableTypeCodeFixProvider))]
[Shared]
public sealed class RedundantValidatableTypeCodeFixProvider : CodeFixProvider
{
	/// <summary>The diagnostic id reported by the ZodSharp analyzer.</summary>
	const string DiagnosticId = "ZODSGEN047";

	const string Title = "Remove [ValidatableType]";

	/// <inheritdoc/>
	public override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticId];

	/// <inheritdoc/>
	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc/>
	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var diagnostic = context.Diagnostics[0];
		var attribute = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
			.FirstAncestorOrSelf<AttributeSyntax>();
		if (attribute is null)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				Title,
				cancellationToken => RemoveAsync(context.Document, attribute, cancellationToken),
				equivalenceKey: Title
			),
			diagnostic
		);
	}

	static async Task<Document> RemoveAsync(
		Document document,
		AttributeSyntax attribute,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null || attribute.Parent is not AttributeListSyntax list)
			return document;

		var updated =
			list.Attributes.Count == 1
				? root.RemoveNode(list, SyntaxRemoveOptions.KeepExteriorTrivia)
				: root.ReplaceNode(list, list.WithAttributes(list.Attributes.Remove(attribute)));

		return updated is null ? document : document.WithSyntaxRoot(updated);
	}
}
