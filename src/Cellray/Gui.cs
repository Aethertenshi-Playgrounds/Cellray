namespace Cellray;

/// <summary>Rectangle in character cells.</summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public bool Contains(int px, int py) => px >= X && py >= Y && px < X + Width && py < Y + Height;
}

/// <summary>
/// Immediate-mode widgets (raygui style): call every frame between BeginDrawing/EndDrawing.
/// Each call draws the widget and returns whether the user changed/activated it.
/// Mouse and keyboard both work: Tab / Shift+Tab move focus, Enter or Space activate, arrows adjust sliders.
/// Look: thin dim lines and text on the terminal background; hover/focus switch to the accent colour (see <see cref="Theme"/>).
/// </summary>
public static class Gui
{
    /// <summary>When true, widgets draw dimmed and ignore input.</summary>
    public static bool Disabled;

    private static int _active;   // widget currently held with the mouse
    private static int _focus;    // widget with keyboard focus
    private static bool _claimed; // a widget consumed this frame's mouse press
    private static readonly List<int> Order = new();

    // text boxes
    private static int _textFocusId; // text box that was drawn focused during the last frame
    private static readonly Dictionary<int, (int Cursor, int Scroll)> TextStates = new();

    // dropdown (only one can be open at a time)
    private static int _openDd;
    private static Rect _popup;
    private static bool _ddSeen;
    private static int _ddHighlight, _ddScroll, _ddLastMx = -1, _ddLastMy = -1;

    // drawn after everything else so pop-ups sit on top
    private static readonly List<Action> Overlays = new();

    private static readonly string[] Superscript =
        { "⁰", "¹", "²", "³", "⁴", "⁵", "⁶", "⁷", "⁸", "⁹" };

    private struct State
    {
        public bool Hover, Held, Clicked, Activated, Focused;
        public readonly bool Lit => Hover || Focused;
    }

    /// <summary>True when Esc should be handled by a widget (open dropdown, focused text box) instead of quitting.</summary>
    internal static bool CapturesEscape => _openDd != 0 || (_textFocusId != 0 && _textFocusId == _focus);

    // ---- frame hooks (called by Tui) ------------------------------------

    internal static void BeginFrame()
    {
        Order.Clear();
        _claimed = false;
        _ddSeen = false;
        _textFocusId = 0;
    }

    internal static void EndFrame()
    {
        foreach (var draw in Overlays) draw();
        Overlays.Clear();

        if (!_ddSeen) _openDd = 0; // dropdown disappeared while open

        // clicking empty space drops focus
        if (Input.MousePressed(MouseButton.Left) && !_claimed) _focus = 0;
        if (Input.MouseReleased(MouseButton.Left)) _active = 0;

        // Tab / Shift+Tab cycle focus through widgets in draw order
        if (Tui.IsKeyPressed(ConsoleKey.Tab) && Order.Count > 0)
        {
            int idx = Order.IndexOf(_focus);
            if (Input.ShiftTab) idx = idx <= 0 ? Order.Count - 1 : idx - 1;
            else idx = idx < 0 ? 0 : (idx + 1) % Order.Count;
            _focus = Order[idx];
        }
    }

    // ---- shared interaction logic ---------------------------------------

    /// <summary>Gives keyboard focus to the text box that is (or will be) drawn with this rectangle, e.g. when a screen opens.</summary>
    public static void FocusTextBox(Rect r) => _focus = Id(6, r.X, r.Y);

    // widget kinds: 1 button, 2 checkbox, 3 toggle, 4 slider, 5 radio, 6 text box, 7 dropdown
    private static int Id(int kind, int x, int y)
    {
        int id = HashCode.Combine(kind, x, y);
        return id == 0 ? 1 : id;
    }

