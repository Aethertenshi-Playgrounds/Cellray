<div align="center">

# ▦ Cellray

**A terminal UI engine for C# with a Raylib-shaped, immediate-mode API.**

[![NativeAOT](https://img.shields.io/badge/NativeAOT-ready-blueviolet)](#)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](#)
[![License: MIT](https://img.shields.io/badge/license-MIT-brightgreen)](LICENSE)
[![Platform](https://img.shields.io/badge/tested_on-Windows_Terminal-0078D6)](#-status)

Draw cells, not widgets-in-widgets. One `while` loop, a handful of calls, and a
single native executable with no runtime to install.

</div>

---

## ✨ Why Cellray

If you know Raylib, you already know how this works: open, loop, begin drawing, draw, end drawing.
Cellray brings that rhythm to the terminal, so a program is a loop and a few calls instead of a
framework to learn.

- 🎯 **Raylib-shaped API** — `InitTerminal`, `BeginDrawing`, `DrawText`, `DrawRectangle`, `EndDrawing`
- 🧩 **Immediate-mode widgets** — `if (Gui.Button(...)) { ... }` with no objects, handlers or layout tree
- 🖱️ **Mouse and keyboard** — click, drag, wheel, Tab focus, typing, all through plain terminal input
- 📈 **Braille dot graphs** — two samples and four levels per character cell, coloured by a gradient
- 🎨 **Themeable** — thin dim lines, rounded titled panels, truecolor gradients, and your terminal's own background
- ⚡ **Fast by construction** — a back buffer is diffed against the last frame, so only changed cells are written
- 📦 **NativeAOT-friendly** — no reflection, so apps publish as one small native executable

No window, no GPU, no shell dependency: just ANSI/VT escape codes, so it runs inside any modern terminal.

## 🚀 Quick start

```csharp
using Cellray;

Tui.InitTerminal();
Tui.SetTargetFPS(60);

int clicks = 0;
while (!Tui.ShouldClose())            // Esc or Ctrl+C
{
    Tui.BeginDrawing();
    Tui.ClearBackground(Theme.MainBg);

    Gui.Panel(new Rect(2, 1, 34, 7), "hello", number: 1);
    if (Gui.Button(new Rect(4, 3, 14, 3), "Click me")) clicks++;
    Gui.Label(20, 4, $"clicked {clicks} times");

    Tui.EndDrawing();
}

Tui.CloseTerminal();
```

Run the full widget showcase:

```powershell
# needs the .NET SDK; on Windows also the VS Build Tools C++ workload for NativeAOT
dotnet publish src/CellrayDemo -c Release -r win-x64
src/CellrayDemo/bin/Release/net10.0/win-x64/publish/CellrayDemo.exe
```

Use it in your own project by referencing it (a NuGet package is planned):

```xml
<ProjectReference Include="path\to\Cellray\src\Cellray\Cellray.csproj" />
```

## 🧰 What's in the box

| Call | What it does |
|---|---|
| `Gui.Panel` | Rounded box with the title notched into the top edge: `╭─┐¹cpu┌──╮` |
| `Gui.Button` · `Checkbox` · `Toggle` · `Slider` | The basics, mouse and keyboard operable |
| `Gui.TextBox` | Single-line input with cursor, editing keys, scrolling and a placeholder |
| `Gui.RadioGroup` · `Dropdown` | Choices; the dropdown's list draws on top and blocks clicks beneath it |
| `Gui.Meter` | A row of `■` beads filled along a gradient |
| `Gui.Graph` | Braille dot graph fed by a rolling `History` buffer |
| `Gui.Label` | Plain text in the theme colours |
| `Tui.Draw*` | Text, characters, filled and outlined rectangles for custom drawing |
| `Theme` · `Gradient` · `Color` | Named colours, 101-step ramps, `FromHex`, `Lerp`, and `Color.Default` for the terminal's own colour |

Tab and Shift+Tab move focus, Enter or Space activates, and arrow keys adjust sliders and lists.

## 🛰️ Built with Cellray

**[Farview](https://github.com/Aethertenshi-Playgrounds/Farview)** is a btop-style dashboard that monitors a remote
Linux server over SSH: live CPU graphs, memory, per-disk meters and power draw, in a ~2 MB native executable.

## 🧠 How it works

```
 your loop ──▶ BeginDrawing   read input, handle resize
                  │
                  ▼           Tui.Draw*() / Gui.*() write into a back buffer of cells
              EndDrawing ──▶  diff against the previous frame
                  │           write only the changed cells as one string of ANSI/VT codes
                  ▼
              frame limiter   fixed-deadline pacing for a steady frame rate
```

Input arrives as raw bytes from a background thread and is parsed once per frame into key presses, typed
characters and SGR mouse events. Windows needs a little setup (VT output and input modes, a 1 ms timer); the
rest of the library is plain C#.

Want the details? See **[OVERVIEW.md](OVERVIEW.md)**: the frame loop, source map, API reference, and how focus,
overlays and the Braille graph work.

## 📋 Status

| | |
|---|---|
| ✅ Tested | Windows 11 with Windows Terminal: rendering, mouse, keyboard, every widget, resizing, 60 fps pacing |
| ⚠️ Written, not yet run | Linux and macOS input and raw-mode code |
| 🔜 Not yet | Wide-character (CJK, emoji) support, 256/16-colour fallback, key-up detection, NuGet package, CI |

Cellray is young and the API may still change.

## 📄 License

[MIT](LICENSE)
