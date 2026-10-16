/**
 * Guid/UUID cross-platform parity: C# (Purview.ZodSharp) -> Zod.
 *
 * The C# side (SourceGenerators.UnitTests) exports each generated schema with `BuildJsonSchema()` and writes
 * a shared payload set with the outcome of the generated C# validator. This suite imports each exported
 * schema with Zod's built-in `z.fromJSONSchema()` and checks it agrees with C# for the same payloads — no
 * hand-written TypeScript equivalent schemas.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync } from "node:fs";
import { resolve } from "node:path";
import { z } from "zod";

const parityDir = resolve("src", "tests", "cross-platform", "guid-parity");

interface ParityCase {
  name: string;
  value: string;
  valid: boolean;
}

interface ParityArtifact {
  name: string;
  schema: unknown;
  cases: ParityCase[];
}

let artifacts: ParityArtifact[] = [];
try {
  artifacts = readdirSync(parityDir)
    .filter((file) => file.endsWith(".json"))
    .map((file) => JSON.parse(readFileSync(resolve(parityDir, file), "utf-8")) as ParityArtifact);
} catch {
  // The directory does not exist until the C# tests run. Handled below.
}

describe("Guid/UUID export matches C# validation under z.fromJSONSchema", () => {
  if (artifacts.length === 0) {
    // Do NOT pass silently: an empty directory means this suite verified nothing.
    it("has C# parity artifacts to validate", () => {
      expect.fail(
        `No parity artifacts found in ${parityDir}. ` +
          "Run the C# tests first (`just test`) so they write their output, then re-run `bun run test`.",
      );
    });
  }

  for (const artifact of artifacts) {
    const zodSchema = z.fromJSONSchema(artifact.schema as never);

    for (const testCase of artifact.cases) {
      it(`${artifact.name} / ${testCase.name} — matches C#`, () => {
        const result = zodSchema.safeParse(JSON.parse(testCase.value));
        expect(result.success, `${artifact.name}/${testCase.name} (${testCase.value})`).toBe(testCase.valid);
      });
    }
  }
});