    private static State Update(int id, Rect r)
    {
        Order.Add(id);
        var s = new State();
        int mx = Tui.GetMouseX(), my = Tui.GetMouseY();
        bool hover = !Disabled && r.Contains(mx, my);

        // an open dropdown list covers whatever is underneath it
        if (hover && _openDd != 0 && id != _openDd && _popup.Contains(mx, my)) hover = false;

        if (!Disabled)
        {
            if (hover && Tui.IsMouseButtonPressed(MouseButton.Left))
            {
                _active = id;
                _focus = id;
                _claimed = true;
            }
            if (_active == id && Tui.IsMouseButtonReleased(MouseButton.Left) && hover) s.Clicked = true;
            if (_focus == id && (Tui.IsKeyPressed(ConsoleKey.Enter) || Tui.IsKeyPressed(ConsoleKey.Spacebar))) s.Activated = true;
        }

        s.Hover = hover;
        s.Held = !Disabled && _active == id && Tui.IsMouseButtonDown(MouseButton.Left);
        s.Focused = !Disabled && _focus == id;
        return s;
    }

    /// <summary>Text colour: dim when disabled, accent when hovered/focused.</summary>
    private static Color TextColor(State s) => Disabled ? Theme.InactiveFg : s.Lit ? Theme.HiFg : Theme.MainFg;

    /// <summary>Line/bracket colour: dim normally, accent when hovered/focused/held.</summary>
    private static Color LineColor(State s) => Disabled ? Theme.InactiveFg : (s.Lit || s.Held) ? Theme.HiFg : Theme.Box;

    private static string Fit(string text, int width) =>
        width <= 0 ? "" : text.Length > width ? text.Substring(0, width) : text;

    // ---- basic widgets --------------------------------------------------

    public static void Label(int x, int y, string text) => Tui.DrawText(text, x, y, Disabled ? Theme.InactiveFg : Theme.MainFg);

    public static void Label(int x, int y, string text, Color color) => Tui.DrawText(text, x, y, color);

    /// <summary>
    /// Rounded outline with the title notched into the top edge: ╭─┐¹Title┌────╮.
    /// number (1-9) adds a superscript section number like btop's "¹cpu". The interior is cleared to the theme background.
    /// </summary>
    public static void Panel(Rect r, string title, int number = 0)
    {
        Tui.DrawRectangle(r.X, r.Y, r.Width, r.Height, Theme.MainBg);
        Tui.DrawRectangleLines(r.X, r.Y, r.Width, r.Height, Theme.Box, rounded: true);
        if (title.Length == 0 || r.Width < 8) return;

        int maxTitle = r.Width - 6 - (number > 0 ? 1 : 0);
        title = Fit(title, maxTitle);

        int x = r.X + 2;
        Tui.DrawText("┐", x++, r.Y, Theme.Box);
        if (number > 0 && number <= 9) Tui.DrawText(Superscript[number], x++, r.Y, Theme.HiFg);
        Tui.DrawText(title, x, r.Y, Theme.Title);
        Tui.DrawText("┌", x + title.Length, r.Y, Theme.Box);
    }

    /// <summary>
    /// Push button. Height 3+ draws a rounded box; height 1-2 draws "[ text ]". Returns true on the frame it is
    /// clicked (or activated with Enter/Space while focused).
    /// </summary>
    public static bool Button(Rect r, string text)
    {
        var s = Update(Id(1, r.X, r.Y), r);
        var line = LineColor(s);
        var fg = TextColor(s);

        Tui.DrawRectangle(r.X, r.Y, r.Width, r.Height, s.Held ? Theme.SelectedBg : Theme.MainBg);

        int ty = r.Y + r.Height / 2;
        if (r.Height >= 3 && r.Width >= 3)
        {
            Tui.DrawRectangleLines(r.X, r.Y, r.Width, r.Height, line, rounded: true);
            DrawCentered(text, r.X + 1, ty, r.Width - 2, fg);
        }
        else if (r.Width >= 3)
        {
            DrawCentered(text, r.X + 1, ty, r.Width - 2, fg);
            Tui.DrawText("[", r.X, ty, line);
            Tui.DrawText("]", r.X + r.Width - 1, ty, line);
        }
        return s.Clicked || s.Activated;
    }

