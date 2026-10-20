/**
 * Temporal cross-platform parity: C# (Purview.ZodSharp) -> Zod.
 *
 * The C# producer compiles a `[ZodSchema]` model with the default PreserveOffset contract and writes each
 * exported schema with the outcome of the C# validator pipeline. This suite imports the schema with Zod's
 * `z.fromJSONSchema()` and checks that both sides agree, including the offset-equivalent bounds
 * (`0001-01-01T05:30:00+05:30` and `9999-12-31T18:29:59.9999999-05:30`) and the local `TimeOnly` wire form.
 */
import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync } from "node:fs";
import { resolve } from "node:path";
import { z } from "zod";

const parityDir = resolve("src", "tests", "cross-platform", "temporal-parity");

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

describe("Temporal export matches C# validation under z.fromJSONSchema", () => {
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
    // The UTC contract is exact, so the schema must not carry the incomplete-rule marker.
    it(`${artifact.name} has no x-zodsharp-unsupported marker`, () => {
      expect(JSON.stringify(artifact.schema)).not.toContain("x-zodsharp-unsupported");
    });

    const zodSchema = z.fromJSONSchema(artifact.schema as never);

    for (const testCase of artifact.cases) {
      it(`${artifact.name} / ${testCase.name} — matches C#`, () => {
        const result = zodSchema.safeParse(JSON.parse(testCase.value));
        expect(result.success, `${artifact.name}/${testCase.name} (${testCase.value})`).toBe(
          testCase.valid,
        );
      });
    }
  }
});
