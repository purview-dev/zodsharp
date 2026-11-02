namespace ZodSharp.Extensions.Validation;

/// <summary>
/// Registered by <c>AddZodSharpValidation()</c> (and its <c>validateParameters: true</c> overload) to record that the
/// application asked for parameter validation, so the resolver claims parameters even when <c>AddZodSharpBlazor()</c>,
/// which does not, registered the bridge too.
/// </summary>
sealed class ZodParameterValidationRequest;
