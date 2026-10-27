using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZodSharp.CodeFixes;

/// <summary>
/// Code fix for <c>ZODSGEN053</c>. Replaces <c>[MinLength]</c>, <c>[MaxLength]</c> or <c>[Length]</c> on a collection
/// member with the trim-safe <c>[MinCount]</c>, <c>[MaxCount]</c> or <c>[CountRange]</c>, keeping the arguments, and
/// imports <c>ZodSharp.Rules</c> when the file does not already.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionCountAttributeCodeFixProvider))]
[Shared]
public sealed class CollectionCountAttributeCodeFixProvider : CodeFixProvider
{
	/// <summary>The diagnostic id reported by the ZodSharp analyzer.</summary>
	const string DiagnosticId = "ZODSGEN053";

	/// <summary>The diagnostic property naming the replacement attribute.</summary>
	const string ReplacementProperty = "Replacement";

	const string RulesNamespace = "ZodSharp.Rules";

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
		if (
			!diagnostic.Properties.TryGetValue(ReplacementProperty, out var replacement)
			|| string.IsNullOrEmpty(replacement)
		)
			return;

		var attribute = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
			.FirstAncestorOrSelf<AttributeSyntax>();

		// [MaxLength] without an argument means "the largest allowed"; [MaxCount] needs an explicit maximum, so
		// there is nothing to carry over.
		if (attribute?.ArgumentList is not { Arguments.Count: > 0 })
			return;

		var title = $"Use [{replacement}]";
		context.RegisterCodeFix(
			CodeAction.Create(
				title,
				cancellationToken => ReplaceAsync(context.Document, attribute, replacement!, cancellationToken),
				equivalenceKey: title
			),
			diagnostic
		);
	}

	static async Task<Document> ReplaceAsync(
		Document document,
		AttributeSyntax attribute,
		string replacement,
		CancellationToken cancellationToken
	)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is null)
			return document;

		var renamed = attribute.WithName(SyntaxFactory.IdentifierName(replacement).WithTriviaFrom(attribute.Name));
		var updated = root.ReplaceNode(attribute, renamed);

		if (updated is CompilationUnitSyntax compilationUnit && !Imports(compilationUnit, RulesNamespace))
		{
			updated = compilationUnit.AddUsings(
				SyntaxFactory
					.UsingDirective(SyntaxFactory.ParseName(RulesNamespace))
					.WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
			);
		}

		return document.WithSyntaxRoot(updated);
	}

	static bool Imports(CompilationUnitSyntax compilationUnit, string namespaceName)
	{
		foreach (var directive in compilationUnit.Usings)
		{
			if (
				directive.Alias is null
				&& string.Equals(directive.Name?.ToString(), namespaceName, StringComparison.Ordinal)
			)
				return true;
		}

		// A using inside a namespace declaration imports it too.
		foreach (var member in compilationUnit.Members)
		{
			if (member is not BaseNamespaceDeclarationSyntax declaration)
				continue;

			foreach (var directive in declaration.Usings)
			{
				if (
					directive.Alias is null
					&& string.Equals(directive.Name?.ToString(), namespaceName, StringComparison.Ordinal)
				)
					return true;
			}
		}

		return false;
	}
}
