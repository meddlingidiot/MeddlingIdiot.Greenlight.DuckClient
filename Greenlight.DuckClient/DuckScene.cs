namespace Greenlight.DuckClient;

/// <summary>What the duck is doing, which is Greenlight's aggregate colour by another name.</summary>
public enum DuckState
{
    /// <summary>
    /// No Greenlight attached, or it has nothing to say. The duck goes grey rather than
    /// vanishing — a green duck on a four-minute-old snapshot would be the toy lying, and a
    /// blank desktop looks like it crashed.
    /// </summary>
    Off,

    /// <summary>Everything passing.</summary>
    Green,

    /// <summary>A pull request wants you. Greenlight's yellow, which is the duck's own colour.</summary>
    Amber,

    /// <summary>A pipeline is broken.</summary>
    Red,
}

/// <summary>What the mouse is over, in edit mode.</summary>
public enum DuckGrip
{
    /// <summary>Nothing. A click here means "I am finished".</summary>
    None,

    /// <summary>The duck himself — drag to carry him around the desktop.</summary>
    Body,

    ResizeTopLeft,
    ResizeTopRight,
    ResizeBottomLeft,
    ResizeBottomRight,

    /// <summary>The tick. Keep where he has been dragged to.</summary>
    Save,

    /// <summary>The cross. Put him back where he started.</summary>
    Cancel,
}

/// <summary>A point in the overlay's logical pixels.</summary>
public readonly record struct ScenePoint(double X, double Y)
{
    public static ScenePoint operator +(ScenePoint a, ScenePoint b) => new(a.X + b.X, a.Y + b.Y);

    public static ScenePoint operator -(ScenePoint a, ScenePoint b) => new(a.X - b.X, a.Y - b.Y);

    public static ScenePoint operator *(ScenePoint a, double k) => new(a.X * k, a.Y * k);

    public double Length => Math.Sqrt(X * X + Y * Y);
}

/// <summary>A rectangle in the overlay's logical pixels.</summary>
public readonly record struct SceneRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public ScenePoint Centre => new(X + Width / 2, Y + Height / 2);

    public ScenePoint TopLeft => new(X, Y);

    public ScenePoint TopRight => new(Right, Y);

    public ScenePoint BottomLeft => new(X, Bottom);

    public ScenePoint BottomRight => new(Right, Bottom);

    public bool Contains(ScenePoint p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public SceneRect Inflate(double by) => new(X - by, Y - by, Width + by * 2, Height + by * 2);

    /// <summary>A square of side <paramref name="side"/> centred on a point.</summary>
    public static SceneRect Square(ScenePoint centre, double side) =>
        new(centre.X - side / 2, centre.Y - side / 2, side, side);
}

/// <summary>Where the duck sits and how big he is.</summary>
/// <remarks>
/// A mutable class rather than a record, because edit mode drags it in place while the tray
/// menu and the file are both looking at the same instance — the same arrangement
/// <see cref="DuckConfig"/> relies on when it writes a finished edit straight back to disk.
/// </remarks>
public sealed class DuckPlacement
{
    /// <summary>
    /// Where the centre of the duck sits, as a fraction of the overlay: 0 is the left or top
    /// edge, 1 the right or the bottom.
    /// </summary>
    /// <remarks>
    /// A fraction rather than a pixel so the duck survives the screen he was placed on — a
    /// laptop undocked from a 4K monitor would otherwise find him jammed in the corner.
    /// </remarks>
    public double AnchorX { get; set; } = 0.90;

    public double AnchorY { get; set; } = 0.16;

    /// <summary>How big the box is, as a fraction of the overlay's height.</summary>
    public double Size { get; set; } = 0.20;

    public DuckPlacement Copy() => new() { AnchorX = AnchorX, AnchorY = AnchorY, Size = Size };

    public void CopyFrom(DuckPlacement other)
    {
        AnchorX = other.AnchorX;
        AnchorY = other.AnchorY;
        Size = other.Size;
    }
}

