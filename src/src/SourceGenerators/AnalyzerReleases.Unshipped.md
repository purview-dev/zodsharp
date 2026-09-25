### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
ZODSGEN020 | ZodSharp.SourceGenerator | Error | CompareAttribute references an unknown property
ZODSGEN021 | ZodSharp.SourceGenerator | Error | Add a reference to System.ComponentModel.DataAnnotations
ZODSGEN022 | ZodSharp.SourceGenerator | Error | Synchronous refinement method must return IEnumerable<ValidationError>
ZODSGEN023 | ZodSharp.SourceGenerator | Error | Synchronous refinement method must not have more than one parameter
ZODSGEN024 | ZodSharp.SourceGenerator | Error | Synchronous refinement method must be an instance method
ZODSGEN025 | ZodSharp.SourceGenerator | Error | Synchronous refinement method must be public or internal
ZODSGEN026 | ZodSharp.SourceGenerator | Error | Synchronous refinement method context parameter must be RefineCtx<T>
ZODSGEN027 | ZodSharp.SourceGenerator | Error | IValidateOptions generation requires a reference to Microsoft.Extensions.Options
ZODSGEN028 | ZodSharp.SourceGenerator | Error | IValidateOptions generation requires a reference type
ZODSGEN029 | ZodSharp.SourceGenerator | Error | Synchronous refinement and async custom validation methods are mutually exclusive
ZODSGEN030 | ZodSharp.SourceGenerator | Error | Custom rule does not implement IValidationRule<T> for the property type (or an unbound generic rule cannot be closed with it)
ZODSGEN031 | ZodSharp.SourceGenerator | Error | Unable to map an attribute value to a custom rule constructor parameter
ZODSGEN032 | ZodSharp.SourceGenerator | Error | Unable to generate a validation attribute for a custom rule
ZODSGEN033 | ZodSharp.SourceGenerator | Warning | Rule attribute is applied to a type that gets no generated schema
ZODSASP001 | ZodSharp.SourceGenerator | Warning | MessageFormat placeholder is not declared in Parameters
ZODSASP002 | ZodSharp.SourceGenerator | Warning | Error type containing type must be partial
ZODSASP003 | ZodSharp.SourceGenerator | Warning | ErrorType field must be static readonly
ZODSASP100 | ZodSharp.SourceGenerator | Error | Unhandled exception in the ErrorType source generator
ZODSASP101 | ZodSharp.SourceGenerator | Error | ErrorType Parameters could not be extracted