    private static void DrawCentered(string text, int x, int y, int width, Color fg)
    {
        if (width < 1) return;
        text = Fit(text, width);
        Tui.DrawText(text, x + (width - text.Length) / 2, y, fg);
    }

    /// <summary>Checkbox "[x] label". Returns true when toggled this frame.</summary>
    public static bool Checkbox(int x, int y, string text, ref bool value)
    {
        var r = new Rect(x, y, 4 + text.Length, 1);
        var s = Update(Id(2, x, y), r);
        bool changed = s.Clicked || s.Activated;
        if (changed) value = !value;

        var line = LineColor(s);
        Tui.DrawText("[", x, y, line);
        Tui.DrawText(value ? "x" : " ", x + 1, y, Disabled ? Theme.InactiveFg : Theme.Ok);
        Tui.DrawText("]", x + 2, y, line);
        Tui.DrawText(text, x + 4, y, TextColor(s));
        return changed;
    }

    /// <summary>On/off switch (●──── / ━━━━●) followed by a label. Returns true when toggled this frame.</summary>
    public static bool Toggle(int x, int y, string text, ref bool value)
    {
        const int trackWidth = 5;
        var r = new Rect(x, y, trackWidth + 1 + text.Length, 1);
        var s = Update(Id(3, x, y), r);
        bool changed = s.Clicked || s.Activated;
        if (changed) value = !value;

        Color track = Disabled ? Theme.InactiveFg : value ? Theme.Ok : Theme.Box;
        Color knob = Disabled ? Theme.InactiveFg : s.Lit ? Theme.HiFg : value ? Theme.Ok : Theme.MainFg;
        string bar = value ? "━━━━" : "────";
        if (value)
        {
            Tui.DrawText(bar, x, y, track);
            Tui.DrawText("●", x + trackWidth - 1, y, knob);
        }
        else
        {
            Tui.DrawText("●", x, y, knob);
            Tui.DrawText(bar, x + 1, y, track);
        }
        Tui.DrawText(text, x + trackWidth + 1, y, TextColor(s));
        return changed;
    }

    /// <summary>
    /// Horizontal slider ━━━●───. The filled part is coloured along <see cref="Theme.Heat"/>.
    /// Drag with the mouse or use Left/Right/Home/End while focused. Returns true when the value changed.
    /// </summary>
    public static bool Slider(int x, int y, int width, ref float value, float min, float max)
    {
        if (width < 3 || max <= min) return false;
        var r = new Rect(x, y, width, 1);
        var s = Update(Id(4, x, y), r);
        float before = value;

        if (s.Held)
        {
            float t = (Tui.GetMouseX() - x) / (float)(width - 1);
            value = min + Math.Clamp(t, 0f, 1f) * (max - min);
        }
        if (s.Focused)
        {
            float step = (max - min) / 20f;
            if (Tui.IsKeyPressed(ConsoleKey.LeftArrow)) value -= step;
            if (Tui.IsKeyPressed(ConsoleKey.RightArrow)) value += step;
            if (Tui.IsKeyPressed(ConsoleKey.Home)) value = min;
            if (Tui.IsKeyPressed(ConsoleKey.End)) value = max;
        }
        value = Math.Clamp(value, min, max);

        int knob = (int)MathF.Round((value - min) / (max - min) * (width - 1));
        for (int i = 0; i < width; i++)
        {
            if (i < knob)
                Tui.DrawText("━", x + i, y, Disabled ? Theme.InactiveFg : Theme.Heat.At(i / (float)(width - 1)));
            else if (i > knob)
                Tui.DrawText("─", x + i, y, Theme.Box);
        }
        Tui.DrawText("●", x + knob, y, Disabled ? Theme.InactiveFg : (s.Lit || s.Held) ? Theme.HiFg : Theme.MainFg);

        return value != before;
    }

    // ---- data display: meter and graph ----------------------------------