/// <summary>
/// Every measurement of the widget at a given overlay size: the box it occupies, the duck
/// inside it, the balloon he is speaking into, and the edit-mode furniture.
/// </summary>
/// <remarks>
/// Separate from both the drawing and the placement on purpose. The canvas needs it to draw,
/// and edit mode needs exactly the same numbers to work out what the mouse is over — two
/// copies of this arithmetic would be two chances for the button you can press to sit
/// somewhere other than the button you can see.
/// </remarks>
public readonly record struct DuckLayout(
    SceneRect Box,
    ScenePoint Centre,
    double DuckRadius,
    bool FacesLeft,
    ScenePoint Beak,
    SceneRect Balloon,
    double TextSize,
    double HandleSize,
    SceneRect SaveButton,
    SceneRect CancelButton)
{
    public SceneRect Handle(DuckGrip grip) => grip switch
    {
        DuckGrip.ResizeTopLeft => SceneRect.Square(Box.TopLeft, HandleSize),
        DuckGrip.ResizeTopRight => SceneRect.Square(Box.TopRight, HandleSize),
        DuckGrip.ResizeBottomLeft => SceneRect.Square(Box.BottomLeft, HandleSize),
        DuckGrip.ResizeBottomRight => SceneRect.Square(Box.BottomRight, HandleSize),
        _ => default,
    };

    /// <summary>The four corners, in the order the canvas draws them.</summary>
    public static readonly DuckGrip[] Corners =
    [
        DuckGrip.ResizeTopLeft,
        DuckGrip.ResizeTopRight,
        DuckGrip.ResizeBottomRight,
        DuckGrip.ResizeBottomLeft,
    ];
}

/// <summary>
/// The duck himself: where he stands, what colour he is currently wearing, whether he is
/// mid-quack, and what the mouse is over while he is being moved. Deliberately free of
/// Avalonia — it is all arithmetic, so it can be tested without a window, which is the only
/// way the quack timing and the edit-mode maths were ever going to be checkable.
/// </summary>
public sealed class DuckScene
{
    /// <summary>Seconds for the duck to fade out, or back in, across a change of state.</summary>
    private const double FadeSeconds = 0.35;

    /// <summary>Seconds the widget sits blank between one state going out and the next coming in.</summary>
    /// <remarks>
    /// The whole point of the beat. A duck that slid from green to red would read as a gradient
    /// and not as news; a blank frame and then a red duck reads as something having happened,
    /// which is what the toy is trying to say.
    /// </remarks>
    private const double ChangeoverPause = 0.12;

    /// <summary>Seconds a balloon is on screen, from the pop to the last of the fade.</summary>
    private const double QuackSeconds = 2.2;

    /// <summary>How much of the balloon's life is spent popping in.</summary>
    private const double PopFraction = 0.16;

    /// <summary>How much of it is spent fading out at the end.</summary>
    private const double SettleFraction = 0.28;

    /// <summary>Seconds between quacks while a pipeline is broken and nagging is on.</summary>
    /// <remarks>
    /// Long enough to be ignorable while you are actually fixing it, short enough that a red
    /// duck nobody has looked at since half past nine is still asking.
    /// </remarks>
    private const double NagSeconds = 14.0;

    /// <summary>Seconds for the horns to grow in, or to go back where they came from.</summary>
    /// <remarks>
    /// Grown rather than switched on, and quicker than the fade that carries the colour change:
    /// horns that were simply there on the first red frame read as a second duck having been
    /// swapped in, where horns that push out of his skull read as him having taken it badly.
    /// </remarks>
    private const double MenaceSeconds = 0.55;

    /// <summary>Seconds for one full breath of the build pulse — down and back up again.</summary>
    /// <remarks>
    /// Faster than a resting breath on purpose, and the one place this toy is allowed to catch
    /// the eye: "a build is running" is the state a person is most likely to be waiting on.
    /// </remarks>
    private const double PulseSeconds = 1.8;

    /// <summary>How wide one character is, as a fraction of the text size.</summary>
    /// <remarks>
    /// An approximation, and deliberately one: the balloon has to be measurable without a
    /// rendering stack or none of this could be tested. The canvas measures the real string and
    /// shrinks it to fit whatever this produced, so an approximation that is a little wide
    /// costs a little air inside the balloon and nothing else.
    /// </remarks>
    private const double GlyphWidth = 0.66;

    /// <summary>
    /// How far out from the middle the near edge of the balloon sits, as a fraction of the box.
    /// </summary>
    /// <remarks>
    /// Wider than the duck himself, which is why it is this and not something smaller: the bill
    /// reaches 0.45 of the box, and a balloon nearer than that has its tail coming down across
    /// his own face.
    /// </remarks>
    private const double BalloonReach = 0.42;

    /// <summary>Smallest the box may be dragged, in logical pixels. Below this there is nothing to grab.</summary>
    public const double MinimumSide = 56;

