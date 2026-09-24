# AGENTS.md — Notes for AI agents (gregMod.Economics)

Repo: https://github.com/mleem97/gregMod.Economics · License: Apache-2.0 · Version: see `VERSION` (1.0.1).

**Experimental mod** (`.experimental-mods/`, excluded from central
`build.sh` build/deploy). MelonMod for Data Center (`EconomyEngineMod`).
Economy engine with core logic and UI. Panel key: **F11** (shared with
MemeRoulette) — verify before changing defaults.

## Duties

1. **Read first:** `README.md`, `QUICKSTART.md`, `docs/INDEX.md` — only then make changes.
2. **Do not commit secrets** (keys, tokens, `.env`). Use keys only via environment variables.
3. **Preserve history:** no `push --force`, no history rewrite without instruction.
4. **Verify changes:** before reporting done, build and test whatever the repo
   provides (`QUICKSTART.md`, `scripts/`, `tests/` — `dotnet build gregMod.EconomyEngine.csproj -c Release`).
5. **Keep docs in sync:** for new features update `README.md` + `docs/` + `CHANGELOG.md` (Unreleased).
6. **Conventions:** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:` …), one logical change per commit.
7. **When unsure:** stop and ask instead of guessing — especially for deletes, migrations, CI.

## Build and references

- Target: `net6.0`, x64. Game: Data Center. Note the project file is named
  `gregMod.EconomyEngine.csproj` (not `gregMod.Economics.csproj`).
- `references/` holds symlinks into the Steam install. Never commit
  `references/*.dll`, `bin/`, or `obj/`.
- Not covered by `ModRepositories/build.sh`; build directly in this folder.

## Hard rules

- All value changes go through the game's own economy methods so HUD and
  save stay consistent — never write currency/XP fields directly.
- **Never** touch gregCore types outside a soft-probe/JIT-split bridge.
- Security: `SECURITY.md` applies.

## Layout

- `src/EconomyEngineMod.cs` — MelonMod entry. `src/Core/` — engine.
- `src/UI/` — panels. `scripts/`, `tests/`, `examples/`, `docs/` — support.