    /// <summary>
    /// Row of ■ beads filled left to right, coloured along a gradient (default <see cref="Theme.Heat"/>);
    /// the unfilled beads use <see cref="Theme.MeterBg"/>. percent is 0..100.
    /// </summary>
    public static void Meter(int x, int y, int width, float percent, Gradient? gradient = null)
    {
        if (width < 1) return;
        var g = gradient ?? Theme.Heat;
        percent = Math.Clamp(percent, 0f, 100f);
        for (int i = 1; i <= width; i++)
        {
            int at = (int)MathF.Round(i * 100f / width);
            Color c = percent >= at ? (Disabled ? Theme.InactiveFg : g.At(at)) : Theme.MeterBg;
            Tui.DrawChar('■', x + i - 1, y, c);
        }
    }

    // Braille cell: left dot column fills bottom-up as 0x40,0x04,0x02,0x01; right column as 0x80,0x20,0x10,0x08.
    // Index = leftLevel * 5 + rightLevel (each 0..4 dots), so one cell shows two samples side by side.
    private static readonly char[] BrailleUp = BuildBraille();

    private static char[] BuildBraille()
    {
        int[] left = { 0x40, 0x04, 0x02, 0x01 };
        int[] right = { 0x80, 0x20, 0x10, 0x08 };
        var table = new char[25];
        for (int l = 0; l <= 4; l++)
            for (int r = 0; r <= 4; r++)
            {
                int mask = 0;
                for (int k = 0; k < l; k++) mask |= left[k];
                for (int k = 0; k < r; k++) mask |= right[k];
                table[l * 5 + r] = mask == 0 ? ' ' : (char)(0x2800 + mask);
            }
        return table;
    }

    /// <summary>
    /// Braille dot graph of the most recent width*2 samples (two per cell, four dot-levels per row).
    /// Rows are coloured along the gradient (hot at the top); a one-row graph is coloured by value.
    /// baseline keeps a dot on the bottom row so a flat zero still draws a line.
    /// </summary>
    public static void Graph(int x, int y, int width, int height, IReadOnlyList<float> data,
                             float min = 0f, float max = 100f, Gradient? gradient = null, bool baseline = true)
    {
        if (width < 1 || height < 1 || max <= min) return;
        var g = gradient ?? Theme.Heat;
        Tui.DrawRectangle(x, y, width, height, Theme.MainBg);

        int first = data.Count - width * 2; // data index shown at the left edge (negative = not enough data yet)
        for (int c = 0; c < width; c++)
        {
            int il = first + 2 * c, ir = il + 1;
            bool vl = il >= 0 && il < data.Count, vr = ir >= 0 && ir < data.Count;
            float pl = vl ? Percent(data[il], min, max) : 0f;
            float pr = vr ? Percent(data[ir], min, max) : 0f;

            for (int r = 0; r < height; r++)
            {
                float high = 100f * (height - r) / height;
                float low = 100f * (height - r - 1) / height;
                bool bottom = baseline && r == height - 1;
                char ch = BrailleUp[Level(pl, vl, high, low, bottom) * 5 + Level(pr, vr, high, low, bottom)];
                if (ch == ' ') continue;

                Color color = Disabled ? Theme.InactiveFg
                            : height > 1 ? g.At(100 - r * 100 / height)
                            : g.At((int)MathF.Max(pl, pr));
                Tui.DrawChar(ch, x + c, y + r, color);
            }
        }
    }

    private static float Percent(float v, float min, float max) => Math.Clamp((v - min) / (max - min) * 100f, 0f, 100f);

    private static int Level(float pct, bool valid, float high, float low, bool bottom)
    {
        if (!valid) return 0;
        int floor = bottom ? 1 : 0;
        if (pct >= high) return 4;
        if (pct <= low) return floor;
        return Math.Clamp((int)MathF.Round((pct - low) * 4f / (high - low)), floor, 4);
    }

    // ---- text box -------------------------------------------------------

    /// <summary>
    /// Single-line text input. Height 3+ draws a rounded box, otherwise "[ text ]". Click to focus and place the cursor;
    /// type, Backspace, Delete, Left/Right, Home/End. Esc leaves the box. Returns true when the text changed this frame.
    /// </summary>
    public static bool TextBox(Rect r, ref string text, string placeholder = "", int maxLength = 256)
        => TextBoxCore(r, ref text, placeholder, maxLength, out _);