    // ── the duck's own units ──────────────────────────────────────────────────
    // He is drawn in units of his radius, traced off the duck on the Meddling Idiot badge. The
    // numbers live here rather than with the drawing because the balloon has to know where his
    // bill is, and this is the half of the app that can be tested without a window.

    /// <summary>
    /// Where the drawing sits relative to his own origin, in those units.
    /// </summary>
    /// <remarks>
    /// The trace is centred on the duck's bounding box, which is not where the drawing's middle
    /// ends up: the tail reaches further right than the bill reaches left, and a pair of horns
    /// adds to the top and nothing to the bottom. Without this he sits low and left in his own
    /// box, and a horn crosses the edge of the disc he is standing on.
    /// </remarks>
    public const double DriftX = -0.05;

    /// <inheritdoc cref="DriftX"/>
    public const double DriftY = 0.14;

    /// <summary>The tip of the bill, before the drift.</summary>
    private const double BillTipX = -1.05;

    /// <inheritdoc cref="BillTipX"/>
    private const double BillTipY = -0.42;

    private readonly Random _random;

    private DuckState _state = DuckState.Off;
    private double _pulsePhase;
    private double _pause;
    private DuckPlacement? _beforeEdit;

    private string? _quackText;
    private double _quackLeft;
    private double _nagIn;

    public DuckScene(DuckPlacement placement, int? randomSeed = null)
    {
        Placement = placement;
        _random = randomSeed is null ? new Random() : new Random(randomSeed.Value);
    }

    public DuckPlacement Placement { get; }

    public double Width { get; private set; } = 1920;

    public double Height { get; private set; } = 1080;

    /// <summary>
    /// What Greenlight last said. Setting it starts a changeover: what is on screen fades out,
    /// the state is swapped while the widget is blank, and the new one fades in — and quacks
    /// as it arrives.
    /// </summary>
    public DuckState State
    {
        get => _state;
        set
        {
            if (value == _state) return;
            _state = value;

            // Nothing on screen to put away, so there is nothing to wait for and no blank beat
            // worth showing — this is the first snapshot after a cold start. He still gets his
            // quack; arriving is news too.
            if (Fade <= 0)
            {
                Wear(value);
                _pause = 0;
            }
            else
            {
                _pause = ChangeoverPause;
            }
        }
    }

    /// <summary>
    /// The state the duck is currently wearing. It lags <see cref="State"/> for as long as the
    /// changeover takes.
    /// </summary>
    /// <remarks>
    /// This is what makes a red duck fade out as a red duck. Drawing from <see cref="State"/>
    /// instead would turn him green on his way out, which reads as the status having changed a
    /// third of a second before anything moved.
    /// </remarks>
    public DuckState Shown { get; private set; } = DuckState.Off;

    /// <summary>Whether the widget is mid-swap: fading out, or sitting blank before it fades in.</summary>
    public bool IsChangingOver => Shown != _state;

    /// <summary>A build is running. The duck breathes and bobs — Greenlight's own rule.</summary>
    public bool IsBuilding { get; set; }

    /// <summary>
    /// Show the duck regardless of what Greenlight says. Edit mode turns this on: a duck you
    /// cannot see is a duck you cannot drag, and dragging him is the whole point.
    /// </summary>
    public bool ForceVisible { get; set; }

    /// <summary>
    /// Whether a grey duck is drawn when there is no Greenlight to ask.
    /// </summary>
    /// <remarks>
    /// On by default, and the same judgement the cars make by parking rather than vanishing: a
    /// blank desktop looks like the app crashed, where a grey duck looks like what it is.
    /// </remarks>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>Whether he says anything at all when the colour changes.</summary>
    public bool Quacks { get; set; } = true;

    /// <summary>Whether a red duck goes on quacking rather than saying it once and settling.</summary>
    public bool NagsWhenRed { get; set; } = true;

    /// <summary>Whether the widget is being moved and resized.</summary>
    public bool IsEditing => _beforeEdit is not null;

    /// <summary>
    /// How far in the widget is, 0 to 1. Animated rather than switched, because a duck that
    /// simply appears reads as a drawing bug and not as an arrival.
    /// </summary>
    public double Fade { get; private set; }

    /// <summary>
    /// A small waver on the glow, well under a pixel of meaning. A perfectly steady glow looks
    /// printed on.
    /// </summary>
    public double Flicker { get; private set; } = 1.0;

