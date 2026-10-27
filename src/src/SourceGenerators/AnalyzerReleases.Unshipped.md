; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every rule is recorded in AnalyzerReleases.Shipped.md. Add a new or changed diagnostic here, and move it
; across when the next version ships.
;
; ZODSGEN002 and ZODSGEN022-ZODSGEN026 were retired during development and are never reused.

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ZODSGEN047 | ZodSharp.SourceGenerator | Info | [ValidatableType] (Microsoft.Extensions.Validation) is redundant on a [ZodSchema] type
ZODSGEN048 | ZodSharp.SourceGenerator | Warning | A type has asynchronous-only rules (AsyncValidationAttribute, IAsyncValidatableObject) but a synchronous Validate is generated
ZODSGEN049 | ZodSharp.SourceGenerator | Warning | A member typed as a closed class has a derived type without [ZodSchema]
ZODSGEN050 | ZodSharp.SourceGenerator | Error | A generated IValidateOptions validator conflicts with a hand-authored [OptionsValidator] class (SYSLIB1205)
ZODSGEN051 | ZodSharp.SourceGenerator | Warning | A [ZodSchema] type is declared in a Razor file, which the generator cannot see
ZODSGEN052 | ZodSharp.SourceGenerator | Warning | An attribute ErrorMessage uses a placeholder the attribute does not supply, or an unbalanced brace
