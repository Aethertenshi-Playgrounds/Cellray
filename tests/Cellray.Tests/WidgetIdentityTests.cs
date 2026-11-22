using Cellray;

namespace Cellray.Tests;

/// <summary>
/// Widget identity: unscoped widgets are identified by kind + position; widgets inside Gui.PushId/Scope are identified
/// by scope + kind + order, so focus and text-cursor state follow an item wherever it is drawn.
/// </summary>
public class WidgetIdentityTests
{
    public WidgetIdentityTests() => Ui.Init();

    // ---- ids -------------------------------------------------------------

    [Fact]
    public void Unscoped_WidgetsAreIdentifiedByKindAndPosition()
    {
        int first = 0, again = 0, moved = 0, otherKind = 0;
        bool on = false;
        Ui.Frame(() => { Gui.Checkbox(2, 2, "x", ref on); first = Gui.LastId; });
        Ui.Frame(() =>
        {
            Gui.Checkbox(2, 2, "x", ref on); again = Gui.LastId;
            Gui.Checkbox(2, 3, "x", ref on); moved = Gui.LastId;
            Gui.Toggle(2, 2, "x", ref on); otherKind = Gui.LastId;
        });

        Assert.Equal(first, again);
        Assert.NotEqual(first, moved);
        Assert.NotEqual(first, otherKind);
    }

    [Fact]
    public void Scoped_IdDoesNotDependOnPosition()
    {
        int here = 0, there = 0;
        bool on = false;
        Ui.Frame(() => { using (Gui.Scope(42)) { Gui.Checkbox(2, 2, "x", ref on); here = Gui.LastId; } });
        Ui.Frame(() => { using (Gui.Scope(42)) { Gui.Checkbox(30, 15, "x", ref on); there = Gui.LastId; } });

        Assert.Equal(here, there);
    }