    /// <summary>
    /// How far gone he is, 0 to 1: the horns on his head and the fire in his eye. Pinned at 0
    /// for every state that is not red, and it grows and retracts rather than switching.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Fade"/> because they are two different pieces of news. The fade
    /// says a status arrived; this says which one, and it carries on being true for as long as
    /// the pipeline is broken — a duck who put his horns away after a second would be saying
    /// somebody had fixed it.
    /// </remarks>
    public double Menace { get; private set; }

    /// <summary>
    /// Where in the breath we are: 1 at the top, 0 at the bottom, and a flat 1 when nothing is
    /// building.
    /// </summary>
    public double Pulse => IsBuilding ? 0.5 * (1 + Math.Cos(_pulsePhase * 2 * Math.PI)) : 1;

    /// <summary>
    /// How bright the duck is this frame: 1 at rest, easing down and back up while a build
    /// runs.
    /// </summary>
    /// <remarks>
    /// The depth is deliberately spent mostly on the halo rather than the body. The eye reads a
    /// change in size far more readily than a change in brightness, and a duck that dimmed hard
    /// would read as going out — which already means something else here.
    /// </remarks>
    public double Brightness => 0.78 + 0.22 * Pulse;

    /// <summary>How far the halo reaches this frame, as a multiple of its resting reach.</summary>
    public double Halo => (IsBuilding ? 0.82 + 0.45 * Pulse : 1.0) * Flicker;

    /// <summary>
    /// How far off his resting height the duck is floating, as a fraction of the box. Zero
    /// unless a build is running.
    /// </summary>
    /// <remarks>
    /// The other half of the pulse, and the half people actually notice. A halo swelling on a
    /// bright wallpaper can be missed entirely; something bobbing cannot.
    /// </remarks>
    public double Bob => IsBuilding ? (0.5 - Pulse) * 0.06 : 0;

    /// <summary>Whether a balloon is on screen this frame.</summary>
    public bool IsQuacking => _quackLeft > 0 && _quackText is not null;

    /// <summary>What the balloon says, or null when there is no balloon.</summary>
    public string? QuackText => IsQuacking ? _quackText : null;

    /// <summary>How far through the balloon's life we are, 0 to 1.</summary>
    public double Quack => IsQuacking ? Math.Clamp(1 - _quackLeft / QuackSeconds, 0, 1) : 0;

    /// <summary>
    /// How solid the balloon is this frame: straight in, a long hold, and out at the end.
    /// </summary>
    public double BalloonOpacity
    {
        get
        {
            if (!IsQuacking) return 0;

            var t = Quack;
            if (t < PopFraction) return t / PopFraction;
            if (t > 1 - SettleFraction) return (1 - t) / SettleFraction;
            return 1;
        }
    }

    /// <summary>
    /// How big the balloon is drawn, as a multiple of its measured size. It overshoots on the
    /// way in and settles — a balloon that arrives at exactly its final size reads as a label
    /// being switched on rather than as somebody speaking.
    /// </summary>
    public double BalloonScale
    {
        get
        {
            if (!IsQuacking) return 0;

            var t = Quack;
            if (t >= PopFraction) return 1;

            var k = t / PopFraction;
            return 0.55 + 0.57 * k - 0.12 * k * k;
        }
    }

    public void Resize(double width, double height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
    }

