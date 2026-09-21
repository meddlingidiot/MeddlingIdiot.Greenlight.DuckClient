namespace Greenlight.DuckClient.UnitTests;

/// <summary>
/// The scene, without a window. Everything here is arithmetic somebody would otherwise have to
/// check by dragging a duck around a desktop and squinting at it.
/// </summary>
public class DuckSceneTests
{
    private static DuckScene Scene(double size = 0.2, double x = 0.5, double y = 0.5) =>
        new(new DuckPlacement { AnchorX = x, AnchorY = y, Size = size }, randomSeed: 7)
        {
            ShowWhenOff = false,
        };

    private static DuckScene Sized(double size = 0.2, double x = 0.5, double y = 0.5)
    {
        var scene = Scene(size, x, y);
        scene.Resize(1600, 900);
        return scene;
    }

    private static void Run(DuckScene scene, double seconds, double step = 1.0 / 60)
    {
        for (var t = 0.0; t < seconds; t += step) scene.Advance(TimeSpan.FromSeconds(step));
    }

    // ── states and the changeover ─────────────────────────────────────────────

    [Fact]
    public void FirstStateArrivesWithoutAChangeover()
    {
        var scene = Sized();

        // Nothing on screen to put away, so the first snapshot after a cold start is simply
        // worn rather than waited for.
        scene.State = DuckState.Green;

        Assert.Equal(DuckState.Green, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void AChangeOfStateFadesTheOldOneOutFirst()
    {
        var scene = Sized();
        scene.State = DuckState.Green;
        Run(scene, 1);

        scene.State = DuckState.Red;

        // Still green on the very next frame: the new colour must not appear on top of the old.
        scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
        Assert.Equal(DuckState.Green, scene.Shown);
        Assert.True(scene.IsChangingOver);

        Run(scene, 2);
        Assert.Equal(DuckState.Red, scene.Shown);
        Assert.False(scene.IsChangingOver);
    }

    [Fact]
    public void NothingToShowFadesAllTheWayOut()
    {
        var scene = Sized();
        scene.State = DuckState.Green;
        Run(scene, 1);

        scene.State = DuckState.Off;
        Run(scene, 3);

        Assert.Equal(0, scene.Fade);
    }

    [Fact]
    public void GreyWhenOffKeepsTheDuckOnScreen()
    {
        var scene = Sized();
        scene.ShowWhenOff = true;

        Run(scene, 2);

        Assert.Equal(1, scene.Fade);
        Assert.Equal(DuckState.Off, scene.Shown);
    }

    // ── the quack ─────────────────────────────────────────────────────────────

    [Fact]
    public void ArrivingIsWorthAQuack()
    {
        var scene = Sized();
        scene.State = DuckState.Green;

        Assert.True(scene.IsQuacking);
        Assert.Equal("Quack!", scene.QuackText);
    }

    [Fact]
    public void EachStateHasItsOwnWord()
    {
        Assert.Equal("Quack!", DuckScene.WordFor(DuckState.Green));
        Assert.Equal("Quack?", DuckScene.WordFor(DuckState.Amber));
        Assert.Equal("QUACK!", DuckScene.WordFor(DuckState.Red));
    }

    [Fact]
    public void TheBalloonWaitsForTheNewColourRatherThanTalkingOverTheOldOne()
    {
        var scene = Sized();
        scene.State = DuckState.Green;
        Run(scene, 3);                      // the arrival quack has been and gone

        Assert.False(scene.IsQuacking);

        scene.State = DuckState.Red;
        scene.Advance(TimeSpan.FromSeconds(1.0 / 60));

        // Mid-changeover: still wearing green, so there is nothing to say yet.
        Assert.False(scene.IsQuacking);

        Run(scene, 1);
        Assert.Equal("QUACK!", scene.QuackText);
    }

    [Fact]
    public void ABalloonGoesAwayOnItsOwn()
    {
        var scene = Sized();
        scene.State = DuckState.Green;

        Run(scene, 1);
        Assert.True(scene.IsQuacking);

        Run(scene, 2);
        Assert.False(scene.IsQuacking);
        Assert.Null(scene.QuackText);
    }

    [Fact]
    public void TheBalloonPopsInAndFadesOutRatherThanBlinking()
    {
        var scene = Sized();
        scene.State = DuckState.Green;

        // One frame in: on its way, and neither fully solid nor at full size.
        scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
        Assert.InRange(scene.BalloonOpacity, 0.01, 0.99);
        Assert.InRange(scene.BalloonScale, 0.5, 0.99);

        // Halfway through: sitting there at its full size, saying its piece.
        Run(scene, 1.0);
        Assert.Equal(1, scene.BalloonOpacity, 6);
        Assert.Equal(1, scene.BalloonScale, 6);

        // Near the end: on its way out, but still at full size — a balloon that shrank as it
        // faded would read as being sucked back in.
        Run(scene, 1.0);
        Assert.InRange(scene.BalloonOpacity, 0.01, 0.99);
        Assert.Equal(1, scene.BalloonScale, 6);
    }

    [Fact]
    public void GoingQuietIsTheOneChangeHeMeetsInSilence()
    {
        var scene = Sized();
        scene.ShowWhenOff = true;
        scene.State = DuckState.Green;
        Run(scene, 3);

        scene.State = DuckState.Off;
        Run(scene, 1.5);

        Assert.Equal(DuckState.Off, scene.Shown);
        Assert.False(scene.IsQuacking);
    }

    [Fact]
    public void ADuckToldNotToQuackDoesNot()
    {
        var scene = Sized();
        scene.Quacks = false;

        scene.State = DuckState.Red;
        Run(scene, 30);

        Assert.False(scene.IsQuacking);
    }

    [Fact]
    public void ABrokenPipelineGoesOnAsking()
    {
        var scene = Sized();
        scene.State = DuckState.Red;
        Run(scene, 3);

        Assert.False(scene.IsQuacking);

        // The nag. Long enough to be ignorable while you are fixing it, short enough that a red
        // duck nobody has looked at all afternoon is still asking.
        Run(scene, 13);
        Assert.True(scene.IsQuacking);
        Assert.Equal("QUACK!", scene.QuackText);
    }

    [Fact]
    public void NoOtherStateNags()
    {
        var scene = Sized();
        scene.State = DuckState.Green;
        Run(scene, 40);

        Assert.False(scene.IsQuacking);
    }

    [Fact]
    public void TheNagCanBeTurnedOffWithoutSilencingHimAltogether()
    {
        var scene = Sized();
        scene.NagsWhenRed = false;

        scene.State = DuckState.Red;
        Assert.True(scene.IsQuacking);      // he still says it once

        Run(scene, 40);
        Assert.False(scene.IsQuacking);     // and then leaves it alone
    }

    [Fact]
    public void EditModeShutsHimUp()
    {
        var scene = Sized();
        scene.State = DuckState.Red;
        Assert.True(scene.IsQuacking);

        // The balloon lands exactly where the tick and the cross go, so edit mode is silent.
        scene.SnapVisible();

        Assert.False(scene.IsQuacking);
        Assert.Equal(1, scene.Fade);
    }

    [Fact]
    public void TheTrayCanAskHimToSaySomething()
    {
        var scene = Sized();
        scene.State = DuckState.Amber;
        Run(scene, 3);
        Assert.False(scene.IsQuacking);

        scene.QuackNow();

        Assert.True(scene.IsQuacking);
        Assert.Equal("Quack?", scene.QuackText);
    }

    // ── where the balloon lands ───────────────────────────────────────────────

    [Fact]
    public void TheBalloonSitsAboveHimAndOnTheSideHeIsFacing()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.State = DuckState.Green;

        var layout = scene.Measure();

        Assert.True(layout.FacesLeft);
        Assert.True(layout.Balloon.Bottom <= layout.Box.Y, "the balloon is not above him");
        Assert.True(layout.Balloon.Right <= layout.Box.Centre.X, "the balloon is not on his left");
    }

    [Fact]
    public void HeTurnsRoundRatherThanTalkOffTheEdgeOfTheScreen()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.State = DuckState.Green;

        scene.MoveTo(new ScenePoint(0, 450));
        var layout = scene.Measure();

        Assert.False(layout.FacesLeft);
        Assert.True(layout.Balloon.X >= layout.Box.Centre.X, "the balloon stayed on the wrong side");
        Assert.True(layout.Balloon.X >= -0.001, "the balloon is off the left of the desktop");
    }