    /// <summary>As <see cref="TextBox(Rect, ref string, string, int)"/>, also reporting when Enter was pressed.</summary>
    public static bool TextBox(Rect r, ref string text, out bool submitted, string placeholder = "", int maxLength = 256)
        => TextBoxCore(r, ref text, placeholder, maxLength, out submitted);

    private static bool TextBoxCore(Rect r, ref string text, string placeholder, int maxLength, out bool submitted)
    {
        submitted = false;
        text ??= "";
        int iw = r.Width - 2;
        if (iw < 2) return false;

        int id = Id(6, r.X, r.Y);
        var s = Update(id, r);
        TextStates.TryGetValue(id, out var st);
        int cursor = Math.Clamp(st.Cursor, 0, text.Length);
        int scroll = st.Scroll;
        bool changed = false;

        int innerX = r.X + 1;
        int innerY = r.Y + r.Height / 2;

        if (s.Focused)
        {
            _textFocusId = id;

            if (Tui.IsMouseButtonPressed(MouseButton.Left) && s.Hover)
                cursor = Math.Clamp(scroll + Tui.GetMouseX() - innerX, 0, text.Length);

            char c;
            while ((c = Tui.GetCharPressed()) != '\0')
            {
                if (c < ' ' || c == '\x7f' || text.Length >= maxLength) continue;
                text = text.Insert(cursor, c.ToString());
                cursor++;
                changed = true;
            }
            if (Tui.IsKeyPressed(ConsoleKey.Backspace) && cursor > 0) { text = text.Remove(cursor - 1, 1); cursor--; changed = true; }
            if (Tui.IsKeyPressed(ConsoleKey.Delete) && cursor < text.Length) { text = text.Remove(cursor, 1); changed = true; }
            if (Tui.IsKeyPressed(ConsoleKey.LeftArrow) && cursor > 0) cursor--;
            if (Tui.IsKeyPressed(ConsoleKey.RightArrow) && cursor < text.Length) cursor++;
            if (Tui.IsKeyPressed(ConsoleKey.Home)) cursor = 0;
            if (Tui.IsKeyPressed(ConsoleKey.End)) cursor = text.Length;
            if (Tui.IsKeyPressed(ConsoleKey.Enter)) submitted = true;
            if (Tui.IsKeyPressed(ConsoleKey.Escape)) _focus = 0;
        }

        // keep the cursor inside the visible window
        if (cursor < scroll) scroll = cursor;
        if (cursor > scroll + iw - 1) scroll = cursor - iw + 1;
        scroll = Math.Clamp(scroll, 0, text.Length);
        TextStates[id] = (cursor, scroll);

        // draw
        var line = LineColor(s);
        Tui.DrawRectangle(r.X, r.Y, r.Width, r.Height, Theme.MainBg);
        if (r.Height >= 3) Tui.DrawRectangleLines(r.X, r.Y, r.Width, r.Height, line, rounded: true);
        else
        {
            Tui.DrawText("[", r.X, innerY, line);
            Tui.DrawText("]", r.X + r.Width - 1, innerY, line);
        }

        if (text.Length == 0 && !s.Focused && placeholder.Length > 0)
            Tui.DrawText(Fit(placeholder, iw), innerX, innerY, Theme.InactiveFg);
        else
            Tui.DrawText(text.Substring(scroll, Math.Min(iw, text.Length - scroll)), innerX, innerY,
                         Disabled ? Theme.InactiveFg : Theme.MainFg);

        if (s.Focused)
        {
            char under = cursor < text.Length ? text[cursor] : ' ';
            Tui.DrawText(under.ToString(), innerX + cursor - scroll, innerY, Color.Black, Theme.HiFg);
        }
        return changed;
    }

    // ---- radio group ----------------------------------------------------

