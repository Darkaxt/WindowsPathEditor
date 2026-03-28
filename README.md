Windows Path Editor
===================

A Windows PATH manager with drag-and-drop reordering, conflict detection, and
automatic cleanup — now with a grouped conflict viewer, auto-sort, and
registry backup on every save.

[Download Latest Version (1.8)](https://github.com/Darkaxt/WindowsPathEditor/releases/download/1.8/windowspatheditor-1.8.zip)

Introduction
------------

In a fit of horrible irony, on Windows you'll both have the most need to edit
your PATH (since all applications insist on creating their own `bin`
directories instead of installing to a global `bin` directory like on Unices),
and you're also equipped with the absolute worst tools to deal with this. The
default environment editor dialog where you get to see 30 characters at once if
you're lucky? Yuck.

*Windows Path Editor* gives you a better overview and easier ways to manage
your PATH settings, covering both the System and User PATH side by side.

Features
--------

- **Drag and drop** — reorder entries within and between the System and User
  PATH lists.
- **Live validation** — entries are checked in the background; missing paths
  are shown in red with strikethrough, conflicting ones in orange, while a
  pending indicator shows when a check is still in flight.
- **Grouped conflict viewer** — conflicting DLL and EXE files are grouped by
  the exact set of PATH locations that share them. Each group shows a
  per-file version matrix with the runtime winner and highest-version copy
  highlighted. Column headers carry colour-coded SYSTEM / USER / MIXED origin
  badges.
- **Auto Sort** — suggests and applies a reordering that minimises DLL
  shadowing by higher-version copies.
- **Clean Up** — removes non-existent entries and exact duplicates in one
  click.
- **Automatic scan** — searches `C:\` for `bin` directories and lets you
  cherry-pick which ones to add.
- **Registry backup** — writes a `.reg` rollback file beside the executable
  before every save, so you always have an emergency restore point.
- **UAC aware** — edits to the System PATH trigger a UAC elevation prompt;
  User PATH changes save silently.

![Screen Shot of Windows Path Editor](https://raw.github.com/Darkaxt/WindowsPathEditor/master/screenshot.png)

Building from source
--------------------

Requirements: Visual Studio 2019 or later (or the MSBuild CLI), targeting
.NET 4.0 Client Profile (x86).

```
MSBuild WindowsPathEditor.sln /restore /p:Configuration=Release /p:Platform=x86
```

The test suite targets net48 and uses MSTest:

```
vstest.console.exe WindowsPathEditor.Tests\bin\x86\Release\net48\WindowsPathEditor.Tests.dll
```

Credits
-------

Originally created by [rix0rrr](https://github.com/rix0rrr/WindowsPathEditor).
Extended by [Darkaxt](https://github.com/Darkaxt/WindowsPathEditor).
