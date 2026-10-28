# Engineering Rules for AI Agents

These rules are mandatory for every AI agent working in this repository.

## Keep Code DRY

- Never copy and paste an entire function or method implementation.
- Before adding logic, search the repository for an existing implementation that can be reused or generalized.
- When two consumers need the same behavior, extract a shared helper while keeping consumer-specific state and policy separate.
- Do not introduce an abstraction when a smaller non-duplicated change is clearer.

## Keep Diffs Small

- Prefer the smallest correct change that fully satisfies the request.
- Do not perform unrelated refactors, formatting changes, renames, or cleanup.
- Preserve existing style and structure unless changing them is required for correctness.
- Inspect staged and unstaged changes before editing, and preserve user-owned work.
- Review the final diff and remove accidental or unnecessary changes before handing off.

## Persist Plans

- Always save plans in the `Plans` directory.
- When a plan needs to change, edit its file like a code file: preserve the existing content and structure, minimize the diff, and never rewrite the entire plan from conversational context.

## Preserve Functionality When Fixing Builds

- Never make a build pass by removing, disabling, bypassing, or excluding functionality unless the user explicitly approves that tradeoff.
- Do not comment out failing code, remove features, skip targets, weaken validation, or exclude tests merely to obtain a successful build.
- Diagnose and fix the underlying compatibility, dependency, API, or implementation problem.
- If preserving functionality requires a product or architecture decision, stop and ask the user before changing behavior.
- Report any remaining build failure honestly rather than presenting reduced functionality as a fix.
