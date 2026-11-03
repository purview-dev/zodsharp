<!-- Page order for the documentation site. purview.dev reads these links in document order to order the published
     sidebar, so every page appears here exactly once, grouped by section and in reading order. Paths are relative to
     this file at the wiki root, which is also what the sync slugifies: `schemas/Custom-Rules.md` is published at
     /docs/zodsharp/schemas/custom-rules/. Keep the labels in step with the matching `## ` headings in Home.md;
     DocumentationWikiTests enforces both rules. -->

- [Home](Home.md)
- **Get started**
  - [Getting Started](Getting-Started.md)
  - [Core Concepts](concepts/Core-Concepts.md)
  - [Fluent Schema API](Fluent-Schema-API.md)
- **Validation reference**
  - [String Validation](schemas/String-Validation.md)
  - [Number Validation](schemas/Number-Validation.md)
  - [Object Validation](schemas/Object-Validation.md)
  - [Arrays and Other Schemas](schemas/Arrays-and-Other-Schemas.md)
  - [Unions and Discriminated Unions](schemas/Unions-and-Discriminated-Unions.md)
  - [Composition and Transforms](concepts/Composition-and-Transforms.md)
  - [Validation Rules Reference](schemas/Validation-Rules-Reference.md)
  - [Custom Rules](schemas/Custom-Rules.md)
  - [Value Objects Integration](schemas/Value-Objects-Integration.md)
  - [Compiled Validators and Caching](schemas/Compiled-Validators-and-Caching.md)
- **Source generator**
  - [Source Generator](generator/Source-Generator.md)
  - [Source Generator DataAnnotations](generator/Source-Generator-DataAnnotations.md)
  - [Source Generator Diagnostics](generator/Source-Generator-Diagnostics.md)
- **JSON and serialization**
  - [JSON Schema Export](serialization/JsonSchema-Export.md)
  - [JSON Schema Import](serialization/JsonSchema-Import.md)
  - [System.Text.Json Integration](serialization/SystemTextJson-Integration.md)
  - [Newtonsoft.Json Integration](serialization/NewtonsoftJson-Integration.md)
- **ASP.NET Core, Blazor and .NET 11**
  - [ASP.NET Core Integration](AspNetCore-Integration.md)
  - [Microsoft.Extensions.Validation](hosting/Microsoft-Extensions-Validation.md)
  - [OpenAPI Integration](hosting/OpenAPI-Integration.md)
  - [Blazor Integration](hosting/Blazor-Integration.md)
  - [HttpClient Integration](hosting/Http-Integration.md)
  - [Dependency Injection](hosting/Dependency-Injection.md)
  - [Localization](hosting/Localization.md)
- **Guarantees, performance and deployment**
  - [Guarantees and Limitations](quality/Guarantees-and-Limitations.md)
  - [Performance](quality/Performance.md)
  - [Trimming and Native AOT](quality/Trimming-and-AOT.md)
  - [Cross-Platform Interop](quality/Cross-Platform-Interop.md)
- **Contributing and releases**
  - [Contributing](development/Contributing.md)
  - [Release Flow](development/Release-Flow.md)