    [Fact]
    public void TheBalloonGoesUnderneathHimWhenThereIsNoRoomAbove()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.State = DuckState.Green;

        scene.MoveTo(new ScenePoint(800, 0));
        var layout = scene.Measure();

        Assert.True(layout.Balloon.Y >= layout.Box.Bottom, "the balloon stayed off the top of the screen");
    }

    [Fact]
    public void TheBalloonStaysOnTheDesktop()
    {
        var scene = Sized(size: 0.2);
        scene.State = DuckState.Red;

        foreach (var (x, y) in new[] { (0.0, 0.0), (1600.0, 0.0), (0.0, 900.0), (1600.0, 900.0) })
        {
            scene.MoveTo(new ScenePoint(x, y));
            var balloon = scene.Measure().Balloon;

            Assert.True(balloon.X >= -0.001, $"off the left at {balloon.X}");
            Assert.True(balloon.Y >= -0.001, $"off the top at {balloon.Y}");
            Assert.True(balloon.Right <= 1600.001, $"off the right at {balloon.Right}");
            Assert.True(balloon.Bottom <= 900.001, $"off the bottom at {balloon.Bottom}");
        }
    }

    [Fact]
    public void TheTailPointsAtTheBillWhicheverWayRoundHeIs()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.State = DuckState.Green;

        var facingLeft = scene.Measure();
        Assert.True(facingLeft.Beak.X < facingLeft.Centre.X);

        scene.MoveTo(new ScenePoint(0, 450));
        var facingRight = scene.Measure();
        Assert.True(facingRight.Beak.X > facingRight.Centre.X);
    }

    [Fact]
    public void ThereIsNoBalloonWhenHeIsNotSayingAnything()
    {
        var scene = Sized();
        scene.State = DuckState.Green;
        Run(scene, 3);

        // The balloon's time is up, so there is nothing to lay out — otherwise the canvas would
        // go on drawing a tail pointing at an empty rectangle.
        Assert.False(scene.IsQuacking);
        Assert.Equal(default, scene.Measure().Balloon);
    }

    [Fact]
    public void ALongerWordGetsAWiderBalloon()
    {
        var wordy = Sized();
        wordy.State = DuckState.Green;                  // "Quack!"

        var terse = Sized();
        terse.QuackNow();                               // "…", which is what an Off duck has

        Assert.True(
            wordy.Measure().Balloon.Width > terse.Measure().Balloon.Width,
            "the balloon is not sized to what it holds");
    }

    // ── horns and the fire in his eye ─────────────────────────────────────────

    [Fact]
    public void OnlyABrokenPipelineGrowsHorns()
    {
        var scene = Sized();
        scene.ShowWhenOff = true;

        foreach (var quiet in new[] { DuckState.Off, DuckState.Green, DuckState.Amber })
        {
            scene.State = quiet;
            Run(scene, 3);

            Assert.Equal(0, scene.Menace);
        }

        scene.State = DuckState.Red;
        Run(scene, 3);

        Assert.Equal(1, scene.Menace);
    }

    [Fact]
    public void TheHornsGrowRatherThanBeingSwitchedOn()
    {
        var scene = Sized();
        scene.State = DuckState.Red;

        Assert.Equal(0, scene.Menace);

        // Part way out. Horns that were simply there on the first red frame read as a second
        // duck having been swapped in.
        Run(scene, 0.25);
        Assert.InRange(scene.Menace, 0.05, 0.95);

        Run(scene, 1);
        Assert.Equal(1, scene.Menace);
    }

    [Fact]
    public void FixingItPutsTheHornsAway()
    {
        var scene = Sized();
        scene.State = DuckState.Red;
        Run(scene, 3);

        scene.State = DuckState.Green;
        Run(scene, 3);

        Assert.Equal(0, scene.Menace);
        Assert.Equal(DuckState.Green, scene.Shown);
    }

    [Fact]
    public void TheHornsComeOffBeforeTheRedDuckDoes()
    {
        var scene = Sized();
        scene.State = DuckState.Red;
        Run(scene, 3);

        scene.State = DuckState.Green;

        // Still wearing red, and still faded in, but already retracting: the changeover has to
        // be one movement, not a duck losing his horns half a second after he has gone.
        Run(scene, 0.2);

        Assert.Equal(DuckState.Red, scene.Shown);
        Assert.True(scene.Fade > 0, "he had already gone");
        Assert.InRange(scene.Menace, 0.0, 0.95);
    }

    [Fact]
    public void EditModeGetsTheHornsWithoutTheTransformation()
    {
        var scene = Sized();
        scene.State = DuckState.Red;

        // Straight to fully wound up. Somebody who opened edit mode to move him four pixels
        // should not have to watch him grow horns first.
        scene.SnapVisible();

        Assert.Equal(1, scene.Menace);
        Assert.Equal(1, scene.Fade);
    }

    [Fact]
    public void EditingAnythingElseGetsNoHorns()
    {
        var scene = Sized();
        scene.State = DuckState.Amber;
        scene.SnapVisible();

        Assert.Equal(0, scene.Menace);
    }

    // ── the build pulse ───────────────────────────────────────────────────────

    [Fact]
    public void NothingBreathesUnlessABuildIsRunning()
    {
        var scene = Sized();
        scene.State = DuckState.Green;
        Run(scene, 1);

        Assert.Equal(1, scene.Pulse);
        Assert.Equal(1, scene.Brightness, 6);
        Assert.Equal(0, scene.Bob);
    }

    [Fact]
    public void ABuildMakesTheHaloSwellAndSettle()
    {
        var scene = Sized();
        scene.State = DuckState.Green;
        scene.IsBuilding = true;

        var low = double.MaxValue;
        var high = double.MinValue;

        // Two full breaths, sampled every frame. The halo carries most of the pulse, so this is
        // the number that has to actually move.
        for (var i = 0; i < 240; i++)
        {
            scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
            low = Math.Min(low, scene.Halo);
            high = Math.Max(high, scene.Halo);
        }

        Assert.True(high - low > 0.3, $"the halo barely moved: {low:F2} to {high:F2}");
        Assert.InRange(scene.Brightness, 0.7, 1.0);
    }

    [Fact]
    public void ABuildMakesHimBobWithoutMovingTheBoxYouGrab()
    {
        var scene = Sized();
        scene.State = DuckState.Green;
        scene.IsBuilding = true;

        var box = scene.Measure().Box;
        var low = double.MaxValue;
        var high = double.MinValue;

        for (var i = 0; i < 240; i++)
        {
            scene.Advance(TimeSpan.FromSeconds(1.0 / 60));
            var layout = scene.Measure();

            low = Math.Min(low, layout.Centre.Y);
            high = Math.Max(high, layout.Centre.Y);

            // The thing you drag has to stand still while he floats inside it, or grabbing him
            // would be a moving target.
            Assert.Equal(box.Y, layout.Box.Y, 6);
        }

        Assert.True(high - low > 1, $"he barely moved: {low:F2} to {high:F2}");
    }

    // ── where he sits ─────────────────────────────────────────────────────────

    [Fact]
    public void TheBoxIsSquareAndCentredOnTheAnchor()
    {
        var scene = Sized(size: 0.2, x: 0.25, y: 0.75);
        var box = scene.Measure().Box;

        Assert.Equal(box.Width, box.Height, 6);
        Assert.Equal(180, box.Width, 6);          // 0.2 of 900
        Assert.Equal(400, box.Centre.X, 6);       // 0.25 of 1600
        Assert.Equal(675, box.Centre.Y, 6);       // 0.75 of 900
    }

    [Fact]
    public void DraggingKeepsTheWholeBoxOnTheDesktop()
    {
        var scene = Sized();

        scene.MoveTo(new ScenePoint(-500, -500));
        var box = scene.Measure().Box;

        Assert.True(box.X >= -0.001, $"left edge off screen at {box.X}");
        Assert.True(box.Y >= -0.001, $"top edge off screen at {box.Y}");

        scene.MoveTo(new ScenePoint(9000, 9000));
        box = scene.Measure().Box;

        Assert.True(box.Right <= 1600.001, $"right edge off screen at {box.Right}");
        Assert.True(box.Bottom <= 900.001, $"bottom edge off screen at {box.Bottom}");
    }

    [Fact]
    public void ResizingHoldsTheOppositeCornerStill()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        var before = scene.Measure().Box;

        scene.ResizeTo(DuckGrip.ResizeBottomRight, new ScenePoint(before.X + 300, before.Y + 300));
        var after = scene.Measure().Box;

        Assert.Equal(before.X, after.X, 3);
        Assert.Equal(before.Y, after.Y, 3);
        Assert.Equal(300, after.Width, 3);
        Assert.Equal(after.Width, after.Height, 6);
    }

    [Fact]
    public void ResizingTheOtherWayHoldsItsOwnCorner()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        var before = scene.Measure().Box;

        scene.ResizeTo(DuckGrip.ResizeTopLeft, new ScenePoint(before.Right - 250, before.Bottom - 250));
        var after = scene.Measure().Box;

        Assert.Equal(before.Right, after.Right, 3);
        Assert.Equal(before.Bottom, after.Bottom, 3);
        Assert.Equal(250, after.Width, 3);
    }

    [Fact]
    public void ABoxCannotBeDraggedDownToNothing()
    {
        var scene = Sized(size: 0.2);
        var box = scene.Measure().Box;

        scene.ResizeTo(DuckGrip.ResizeBottomRight, new ScenePoint(box.X + 2, box.Y + 2));

        Assert.Equal(DuckScene.MinimumSide, scene.Measure().Box.Width, 3);
    }

    [Fact]
    public void ABoxCannotBeDraggedBiggerThanTheDesktop()
    {
        var scene = Sized(size: 0.2);
        var box = scene.Measure().Box;

        scene.ResizeTo(DuckGrip.ResizeBottomRight, new ScenePoint(box.X + 5000, box.Y + 5000));

        Assert.Equal(scene.MaximumSide, scene.Measure().Box.Width, 3);
    }

    [Fact]
    public void ADuckWrittenOnABiggerScreenStillFitsOnThisOne()
    {
        // A placement written on a 4K monitor, opened on something much smaller. The size clamp
        // is what keeps this honest — without it the box would be wider than the desktop, and
        // MoveTo would be asked to clamp a position between a low above its own high.
        var scene = Scene(size: 4.0);
        scene.Resize(200, 120);

        scene.MoveTo(new ScenePoint(10, 10));
        var box = scene.Measure().Box;

        Assert.True(box.Width <= 120, $"the box is wider than the desktop at {box.Width}");
        Assert.True(box.X >= -0.001 && box.Right <= 200.001, $"off the side: {box.X} to {box.Right}");
        Assert.True(box.Y >= -0.001 && box.Bottom <= 120.001, $"off the top or bottom: {box.Y} to {box.Bottom}");
    }

    [Fact]
    public void AnOverlayWithNoRoomAtAllCentresRatherThanThrowing()
    {
        // Asked to lay out before the window has been given a size. The minimum side is bigger
        // than the whole overlay here, so there is no legal position — Math.Clamp would throw
        // on a low above its high, and this is the frame that would take the app down with it.
        var scene = Scene();
        scene.Resize(0, 0);

        scene.State = DuckState.Red;        // and a balloon with nowhere to go, for the same reason
        scene.MoveTo(new ScenePoint(10, 10));

        Assert.Equal(0.5, scene.Placement.AnchorX, 6);
        Assert.Equal(0.5, scene.Placement.AnchorY, 6);

        var layout = scene.Measure();
        Assert.True(double.IsFinite(layout.Balloon.X) && double.IsFinite(layout.Balloon.Y));
    }

    // ── what the mouse is over ────────────────────────────────────────────────

    [Fact]
    public void TheMiddleOfTheBoxIsTheThingYouDrag()
    {
        var scene = Sized();
        Assert.Equal(DuckGrip.Body, scene.HitTest(scene.Measure().Box.Centre));
    }

    [Fact]
    public void BareDesktopIsNotTheDuck()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        Assert.Equal(DuckGrip.None, scene.HitTest(new ScenePoint(10, 10)));
    }

    [Theory]
    [InlineData(DuckGrip.ResizeTopLeft)]
    [InlineData(DuckGrip.ResizeTopRight)]
    [InlineData(DuckGrip.ResizeBottomLeft)]
    [InlineData(DuckGrip.ResizeBottomRight)]
    public void EveryCornerAnswersItsOwnHandle(DuckGrip corner)
    {
        var scene = Sized();
        var layout = scene.Measure();

        Assert.Equal(corner, scene.HitTest(layout.Handle(corner).Centre));
    }

    [Fact]
    public void TheButtonsWinAgainstAnythingUnderneathThem()
    {
        var scene = Sized();
        var layout = scene.Measure();

        Assert.Equal(DuckGrip.Save, scene.HitTest(layout.SaveButton.Centre));
        Assert.Equal(DuckGrip.Cancel, scene.HitTest(layout.CancelButton.Centre));
    }

    [Fact]
    public void TheButtonsMoveBelowTheBoxWhenThereIsNoRoomAbove()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        Assert.True(scene.Measure().SaveButton.Bottom < scene.Measure().Box.Y);

        // Dragged to the very top of the desktop, where buttons drawn above it would be off the
        // edge and unpressable.
        scene.MoveTo(new ScenePoint(800, 0));
        var layout = scene.Measure();

        Assert.True(layout.SaveButton.Y > layout.Box.Bottom, "the buttons stayed off the top of the screen");
    }

    // ── save and cancel ───────────────────────────────────────────────────────

    [Fact]
    public void CancelPutsHimBackWhereHeStarted()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.BeginEdit();

        scene.MoveTo(new ScenePoint(200, 200));
        scene.ResizeTo(DuckGrip.ResizeBottomRight, new ScenePoint(600, 600));
        scene.CancelEdit();

        Assert.Equal(0.5, scene.Placement.AnchorX, 6);
        Assert.Equal(0.5, scene.Placement.AnchorY, 6);
        Assert.Equal(0.2, scene.Placement.Size, 6);
        Assert.False(scene.IsEditing);
    }

    [Fact]
    public void SaveKeepsWhereHeWasDraggedTo()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.BeginEdit();

        scene.MoveTo(new ScenePoint(200, 300));
        scene.CommitEdit();

        Assert.Equal(200.0 / 1600, scene.Placement.AnchorX, 6);
        Assert.Equal(300.0 / 900, scene.Placement.AnchorY, 6);
        Assert.False(scene.IsEditing);

        // And a cancel afterwards has nothing left to undo, which is what stops the cross
        // rolling him back to somewhere he was ten minutes ago.
        scene.CancelEdit();
        Assert.Equal(200.0 / 1600, scene.Placement.AnchorX, 6);
    }

    [Fact]
    public void EnteringEditModeTwiceDoesNotMoveTheGoalposts()
    {
        var scene = Sized(size: 0.2, x: 0.5, y: 0.5);
        scene.BeginEdit();

        scene.MoveTo(new ScenePoint(100, 100));
        scene.BeginEdit();      // the tray asking for a mode that is already on
        scene.CancelEdit();

        Assert.Equal(0.5, scene.Placement.AnchorX, 6);
        Assert.Equal(0.5, scene.Placement.AnchorY, 6);
    }

    [Fact]
    public void EditModeShowsHimWhateverGreenlightSays()
    {
        var scene = Sized();
        scene.ForceVisible = true;
        scene.SnapVisible();

        Assert.Equal(1, scene.Fade);
    }
}