    /// <summary>Move the scene on by one frame.</summary>
    public void Advance(TimeSpan elapsed)
    {
        // A frame that arrives after the machine has been asleep, or after a breakpoint, is
        // worth clamping: a two-minute step would run a whole balloon inside one frame, which
        // looks like a glitch rather than like time passing.
        var dt = Math.Clamp(elapsed.TotalSeconds, 0, 0.25);
        if (dt <= 0) return;

        _pulsePhase = (_pulsePhase + dt / PulseSeconds) % 1.0;

        if (IsChangingOver)
        {
            // Out, wait, then swap. Fading back in is the ordinary path below, on the frame
            // after the state has changed.
            if (Fade > 0) Fade = Math.Max(0, Fade - dt / FadeSeconds);
            else if (_pause > 0) _pause = Math.Max(0, _pause - dt);
            else Wear(_state);
        }
        else if (ForceVisible || ShowWhenOff || Shown != DuckState.Off)
        {
            Fade = Math.Min(1, Fade + dt / FadeSeconds);
        }
        else
        {
            Fade = Math.Max(0, Fade - dt / FadeSeconds);
        }

        // The horns follow what he is wearing, not what Greenlight has just said: they retract
        // while a red duck is fading out and grow on the way back in, so the changeover stays
        // one movement rather than a duck losing his horns half a second before he goes.
        // Clamped to where it is going rather than to 0 and 1, which is not the same thing once
        // it has arrived: "grow unless we are already past it" leaves a horn that has reached
        // full size taking the retracting branch on the very next frame, and the pair of them
        // then buzz a few per cent in and out for as long as the pipeline is broken.
        var wanted = Shown == DuckState.Red && !IsChangingOver ? 1.0 : 0.0;
        Menace = wanted > Menace
            ? Math.Min(wanted, Menace + dt / MenaceSeconds)
            : Math.Max(wanted, Menace - dt / MenaceSeconds);

        if (_quackLeft > 0) _quackLeft = Math.Max(0, _quackLeft - dt);

        // The nag. Only red, only while it is still red, and only once whatever he last said
        // has finished — a balloon interrupting itself reads as a stutter.
        if (Shown == DuckState.Red && !IsChangingOver && Quacks && NagsWhenRed)
        {
            _nagIn -= dt;
            if (_nagIn <= 0 && !IsQuacking) Say(WordFor(DuckState.Red));
        }

        Flicker = Fade > 0 ? 1 + (_random.NextDouble() - 0.5) * 0.04 : 1;
    }

    /// <summary>Bring the widget straight in, and quietly. For entering edit mode.</summary>
    public void SnapVisible()
    {
        Shown = _state;
        _pause = 0;
        Fade = 1;

        // Horns and all, rather than growing them again: edit mode is not news, and somebody
        // who opened it to move him four pixels should not have to watch him transform first.
        Menace = Shown == DuckState.Red ? 1 : 0;

        // Silent, because edit mode puts its buttons exactly where the balloon goes and a
        // person aiming at the tick should not have a duck talking over it.
        Hush();
    }

    /// <summary>Make him say his piece now, whatever the state has been doing.</summary>
    /// <remarks>
    /// The tray's one frivolous item, and the only way to see where the balloon lands without
    /// waiting for a build to break.
    /// </remarks>
    public void QuackNow() => Say(WordFor(Shown));

    /// <summary>Put the balloon away and reset the nag.</summary>
    public void Hush()
    {
        _quackLeft = 0;
        _quackText = null;
        _nagIn = NagSeconds;
    }

    /// <summary>What he says in a given state. Off says nothing at all.</summary>
    public static string WordFor(DuckState state) => state switch
    {
        DuckState.Green => "Quack!",
        DuckState.Amber => "Quack?",
        DuckState.Red => "QUACK!",
        _ => "…",
    };

    // ── layout ────────────────────────────────────────────────────────────────

    /// <summary>Every measurement of the widget at the current overlay size.</summary>
    public DuckLayout Measure()
    {
        var side = Side();
        var box = SceneRect.Square(new ScenePoint(Placement.AnchorX * Width, Placement.AnchorY * Height), side);

        // The duck fills the box with a margin, and floats within it while a build runs. The
        // bob is part of the layout rather than of the drawing so the balloon's tail follows
        // the beak up and down instead of pointing at where he used to be.
        var centre = new ScenePoint(box.Centre.X, box.Centre.Y + Bob * side);
        // 0.39 rather than half the box: after the drift he reaches 1.15 of this radius at the
        // back of the body and about 1.16 at the tip of a horn, so a larger one would hang him
        // over the edge of his own disc and out of the box edit mode draws round him.
        var radius = side * 0.39;

        var handle = Math.Clamp(side * 0.14, 10, 22);

        // The buttons live above the box, unless there is no room above — at the top of the
        // screen they go underneath rather than off the edge, where they could not be pressed.
        var button = Math.Clamp(side * 0.26, 22, 40);
        var gap = button * 0.35;
        var buttonY = box.Y - gap - button < 0 ? box.Bottom + gap : box.Y - gap - button;

        var save = new SceneRect(box.Right - button, buttonY, button, button);
        var cancel = new SceneRect(box.Right - button * 2 - gap, buttonY, button, button);

        // Which way he is looking. Left by default — it is the way round he is drawn on the
        // badge — but he turns to face his own balloon when there is no room on that side, and
        // a duck talking over his own shoulder looks like a mistake.
        var textSize = Math.Max(7, side * 0.15);
        var balloonSize = BalloonSize(textSize);
        var facesLeft = PrefersLeft(box, balloonSize.Width, side);

        var beak = Beak(centre, radius, facesLeft);
        var balloon = BalloonAt(box, balloonSize, facesLeft, side);

        return new DuckLayout(
            box, centre, radius, facesLeft, beak, balloon, textSize, handle, save, cancel);
    }

