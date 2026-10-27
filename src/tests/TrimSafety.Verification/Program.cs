// The trim and Native AOT verification harness. Each check exercises one part of the trim-safe surface and records a
// failure instead of throwing, so one run reports every mismatch; any failure makes the process exit non-zero.
Harness harness = new();

harness.Run("runtime schemas", CoreChecks.RuntimeSchemas);
harness.Run("generated schema", GeneratedChecks.GeneratedSchema);
harness.Run("discriminated unions over CLR types", CoreChecks.PocoDiscriminatedUnion);
harness.Run("member validation and resolution", GeneratedChecks.MemberValidation);
harness.Run("collection-count attributes", GeneratedChecks.CollectionCounts);
harness.Run("generated registration and assembly metadata", CoreChecks.Registration);
harness.Run("assembly scanning through AddZodSharp", IntegrationChecks.AssemblyScanning);
harness.Run("System.Text.Json with a source-generated context", IntegrationChecks.SystemTextJson);
harness.Run("JSON Schema export and import", IntegrationChecks.JsonSchemaRoundTrip);
await harness.RunAsync("Microsoft.Extensions.Validation bridge", IntegrationChecks.ValidationBridge);
await harness.RunAsync("ZodProblemDetailsWriter without a host", IntegrationChecks.ProblemDetails);
harness.Run("options validation with ValidateWithZod", IntegrationChecks.OptionsValidation);

return harness.Complete();
