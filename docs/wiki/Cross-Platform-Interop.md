# Cross-Platform Interop

ZodSharp ships a cross-platform fixture pipeline that proves the C# implementation agrees with TypeScript/Zod. The TypeScript side runs on [Bun](https://bun.sh); the C# side runs under the TUnit test suite.

## The schema

Both sides define the same user schema — TypeScript `UserSchema` in `src/ts/schema.ts` and the C# `CrossPlatformUserSchema` in `src/tests/SystemTextJson.UnitTests/CrossPlatformFixtures.cs` (mirrored by `NewtonsoftJson.UnitTests`):

| Field | Zod (TS) | ZodSharp (C#) |
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

If the C# output directory is empty, the vitest suite emits a note instructing you to run the C# cross-platform tests first.

## JSON Schema bridge

The same interop goal is available without fixtures via JSON Schema:

- Export: `Z.ToJsonSchema` (core package) → JSON Schema, or `z.toJSONSchema` on the TypeScript side (Zod v4+).
- Import: `Z.FromJsonSchema` (in the System.Text.Json or Newtonsoft.Json package).

See [JSON Schema Export](JsonSchema-Export.md) and [JSON Schema Import](JsonSchema-Import.md).

## Directory layout

```
src/ts/                        TypeScript (Zod) schemas + fixture generation
src/ts/fixtures/               generated JSON fixtures + manifest.json
tests/ts/                      vitest cross-platform tests
src/tests/cross-platform/      shared output directory for C#-generated JSON
```