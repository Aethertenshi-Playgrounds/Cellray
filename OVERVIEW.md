# Cellray - codebase overview

Cellray is a small terminal UI library for C# that looks and feels like [Raylib](https://www.raylib.com/): a
`while (!ShouldClose())` loop, `BeginDrawing` / `DrawText` / `EndDrawing`, and raygui-style immediate-mode widgets.
It draws with plain ANSI/VT escape codes, so it needs no window, GPU or shell, and it is designed for **NativeAOT**:
apps compile to one small native executable with no .NET runtime installed.

Visual style follows btop: thin dim lines, rounded titled panels, Braille dot graphs, gradient-coloured meters, and the
terminal's own background showing through.

License: MIT (`LICENSE`).

## Repository layout

```
Cellray.slnx
src/
  Cellray/        the library (class library, AOT-compatible)
  CellrayDemo/    NativeAOT demo app showing every widget
  HelloCli/       first NativeAOT hello-world (1 line; proves the toolchain)
  TuiDemo/        leftover from before the rename; untracked, safe to delete
```

Apps built on Cellray live in their own repos, for example Farview (`../Cellray-projects/Farview`).

## Build and run

```powershell
dotnet publish src/CellrayDemo -c Release -r win-x64
src/CellrayDemo/bin/Release/net10.0/win-x64/publish/CellrayDemo.exe     # [N] runs N frames then exits
```

NativeAOT on Windows needs the .NET SDK plus the VS Build Tools C++ workload, and `vswhere.exe`
(`C:\Program Files (x86)\Microsoft Visual Studio\Installer`) on `PATH`. A running copy of the exe locks the file, so
close it before rebuilding.

## The frame loop

```csharp
Tui.InitTerminal();
Tui.SetTargetFPS(60);
while (!Tui.ShouldClose())
{
    Tui.BeginDrawing();                  // poll input, handle resize, start Gui frame
    Tui.ClearBackground(Theme.MainBg);
    // ... Tui.Draw*() and Gui.*() calls ...
    Tui.EndDrawing();                    // finish Gui, diff + flush to the terminal, limit frame rate
}
Tui.CloseTerminal();
```

`Tui.cs` internals, in order, per frame:

1. **BeginDrawing**: `Input.Poll()` (parse bytes read since last frame), snapshot "was Esc claimed by a widget", re-read the terminal size (a change reallocates both buffers and forces a full redraw), `Gui.BeginFrame()`.
2. **Your drawing code** writes into a *back buffer* of cells (`char`, foreground, background). Nothing reaches the terminal yet.
3. **EndDrawing**: `Gui.EndFrame()` (draws pop-up overlays, drops/moves focus), then `Flush()` compares back and front buffers cell by cell and emits only changed cells: a cursor move when not contiguous, a colour change only when it differs, then the character. It is written as one UTF-8 string. Finally `LimitFrameRate()`.
4. **Frame limiter**: fixed-deadline pacing (a late frame doesn't delay later ones), 1 ms `Sleep` steps then a short spin. On Windows it requests 1 ms timer resolution (`timeBeginPeriod`); without it `Sleep` rounds to about 15.6 ms and FPS jumps between 60, 30 and 20. `GetFPS()` is smoothed.

Colours are 24-bit (`ESC[38;2;r;g;bm`). `Color.Default` (alpha 0) emits `ESC[39m` / `ESC[49m`, meaning "the terminal's own colour".

## Source map (`src/Cellray`)

| File | Role |
|---|---|
| `Tui.cs` | Raylib-style API, cell buffers, diff renderer, frame limiter, terminal setup/teardown (alternate screen, hidden cursor, mouse modes, restore on exit and on `ProcessExit`). |
| `Input.cs` | Background thread reads raw stdin bytes; `Poll()` parses them once per frame into key presses, typed characters and SGR mouse events (`ESC[<b;x;y M/m`): buttons, position, wheel, press/release. Handles arrows, Home/End/Delete/PageUp/Down, Shift+Tab, Enter, Backspace, UTF-8, Ctrl+C. A lone `ESC` is the Escape key. Typed characters are cleared every frame. |
| `Platform.cs` | The only OS-specific code: enable VT output on Windows, raw input (Windows VT input mode; Linux/macOS `cfmakeraw` via libc), 1 ms timer resolution, terminal size. |
| `Gui.cs` | Immediate-mode widgets, plus `Rect`. See below. |
| `Color.cs` | `Color(R,G,B,A)` record struct: named colours, `Default`, `FromHex`, `Lerp`. |
| `Gradient.cs` | 101-step colour ramp from start/mid/end colours (`At(percent)` or `At(0..1)`). |
| `Theme.cs` | Named colours used by the widgets (Tokyo Night values): `MainBg`, `MainFg`, `Title`, `HiFg` (hover/focus accent), `InactiveFg`, `Box` (lines), `Ok`, `SelectedBg`, `MeterBg`, and the `Heat` gradient. All are mutable static fields. |
| `History.cs` | Fixed-capacity rolling `float` series (oldest first) used as graph data. |

## Public API summary

**`Tui`**: `InitTerminal`, `CloseTerminal`, `ShouldClose`, `RequestClose`, `SetExitKey`, `SetTargetFPS`,
`GetScreenWidth/Height`, `GetFrameTime`, `GetFPS`, `GetTime`, `BeginDrawing`, `EndDrawing`, `ClearBackground`,
`DrawText` (with optional background), `DrawChar`, `DrawRectangle`, `DrawRectangleLines(…, rounded)`;
input: `IsKeyPressed(ConsoleKey)`, `GetCharPressed`, `IsEscapePressed`, `GetMouseX/Y`, `GetMousePosition`,
`IsMouseButtonPressed/Released/Down`, `GetMouseWheelMove`. Coordinates are character cells; `(0,0)` is top-left.
Esc quits by default; call `SetExitKey((ConsoleKey)0)` to handle it yourself with `IsEscapePressed()`.

**`Gui`** (all mouse and keyboard operable; Tab / Shift+Tab move focus, Enter / Space activate):

| Widget | Call | Returns |
|---|---|---|
| Label | `Label(x, y, text[, color])` | |
| Panel | `Panel(rect, title, number)`: rounded box with the title notched into the top edge, optional superscript number | |
| Button | `Button(rect, text)`: rounded box at height 3+, `[ text ]` at 1-2 | clicked |
| Checkbox | `Checkbox(x, y, text, ref bool)` | toggled |
| Toggle | `Toggle(x, y, text, ref bool)` | toggled |
| Slider | `Slider(x, y, width, ref float, min, max)`; fill coloured along `Theme.Heat` | changed |
| TextBox | `TextBox(rect, ref string[, out submitted], placeholder, maxLength)`: cursor, editing keys, scrolling | text changed |
| RadioGroup | `RadioGroup(x, y, options, ref int)`; Up/Down moves | selection changed |
| Dropdown | `Dropdown(x, y, width, items, ref int, maxVisible)`: list overlay, wheel/keys, Esc or outside click closes | selection changed |
| Meter | `Meter(x, y, width, percent[, gradient])`: row of `■` beads | |
| Graph | `Graph(x, y, w, h, IReadOnlyList<float>, min, max, gradient, baseline)`: Braille dot graph | |

Also `Gui.Disabled` (dims and disables everything drawn while it is true) and `Gui.FocusTextBox(rect)`.

## How the immediate-mode GUI works (`Gui.cs`)

- **Identity**: no objects. A widget's id is a hash of (kind, x, y), so two different widgets at the same cell would clash.
- **Focus order** is the order widgets are drawn in. `EndFrame` applies Tab / Shift+Tab to that list, so focus moves on the next frame. Clicking a widget focuses it; clicking empty space clears focus.
- **Mouse**: a press over a widget makes it "active"; releasing over it counts as a click (so you can press, drag off and cancel). Sliders keep tracking while held.
- **Overlays**: an open dropdown registers its list to be drawn in `EndFrame`, on top of everything, and widgets under its rectangle ignore the mouse. Its list height and above/below placement adapt to the screen.
- **Escape**: an open dropdown or focused text box claims Esc (closes / unfocuses) instead of the app seeing it.
- **Braille graph**: each cell holds two samples side by side with four dot-levels per row (the dot patterns are built from Unicode bit masks in `BuildBraille`). Rows are coloured from the gradient, hot at the top; a one-row graph is coloured by value; `baseline` keeps a dot on the bottom row.

## Conventions and gotchas

- Source uses `\uXXXX` escapes for box-drawing and symbol characters in code, because early edits through Windows PowerShell re-encoded the file and corrupted them. Doc comments may contain real Unicode.
- Text is one `char` per cell. Wide characters (CJK, emoji) and combining marks are **not** handled and will misalign.
- Everything is `static` and single-threaded apart from the stdin reader thread, so one terminal UI per process.
- Library calls are AOT-safe: no reflection; native calls use `LibraryImport`.

## Status

- **Tested**: Windows (Windows Terminal), including mouse, keyboard, all widgets, graphs, resizing, and a real app (Farview) over SSH.
- **Not tested**: Linux and macOS (the raw-mode and input code is written but has never been run there), non-Windows-Terminal terminals, terminals without truecolor or mouse support (no 256/16-colour fallback yet).
- **Not done**: wide-character handling, radio groups laid out horizontally, dropdown scrollbar, key-up/held detection, an `IsKeyDown`, clipboard, a README, CI for Windows/Linux/macOS, and a NuGet package.