    /// <summary>
    /// Vertical group "(●) label". Click, Enter/Space, or Up/Down while focused to select.
    /// Returns true when the selection changed.
    /// </summary>
    public static bool RadioGroup(int x, int y, IReadOnlyList<string> options, ref int selected)
    {
        int n = options.Count;
        if (n == 0) return false;
        bool changed = false;
        var states = new State[n];
        bool moved = false; // an arrow key already moved focus this frame

        for (int i = 0; i < n; i++)
        {
            var r = new Rect(x, y + i, 4 + options[i].Length, 1);
            int id = Id(5, x, y + i);
            var s = Update(id, r);
            states[i] = s;

            if (s.Clicked || s.Activated)
            {
                if (selected != i) { selected = i; changed = true; }
            }
            if (s.Focused && !moved)
            {
                if (Tui.IsKeyPressed(ConsoleKey.UpArrow) && i > 0)
                {
                    selected = i - 1; changed = true; moved = true; _focus = Id(5, x, y + i - 1);
                }
                else if (Tui.IsKeyPressed(ConsoleKey.DownArrow) && i < n - 1)
                {
                    selected = i + 1; changed = true; moved = true; _focus = Id(5, x, y + i + 1);
                }
            }
        }

        for (int i = 0; i < n; i++)
        {
            var s = states[i];
            if (_focus == Id(5, x, y + i) && !Disabled) s.Focused = true; // focus may have just moved
            var line = LineColor(s);
            Tui.DrawText("(", x, y + i, line);
            Tui.DrawText(selected == i ? "●" : " ", x + 1, y + i, Disabled ? Theme.InactiveFg : Theme.Ok);
            Tui.DrawText(")", x + 2, y + i, line);
            Tui.DrawText(options[i], x + 4, y + i, TextColor(s));
        }
        return changed;
    }

    // ---- dropdown -------------------------------------------------------

