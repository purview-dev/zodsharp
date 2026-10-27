// End-to-end smoke test of the Blazor sample (src/src/Examples.Blazor) in a real browser: one form per render mode,
// against the published app, so the WebAssembly form runs the trimmed client.
//
//   node tests/e2e/blazor-sample.e2e.mjs http://127.0.0.1:5090
//
// Needs playwright-core (not a repository dependency: the nightly job installs it with --no-save) and a Chromium
// (`npx playwright-core install chromium`), or set E2E_BROWSER_CHANNEL=msedge / chrome to use an installed browser.
// Exits non-zero on the first failed check.
import { chromium } from "playwright-core";

const base = (process.argv[2] ?? "http://127.0.0.1:5090").replace(/\/$/, "");
const timeout = 30_000;

const browser = await chromium.launch({ channel: process.env.E2E_BROWSER_CHANNEL || undefined, headless: true });
const page = await browser.newPage();
const browserErrors = [];
page.on("pageerror", (error) => browserErrors.push(error.message));
page.on("console", (message) => {
  if (message.type() === "error" && !message.text().startsWith("Failed to load resource")) {
    browserErrors.push(message.text());
  }
});

let failures = 0;

async function check(name, body) {
  try {
    await body();
    console.log(`ok - ${name}`);
  } catch (error) {
    failures++;
    console.log(`not ok - ${name}\n  ${String(error?.message ?? error).split("\n").join("\n  ")}`);
  }
}

/** Waits until fn() returns a truthy value, failing with the last value seen. */
async function eventually(description, fn) {
  const deadline = Date.now() + timeout;
  let last;
  while (Date.now() < deadline) {
    last = await fn();
    if (last) return last;
    await page.waitForTimeout(100);
  }

  throw new Error(`Timed out waiting for ${description}; last value: ${JSON.stringify(last)}`);
}

/** The codes and messages rendered next to the input with the given id (ZodValidationMessage markup). */
async function messagesFor(id) {
  return page.$$eval(
    `#${id} ~ .validation-message`,
    (elements) => elements.map((e) => ({ code: e.getAttribute("data-code"), text: e.textContent ?? "" })),
  );
}

try {
  await check("interactive WebAssembly: debounced validation, codes, async pending state", async () => {
    await page.goto(`${base}/register`);
    await page.waitForSelector("#name", { timeout });

    await page.fill("#name", "A");
    await eventually("the invalid_length code on Name", async () =>
      (await messagesFor("name")).some((m) => m.code === "invalid_length"),
    );

    await page.fill("#email", "taken@example.com");
    // The uniqueness check is a service-aware async rule that asks the server; its pending state is visible.
    await eventually("the pending indicator", async () => (await page.locator("#email-pending").count()) === 1);
    await eventually("the email_taken code on Email", async () =>
      (await messagesFor("email")).some((m) => m.code === "email_taken"),
    );
    await eventually("the pending indicator to clear", async () => (await page.locator("#email-pending").count()) === 0);
  });

  await check("interactive WebAssembly: server ProblemDetails mapped onto the fields", async () => {
    await page.fill("#name", "");
    await page.fill("#email", "not-an-email");
    await page.fill("#age", "5");
    await page.click("#post-unvalidated");
    await page.waitForSelector("#status", { timeout });

    await eventually("the server's invalid_range code on Age", async () =>
      (await messagesFor("age")).some((m) => m.code === "invalid_range"),
    );
    await eventually("the server's invalid_string code on Email", async () =>
      (await messagesFor("email")).some((m) => m.code === "invalid_string"),
    );
  });

  await check("interactive Server: nested, collection and field-level messages", async () => {
    await page.goto(`${base}/orders`);
    // Wait for the circuit: the add-line button only works once the page is interactive.
    await eventually("the interactive circuit", async () => {
      await page.click("text=Add a line");
      return (await page.locator("#sku-1").count()) === 1;
    });

    // InputText binds onchange, so each value is committed with a blur.
    await page.fill("#shipping-city", "Leeds");
    await page.press("#shipping-city", "Tab");
    await page.fill("#shipping-city", "");
    await page.press("#shipping-city", "Tab");
    await eventually("the City message next to the nested input", async () =>
      (await page.$$eval("#shipping-city + .validation-message", (e) => e.length)) === 1,
    );
    await eventually("no message on an untouched field yet", async () =>
      (await page.$$eval("#shipping-street + .validation-message", (e) => e.length)) === 0,
    );

    await page.fill("#quantity-1", "500");
    await page.press("#quantity-1", "Tab");
    await eventually("the Quantity message on the second line", async () =>
      (await page.$$eval("#quantity-1 + .validation-message", (e) => e.length)) === 1,
    );
  });

  await check("static SSR: ZodSharp client rules block the submit in the browser", async () => {
    await page.goto(`${base}/returns`);
    await page.fill("#reference", "BAD-1");
    await page.fill("#order-id", "not-a-uuid");
    await page.click("button[type=submit]");

    await eventually("the zod-startswith message", async () =>
      (await page.textContent("[data-valmsg-for='Model.Reference']"))?.includes("RMA-"),
    );
    await eventually("a message on the UUID field", async () =>
      ((await page.textContent("[data-valmsg-for='Model.OrderId']")) ?? "").trim().length > 0,
    );
    if ((await page.locator("#submitted").count()) !== 0) throw new Error("The invalid form was submitted.");
  });

  await check("French: the WebAssembly client loads the satellite resources", async () => {
    await page.goto(`${base}/culture/set?culture=fr&redirectUri=%2Fregister`);
    await page.waitForSelector("#email", { timeout });
    await page.fill("#email", "taken@example.com");
    await eventually("the French email_taken message", async () =>
      (await messagesFor("email")).some((m) => m.text.includes("déjà enregistrée")),
    );
  });

  await check("no unhandled browser errors", async () => {
    if (browserErrors.length > 0) throw new Error(browserErrors.join("\n"));
  });
} finally {
  await browser.close();
}

if (failures > 0) {
  console.log(`${failures} check(s) failed`);
  process.exit(1);
}
