/**
 * Client-validator parity: C# (Purview.ZodSharp rules) -> zodsharp-validation.js.
 *
 * The C# producer (ZodSharp.Blazor.Ssr/ClientRuleParityFixtureTests) writes, for each ZodSharp rule, the client rule
 * the server emits into a static-SSR form's `data-rules` payload and the C# rule's verdict on a set of inputs. This
 * suite runs the browser validators from src/src/Blazor/wwwroot/zodsharp-validation.js against the same inputs and
 * fails when JavaScript and C# disagree.
 */
import { describe, it, expect, beforeAll } from "vitest";
import { readFileSync, readdirSync } from "node:fs";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";

const fixtureDir = resolve("src", "tests", "cross-platform", "client-rules");
const scriptPath = resolve("src", "src", "Blazor", "wwwroot", "zodsharp-validation.js");

interface FixtureRule {
  name: string;
  message: string;
  params: Record<string, string>;
}

interface FixtureCase {
  value: string;
  valid: boolean;
}

interface Fixture {
  name: string;
  rules: FixtureRule[];
  cases: FixtureCase[];
}

type ValidationResult = { success: boolean; message?: string };
type Validator = (ctx: { value: string; element?: unknown; params: Record<string, string> }) => ValidationResult;

interface ZodSharpValidation {
  validators: Record<string, Validator>;
  register: (blazor?: unknown) => boolean;
}

let fixtures: Fixture[] = [];
try {
  fixtures = readdirSync(fixtureDir)
    .filter((file) => file.endsWith(".json"))
    .map((file) => JSON.parse(readFileSync(resolve(fixtureDir, file), "utf-8")) as Fixture);
} catch {
  // The directory does not exist until the C# tests run. Handled below.
}

let api: ZodSharpValidation;

beforeAll(async () => {
  await import(pathToFileURL(scriptPath).href);
  api = (globalThis as unknown as { ZodSharpValidation: ZodSharpValidation }).ZodSharpValidation;
});

function run(rule: FixtureRule, value: string): ValidationResult {
  const validator = api.validators[rule.name];
  expect(validator, `no JavaScript validator named '${rule.name}'`).toBeTypeOf("function");
  return validator({ value, params: rule.params });
}

describe("zodsharp-validation.js matches the C# rules", () => {
  if (fixtures.length === 0) {
    // Do NOT pass silently: an empty directory means this suite verified nothing.
    it("has C# client-rule fixtures to validate", () => {
      expect.fail(
        `No client-rule fixtures found in ${fixtureDir}. ` +
          "Run the C# tests first (`just test`) so they write their output, then re-run `bun run test`.",
      );
    });
  }

  for (const fixture of fixtures) {
    for (const testCase of fixture.cases) {
      it(`${fixture.name} / ${JSON.stringify(testCase.value)} — ${testCase.valid ? "valid" : "invalid"} like C#`, () => {
        const results = fixture.rules.map((rule) => run(rule, testCase.value));
        expect(
          results.every((r) => r.success),
          `${fixture.name} (${JSON.stringify(testCase.value)}) ${JSON.stringify(fixture.rules)}`,
        ).toBe(testCase.valid);
      });
    }

    for (const rule of fixture.rules.filter((r) => r.params.template)) {
      const failing = fixture.cases.find((c) => !c.valid);
      if (!failing) continue;
      it(`${fixture.name} — substitutes the value into the message`, () => {
        const result = run(rule, failing.value);
        expect(result.message).toBe(rule.params.template.split("{value}").join(failing.value));
      });
    }
  }

  it("lets an empty value pass every validator, like the built-in validators", () => {
    for (const [name, validator] of Object.entries(api.validators)) {
      expect(validator({ value: "", params: { value: "1", divisor: "2", kind: "number" } }).success, name).toBe(true);
    }
  });

  it("registers every validator with Blazor.formValidation", () => {
    const registered: string[] = [];
    const blazor = { formValidation: { addValidator: (name: string) => registered.push(name) } };
    expect(api.register(blazor)).toBe(true);
    expect(registered.sort()).toEqual(Object.keys(api.validators).sort());
  });
});