    /// <summary>
    /// Drop-down list "[ item   ▾ ]". Click or Enter/Space to open; in the list use the mouse, wheel, Up/Down and Enter.
    /// Esc or a click outside closes it. While closed and focused, Up/Down step through the items.
    /// Returns true when the selection changed.
    /// </summary>
    public static bool Dropdown(int x, int y, int width, IReadOnlyList<string> items, ref int selected, int maxVisible = 8)
    {
        int n = items.Count;
        if (n == 0 || width < 8) return false;
        selected = Math.Clamp(selected, 0, n - 1);

        var r = new Rect(x, y, width, 1);
        int id = Id(7, x, y);
        var s = Update(id, r);
        bool changed = false;
        bool open = _openDd == id;
        if (open) _ddSeen = true;

        int rows = Math.Max(1, Math.Min(n, maxVisible));
        int popH = rows + 2;
        int popY = y + 1 + popH <= Tui.GetScreenHeight() ? y + 1 : Math.Max(0, y - popH);
        var popup = new Rect(x, popY, width, popH);

        if (open && _focus != id) { _openDd = 0; open = false; } // focus moved elsewhere

        bool consumedEnter = false;
        if (open)
        {
            _popup = popup;
            int mx = Tui.GetMouseX(), my = Tui.GetMouseY();
            bool inList = popup.Contains(mx, my) && my > popup.Y && my < popup.Y + popH - 1;
            bool keyMoved = false;

            // hover follows the mouse only when it actually moves, so keyboard navigation isn't overridden
            if ((mx != _ddLastMx || my != _ddLastMy) && inList) _ddHighlight = _ddScroll + (my - popup.Y - 1);
            _ddLastMx = mx; _ddLastMy = my;

            int wheel = Tui.GetMouseWheelMove();
            if (wheel != 0 && popup.Contains(mx, my)) _ddScroll -= wheel;

            if (Tui.IsKeyPressed(ConsoleKey.UpArrow)) { _ddHighlight--; keyMoved = true; }
            if (Tui.IsKeyPressed(ConsoleKey.DownArrow)) { _ddHighlight++; keyMoved = true; }
            if (Tui.IsKeyPressed(ConsoleKey.Home)) { _ddHighlight = 0; keyMoved = true; }
            if (Tui.IsKeyPressed(ConsoleKey.End)) { _ddHighlight = n - 1; keyMoved = true; }
            _ddHighlight = Math.Clamp(_ddHighlight, 0, n - 1);
            if (keyMoved)
            {
                if (_ddHighlight < _ddScroll) _ddScroll = _ddHighlight;
                if (_ddHighlight >= _ddScroll + rows) _ddScroll = _ddHighlight - rows + 1;
            }
            _ddScroll = Math.Clamp(_ddScroll, 0, Math.Max(0, n - rows));

            if (Tui.IsMouseButtonPressed(MouseButton.Left))
            {
                if (popup.Contains(mx, my))
                {
                    _claimed = true;
                    if (inList)
                    {
                        int idx = _ddScroll + (my - popup.Y - 1);
                        if (idx != selected) { selected = idx; changed = true; }
                        _openDd = 0; open = false;
                    }
                }
                else if (!r.Contains(mx, my)) { _openDd = 0; open = false; }
            }

            if (open && Tui.IsKeyPressed(ConsoleKey.Enter))
            {
                if (_ddHighlight != selected) { selected = _ddHighlight; changed = true; }
                _openDd = 0; open = false; consumedEnter = true;
            }
            if (open && Tui.IsKeyPressed(ConsoleKey.Escape)) { _openDd = 0; open = false; }
        }

        if (!consumedEnter && (s.Clicked || s.Activated))
        {
            if (open) { _openDd = 0; open = false; }
            else
            {
                _openDd = id; open = true;
                _ddHighlight = selected;
                _ddScroll = Math.Clamp(selected - rows / 2, 0, Math.Max(0, n - rows));
                _ddLastMx = Tui.GetMouseX(); _ddLastMy = Tui.GetMouseY();
                _popup = popup;
                _ddSeen = true;
            }
        }
        else if (!open && s.Focused && !consumedEnter)
        {
            if (Tui.IsKeyPressed(ConsoleKey.UpArrow) && selected > 0) { selected--; changed = true; }
            if (Tui.IsKeyPressed(ConsoleKey.DownArrow) && selected < n - 1) { selected++; changed = true; }
        }

        // closed field: [ item        ▾ ]
        var lit = s with { Hover = s.Hover || open };
        var line = LineColor(lit);
        Tui.DrawRectangle(r.X, r.Y, r.Width, 1, Theme.MainBg);
        Tui.DrawText("[", x, y, line);
        Tui.DrawText("]", x + width - 1, y, line);
        Tui.DrawText(Fit(items[selected], width - 6), x + 2, y, TextColor(lit));
        Tui.DrawText("▾", x + width - 3, y, line);

        if (open)
        {
            // draw the list after everything else so it overlaps widgets below
            int hi = _ddHighlight, scroll = _ddScroll, sel = selected;
            bool dis = Disabled;
            Overlays.Add(() => DrawDropdownList(popup, items, rows, scroll, hi, sel, dis));
        }
        return changed;
    }

    private static void DrawDropdownList(Rect popup, IReadOnlyList<string> items, int rows, int scroll, int highlight, int selected, bool disabled)
    {
        Tui.DrawRectangle(popup.X, popup.Y, popup.Width, popup.Height, Theme.MainBg);
        Tui.DrawRectangleLines(popup.X, popup.Y, popup.Width, popup.Height, disabled ? Theme.InactiveFg : Theme.HiFg, rounded: true);
        for (int i = 0; i < rows; i++)
        {
            int idx = scroll + i;
            if (idx >= items.Count) break;
            int ry = popup.Y + 1 + i;
            bool hot = idx == highlight;
            if (hot) Tui.DrawRectangle(popup.X + 1, ry, popup.Width - 2, 1, Theme.SelectedBg);
            if (idx == selected) Tui.DrawChar('›', popup.X + 1, ry, Theme.Ok);
            Tui.DrawText(Fit(items[idx], popup.Width - 4), popup.X + 2, ry, hot ? Theme.HiFg : Theme.MainFg);
        }
    }
}
