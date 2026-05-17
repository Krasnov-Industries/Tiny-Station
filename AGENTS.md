# Tiny-Station — guide for AI agents

This file is the entry point for AI coding agents working in this repository (opencode, Codex CLI, Claude Code via [CLAUDE.md](./CLAUDE.md), and similar tools). It explains how the fork is laid out, the rules for editing code, and where to find the skill library.

## What is this repository

Tiny-Station is a fork of [Space Station 14](https://github.com/space-wizards/space-station-14). The vanilla upstream lives untouched in the standard `Content.Server` / `Content.Shared` / `Content.Client` / `Resources` folders. Our fork-specific code is isolated in `_`-prefixed project folders (see below).

## Project folders

Folders prefixed with `_` separate fork code from vanilla. There are several:

| Folder | Role | Edit policy |
|---|---|---|
| `_Tinystation` | **Primary** — our own new content lives here. | All **new** systems, components, prototypes, assets go here. |
| `_Goobstation` | **Inherited** from Goob-Station. Ported earlier. | Edit existing code when needed. **Do not add brand-new features here** — put them in `_Tinystation/`. |
| `_EE` | **Inherited** from Einstein-Engines. | Same policy as `_Goobstation`. |
| other `_*` | May appear when content is ported from another fork. | Investigate origin via `git log`/`git blame` before editing. Treat as inherited. |

When you adapt an example from a skill that uses the placeholder `_Tinystation/`, substitute one of the inherited folders **only if you are extending a system that already lives there** and a partial-class extension on the spot is the cleanest fix.

## Communication

- Default language with the maintainer: **Russian** (sukhoy, po delu / dry, to the point).
- If a task is ambiguous, ask before guessing.
- Before non-trivial changes: briefly state the plan, wait for an explicit "go".
- Prefer minimal, reversible edits.

## Workflow rules

- **Read the relevant skill** before touching an unfamiliar subsystem. The skill library is at `.agent/skills/` (see catalogue below).
- **Diagnose before changing.** If something is broken, understand the cause first.
- A green exit code is not proof of success — verify the actual effect.
- Do not commit unless asked.
- Do not delete files without explicit confirmation.
- Do not print secrets (tokens, keys, `.env` contents) into responses.
- Back up an existing file (e.g. `.bak` next to the original) before destructive rewrites.
- Follow the rules in [`.agent/skills/ss14-upstream-maintenance/SKILL.md`](.agent/skills/ss14-upstream-maintenance/SKILL.md) for any edit that touches vanilla files. **Minimising vanilla-file edits is more important than "pretty" architecture** — every line you touch outside `_Tinystation/` is a future merge conflict.

## Skill library

Skills are markdown playbooks for specific subsystems. Each agent tool has its own auto-discovery path, so the library is mirrored across several trees, all of which point at the same canonical files:

| Tree | Purpose |
|---|---|
| `.agent/skills/<name>/SKILL.md` | **Canonical content.** Edit only this copy. |
| `.claude/skills/<name>/SKILL.md` | Bridge for Claude Code auto-discovery. |
| `.codex/skills/<name>/SKILL.md` | Bridge for Codex CLI. |
| `.agents/skills/<name>/SKILL.md` | Bridge for other AGENTS-aware tools. |

When updating a skill, edit `.agent/skills/<name>/` only — bridges stay generic.

### Catalogue

- **`ss14-atmos-system-api`** — AtmosSystem API: fresh vs legacy methods, gameplay/device/map-level usage.
- **`ss14-atmos-system-core`** — AtmosSystem architecture: processing cycle, tile state, invalidations, DeltaPressure, overlay sync.
- **`ss14-audio-system-api`** — AudioSystem API: Play/Set/Stop/Resolve, predicted audio, filters, OpenAL EFX.
- **`ss14-databases`** — Database guide (PostgreSQL and SQLite).
- **`ss14-documentation-writing`** — Documentation standard for C#, SWSL, YAML, FTL.
- **`ss14-ecs-components`** — Components: data containers, attributes, networking, marker components, state-as-component.
- **`ss14-ecs-entities`** — Entities: `EntityUid`, `Entity<T>`, component operations, containers, lifecycle.
- **`ss14-ecs-prototypes`** — YAML prototypes: inheritance, prototype classes, YAML linter.
- **`ss14-ecs-systems`** — `EntitySystem` architecture: lifecycle, events, queries, prediction, partial decomposition.
- **`ss14-events`** — Events: taxonomy, subscriptions, by-ref events, networking patterns.
- **`ss14-graphics-animation-player`** — `AnimationPlayerSystem`: lifecycle, track types, keyframes/easing, completion events.
- **`ss14-graphics-generic-visualizer-appearance`** — `AppearanceComponent` + `AppearanceSystem` + `GenericVisualizer`.
- **`ss14-graphics-overlays`** — Overlays: `OverlaySpace`, shaders, `ScreenTexture`, render targets, stencil composition.
- **`ss14-graphics-shaders`** — SS14/SWSL shaders: syntax, presets, built-ins, light_mode/blend_mode, stencil.
- **`ss14-graphics-sprite-system`** — `SpriteSystem`: lifecycle, full API, layers, layer-map, anti-patterns.
- **`ss14-loadout-authoring`** — Role loadouts: `roleLoadout` / `loadoutGroup` / `loadout`, `startingGear` migration.
- **`ss14-localization-strings`** — `.ftl` files: structure, syntax, functions, selectors, inheritance.
- **`ss14-matrix-transform-physics-sprite`** — Matrix transforms: world/grid/local/screen conversions, broadphase queries.
- **`ss14-migrations`** — Database migrations (PostgreSQL and SQLite).
- **`ss14-naming-conventions`** — Naming for C#, YAML, FTL: components/systems/IDs/keys/variables/files.
- **`ss14-netcode`** — Networking: Lidgren, `NetManager`, messages, state sync, PVS, network events.
- **`ss14-npc-system-core`** — NPC system: HTN planning, utility selection, steering/pathfinding, blackboard.
- **`ss14-physics-system-api`** — `SharedPhysicsSystem` public API.
- **`ss14-physics-system-core`** — Physics architecture: broadphase, contacts, islands, solver, prediction.
- **`ss14-prediction`** — Client-side prediction: loop, timing, state reconciliation, randomness, pitfalls.
- **`ss14-pvs`** — PVS: chunk-based partitioning, visibility, overrides, budgets, LoD, visibility masks.
- **`ss14-skill-authoring`** — Meta: how to write and update skills.
- **`ss14-standard-optimizations`** — Hot-path patterns: caching, allocations, no-LINQ, ActiveComponent, ByRef, DirtyField.
- **`ss14-tests-authoring`** — Writing unit/integration tests.
- **`ss14-tests-poolmanager`** — Integration test framework: PoolManager, TestPair lifecycle.
- **`ss14-transform-system-api`** — `SharedTransformSystem` API.
- **`ss14-transform-system-core`** — Transform architecture: `EntityCoordinates`/`MapCoordinates`, hierarchy, anchor/unbind.
- **`ss14-ui-bui`** — Bound UI (BUI): architecture, messages, validation, prediction, lifecycle.
- **`ss14-ui-styles-palettes-sheetlets`** — `StyleClass`, palettes, `StyleProperties`, sheetlets, pseudo-classes.
- **`ss14-ui-xaml`** — XAML UI: window structure, `RobustXamlLoader`, layout, localization, style classes.
- **`ss14-upstream-maintenance`** — **Read this first** before editing any vanilla file. Edit markers, partial classes, prototype-inheritance pattern, `migration.yml`.
- **`ss14-virtual-controller-api`** — `VirtualController` API.
- **`ss14-virtual-controller-core`** — `VirtualController` architecture: UpdateBefore/AfterSolve, prediction, Box2D internals.

Some skills (`ss14-atmos-system-*`, `ss14-audio-system-api`, `ss14-documentation-writing`, `ss14-matrix-transform-physics-sprite`, `ss14-naming-conventions`, `ss14-npc-system-core`, `ss14-standard-optimizations`, `ss14-virtual-controller-*`) include a `references/` subdirectory with extended pattern catalogues. Open them when the main `SKILL.md` points there.

## Not in this library yet

These topics were intentionally skipped from the initial import — feel free to add them later when there is a need and the content is solid:

- `ss14-eventbus` — overlaps with `ss14-events` and is less practical; use `ss14-events`.
- `ss14-ui-eui-and-ui-manager` — needs a focused rewrite.
- `ss14-localization-code` — the upstream version had contradictions about `Loc` deprecation.
- `ss14-audio-system-core` — too shallow as it stood.

## How skill content was adapted

This library was imported from another SS14 fork and adapted for Tiny-Station. All `_Sunrise` / `_Scp` / `_Fish` / `_Lust` references were replaced with the Tiny-Station folder layout (`_Tinystation` + inherited `_Goobstation` / `_EE`). Examples that referenced fork-unique systems (e.g. `RandomPredictedSystem`, `FieldOfViewSetAlphaOverlay`, `HitscanRicochetSystem`) were either removed or generalised, because Tiny-Station does not have those classes.

If you spot a remaining sunrise-specific reference or an example that does not match Tiny-Station code, fix it in `.agent/skills/<name>/SKILL.md` directly.
