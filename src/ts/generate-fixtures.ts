/**
 * Generates JSON fixture files from the TS/Zod side.
 * These files are consumed by the C# cross-platform tests.
 *
 * Run: bun run src/ts/generate-fixtures.ts
 */
import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { UserSchema, fixtures } from "./schema";

const outDir = resolve("src", "ts", "fixtures");

for (const [key, value] of Object.entries(fixtures)) {
  const json = JSON.stringify(value, null, 2);
  const filePath = resolve(outDir, `${key}.json`);
  mkdirSync(dirname(filePath), { recursive: true });
  writeFileSync(filePath, json, "utf-8");
  console.log(`Wrote ${filePath}`);
}

// Also write a manifest of which fixtures should be valid/invalid per Zod
const manifest = Object.fromEntries(
  Object.entries(fixtures).map(([key, value]) => {
    const result = UserSchema.safeParse(value);
    return [key, { valid: result.success }];
  }),
);
const manifestPath = resolve(outDir, "manifest.json");
writeFileSync(manifestPath, JSON.stringify(manifest, null, 2), "utf-8");
console.log(`Wrote ${manifestPath}`);
