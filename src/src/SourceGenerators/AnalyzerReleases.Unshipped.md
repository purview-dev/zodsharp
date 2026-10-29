; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; Every rule is recorded in AnalyzerReleases.Shipped.md. Add a new or changed diagnostic here, and move it
; across when the next version ships.
;
; ZODSGEN002 and ZODSGEN022-ZODSGEN026 were retired during development and are never reused.

; ZODSGEN046 now recognizes the default ImmutableArray behavior option, including value-element
; collections. Its message describes option applicability; its category and severity are unchanged.
