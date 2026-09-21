using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Greenlight.DuckClient;

/// <summary>
/// Draws the duck. Everything comes off the scene's numbers each frame — there are no
/// controls, because a window nobody can click has no use for layout or hit testing, and edit
/// mode does its own.
/// </summary>
/// <remarks>
/// The glow is a stack of translucent ellipses rather than a radial gradient brush. It is a
/// couple of dozen filled ellipses a frame either way, it survives a wallpaper of any colour,
/// and it does not depend on which of the gradient properties the current Avalonia calls what.
/// </remarks>
public sealed class DuckCanvas : Control
{
    /// <summary>
    /// How many rings the halo is built from.
    /// </summary>
    /// <remarks>
    /// Rather more than it looks like it needs. Each ring is a hard-edged circle, so a handful
    /// of them at a workable alpha reads as a target painted round the duck rather than as
    /// light — the fix is many rings, each almost invisible on its own.
    /// </remarks>
    private const int HaloRings = 24;

    private static readonly IBrush DiscBrush = new SolidColorBrush(Color.FromArgb(196, 14, 15, 18));
    private static readonly IPen DiscPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1);

    /// <summary>The paper of the balloon, and the line round it.</summary>
    private static readonly IBrush BalloonBrush = new SolidColorBrush(Color.FromArgb(240, 246, 248, 250));

    private static readonly Color BalloonInk = Color.FromArgb(70, 12, 14, 18);

    private static readonly IBrush EyeBrush = new SolidColorBrush(Color.FromRgb(21, 15, 14));

    /// <summary>
    /// How wide the eye socket opens once he is fully wound up, in units of his own radius.
    /// </summary>
    /// <remarks>
    /// The socket is the whole trick. A red eye lit on a red duck is a red dot on a red duck,
    /// which is to say nothing at all — what reads as "glowing" is the dark it is glowing in,
    /// so the socket widens as the fire comes up and the ember sits inside it.
    /// </remarks>
    private const double EyeSocket = 0.19;

    /// <summary>The socket at rest, before any of that. An ordinary black bead.</summary>
    private const double EyeBead = 0.10;

    private const int EmberRings = 12;

    private static readonly Color EmberGlow = Color.FromRgb(255, 72, 36);
    private static readonly Color EmberCore = Color.FromRgb(255, 236, 206);

    // The horns, as x,y pairs in the duck's own units: the base, two controls and the tip, then
    // two controls and the other side of the base. Both are drawn from a root buried a little
    // way inside his skull, which is what they grow out of and retract back into.
    private static readonly ScenePoint NearHornRoot = new(-0.42, -0.92);

    private static readonly double[] NearHorn =
    [
        -0.56, -0.86,
        -0.66, -1.04, -0.54, -1.20, -0.24, -1.30,
        -0.28, -1.13, -0.23, -0.99, -0.29, -0.92,
    ];

    private static readonly ScenePoint FarHornRoot = new(0.06, -0.92);

    private static readonly double[] FarHorn =
    [
        -0.06, -0.96,
        -0.12, -1.09, 0.00, -1.22, 0.24, -1.24,
        0.19, -1.13, 0.17, -1.00, 0.14, -0.92,
    ];

    /// <summary>
    /// The ink he is drawn in. Heavy, warm and nearly black, the way the badge draws him.
    /// </summary>
    /// <remarks>
    /// Not decoration and not optional: the outline is most of what makes him read as the duck
    /// off the badge rather than as a duck-shaped blob, and it is also what keeps him legible
    /// at forty pixels over somebody's photograph of a beach.
    /// </remarks>
    private static readonly Color Ink = Color.FromRgb(23, 18, 14);

    /// <summary>How thick that line is, in units of his radius.</summary>
    private const double InkWidth = 0.055;

    private static readonly IBrush EditFill = new SolidColorBrush(Color.FromArgb(30, 120, 200, 255));
    private static readonly IPen EditPen = new Pen(
        new SolidColorBrush(Color.FromArgb(210, 150, 210, 255)), 1.5, new DashStyle([4, 3], 0));

    private static readonly IBrush HandleBrush = new SolidColorBrush(Color.FromArgb(235, 245, 250, 255));
    private static readonly IPen HandlePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 40, 60, 90)), 1);

    private static readonly IBrush SaveBrush = new SolidColorBrush(Color.FromArgb(240, 32, 160, 78));
    private static readonly IBrush CancelBrush = new SolidColorBrush(Color.FromArgb(240, 190, 48, 40));
    private static readonly IBrush ButtonHeld = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
    private static readonly IPen GlyphPen = new Pen(Brushes.White, 2.4)
    {
        LineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round,
    };

    /// <summary>
    /// The balloon's lettering. Bold and condensed-ish, because "QUACK!" at a tenth of the
    /// widget's height has to survive being drawn over a photograph.
    /// </summary>
    private static readonly Typeface BalloonFace =
        new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    private readonly DuckConfig _config;
    private readonly Dictionary<string, Color> _colours = [];

    public DuckCanvas(DuckScene scene, DuckConfig config)
    {
        Scene = scene;
        _config = config;
        IsHitTestVisible = false;
    }

    public DuckScene Scene { get; }

    /// <summary>Whether the move-and-resize chrome is drawn.</summary>
    public bool IsEditing { get; set; }

    /// <summary>What is currently being dragged, so it can be shown as pressed.</summary>
    public DuckGrip Held { get; set; }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Height <= 1 || Bounds.Width <= 1) return;

        var layout = Scene.Measure();

        // Faded all the way out and not being edited: nothing to draw, and nothing to spend a
        // frame on either.
        if (Scene.Fade > 0.001)
        {
            using var fade = context.PushOpacity(Ease(Scene.Fade));

            if (_config.ShowDisc) DrawDisc(context, layout);

            DrawDuck(context, layout);

            // Never in edit mode: the balloon goes exactly where the tick and the cross go, and
            // a duck talking over the buttons is a duck standing between you and leaving.
            if (!IsEditing && Scene.IsQuacking) DrawBalloon(context, layout);
        }

        if (IsEditing) DrawEditChrome(context, layout);
    }

    // ── the duck ──────────────────────────────────────────────────────────────

    private void DrawDisc(DrawingContext context, DuckLayout layout)
    {
        // Centred on the box rather than on the duck. The disc is the thing he floats against
        // while a build runs, and a disc that bobbed with him would leave nothing to bob
        // relative to.
        var radius = layout.Box.Width / 2;
        context.DrawEllipse(DiscBrush, DiscPen, ToPoint(layout.Box.Centre), radius, radius);
    }

    /// <summary>The duck in this state's colours: a halo, the body, the wing, the bill, the eye.</summary>
    private void DrawDuck(DrawingContext context, DuckLayout layout)
    {
        var look = _config.LookFor(Scene.Shown);
        var body = Colour(look.Body, fallback: Colors.Gray);
        var bill = Colour(look.Bill, fallback: body);
        var glow = Colour(look.Glow, fallback: body);
        var horn = Colour(look.Horn, fallback: Fade(body, 0.55));

        // Nothing to report throws no light. A grey duck with a halo would look like he was
        // claiming something.
        if (Scene.Shown != DuckState.Off)
            DrawHalo(context, layout.Centre, layout.DuckRadius * 1.15, glow);

        var brightness = Scene.Shown == DuckState.Off ? 1 : Scene.Brightness;

        var centre = layout.Centre;
        var r = layout.DuckRadius;
        var dir = layout.FacesLeft ? -1.0 : 1.0;

        var pen = new Pen(new SolidColorBrush(Ink), Math.Max(1, r * InkWidth))
        {
            LineJoin = PenLineJoin.Round,
            LineCap = PenLineCap.Round,
        };

        // Horns first, so the head is drawn over their roots and they look grown rather than
        // stuck on. Everything from here is outlined: the ink is what makes him the duck off
        // the badge instead of a duck-shaped blob.
        var menace = Scene.Menace;
        if (menace > 0.001)
        {
            context.DrawGeometry(
                new SolidColorBrush(Fade(horn, brightness)), pen, Horn(centre, r, dir, menace, near: true));

            // The far one a shade back, because it is on the other side of his head.
            context.DrawGeometry(
                new SolidColorBrush(Fade(horn, brightness * 0.70)), pen, Horn(centre, r, dir, menace, near: false));
        }

        context.DrawGeometry(new SolidColorBrush(Fade(body, brightness)), pen, Body(centre, r, dir));

        // The wing, a shade down from the body rather than a colour of its own: two hues on a
        // duck this small stops reading as one duck.
        context.DrawGeometry(new SolidColorBrush(Fade(body, brightness * 0.88)), pen, Wing(centre, r, dir));

        context.DrawGeometry(new SolidColorBrush(Fade(bill, brightness)), pen, Bill(centre, r, dir));

        // The line along the bill, which is the whole difference between a bill and an orange
        // wedge stuck to his face.
        var mouth = new Pen(new SolidColorBrush(Ink), Math.Max(0.8, r * InkWidth * 0.75))
        {
            LineCap = PenLineCap.Round,
        };

        context.DrawLine(mouth, At(centre, r, dir, -0.98, -0.40), At(centre, r, dir, -0.30, -0.37));

        // The sheen. A rubber duck is a shiny object, and one soft highlight on the crown is
        // what says so without drawing a second duck on top of the first.
        context.DrawEllipse(
            new SolidColorBrush(Colors.White, 0.16), null,
            At(centre, r, dir, -0.35, -0.82), r * 0.24, r * 0.14);

        DrawEye(context, centre, r, dir, menace);
    }

    /// <summary>
    /// The eye: an ordinary black bead, or — once a pipeline is broken — a coal burning in a
    /// socket that has opened around it.
    /// </summary>
    private void DrawEye(DrawingContext context, ScenePoint centre, double r, double dir, double menace)
    {
        var eye = At(centre, r, dir, -0.15, -0.49);
        var socket = r * (EyeBead + (EyeSocket - EyeBead) * menace);

        context.DrawEllipse(EyeBrush, null, eye, socket * 0.92, socket);

        if (menace > 0.001)
        {
            for (var i = EmberRings; i >= 1; i--)
            {
                var t = (double)i / EmberRings;
                var reach = socket * (0.5 + 1.1 * t);

                // Flickering, unlike the halo round the duck, which only breathes when a build
                // is running. This is fire, and fire is never quite steady.
                var alpha = 0.40 * Math.Pow(1 - t, 2) * menace * Scene.Flicker;
                if (alpha < 0.002) continue;

                context.DrawEllipse(new SolidColorBrush(EmberGlow, alpha), null, eye, reach, reach);
            }

            context.DrawEllipse(new SolidColorBrush(EmberGlow, menace), null, eye, socket * 0.52, socket * 0.58);
            context.DrawEllipse(new SolidColorBrush(EmberCore, menace), null, eye, socket * 0.26, socket * 0.26);
        }

        // The catchlight belongs to a bead, not to a coal: it goes out as the fire comes up, or
        // it reads as a reflection on something that is its own light source.
        if (menace < 0.999)
            context.DrawEllipse(
                new SolidColorBrush(Colors.White, 0.88 * (1 - menace)), null,
                At(centre, r, dir, -0.178, -0.518), r * 0.028, r * 0.028);
    }

    /// <summary>
    /// The light the duck throws: concentric rings, each fainter and wider than the last.
    /// </summary>
    /// <remarks>
    /// Most of the build pulse is spent here rather than on the body's own alpha. The eye reads
    /// a halo swelling and settling far more readily than it reads a colour dimming — and
    /// "dimming" is uncomfortably close to "going out", which already means something else.
    /// </remarks>
    private void DrawHalo(DrawingContext context, ScenePoint centre, double radius, Color colour)
    {
        var reach = radius * (1 + 0.85 * _config.Glow) * Scene.Halo;
        if (reach <= radius || _config.Glow <= 0) return;

        var point = ToPoint(centre);

        for (var i = HaloRings; i >= 1; i--)
        {
            var t = (double)i / HaloRings;
            var r = radius + (reach - radius) * t;

            // Squared falloff, and an alpha low enough that no single ring has a visible edge —
            // what is seen is the two dozen of them piling up towards the middle.
            var alpha = 0.085 * Math.Pow(1 - t, 2) * Scene.Brightness;
            if (alpha < 0.002) continue;

            context.DrawEllipse(new SolidColorBrush(colour, alpha), null, point, r, r);
        }
    }

    // ── the quack ─────────────────────────────────────────────────────────────

    /// <summary>
    /// What he is saying: a balloon with a tail pointing at the bill, popped out of nothing and
    /// settled.
    /// </summary>
    /// <remarks>
    /// Drawn rather than played, and that is the whole design. A desk toy that made an actual
    /// noise every time a pipeline changed its mind would be uninstalled before lunch — and it
    /// would be useless in the one room where this is most wanted, which is an open-plan one.
    /// </remarks>
    private void DrawBalloon(DrawingContext context, DuckLayout layout)
    {
        var rect = layout.Balloon;
        var text = Scene.QuackText;
        if (rect.Width <= 0 || string.IsNullOrEmpty(text)) return;

        // The pop grows out of the corner nearest the bill, so it reads as having come from
        // him. Scaled about the middle it would read as a notification arriving.
        var anchor = new Point(
            layout.FacesLeft ? rect.Right : rect.X,
            IsAbove(layout) ? rect.Bottom : rect.Y);

        var scale = Scene.BalloonScale;

        using var opacity = context.PushOpacity(Scene.BalloonOpacity);
        using var transform = context.PushTransform(
            Matrix.CreateTranslation(-anchor.X, -anchor.Y)
            * Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation(anchor.X, anchor.Y));

        var look = _config.LookFor(Scene.Shown);
        var ink = Fade(Colour(look.Body, Colors.Gray), 0.5);
        var pen = new Pen(new SolidColorBrush(BalloonInk), Math.Max(1, layout.TextSize * 0.08));

        // The tail first and unstroked, then the body of the balloon over the top of it: a
        // stroked triangle would leave its own outline running across the inside of the
        // balloon.
        context.DrawGeometry(BalloonBrush, null, Tail(layout));

        context.DrawRectangle(
            BalloonBrush, pen,
            new RoundedRect(new Rect(rect.X, rect.Y, rect.Width, rect.Height), layout.TextSize * 0.55));

        // Measured for real here, and shrunk if the scene's estimate of how wide the word would
        // be turned out generous — the estimate has to be arithmetic so the balloon can be laid
        // out without a rendering stack, and this is where that is paid for.
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            BalloonFace, layout.TextSize, new SolidColorBrush(ink));

        var room = rect.Width - layout.TextSize * 0.8;
        if (formatted.Width > room && formatted.Width > 0)
            formatted = new FormattedText(
                text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                BalloonFace, layout.TextSize * room / formatted.Width, new SolidColorBrush(ink));

        context.DrawText(formatted, new Point(
            rect.Centre.X - formatted.Width / 2,
            rect.Centre.Y - formatted.Height / 2));
    }

    /// <summary>The balloon's tail: a wedge off the near edge, narrowing to a point at the bill.</summary>
    private static Geometry Tail(DuckLayout layout)
    {
        var rect = layout.Balloon;
        var beak = layout.Beak;

        // Off the bottom when the balloon is above him, off the top when it has been pushed
        // below — either way, the edge that actually faces the duck.
        var edgeY = IsAbove(layout) ? rect.Bottom : rect.Y;

        // Anchored a third of the way in from the near corner rather than in the middle: a tail
        // from the centre of a wide balloon crosses the duck's own head on its way down.
        var near = layout.FacesLeft ? rect.Right : rect.X;
        var inward = layout.FacesLeft ? -1 : 1;

        // Wide at the root and tapering to the bill. A narrow one over the same distance draws
        // as a thin spike hanging off the balloon, which reads as a drip rather than as speech.
        var root = near + inward * rect.Width * 0.16;
        var spread = Math.Max(4, rect.Height * 0.55);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(root, edgeY), true);
            ctx.LineTo(new Point(root + inward * spread, edgeY));
            ctx.LineTo(new Point(beak.X, beak.Y));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>
    /// Whether the balloon is sitting above the duck or has been pushed underneath him.
    /// </summary>
    /// <remarks>
    /// Asked in two places — where the pop grows from and which edge the tail leaves by — and
    /// they have to give the same answer, or the balloon grows out of one corner and speaks
    /// from the opposite one.
    /// </remarks>
    private static bool IsAbove(DuckLayout layout) => layout.Balloon.Bottom <= layout.Box.Centre.Y;

    // ── edit mode ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The frame, the corner handles and the two buttons. Drawn outside the faded content on
    /// purpose: the chrome has to stay solid even when the duck underneath it is mid-change, or
    /// the tick would blink while somebody was aiming at it.
    /// </summary>
    private void DrawEditChrome(DrawingContext context, DuckLayout layout)
    {
        var box = layout.Box;
        context.DrawRectangle(EditFill, EditPen, new Rect(box.X, box.Y, box.Width, box.Height));

        foreach (var corner in DuckLayout.Corners)
        {
            var handle = layout.Handle(corner);
            var fill = Held == corner ? SaveBrush : HandleBrush;
            context.DrawRectangle(
                fill, HandlePen,
                new RoundedRect(new Rect(handle.X, handle.Y, handle.Width, handle.Height), 2));
        }

        DrawButton(context, layout.SaveButton, SaveBrush, tick: true);
        DrawButton(context, layout.CancelButton, CancelBrush, tick: false);
    }

    private void DrawButton(DrawingContext context, SceneRect rect, IBrush fill, bool tick)
    {
        var centre = new Point(rect.Centre.X, rect.Centre.Y);
        var radius = rect.Width / 2;

        context.DrawEllipse(fill, HandlePen, centre, radius, radius);

        if (Held == (tick ? DuckGrip.Save : DuckGrip.Cancel))
            context.DrawEllipse(ButtonHeld, null, centre, radius, radius);

        var r = radius * 0.48;
        var pen = new Pen(Brushes.White, Math.Max(1.6, radius * 0.20))
        {
            LineCap = GlyphPen.LineCap,
            LineJoin = GlyphPen.LineJoin,
        };

        if (tick)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(centre.X - r, centre.Y + r * 0.05), false);
                ctx.LineTo(new Point(centre.X - r * 0.25, centre.Y + r * 0.72));
                ctx.LineTo(new Point(centre.X + r, centre.Y - r * 0.66));
                ctx.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);
        }
        else
        {
            context.DrawLine(pen, new Point(centre.X - r, centre.Y - r), new Point(centre.X + r, centre.Y + r));
            context.DrawLine(pen, new Point(centre.X + r, centre.Y - r), new Point(centre.X - r, centre.Y + r));
        }
    }

    // ── the shapes ────────────────────────────────────────────────────────────
    // Traced off the duck on the Meddling Idiot badge, in units of the duck's radius about the
    // middle of his own bounding box, with x running towards his tail — so the same numbers
    // draw him either way round, and turning him to face his own balloon is a sign rather than
    // a transform. A mirroring transform would have worked too, and would also have mirrored
    // the lettering the moment anything else was pushed onto the context inside it.

    /// <summary>
    /// The whole silhouette — head, jaw, chest, belly, back and the tail flip — as one closed
    /// figure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One path rather than a head circle overlapping a body ellipse, which is the obvious way
    /// to draw a rubber duck and the wrong one here: the widget is drawn through a fade, and
    /// two overlapping shapes of the same colour are one shape only while the opacity is 1. At
    /// anything less the overlap is denser than the rest of him and the join shows as a seam
    /// across his neck.
    /// </para>
    /// <para>
    /// The proportions are the badge's and not a generic duck's, which is most of what makes
    /// him recognisable: a head very nearly as big as the body, a long flat bill, a wide body
    /// sitting low, and a short tail flipped up at the back. Drawn to the usual cartoon
    /// proportions instead — small head, round body, little bill — he reads as a chick.
    /// </para>
    /// </remarks>
    public static Geometry Body(ScenePoint centre, double radius, double dir)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(centre, radius, dir, -0.23, -1.00), true);

            // Over the crown and down the back of the head.
            Curve(ctx, centre, radius, dir, 0.10, -1.00, 0.39, -0.78, 0.39, -0.51);

            // The shoulder, and the shallow dip where the head gives way to the back.
            Curve(ctx, centre, radius, dir, 0.39, -0.36, 0.38, -0.24, 0.42, -0.17);

            // Out along the back to the point of the tail.
            Curve(ctx, centre, radius, dir, 0.66, -0.24, 0.92, -0.22, 1.09, -0.12);

            // Back in underneath it — the notch that makes it a tail rather than a corner.
            Curve(ctx, centre, radius, dir, 1.00, -0.03, 0.92, 0.02, 0.85, 0.07);

            // The back of the body, bulging out past the tail.
            Curve(ctx, centre, radius, dir, 1.02, 0.15, 1.16, 0.27, 1.16, 0.44);

            // Round the bottom, which is flat enough to sit on.
            Curve(ctx, centre, radius, dir, 1.15, 0.64, 0.99, 0.79, 0.80, 0.86);
            Curve(ctx, centre, radius, dir, 0.58, 0.94, 0.32, 0.96, 0.10, 0.95);
            Curve(ctx, centre, radius, dir, -0.16, 0.94, -0.40, 0.92, -0.52, 0.85);
            Curve(ctx, centre, radius, dir, -0.70, 0.76, -0.82, 0.62, -0.81, 0.47);

            // Up the chest into the jaw.
            Curve(ctx, centre, radius, dir, -0.80, 0.32, -0.78, 0.24, -0.77, 0.14);
            Curve(ctx, centre, radius, dir, -0.75, 0.00, -0.72, -0.08, -0.68, -0.14);

            // The face, and back over the crown.
            Curve(ctx, centre, radius, dir, -0.74, -0.30, -0.77, -0.44, -0.76, -0.60);
            Curve(ctx, centre, radius, dir, -0.74, -0.82, -0.52, -1.00, -0.23, -1.00);

            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>The folded wing: a broad curl over the back half of him.</summary>
    public static Geometry Wing(ScenePoint centre, double radius, double dir)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(centre, radius, dir, 0.16, 0.66), true);
            Curve(ctx, centre, radius, dir, 0.28, 0.32, 0.60, 0.00, 1.02, -0.08);
            Curve(ctx, centre, radius, dir, 1.18, 0.16, 1.16, 0.46, 0.98, 0.62);
            Curve(ctx, centre, radius, dir, 0.70, 0.74, 0.36, 0.74, 0.16, 0.66);
            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>
    /// The bill: long, flat and blunt, off the front of the face — the single feature that
    /// stops a yellow blob being a chick.
    /// </summary>
    public static Geometry Bill(ScenePoint centre, double radius, double dir)
    {
        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(centre, radius, dir, -0.29, -0.51), true);
            Curve(ctx, centre, radius, dir, -0.55, -0.58, -0.86, -0.56, -1.05, -0.42);
            Curve(ctx, centre, radius, dir, -0.97, -0.30, -0.60, -0.22, -0.23, -0.28);
            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>
    /// A horn: a curved wedge off the crown, swept back, grown out of the skull rather than
    /// switched on.
    /// </summary>
    /// <remarks>
    /// <paramref name="grow"/> is a lerp of every point towards the root, so at nothing the
    /// whole horn is a point buried in his head and at 1 it is the shape below. Scaling the
    /// drawn horn instead would have it growing from its own middle, which reads as a horn
    /// being lowered onto him.
    /// </remarks>
    public static Geometry Horn(ScenePoint centre, double radius, double dir, double grow, bool near)
    {
        var path = near ? NearHorn : FarHorn;
        var root = near ? NearHornRoot : FarHornRoot;

        Point P(int i) => At(
            centre, radius, dir,
            root.X + (path[i * 2] - root.X) * grow,
            root.Y + (path[i * 2 + 1] - root.Y) * grow);

        var geometry = new StreamGeometry();

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(P(0), true);
            ctx.CubicBezierTo(P(1), P(2), P(3));
            ctx.CubicBezierTo(P(4), P(5), P(6));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>One cubic in the duck's own units, so the traced numbers read as they were measured.</summary>
    private static void Curve(
        StreamGeometryContext ctx, ScenePoint centre, double radius, double dir,
        double x1, double y1, double x2, double y2, double x3, double y3) =>
        ctx.CubicBezierTo(
            At(centre, radius, dir, x1, y1),
            At(centre, radius, dir, x2, y2),
            At(centre, radius, dir, x3, y3));

    /// <summary>
    /// A point in the duck's own units: negative <paramref name="x"/> is towards the bill,
    /// positive towards the tail, and <paramref name="dir"/> is -1 when he is facing left.
    /// </summary>
    /// <remarks>
    /// The drift is applied here rather than baked into the traced numbers, so those still read
    /// as they were measured off the badge and can be checked against it again. It shifts the
    /// whole drawing — horns included — back onto the middle of the box.
    /// </remarks>
    private static Point At(ScenePoint centre, double radius, double dir, double x, double y) =>
        new(
            centre.X - dir * (x + DuckScene.DriftX) * radius,
            centre.Y + (y + DuckScene.DriftY) * radius);

    private static Point ToPoint(ScenePoint point) => new(point.X, point.Y);

    /// <summary>Ease the fade, so a state change does not start and stop with a jolt.</summary>
    private static double Ease(double t)
    {
        var clamped = Math.Clamp(t, 0, 1);
        return clamped * clamped * (3 - 2 * clamped);
    }

    /// <summary>Darken a colour towards nothing, for the breath. Alpha is left alone.</summary>
    private static Color Fade(Color colour, double brightness)
    {
        var k = Math.Clamp(brightness, 0, 1);
        return Color.FromArgb(colour.A, (byte)(colour.R * k), (byte)(colour.G * k), (byte)(colour.B * k));
    }

    /// <summary>
    /// A colour out of the config, parsed once and remembered.
    /// </summary>
    /// <remarks>
    /// Cached because this is asked several times a frame at 60fps, and because a hand-edited
    /// file is entitled to contain <c>"fluorescent"</c> — which must cost one failed parse and
    /// then nothing, rather than one per frame forever.
    /// </remarks>
    private Color Colour(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        if (_colours.TryGetValue(hex, out var cached)) return cached;

        var parsed = Color.TryParse(hex, out var colour) ? colour : fallback;
        _colours[hex] = parsed;
        return parsed;
    }
}
