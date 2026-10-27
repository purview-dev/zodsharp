// Blazor JavaScript initializer for Purview.ZodSharp.Blazor: Blazor loads it automatically and calls
// afterWebStarted once Blazor.formValidation exists, so the ZodSharp client validators are registered with no
// <script> tag in the app.
import "./zodsharp-validation.js";

export function afterWebStarted(blazor) {
  globalThis.ZodSharpValidation?.register(blazor);
}

export function afterStarted(blazor) {
  globalThis.ZodSharpValidation?.register(blazor);
}
