namespace ZodSharp.Examples.MinimalApi;

/// <summary>
/// The marker type for the <c>ValidationMessages</c> resources (<c>ValidationMessages.resx</c> and
/// <c>ValidationMessages.fr.resx</c>). Keys are tried in the order <c>StringLocalizerZodMessageFormatter</c>
/// documents; this sample keys by error code, so one entry localizes a code wherever it is reported.
/// </summary>
sealed class ValidationMessages;
