---
name: purview-architecture
description: >-
  Use when deciding the shape of a .NET solution that uses Purview libraries — how many projects,
  whether to add a repository or CQRS layer, where validation lives — or before generating substantial
  implementation code. Establishes project context first, then applies DRY, KISS and architecture
  proportional to the actual requirements rather than to an enterprise reference diagram.
license: MIT
metadata:
  author: Purview-Dev
  version: "1.0"
---

# Purview architecture

The job of this skill is to stop two failures that cost far more than they look:

- **Architecture bought in advance.** Four projects, a repository interface and a mapping layer for an
  application that has one table and one caller.
- **A recommendation made without context.** Advice about database-side GUID generation given to a
  stateless CLI, or a Clean Architecture split proposed for a 300-line utility.

So: establish context, then choose proportionally. Both steps are mandatory, and the first one is the
one that gets skipped.

## 1. Establish context before deciding

Before recommending a structure, adding a project, or generating substantial code, you must know four
things. **Read them out of the repository first** — asking a user a question the code already answers
is the most common way this skill is applied badly.

| Dimension | Read it from | Ask only if |
| -- | -- | -- |
| Complexity (small / medium / large) | Number of functional areas, integrations and external consumers — not file count | The requirements are genuinely not written down anywhere |
| Project type | SDK attribute, `OutputType`, `Microsoft.NET.Sdk.Web`, an AppHost project, a `Main` | The repository is empty |
| Persistence | `PackageReference` to an EF Core provider, `DbContext`, a connection string, a `Purview.Containers` resource | Persistence is being introduced now |
| Telemetry | `Purview.Telemetry.SourceGenerator`, `ILogger` injection, `ActivitySource`, an OTLP exporter | Nothing is configured and you are about to add it |

Identifier generation is a **conditional** dimension: resolve it only when persistence exists *and*
GUID keys are in play. See `purview-persistence`.

Judge complexity on requirements, not size. A 2,000-line CLI with one output format is small. A
900-line service that reconciles three upstream systems and owns money movement is not.

**Do not re-ask.** A dimension answered by repository configuration, existing source, a project file,
an earlier turn in the conversation, or a recorded architectural decision is answered. Ask concise,
targeted questions only for the unknowns that change your recommendation — and if nothing changes,
ask nothing.

Full decision procedure: `references/project-context-discovery.md`.

## 2. Choose proportionally

| Scale | Shape | What you still do not add |
| -- | -- | -- |
| Small | One project, or two when a test project needs a non-trivial subject. Direct implementations. | Repository over `DbContext`, CQRS, a mapping layer, speculative interfaces |
| Medium | Explicit boundaries where complexity already hurts — usually a domain project and a host | Event sourcing, a generic infrastructure framework, a second infrastructure implementation nobody asked for |
| Large | Modularity, independent infrastructure implementations, enforceable boundaries (see the playbook's Architecture-As-Tests page) | Patterns adopted for symmetry rather than for a named problem |

Architecture is allowed to arrive late. A boundary added when the second caller appears is cheap; a
boundary added in anticipation of a second caller that never appears is permanent.

**Never add a project for symmetry.** "Domain, Application, Infrastructure, API" is a response to a
problem, not a starting position. If you cannot name the failure a layer prevents in this codebase,
do not add the layer.

Full guidance and the per-pattern justification tests: `references/proportional-architecture.md`.

## 3. Name what you do create

A project name states a **responsibility**, optionally qualified by an implementation:

```text
<Responsibility>[.<Implementation>]
```

The responsibility decides the rules; the qualifier never changes them. Three rules people usually
get wrong:

- **Do not qualify a single implementation.** `Persistence` is correct even when it uses SQL Server.
  Rename when a second provider arrives, not in anticipation.
- **Do not create an empty parent.** `Persistence.SqlServer` + `Persistence.PostgreSql` with nothing
  shared need no `Persistence` project.
- **Multiple implementations do not require multiple assemblies.** Two small adapters with the same
  dependencies belong in one project.

Never use a *category* as a project name — `Infrastructure`, `Services`, `Managers`, `Models`,
`Helpers`, `Common`. Nothing can be excluded from a category, so it grows without limit. And never
rename an established project just to match canonical terminology; `Domain`, `UseCases` and
`Integrations` are recognised aliases.

Responsibility catalogue, dependency matrix and the naming decision tree:
`references/project-responsibilities.md`.

## 4. Check the direction

`A → B` means **A depends on B at compile time** — not a runtime call, a DI registration, an HTTP
request or an Aspire orchestration edge. Confusing those is the most common source of a wrong answer
about whether a dependency is allowed.

The rules that matter most:

- Core depends on nothing but `Shared`.
- Application never references a concrete `Persistence`, `Providers` or `Adapters` project.
- Composition references implementations; **nothing references Composition** except a host.
- Sibling implementations (`Providers.GitHub`, `Providers.AzureDevOps`) stay independent.
- Persistence owns migration *definitions*; `Migrator` owns their *execution*. Production API startup
  does not apply schema migrations.

When a boundary is worth having, make it fail the build rather than the review — see
`purview-architecture-testing`.

## 5. DRY, applied where it pays

Reuse the Purview libraries when they remove a real source of truth, not for coverage:

| Concern | Library | Use it when |
| -- | -- | -- |
| Operation outcomes without exceptions for control flow | `Purview.Results` | Failures are expected and the caller must handle them |
| Primitive obsession, validated identity types | `Purview.ValueObjects` | A value has rules that would otherwise be re-checked at each boundary |
| Schema shared with a TypeScript client | `Purview.ZodSharp` | A contract is duplicated between C# and TypeScript today |
| Local dependencies for dev and integration tests | `Purview.Containers` | Tests need a real database, queue or emulator |
| Logging, tracing and metrics from one declaration | `Purview.Telemetry.SourceGenerator` | Always — see `purview-telemetry` |

Duplication that matters: validation repeated per entry point, a domain rule restated in the UI,
configuration bound in two places, two types that both claim to be the truth. Duplication that does
not: two similar-looking methods that answer to different owners.

## 6. Telemetry is not an advanced topic

Recommend instrumentation from the first commit, at every project size. A small application gets
small telemetry — it does not get none. See `purview-telemetry`.

## Checks before you answer

- Did you read the four context dimensions rather than ask for them?
- Can you name, for each project and each abstraction you are proposing, the failure it prevents here?
- Is anything in your proposal there only because a reference architecture has it?
- Did you include telemetry?

## See also

- `purview-telemetry` — instrumentation at every scale
- `purview-persistence` — storage and GUID-key guidance, only when persistence is real
- [Engineering Principles](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Engineering-Principles.md)
- [Adopting This At Your Scale](https://github.com/purview-dev/engineering-playbook/blob/main/docs/wiki/Adopting-This-At-Your-Scale.md)
