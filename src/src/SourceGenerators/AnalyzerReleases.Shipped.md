## Release 1.0

### New Rules

| Rule ID | Category | Severity | Notes |
|---|---|---|---|
| ZODSGEN001 | ZodSharp.SourceGenerator | Error | This is effectively a fatal incident where the source generator has failed. |

## Release 2.0.0

### New Rules

| Rule ID | Category | Severity | Notes |
|---|---|---|---|
| ZODSGEN003 | ZodSharp.SourceGenerator | Error | Invalid LengthAttribute configuration
| ZODSGEN004 | ZodSharp.SourceGenerator | Error | Unsupported LengthAttribute target
| ZODSGEN005 | ZodSharp.SourceGenerator | Error | Invalid DataAnnotations error message resource configuration
| ZODSGEN006 | ZodSharp.SourceGenerator | Error | Unsupported DataAnnotations usage
| ZODSGEN007 | ZodSharp.SourceGenerator | Error | Custom validation method not found
| ZODSGEN008 | ZodSharp.SourceGenerator | Error | Invalid return type for custom validation method
| ZODSGEN009 | ZodSharp.SourceGenerator | Error | Invalid custom validation method parameter count
| ZODSGEN010 | ZodSharp.SourceGenerator | Error | Invalid custom validation model parameter
| ZODSGEN011 | ZodSharp.SourceGenerator | Error | Missing or invalid CancellationToken parameter
| ZODSGEN012 | ZodSharp.SourceGenerator | Error | Generic custom validation method unsupported
| ZODSGEN013 | ZodSharp.SourceGenerator | Error | Custom validation method must be static when defined on model type
| ZODSGEN014 | ZodSharp.SourceGenerator | Error | Inaccessible custom validation method
| ZODSGEN015 | ZodSharp.SourceGenerator | Error | Ambiguous custom validation method overloads
| ZODSGEN016 | ZodSharp.SourceGenerator | Error | Invalid custom validation method name
| ZODSGEN017 | ZodSharp.SourceGenerator | Error | Abstract custom validation method unsupported
| ZODSGEN018 | ZodSharp.SourceGenerator | Error | Partial custom validation method has not been implemented
| ZODSGEN019 | ZodSharp.SourceGenerator | Error | Invalid custom validation method parameter modifier
| ZODSGEN020 | ZodSharp.SourceGenerator | Error | CompareAttribute references an unknown property
| ZODSGEN021 | ZodSharp.SourceGenerator | Error | Add a reference to System.ComponentModel.DataAnnotations
| ZODSGEN027 | ZodSharp.SourceGenerator | Error | IValidateOptions generation requires a reference to Microsoft.Extensions.Options
| ZODSGEN028 | ZodSharp.SourceGenerator | Error | IValidateOptions generation requires a reference type
| ZODSGEN029 | ZodSharp.SourceGenerator | Error | OnZodValidate refinement hook and async custom validation methods are mutually exclusive
| ZODSGEN030 | ZodSharp.SourceGenerator | Error | Custom rule does not implement IValidationRule<T> for the property type (or an unbound generic rule cannot be closed with it)
| ZODSGEN031 | ZodSharp.SourceGenerator | Error | Unable to map an attribute value to a custom rule constructor parameter
| ZODSGEN032 | ZodSharp.SourceGenerator | Error | Unable to generate a validation attribute for a custom rule
| ZODSGEN033 | ZodSharp.SourceGenerator | Warning | Rule attribute is applied to a type that gets no generated schema
| ZODSGEN034 | ZodSharp.SourceGenerator | Error | OnZodValidate refinement hook requires the target type (and its containing types) to be declared partial
| ZODSGEN035 | ZodSharp.SourceGenerator | Error | OnZodValidate refinement hook must be declared as 'partial void OnZodValidate(RefineCtx<T> context)'
| ZODSGEN036 | ZodSharp.SourceGenerator | Error | The synchronous refinement method contract has been replaced by the OnZodValidate hook
| ZODSASP001 | ZodSharp.SourceGenerator | Warning | MessageFormat placeholder is not declared in Parameters
| ZODSASP002 | ZodSharp.SourceGenerator | Warning | Error type containing type must be partial
| ZODSASP003 | ZodSharp.SourceGenerator | Warning | ErrorType field must be static readonly
| ZODSASP100 | ZodSharp.SourceGenerator | Error | Unhandled exception in the ErrorType source generator
| ZODSASP101 | ZodSharp.SourceGenerator | Error | ErrorType Parameters could not be extracted

