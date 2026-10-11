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
ZODSGEN045 | ZodSharp.SourceGenerator | Warning | A type that exports JSON Schema references a nested [ZodSchema] type whose export is disabled ([ZodSchema(GenerateJsonSchema = false)] or the assembly-level ZodSharpGenerateJsonSchemas=false switch), so the parent's exported schema falls back to an empty schema for that property