    [Fact]
    public void Scoped_DifferentKeysGiveDifferentIdsAtTheSamePosition()
    {
        int a = 0, b = 0, s = 0, s2 = 0;
        bool on = false;
        Ui.Frame(() =>
        {
            using (Gui.Scope(1)) { Gui.Checkbox(2, 2, "x", ref on); a = Gui.LastId; }
            using (Gui.Scope(2)) { Gui.Checkbox(2, 2, "x", ref on); b = Gui.LastId; }
            using (Gui.Scope("server-a")) { Gui.Checkbox(2, 2, "x", ref on); s = Gui.LastId; }
        });
        Ui.Frame(() => { using (Gui.Scope("server-a")) { Gui.Checkbox(9, 9, "x", ref on); s2 = Gui.LastId; } });

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, s);
        Assert.Equal(s, s2); // string keys are stable between frames
    }

    [Fact]
    public void Scoped_ScopeIdsAreNotTheSameAsPositionalIds()
    {
        int positional = 0, scoped = 0;
        bool on = false;
        Ui.Frame(() => { Gui.Checkbox(0, 0, "x", ref on); positional = Gui.LastId; });
        Ui.Frame(() => { using (Gui.Scope(0)) { Gui.Checkbox(0, 0, "x", ref on); scoped = Gui.LastId; } });

        Assert.NotEqual(positional, scoped);
    }

    [Fact]
    public void Scoped_WidgetsOfTheSameKindInOneScopeGetDistinctStableIds()
    {
        var first = new List<int>();
        var second = new List<int>();
        bool on = false;
        void Draw(List<int> into)
        {
            using (Gui.Scope(7))
            {
                Gui.Checkbox(0, 0, "a", ref on); into.Add(Gui.LastId);
                Gui.Checkbox(0, 0, "b", ref on); into.Add(Gui.LastId);
            }
        }
        Ui.Frame(() => Draw(first));
        Ui.Frame(() => Draw(second));

        Assert.NotEqual(first[0], first[1]);
        Assert.Equal(first, second);
    }

    [Fact]
    public void NestedScopes_CombineAllKeys()
    {
        int ab = 0, ba = 0, ab2 = 0, onlyB = 0;
        bool on = false;
        Ui.Frame(() =>
        {
            using (Gui.Scope(1)) using (Gui.Scope(2)) { Gui.Checkbox(0, 0, "x", ref on); ab = Gui.LastId; }
            using (Gui.Scope(2)) using (Gui.Scope(1)) { Gui.Checkbox(0, 0, "x", ref on); ba = Gui.LastId; }
            using (Gui.Scope(3)) { Gui.Checkbox(0, 0, "x", ref on); onlyB = Gui.LastId; }
        });
        Ui.Frame(() => { using (Gui.Scope(1)) using (Gui.Scope(2)) { Gui.Checkbox(0, 0, "x", ref on); ab2 = Gui.LastId; } });

        Assert.Equal(ab, ab2);
        Assert.NotEqual(ab, ba);
        Assert.NotEqual(ab, onlyB);
    }

    [Fact]
    public void ThousandsOfScopes_GiveDistinctIds()
    {
        var seen = new HashSet<int>();
        bool on = false;
        Ui.Frame(() =>
        {
            for (int i = 0; i < 2000; i++)
                using (Gui.Scope(i)) { Gui.Checkbox(0, 0, "x", ref on); seen.Add(Gui.LastId); }
        });

        Assert.Equal(2000, seen.Count);
    }

    // ---- focus and cursor follow the item --------------------------------

    [Fact]
    public void Focus_SurvivesTheWidgetMoving()
    {
        string name = "";
        void Draw(int y) { using (Gui.Scope("name")) Gui.TextBox(new Rect(2, y, 20, 3), ref name); }

        Ui.Frame(() => Draw(2));
        int id = Gui.LastId;
        Ui.Frame(() => Draw(2), Ui.Click(3, 3));  // click inside the box focuses it
        Assert.Equal(id, Gui.FocusedId);

        Ui.Frame(() => Draw(2), "hi");
        Assert.Equal("hi", name);

        Ui.Frame(() => Draw(12), "!");            // the list scrolled: same item, new position
        Assert.Equal(id, Gui.FocusedId);
        Assert.Equal("hi!", name);                // it still receives typing, with its cursor intact
    }

    [Fact]
    public void State_FollowsAReorderedItem()
    {
        string a = "", b = "";
        int idA = 0, idB = 0;
        void Draw(bool swapped)
        {
            int ya = swapped ? 8 : 2, yb = swapped ? 2 : 8;
            using (Gui.Scope(1)) { Gui.TextBox(new Rect(2, ya, 20, 3), ref a); idA = Gui.LastId; }
            using (Gui.Scope(2)) { Gui.TextBox(new Rect(2, yb, 20, 3), ref b); idB = Gui.LastId; }
        }

        Ui.Frame(() => Draw(false));
        int idA0 = idA, idB0 = idB;
        Ui.Frame(() => Draw(false), Ui.Click(3, 3));   // focus A (row 3 is its text row)
        Ui.Frame(() => Draw(false), "abc");
        Ui.Frame(() => Draw(false), Ui.Click(3, 9));   // focus B
        Ui.Frame(() => Draw(false), "z");
        Ui.Frame(() => Draw(false), Ui.Click(6, 3));   // focus A again, cursor placed at the end of "abc"
        Assert.Equal(idA0, Gui.FocusedId);
        Assert.Equal(3, Gui.TextCursor(idA0));
        Assert.Equal(1, Gui.TextCursor(idB0));

        Ui.Frame(() => Draw(true));                    // the two items swap places on screen

        Assert.Equal(idA0, idA);
        Assert.Equal(idB0, idB);
        Assert.Equal(idA0, Gui.FocusedId);
        Assert.Equal(3, Gui.TextCursor(idA0));         // each cursor moved with its item
        Assert.Equal(1, Gui.TextCursor(idB0));

        Ui.Frame(() => Draw(true), "d");
        Assert.Equal("abcd", a);                       // typing goes to the focused item at its new position
        Assert.Equal("z", b);
    }

    [Fact]
    public void ReplacementAtTheSamePosition_DoesNotInheritFocusOrCursor()
    {
        string a = "", b = "";
        void DrawA() { using (Gui.Scope("A")) Gui.TextBox(new Rect(2, 2, 20, 3), ref a); }

        Ui.Frame(DrawA);
        int idA = Gui.LastId;
        Ui.Frame(DrawA, Ui.Click(3, 3));
        Ui.Frame(DrawA, "abc");
        Assert.Equal(3, Gui.TextCursor(idA));

        // item A leaves the list and a different item B is drawn on the very same spot
        int idB = 0;
        Ui.Frame(() => { using (Gui.Scope("B")) { Gui.TextBox(new Rect(2, 2, 20, 3), ref b); idB = Gui.LastId; } }, "x");

        Assert.NotEqual(idA, idB);
        Assert.Equal(0, Gui.TextCursor(idB));          // fresh cursor, not A's
        Assert.Equal("", b);                           // the keystroke did not land in B
        Assert.Equal(idA, Gui.FocusedId);              // focus still belongs to A (not drawn right now)
        Assert.Equal("abc", a);
    }

    [Fact]
    public void Unscoped_ReplacementAtTheSamePositionInheritsState()
    {
        // Documents the trade-off of positional ids: fine for fixed layouts, wrong for lists (use PushId there).
        string a = "", b = "";
        Ui.Frame(() => Gui.TextBox(new Rect(2, 2, 20, 3), ref a), Ui.Click(3, 3));
        Ui.Frame(() => Gui.TextBox(new Rect(2, 2, 20, 3), ref a), "abc");

        Ui.Frame(() => Gui.TextBox(new Rect(2, 2, 20, 3), ref b), "x");

        Assert.Equal("x", b);
    }

    // ---- widgets that disappear -------------------------------------------

    [Fact]
    public void Focus_IsRemembered_WhileTheWidgetIsAbsent_ButTabStartsFromTheTop()
    {
        string name = "";
        bool on = false;
        int idBox = 0, idCheck = 0;
        void DrawBoth()
        {
            using (Gui.Scope("box")) { Gui.TextBox(new Rect(2, 2, 20, 3), ref name); idBox = Gui.LastId; }
            Gui.Checkbox(2, 8, "c", ref on); idCheck = Gui.LastId;
        }
        void DrawOnlyCheckbox() { Gui.Checkbox(2, 8, "c", ref on); idCheck = Gui.LastId; }

        Ui.Frame(DrawBoth);
        Ui.Frame(DrawBoth, Ui.Click(3, 3));
        Assert.Equal(idBox, Gui.FocusedId);
        Assert.True(Gui.CapturesEscape);               // a focused text box keeps Esc for itself

        Ui.Frame(DrawOnlyCheckbox);                    // the text box scrolled out of view
        Assert.Equal(idBox, Gui.FocusedId);            // focus is remembered...
        Assert.False(Gui.CapturesEscape);              // ...but it can't capture anything while it isn't drawn

        Ui.Frame(DrawBoth);                            // scrolled back into view
        Assert.True(Gui.CapturesEscape);               // focus is still there

        Ui.Frame(DrawOnlyCheckbox);
        Ui.Frame(DrawOnlyCheckbox, Ui.Tab);            // Tab from a widget that isn't drawn starts at the first one
        Assert.Equal(idCheck, Gui.FocusedId);
    }

    [Fact]
    public void TextState_IsKeptForTheTimeout_ThenForgotten()
    {
        Gui.StateTimeoutFrames = 20;
        string name = "abc";
        int id = 0;
        void DrawBox() { using (Gui.Scope("n")) { Gui.TextBox(new Rect(2, 2, 20, 3), ref name); id = Gui.LastId; } }

        Ui.Frame(DrawBox, Ui.Click(5, 3));             // cursor placed at column 2
        Assert.Equal(2, Gui.TextCursor(id));

        for (int i = 0; i < 10; i++) Ui.Frame(() => { });   // away for less than the timeout
        Ui.Frame(DrawBox);
        Assert.Equal(2, Gui.TextCursor(id));           // remembered

        for (int i = 0; i < 80; i++) Ui.Frame(() => { });   // away well past the timeout
        Assert.False(Gui.HasTextState(id));            // forgotten, so the table can't grow forever
        Ui.Frame(DrawBox);
        Assert.Equal(0, Gui.TextCursor(id));           // starts fresh
    }

    // ---- misuse ----------------------------------------------------------

    [Fact]
    public void DuplicateScopeKeys_AreCounted_ButWidgetsStayDistinct()
    {
        string a = "", b = "";
        int idA = 0, idB = 0;
        Ui.Frame(() =>
        {
            using (Gui.Scope(7)) { Gui.TextBox(new Rect(2, 2, 20, 3), ref a); idA = Gui.LastId; }
            using (Gui.Scope(7)) { Gui.TextBox(new Rect(2, 8, 20, 3), ref b); idB = Gui.LastId; }
        });

        Assert.Equal(1, Gui.DuplicateIdCount);
        Assert.NotEqual(idA, idB);                     // distinct by order, so nothing is merged or crashes

        Ui.Frame(() =>
        {
            using (Gui.Scope(1)) Gui.TextBox(new Rect(2, 2, 20, 3), ref a);
            using (Gui.Scope(2)) Gui.TextBox(new Rect(2, 8, 20, 3), ref b);
        });
        Assert.Equal(0, Gui.DuplicateIdCount);
    }

    [Fact]
    public void MissingPopId_IsClosedAtTheEndOfTheFrame()
    {
        bool on = false;
        int positional = 0, afterwards = 0;
        Ui.Frame(() => { Gui.Checkbox(2, 2, "x", ref on); positional = Gui.LastId; });

        Ui.Frame(() => { Gui.PushId(1); Gui.Checkbox(2, 2, "x", ref on); });   // forgot PopId
        Assert.Equal(1, Gui.UnbalancedScopeCount);

        Ui.Frame(() => { Gui.Checkbox(2, 2, "x", ref on); afterwards = Gui.LastId; });
        Assert.Equal(0, Gui.UnbalancedScopeCount);
        Assert.Equal(positional, afterwards);          // the leaked scope did not affect the next frame
    }

    [Fact]
    public void PopIdWithoutPushId_Throws()
    {
        Ui.Frame(() => Assert.Throws<InvalidOperationException>(() => Gui.PopId()));
    }

    // ---- other widgets and helpers ---------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RadioGroup_ArrowKeysMoveSelectionAndFocus_WithAndWithoutScopes(bool scoped)
    {
        string[] options = { "a", "b", "c" };
        int selected = -1;
        void Draw()
        {
            if (scoped) Gui.PushId("radio");
            Gui.RadioGroup(2, 2, options, ref selected);
            if (scoped) Gui.PopId();
        }

        Ui.Frame(Draw);
        Ui.Frame(Draw, Ui.Click(3, 2));                // first option
        Assert.Equal(0, selected);
        Ui.Frame(Draw, Ui.Down); Assert.Equal(1, selected);
        Ui.Frame(Draw, Ui.Down); Assert.Equal(2, selected);   // focus really moved: a stuck focus would give 1 again
        Ui.Frame(Draw, Ui.Up); Assert.Equal(1, selected);
    }

    [Fact]
    public void FocusNextWidget_FocusesTheNextWidgetDrawn_EvenInsideAScope()
    {
        string name = "";
        void Draw(bool focusIt)
        {
            if (focusIt) Gui.FocusNextWidget();
            using (Gui.Scope(5)) Gui.TextBox(new Rect(2, 2, 20, 3), ref name);
        }

        Ui.Frame(() => Draw(true));
        Assert.Equal(Gui.LastId, Gui.FocusedId);
        Ui.Frame(() => Draw(false), "ok");
        Assert.Equal("ok", name);
    }

    [Fact]
    public void FocusTextBox_StillWorksForUnscopedTextBoxes()
    {
        string name = "";
        var rect = new Rect(2, 2, 20, 3);
        Ui.Frame(() => { Gui.FocusTextBox(rect); Gui.TextBox(rect, ref name); });
        Assert.Equal(Gui.LastId, Gui.FocusedId);
    }
}
