# Cross-Platform Interop

Purview.ZodSharp ships a cross-platform fixture pipeline that proves the C# implementation agrees with TypeScript/Zod. The TypeScript side runs on [Bun](https://bun.sh); the C# side runs under the TUnit test suite.

## The schema

Both sides define the same user schema — TypeScript `UserSchema` in `src/ts/schema.ts` and the C# `CrossPlatformUserSchema` in `src/tests/SystemTextJson.UnitTests/CrossPlatformFixtures.cs` (mirrored by `NewtonsoftJson.UnitTests`):

| Field | Zod (TS) | Purview.ZodSharp (C#) |
|---|---|---|
| `name` | `z.string().min(1)` | min-length 1 |
| `age` | `z.number().int().min(0).max(120)` | `0..120` integer |
| `email` | `z.string().email().optional()` | optional email |
| `tags` | `z.array(z.string()).default([])` | string array, default `[]` |

## Fixture generation

```bash
bun run generate-fixtures   # runs src/ts/generate-fixtures.ts
```

For each of the eight fixture cases (three valid, five invalid) the script:

1. Writes a JSON file to `src/ts/fixtures/{key}.json`.
2. Runs `UserSchema.safeParse(value)` and records the outcome in `src/ts/fixtures/manifest.json` — the authoritative `{ "valid": true|false }` result for every fixture.

## The validation loop

1. **`bun run generate-fixtures`** writes `src/ts/fixtures/*.json` and `manifest.json` (Zod is the authority on validity).
2. **`just test`** (C# TUnit) reads the fixtures and manifest, asserts outcomes match, and writes validated C# output to `src/tests/cross-platform/output/{systemtext,newtonsoft}-valid.json`.
3. **`bun run test`** (vitest) re-checks fixture validity under Zod and parses the C# output JSON — closing the TypeScript ↔ C# loop.

**C# cross-platform tests** (`SystemTextCrossPlatformTests`, `NewtonsoftCrossPlatformTests`) load every fixture and assert the C# outcome matches `manifest.json`, deserialize-and-validate valid fixtures, round-trip TS fixture → C# → JSON → C#, and serialize a validated `CrossPlatformUser` into `src/tests/cross-platform/output/{systemtext,newtonsoft}-valid.json`.

**Vitest cross-platform tests** (`tests/ts/cross-platform.test.ts`) re-check fixture validity under Zod, verify canonical serialization, and parse every JSON file in `cross-platform/output` with Zod to prove C# output is acceptable to TypeScript/Zod.

If the C# output directory is empty, the vitest suite fails rather than passing vacuously: run the C# cross-platform tests first. `just test` does both, because `Repository.UnitTests` drives every vitest suite from the .NET test run.

## Issue shape

Validity is not the only thing that lines up. Every built-in ZodSharp error reports the Zod v4 issue code as its
`Category` (`invalid_type`, `too_small`, `too_big`, `invalid_format`, `invalid_union`, `unrecognized_keys`, …)
while `Code` keeps ZodSharp's more specific identity, and `FullPath.ToZodPath()` returns the path in Zod's issue
shape (strings for members and keys, integers for indexes). Messages use Zod's type names (`"Expected number,
received string"`), never CLR names. A client can therefore handle ZodSharp and Zod issues with the same code;
see [Core Concepts](Core-Concepts.md#error-fields).

| Zod issue | ZodSharp error |
|---|---|
| `code` | `Category` |
| `path` (`["items", 0, "name"]`) | `FullPath.ToZodPath()` |
| `origin`, `minimum`, `maximum`, `inclusive` | `Origin`, `Lower`/`Upper` (`ErrorBound.Value`, `ErrorBound.Inclusive`) |
| `expected`, `keys`, `format`, `divisor` | `Parameters` (`ZodIssueParameters.Expected`, `Keys`, `Format`, `Divisor`) |
| `errors` of `invalid_union` | `error.GetUnionErrors()` |

## JSON Schema bridge

The same interop goal is available without fixtures via JSON Schema:

- Export: `Z.ToJsonSchema` (core package) → JSON Schema, or `z.toJSONSchema` on the TypeScript side (Zod v4+).
- Import: `Z.FromJsonSchema` (in the System.Text.Json or Newtonsoft.Json package — namespace `ZodSharp.JsonSchema.SystemTextJson` / `ZodSharp.JsonSchema.NewtonsoftJson`).

A generated collection maps to Zod's array element type: `T[]` / `List<T>` ↔ `z.array(T)`, and a nullable element (`T?[]`, `List<T?>`, or a property that allows nulls via `[CollectionBehavior(AllowNulls = true)]`) ↔ `z.array(T.nullable())`. A collection whose elements may be null exports `items.anyOf: [ …, { "type": "null" } ]`, and both `z.fromJSONSchema` and `Z.FromJsonSchema` read that shape back. Null elements are rejected by default, so a generated `T[]` matches `z.array(T)` exactly — see [Collection null items](Source-Generator.md#collection-null-items).

A dictionary property (`IDictionary<TKey, TValue>` / `IReadOnlyDictionary<TKey, TValue>`) is a map:
`Dictionary<string, V>` ↔ `z.record(z.string(), V)`. The generated schema exports
`{ "type": "object", "additionalProperties": … }` (with `minProperties`/`maxProperties` for size constraints),
and its failures use the same key-prefixed paths as `z.record`. Both `z.fromJSONSchema` and
`Z.FromJsonSchema` read that shape back as a record — see
[JSON Schema Export](JsonSchema-Export.md#dictionaries).

The temporal parity suite (`tests/ts/temporal-parity.test.ts`) proves the same loop for `[NonSentinel]` on
`DateOnly`, `TimeOnly`, `DateTime`, and `DateTimeOffset` under the default preserve-offset contract, including
the offset-equivalent bounds. The C# producer
(`JsonSchema_GivenGeneratedTemporalSchemas_WritesZodParityArtifacts`) writes to
`src/tests/cross-platform/temporal-parity/`; the UTC contract has its own generator tests.

See [JSON Schema Export](JsonSchema-Export.md) and [JSON Schema Import](JsonSchema-Import.md).

## Directory layout

```
src/ts/                        TypeScript (Zod) schemas + fixture generation
src/ts/fixtures/               generated JSON fixtures + manifest.json
tests/ts/                      vitest cross-platform tests
src/tests/cross-platform/      shared output directory for C#-generated JSON
```