## Release 2.0.2

### New Rules

| Rule ID | Category | Severity | Notes |
|---|---|---|---|
| ZODSGEN037 | ZodSharp.SourceGenerator | Warning | A rule's derived validation attribute name is already declared by a hand-authored type, so the generated attribute is suppressed in favour of that declaration's own [ZodRule] mapping |
| ZODSGEN038 | ZodSharp.SourceGenerator | Warning | A hand-authored rule attribute's [ZodRule] mapping does not address every rule declared under the name the attribute derives from |
| ZODSGEN039 | ZodSharp.SourceGenerator | Warning | A rule accepts a code/origin constructor parameter without implementing IZodRule, so the value never reaches the reported error identity |
| ZODSGEN040 | ZodSharp.SourceGenerator | Warning | An attribute argument is not consumed by the resolved rule: no matching constructor parameter and not part of the error identity |
| ZODSGEN041 | ZodSharp.SourceGenerator | Info | A typed union whose option types are all reference types can use the native C# 15 union returned by Z.NativeUnion on net11+ (allocation-free, exhaustive pattern matching) |
| ZODSGEN042 | ZodSharp.SourceGenerator | Warning | A validation rule does not expose its error identity as public const ErrorCode/MessageFormat constants, so tests cannot assert against the rule without duplicating literals |
| ZODSGEN043 | ZodSharp.SourceGenerator | Info | A built-in rule marked [ZodRule] does not generate a validation attribute because a constructor parameter cannot be represented as an attribute property (a derived name that collides with System.ComponentModel.DataAnnotations is emitted under a "Zod" suffix instead) |
| ZODSGEN044 | ZodSharp.SourceGenerator | Error | A [ZodSchema] type that uses an automatic [Scalar] form ([Scalar<TValue>] or [Scalar(typeof(TValue))]) cannot produce a usable schema, because the generated Validate method is suppressed or the underlying type cannot be represented |

## Release 2.1.0

### New Rules

| Rule ID | Category | Severity | Notes |
|---|---|---|---|
| ZODSGEN045 | ZodSharp.SourceGenerator | Warning | A type that exports JSON Schema references a nested [ZodSchema] type whose export is disabled ([ZodSchema(GenerateJsonSchema = false)] or the assembly-level ZodSharpGenerateJsonSchemas=false switch), so the parent's exported schema falls back to an empty schema for that property |

## Release 2.2.0

### New Rules

| Rule ID | Category | Severity | Notes |
|---|---|---|---|
| ZODSGEN046 | ZodSharp.SourceGenerator | Warning | A [CollectionBehavior] attribute is applied to a property that is not a collection of nullable elements, so the setting can never take effect |

## Release 3.0.0

### New Rules

| Rule ID | Category | Severity | Notes |
|---|---|---|---|
| ZODSGEN047 | ZodSharp.SourceGenerator | Info | [ValidatableType] (Microsoft.Extensions.Validation) is redundant on a [ZodSchema] type |
| ZODSGEN048 | ZodSharp.SourceGenerator | Warning | A type has asynchronous-only rules (AsyncValidationAttribute, IAsyncValidatableObject) but a synchronous Validate is generated |
| ZODSGEN049 | ZodSharp.SourceGenerator | Warning | A member typed as a closed class has a derived type without [ZodSchema] |
| ZODSGEN050 | ZodSharp.SourceGenerator | Error | A generated IValidateOptions validator conflicts with a hand-authored [OptionsValidator] class (SYSLIB1205) |
| ZODSGEN051 | ZodSharp.SourceGenerator | Warning | A [ZodSchema] type is declared in a Razor file, which the generator cannot see |
| ZODSGEN052 | ZodSharp.SourceGenerator | Warning | An attribute ErrorMessage uses a placeholder the attribute does not supply, or an unbalanced brace |
| ZODSGEN053 | ZodSharp.SourceGenerator | Warning | [MinLength]/[MaxLength]/[Length] on a collection member in a trimmed or AOT project; use [MinCount]/[MaxCount]/[CountRange] |
| ZODSGEN054 | ZodSharp.SourceGenerator | Error | A three-parameter (service-aware) custom validation method's second parameter is not IServiceProvider |
