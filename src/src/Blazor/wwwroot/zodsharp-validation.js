/*
 * Purview.ZodSharp.Blazor client-side validators for statically rendered (static SSR) Blazor forms.
 *
 * Each validator mirrors the semantics of the ZodSharp rule it is named after, and is registered with
 * Blazor.formValidation.addValidator(name, ctx => ({ success, message })). The rules come from the
 * `data-rules` payload that Purview.ZodSharp.AspNetCore's AddZodSharpClientValidation() renders.
 *
 * Like the built-in validators, an empty value passes: emptiness is the `required` rule's concern.
 * Client validation only improves the user experience; the server always validates the posted form.
 *
 * Loaded automatically by Purview.ZodSharp.Blazor.lib.module.js (a Blazor JavaScript initializer). It can also
 * be loaded with a plain <script> tag; it registers as soon as Blazor.formValidation exists.
 */
(function (global) {
  "use strict";

  const VALUE_PLACEHOLDER = "{value}";

  const ok = { success: true };

  function fail(ctx) {
    const template = ctx && ctx.params ? ctx.params.template : undefined;
    return template
      ? { success: false, message: template.split(VALUE_PLACEHOLDER).join(String(ctx.value)) }
      : { success: false };
  }

  function result(ctx, valid) {
    return valid ? ok : fail(ctx);
  }

  function isEmpty(value) {
    return value === undefined || value === null || value === "";
  }

  // ---- strings -------------------------------------------------------------------------------------------

  function compareCase(params) {
    return params && params.comparison === "ignorecase";
  }

  function startsWith(ctx) {
    const { value, params } = ctx;
    if (isEmpty(value)) return ok;
    const prefix = params.prefix ?? "";
    return compareCase(params)
      ? result(ctx, value.toUpperCase().startsWith(prefix.toUpperCase()))
      : result(ctx, value.startsWith(prefix));
  }

  function endsWith(ctx) {
    const { value, params } = ctx;
    if (isEmpty(value)) return ok;
    const suffix = params.suffix ?? "";
    return compareCase(params)
      ? result(ctx, value.toUpperCase().endsWith(suffix.toUpperCase()))
      : result(ctx, value.endsWith(suffix));
  }

  function includes(ctx) {
    const { value, params } = ctx;
    if (isEmpty(value)) return ok;
    return result(ctx, value.includes(params.includes ?? ""));
  }

  function regex(ctx) {
    const { value, params } = ctx;
    if (isEmpty(value) || !params.pattern) return ok;
    let expression;
    try {
      expression = new RegExp(params.pattern);
    } catch {
      // A .NET pattern JavaScript cannot compile is enforced on the server only.
      return ok;
    }
    // RegexRule uses Regex.IsMatch: a match anywhere in the value, not a full match.
    return result(ctx, expression.test(value));
  }

  // ---- identifiers ---------------------------------------------------------------------------------------

  const HEX = /^[0-9a-fA-F]$/;
  const NIL_UUID = "00000000-0000-0000-0000-000000000000";
  const MAX_UUID = "ffffffff-ffff-ffff-ffff-ffffffffffff";

  function isUuid(value, version) {
    if (value.trim().length === 0 || value.length !== 36) return false;
    if (value[8] !== "-" || value[13] !== "-" || value[18] !== "-" || value[23] !== "-") return false;
    for (let i = 0; i < 36; i++) {
      if (i === 8 || i === 13 || i === 18 || i === 23) continue;
      if (!HEX.test(value[i])) return false;
    }
    const variantOk = "89abAB".includes(value[19]);
    if (version) return value[14] === String(version) && variantOk;
    // nil and max are allowed regardless of version/variant (Zod parity). Ordinal, so lower case only.
    if (value === NIL_UUID || value === MAX_UUID) return true;
    return value[14] >= "1" && value[14] <= "8" && variantOk;
  }

  function uuid(ctx) {
    const { value, params } = ctx;
    if (isEmpty(value)) return ok;
    const version = params.version ? parseInt(params.version, 10) : 0;
    return result(ctx, isUuid(value, version));
  }

  function cuid2(ctx) {
    const { value } = ctx;
    if (isEmpty(value)) return ok;
    return result(ctx, /^[a-z0-9]+$/.test(value));
  }

  function ulid(ctx) {
    const { value } = ctx;
    if (isEmpty(value)) return ok;
    // First character 0-7 (48-bit timestamp); Crockford base32 excludes I, L, O and U, in either case.
    return result(ctx, /^[0-7][0-9A-HJKMNP-TV-Za-hjkmnp-tv-z]{25}$/.test(value));
  }

  // ---- IP addresses (mirrors System.Net.IPAddress.TryParse) ----------------------------------------------

  // IPv4 in the forms IPAddress.TryParse accepts: one to four dot-separated parts, each decimal, octal (leading 0)
  // or hexadecimal (0x); the last part fills the remaining bytes ("127.1" is 127.0.0.1).
  function parseIPv4(text) {
    if (text.length === 0) return null;
    const parts = text.split(".");
    if (parts.length > 4) return null;
    const values = [];
    for (const part of parts) {
      if (part.length === 0) return null;
      let digits = part;
      let radix = 10;
      if (/^0[xX]/.test(part)) {
        radix = 16;
        digits = part.slice(2);
        if (digits.length === 0) {
          values.push(0);
          continue;
        }
        if (!/^[0-9a-fA-F]+$/.test(digits)) return null;
      } else if (part.length > 1 && part[0] === "0") {
        radix = 8;
        digits = part.slice(1);
        if (!/^[0-7]+$/.test(digits)) return null;
      } else if (!/^[0-9]+$/.test(digits)) {
        return null;
      }
      const number = parseInt(digits, radix);
      if (!Number.isFinite(number) || number > 0xffffffff) return null;
      values.push(number);
    }
    const last = values[values.length - 1];
    const maxLast = [0xffffffff, 0xffffff, 0xffff, 0xff][values.length - 1];
    if (last > maxLast) return null;
    for (let i = 0; i < values.length - 1; i++) {
      if (values[i] > 0xff) return null;
    }
    return values;
  }

  function isIPv6(text) {
    let address = text;
    // Bracketed form, optionally with a port: [::1] or [::1]:443.
    if (address.startsWith("[")) {
      const close = address.indexOf("]");
      if (close < 0) return false;
      const rest = address.slice(close + 1);
      if (rest.length > 0 && !/^:[0-9]{1,5}$/.test(rest)) return false;
      if (rest.length > 0 && parseInt(rest.slice(1), 10) > 65535) return false;
      address = address.slice(1, close);
    }
    // Scope id: fe80::1%eth0.
    const percent = address.indexOf("%");
    if (percent >= 0) {
      if (percent === address.length - 1) return false;
      address = address.slice(0, percent);
    }
    if (address.length === 0) return false;

    const doubleColon = address.indexOf("::");
    if (doubleColon >= 0 && address.indexOf("::", doubleColon + 1) >= 0) return false;

    let groups = 0;
    const sections = doubleColon >= 0 ? [address.slice(0, doubleColon), address.slice(doubleColon + 2)] : [address];
    for (let s = 0; s < sections.length; s++) {
      const section = sections[s];
      if (section.length === 0) continue;
      const pieces = section.split(":");
      for (let i = 0; i < pieces.length; i++) {
        const piece = pieces[i];
        const isLastPiece = s === sections.length - 1 && i === pieces.length - 1;
        if (isLastPiece && piece.includes(".")) {
          // Embedded IPv4 (::ffff:192.168.0.1): strict dotted-quad.
          if (!/^\d{1,3}(\.\d{1,3}){3}$/.test(piece)) return false;
          if (piece.split(".").some((octet) => parseInt(octet, 10) > 255)) return false;
          groups += 2;
          continue;
        }
        if (!/^[0-9a-fA-F]{1,4}$/.test(piece)) return false;
        groups++;
      }
    }

    return doubleColon >= 0 ? groups <= 7 : groups === 8;
  }

  // "v4", "v6" or "any": the address family IPAddress.TryParse would report, or null.
  function ipFamily(text) {
    if (text.trim().length === 0) return null;
    if (text.includes(":")) return isIPv6(text) ? "v6" : null;
    return parseIPv4(text) ? "v4" : null;
  }

  function ip(ctx) {
    const { value, params } = ctx;
    if (isEmpty(value)) return ok;
    const family = ipFamily(value);
    const version = params.version || "any";
    return result(ctx, family !== null && (version === "any" || version === family));
  }

  function cidr(ctx) {
    const { value, params } = ctx;
    if (isEmpty(value)) return ok;
    const separator = value.lastIndexOf("/");
    if (separator <= 0 || separator === value.length - 1) return fail(ctx);
    const prefixText = value.slice(separator + 1);
    if (!/^[0-9]+$/.test(prefixText) || prefixText.length > 10) return fail(ctx);
    const prefix = parseInt(prefixText, 10);
    if (prefix > 2147483647) return fail(ctx);
    const family = ipFamily(value.slice(0, separator));
    const version = params.version || "any";
    if (family === "v4") return result(ctx, (version === "any" || version === "v4") && prefix <= 32);
    if (family === "v6") return result(ctx, (version === "any" || version === "v6") && prefix <= 128);
    return fail(ctx);
  }

  // ---- numbers and dates ---------------------------------------------------------------------------------

  function toNumber(value) {
    return typeof value === "number" ? value : Number(String(value).trim());
  }

  function int(ctx) {
    const { value } = ctx;
    if (isEmpty(value)) return ok;
    const number = toNumber(value);
    // IntRule: value % 1 == 0, which is false for NaN and the infinities.
    return result(ctx, number % 1 === 0);
  }

  function multipleOf(ctx) {
    const { value, params } = ctx;
    if (isEmpty(value)) return ok;
    const number = toNumber(value);
    const divisor = Number(params.divisor);
    if (!Number.isFinite(number) || !Number.isFinite(divisor) || divisor === 0) return fail(ctx);
    // MultipleOfRule: within a relative tolerance of 1e-12 of a multiple.
    const remainder = Math.abs(number % divisor);
    const distance = Math.min(remainder, Math.abs(divisor) - remainder);
    const tolerance = 1e-12 * Math.max(Math.abs(divisor), Math.abs(number));
    return result(ctx, distance <= tolerance);
  }

  // Dates compare as naive (offset-free) date-times, the way DateTime bounds compare in C#:
  // yyyy-MM-dd[THH:mm[:ss[.fffffff]]].
  const DATE = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,7}))?)?)?$/;

  function parseDate(text) {
    const match = DATE.exec(String(text).trim());
    if (!match) return null;
    const fraction = (match[7] || "").padEnd(7, "0");
    return [
      +match[1],
      +match[2],
      +match[3],
      +(match[4] || 0),
      +(match[5] || 0),
      +(match[6] || 0),
      +fraction,
    ];
  }

  function compareDates(left, right) {
    for (let i = 0; i < left.length; i++) {
      if (left[i] !== right[i]) return left[i] < right[i] ? -1 : 1;
    }
    return 0;
  }

  // The sign of value - bound, or null when either side is not comparable (which fails the rule).
  function compareToBound(value, params) {
    if (params.kind === "date") {
      const left = parseDate(value);
      const right = parseDate(params.value);
      return left && right ? compareDates(left, right) : null;
    }
    const left = toNumber(value);
    const right = Number(params.value);
    if (Number.isNaN(left) || Number.isNaN(right)) return null;
    return left === right ? 0 : left < right ? -1 : 1;
  }

  function bound(test) {
    return (ctx) => {
      if (isEmpty(ctx.value)) return ok;
      const comparison = compareToBound(ctx.value, ctx.params);
      return result(ctx, comparison !== null && test(comparison));
    };
  }

  const validators = {
    "zod-startswith": startsWith,
    "zod-endswith": endsWith,
    "zod-includes": includes,
    "zod-regex": regex,
    "zod-uuid": uuid,
    "zod-cuid2": cuid2,
    "zod-ulid": ulid,
    "zod-ip": ip,
    "zod-cidr": cidr,
    "zod-int": int,
    "zod-multipleof": multipleOf,
    "zod-min": bound((c) => c >= 0),
    "zod-max": bound((c) => c <= 0),
    "zod-gt": bound((c) => c > 0),
    "zod-lt": bound((c) => c < 0),
  };

  let registered = false;

  function register(blazor) {
    const formValidation = (blazor || global.Blazor || {}).formValidation;
    if (!formValidation || typeof formValidation.addValidator !== "function") return false;
    for (const name of Object.keys(validators)) formValidation.addValidator(name, validators[name]);
    registered = true;
    return true;
  }

  global.ZodSharpValidation = { validators, register, isRegistered: () => registered };

  // Blazor.formValidation exists once Blazor has started. The JavaScript initializer registers at that point; a plain
  // <script> tag polls briefly instead.
  if (!register() && typeof global.setTimeout === "function" && typeof global.document !== "undefined") {
    let attempts = 0;
    const retry = () => {
      if (registered || register() || ++attempts >= 100) return;
      global.setTimeout(retry, 50);
    };
    global.setTimeout(retry, 0);
  }
})(globalThis);