    /// <summary>The box's side in logical pixels, clamped to something draggable and something sane.</summary>
    public double Side() => Math.Clamp(Height * Placement.Size, MinimumSide, MaximumSide);

    /// <summary>
    /// The largest the box may be: most of the shorter side of the overlay, and never smaller
    /// than the minimum — on a very short screen the two clamps would otherwise cross over and
    /// Math.Clamp would throw.
    /// </summary>
    public double MaximumSide => Math.Max(MinimumSide, Math.Min(Width, Height) * 0.9);

    /// <summary>The tip of the bill, which is where the balloon's tail points.</summary>
    public static ScenePoint Beak(ScenePoint centre, double radius, bool facesLeft) =>
        new(
            centre.X + (facesLeft ? -1 : 1) * radius * -(BillTipX + DriftX),
            centre.Y + (BillTipY + DriftY) * radius);

    /// <summary>
    /// What is under the mouse. Edit mode uses it for the cursor and for the drag, and both
    /// have to agree with what the canvas drew.
    /// </summary>
    /// <remarks>
    /// Buttons first, then corners, then the body. The save button sits close to the box's own
    /// corner handle at small sizes, and a tick that resized the widget instead of keeping it
    /// would be the single most annoying bug this thing could have. The balloon is not in here
    /// at all: it is never on screen in edit mode, and it would be a moving target if it were.
    /// </remarks>
    public DuckGrip HitTest(ScenePoint point)
    {
        var layout = Measure();

        if (layout.SaveButton.Contains(point)) return DuckGrip.Save;
        if (layout.CancelButton.Contains(point)) return DuckGrip.Cancel;

        foreach (var corner in DuckLayout.Corners)
            if (layout.Handle(corner).Contains(point))
                return corner;

        return layout.Box.Contains(point) ? DuckGrip.Body : DuckGrip.None;
    }

    /// <summary>Carry the duck somewhere else. <paramref name="centre"/> is where his middle lands.</summary>
    public void MoveTo(ScenePoint centre)
    {
        // Clamped by the box rather than by its centre: an anchor clamped to 0–1 would still
        // let half the duck hang off the edge of the desktop, and half a duck is half a thing
        // to grab hold of when you want him back.
        var half = Side() / 2;

        Placement.AnchorX = Fraction(centre.X, half, Width);
        Placement.AnchorY = Fraction(centre.Y, half, Height);
    }

    /// <summary>
    /// Drag a corner. The opposite corner stays where it is, and the box stays square — the
    /// duck is drawn to one radius, and a rectangle would only ever mean squashing him.
    /// </summary>
    public void ResizeTo(DuckGrip corner, ScenePoint point)
    {
        if (corner is not (DuckGrip.ResizeTopLeft or DuckGrip.ResizeTopRight
            or DuckGrip.ResizeBottomLeft or DuckGrip.ResizeBottomRight)) return;

        var box = Measure().Box;

        var anchored = corner switch
        {
            DuckGrip.ResizeTopLeft => box.BottomRight,
            DuckGrip.ResizeTopRight => box.BottomLeft,
            DuckGrip.ResizeBottomLeft => box.TopRight,
            _ => box.TopLeft,
        };

        // The longer of the two reaches, so the box follows whichever way the mouse actually
        // went rather than stalling when it moves along only one axis.
        var side = Math.Max(Math.Abs(point.X - anchored.X), Math.Abs(point.Y - anchored.Y));
        side = Math.Clamp(side, MinimumSide, MaximumSide);

        var signX = corner is DuckGrip.ResizeTopRight or DuckGrip.ResizeBottomRight ? 1 : -1;
        var signY = corner is DuckGrip.ResizeBottomLeft or DuckGrip.ResizeBottomRight ? 1 : -1;

        // Size before the move, so MoveTo clamps the new centre against the new half-width
        // rather than the old one — resizing towards an edge would otherwise push the box off
        // it and then refuse to bring it back.
        Placement.Size = Height <= 0 ? Placement.Size : side / Height;
        MoveTo(new ScenePoint(anchored.X + signX * side / 2, anchored.Y + signY * side / 2));
    }

