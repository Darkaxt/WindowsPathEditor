# Auto Sort Cleanup, SystemDrive Normalization, And Bin Crawler Design

## Goal

Refine the current PATH editing workflow so that:

- `Auto Sort` can remove broken and duplicate entries as part of the same review flow
- cleanup remains explicit in the preview instead of being silently bundled into sorting
- raw `C:\...` paths normalize to `%SystemDrive%\...` when no more specific symbolic path applies
- the misleading `Automatic` button is renamed to `Bin Crawler`

Nothing is written to the registry until `Save`.

## Why This Change Exists

The current UI splits three related concerns in a way that is easy to misread:

- `Clean Up` removes broken and duplicate entries
- `Auto Sort` handles migration-aware scope and ordering changes
- `Automatic` does neither; it only scans the drive for `bin` directories

That leaves two practical problems:

- autosort currently plans around junk entries instead of treating cleanup as part of the optimization flow
- some paths that could be made more readable still remain as raw `C:\...` strings because `%SystemDrive%` is not part of the normalization set

The user wants cleanup folded into the autosort experience, but still surfaced as its own decision area so the behavior remains understandable.

## Scope

- Extend `Auto Sort` so it plans against a cleaned temporary path set
- Add a dedicated cleanup section to the Auto Sort preview
- Add `%SystemDrive%` as a fallback normalization variable
- Rename the `Automatic` scan button and related window text to `Bin Crawler`

## Out Of Scope

- removing the standalone `Clean Up` button
- changing what the scan feature searches for beyond `bin` directories
- adding new scope-migration policy beyond the autosort work already implemented
- changing save/apply semantics

## Auto Sort With Explicit Cleanup

### High-Level Flow

When `Auto Sort` is clicked:

1. Capture the current in-memory `System PATH` and `User PATH`
2. Run cleanup analysis first
3. Build a cleaned temporary path set by removing:
   - nonexistent resolved paths
   - duplicate resolved paths
4. Run the existing migration-aware autosort planner against that cleaned temporary path set
5. Show one preview dialog that contains:
   - cleanup removals
   - promotions
   - demotions
   - normalizations
   - reorders
   - warnings
6. If the user confirms, apply the cleaned + migrated + autosorted lists to the editor
7. If the user cancels, leave the editor unchanged

### Cleanup Visibility

Cleanup must be visible as its own preview section rather than silently merged into autosort.

The preview should gain a dedicated `Cleanup` section/tab containing:

- removed broken paths
- removed duplicate paths
- original scope for each removed entry
- a short reason such as:
  - `Path does not exist`
  - `Duplicate of earlier resolved path`

This keeps the current mental model intact:

- cleanup is still cleanup
- autosort is still autosort
- the user sees both in one place before applying

### Cleanup Source Of Truth

Reuse the existing cleanup behavior as much as possible rather than duplicating rules.

The existing cleanup semantics are:

- unresolved paths are preserved
- resolved nonexistent paths are removed
- duplicate resolved paths are removed, keeping the earliest occurrence

The autosort preview model should record cleanup removals explicitly, but the actual cleaned path lists should still follow the existing cleanup rules.

## SystemDrive Normalization

### Rule

Add `%SystemDrive%` to the normalization variable set as a fallback-only alias.

Examples:

- `C:\libjpeg-turbo64\bin` -> `%SystemDrive%\libjpeg-turbo64\bin`
- `C:\Users\darka\AppData\Local\Tools` should still prefer `%LocalAppData%\Tools`
- `C:\Windows\System32` should still prefer `%SystemRoot%\System32`

### Ordering

Normalization must continue to prefer the most specific available variable match.

That means `%SystemDrive%` must lose to more specific variables such as:

- `%SystemRoot%`
- `%windir%`
- `%ProgramFiles%`
- `%ProgramW6432%`
- `%ProgramFiles(x86)%`
- `%ProgramData%`
- `%LocalAppData%`
- `%AppData%`
- `%UserProfile%`

No special-case sorting logic should be added beyond making `%SystemDrive%` available to the existing longest-match selection.

## UI Naming

Rename the current `Automatic` button to `Bin Crawler`.

Also update related text for consistency:

- button label: `Bin Crawler`
- scanning window title: `Bin Crawler`
- tooltip/help text should explicitly mention that it scans for `bin` directories

Recommended tooltip:

`Search the system for directories named "bin" and offer them for import.`

This keeps the feature name honest while still allowing a short button label.

## Preview Model Changes

The Auto Sort preview data model should gain cleanup records alongside the existing:

- promotions
- demotions
- normalizations
- reorders
- warnings

Each cleanup record should include:

- original path
- original scope
- cleanup reason

The summary area should include a cleanup count in addition to the existing counts.

## CLI Alignment

GUI behavior should remain the priority, but the CLI should not drift.

If `/cli autosort` is intended to reflect the button behavior, its payload should eventually expose cleanup removals too. If this is too much for the first pass, GUI support may land first, but the design target is shared behavior rather than two divergent implementations.

## Risks And Guardrails

### Keep Cleanup Explicit

The main risk is making Auto Sort feel like it silently deletes entries. That is why cleanup must be previewed as a separate section instead of being hidden inside a generic “optimized result”.

### Preserve Existing Cleanup Semantics

Do not broaden cleanup to remove unresolved entries. Unresolved paths must remain preserved unless the user manually deletes them.

### Preserve Specific Variable Preference

Adding `%SystemDrive%` must not make the display noisier by replacing clearer aliases with a generic drive-root alias.

## Testing

Automated coverage must include:

- autosort preview records broken-path cleanup entries separately from sort/migration changes
- autosort preview records duplicate cleanup entries separately from sort/migration changes
- applying the preview after confirmation yields the cleaned + autosorted result
- cancelling the preview leaves the original editor lists unchanged
- `%SystemDrive%` normalizes raw drive-root paths when no more specific alias exists
- `%SystemDrive%` does not override `%SystemRoot%`, `%ProgramFiles%`, `%LocalAppData%`, or other more specific aliases
- the `Bin Crawler` rename updates the visible UI text without changing scan behavior

Manual verification must include:

- a live run where broken entries appear under the new cleanup section in Auto Sort preview
- a live run where a raw `C:\...` path becomes `%SystemDrive%\...`
- confirming that the renamed `Bin Crawler` button still opens the scan workflow