    // ── edit mode ─────────────────────────────────────────────────────────────

    /// <summary>Remember where the duck was, so the cross has something to put him back to.</summary>
    /// <remarks>
    /// A second call while already editing is ignored rather than re-snapshotting. The tray can
    /// ask for edit mode while it is already on, and taking a fresh snapshot there would
    /// quietly turn the cross into a second tick.
    /// </remarks>
    public void BeginEdit() => _beforeEdit ??= Placement.Copy();

    /// <summary>Keep him where he has been dragged to.</summary>
    public void CommitEdit() => _beforeEdit = null;

    /// <summary>Put him back where he was when edit mode started.</summary>
    public void CancelEdit()
    {
        if (_beforeEdit is null) return;

        Placement.CopyFrom(_beforeEdit);
        _beforeEdit = null;
    }

    // ── the balloon ───────────────────────────────────────────────────────────

    /// <summary>Put a state on, and say so if there is anything worth saying.</summary>
    private void Wear(DuckState state)
    {
        Shown = state;

        // Nothing to announce about having nothing to announce. Greenlight going away is the
        // one change the duck meets in silence — he simply goes grey.
        if (state == DuckState.Off) Hush();
        else if (Quacks) Say(WordFor(state));
    }

    private void Say(string text)
    {
        _quackText = text;
        _quackLeft = QuackSeconds;

        // Counted from the start of a quack rather than from the end of one, so the gap between
        // two nags is the same whatever the balloon is doing when the clock runs out.
        _nagIn = NagSeconds;
    }

    /// <summary>How big the balloon has to be to hold what he is saying.</summary>
    private SceneRect BalloonSize(double textSize)
    {
        // The property rather than the field: a balloon whose time is up is not on screen, and
        // a layout that went on reporting one would have the canvas drawing a tail at nothing.
        var text = QuackText;
        if (string.IsNullOrEmpty(text)) return default;

        var padX = textSize * 0.66;
        var padY = textSize * 0.40;

        return new SceneRect(
            0, 0,
            textSize * GlyphWidth * text.Length + padX * 2,
            textSize * 1.22 + padY * 2);
    }

    /// <summary>
    /// Which side the balloon goes, and therefore which way the duck is looking. Left unless
    /// the left is off the edge of the desktop and the right is not.
    /// </summary>
    private bool PrefersLeft(SceneRect box, double balloonWidth, double side)
    {
        if (balloonWidth <= 0) return true;

        var reach = side * BalloonReach;
        var leftFits = box.Centre.X - reach - balloonWidth >= 0;
        var rightFits = box.Centre.X + reach + balloonWidth <= Width;

        return leftFits || !rightFits;
    }

    /// <summary>
    /// Where the balloon sits: above the duck and out to the side he is facing, moved
    /// underneath him if there is no room above, and clamped to the desktop either way.
    /// </summary>
    private SceneRect BalloonAt(SceneRect box, SceneRect size, bool facesLeft, double side)
    {
        if (size.Width <= 0) return default;

        var gap = side * 0.05;
        var reach = side * BalloonReach;

        var x = facesLeft ? box.Centre.X - reach - size.Width : box.Centre.X + reach;
        var y = box.Y - gap - size.Height;

        // Underneath rather than off the top of the screen. The tail flips with it, because the
        // tail is drawn from whichever edge is nearest the beak.
        if (y < 0) y = box.Bottom + gap;

        // Clamped last, and only if there is anywhere to clamp to: a balloon wider than the
        // desktop has no legal position, and Math.Clamp with a low above its high throws.
        if (size.Width < Width) x = Math.Clamp(x, 0, Width - size.Width);
        if (size.Height < Height) y = Math.Clamp(y, 0, Height - size.Height);

        return new SceneRect(x, y, size.Width, size.Height);
    }

    /// <summary>
    /// Where a coordinate sits in the overlay as a fraction, with the box kept fully on screen.
    /// </summary>
    private static double Fraction(double value, double half, double extent)
    {
        if (extent <= 0 || !double.IsFinite(value)) return 0.5;

        // A box wider than the overlay has no legal position at all, so it is centred rather
        // than clamped — Math.Clamp with a low above its high throws, and a 4K widget dropped
        // onto a netbook is an ordinary way to arrive here.
        if (half * 2 >= extent) return 0.5;

        return Math.Clamp(value, half, extent - half) / extent;
    }
}
