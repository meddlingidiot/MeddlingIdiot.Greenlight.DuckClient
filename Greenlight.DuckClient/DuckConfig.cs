using System.Text.Json;
using System.Text.Json.Serialization;

namespace Greenlight.DuckClient;

/// <summary>
/// One state's colours: the duck himself, his bill, and the light he throws.
/// </summary>
/// <remarks>
/// Three rather than one. The glow is not simply the body at a lower alpha — a hot core reads
/// as lit where a faded copy of the same green reads as a printing error — and the bill is
/// what keeps a green blob reading as a rubber duck rather than as a green blob.
/// </remarks>
public sealed class DuckLook
{
    /// <summary>The body. Any hex Avalonia can parse — <c>#FFD23A</c>, <c>#CCFFD23A</c>.</summary>
    public string Body { get; set; } = "#FFD23A";

    /// <summary>The bill, and the feet if they were ever drawn. Warm, and mostly left alone.</summary>
    public string Bill { get; set; } = "#F08A1C";

    /// <summary>The halo around him, and the light he spills onto the disc behind.</summary>
    public string Glow { get; set; } = "#C27A10";

    /// <summary>
    /// The horns.
    /// </summary>
    /// <remarks>
    /// Only the red state grows any, so this is only ever read from <see cref="DuckConfig.WhenRed"/>
    /// — it sits on every look because they are all the same shape of thing, not because a
    /// green duck has horns you could recolour. The eyes are deliberately not here: they are
    /// fire, and fire has its own colours.
    /// </remarks>
    public string Horn { get; set; } = "#8E1A10";
}

/// <summary>
/// The duck, read from a JSON file the user can edit. Written out with the defaults the first
/// time it is missing, so "where do I change the green" has an answer that does not involve
/// rebuilding anything.
/// </summary>
/// <remarks>
/// Kept in AppData rather than beside the executable: the executable lives under <c>bin</c>,
/// which a rebuild is entitled to delete, and losing somebody's arrangement to a rebuild would
/// be its own small betrayal.
/// </remarks>
public sealed class DuckConfig
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Greenlight.Duck", "duck.json");

    /// <summary>Where the duck sits and how big he is. Edit mode writes straight into this.</summary>
    public DuckPlacement Duck { get; set; } = new();

    /// <summary>Nothing to report: a grey bath duck with the lights off.</summary>
    public DuckLook WhenOff { get; set; } = new()
    {
        Body = "#8A9099",
        Bill = "#6C727A",
        Glow = "#3A3F47",
    };

    /// <summary>Everything passing.</summary>
    public DuckLook WhenGreen { get; set; } = new()
    {
        Body = "#5CFF8A",
        Bill = "#F08A1C",
        Glow = "#17C24B",
    };

    /// <summary>A pull request waiting on you. The colour he is on the badge.</summary>
    public DuckLook WhenAmber { get; set; } = new()
    {
        Body = "#FFD23A",
        Bill = "#F08A1C",
        Glow = "#E39B10",
    };

    /// <summary>A broken pipeline. The one state he grows horns for.</summary>
    public DuckLook WhenRed { get; set; } = new()
    {
        Body = "#FF5A48",
        Bill = "#F08A1C",
        Glow = "#B01208",
        Horn = "#8E1A10",
    };

    /// <summary>
    /// How much of the screen the duck may stand on. The work area by default, so a duck
    /// dragged to the bottom edge does not end up over the Start button.
    /// </summary>
    public AreaChoice Area { get; set; } = AreaChoice.WorkArea;

    /// <summary>Overall opacity, for when the duck is livelier than you want him to be.</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// How far the halo reaches, as a multiple of the ordinary reach. Low is a flat sticker;
    /// high is the duck lighting up the wallpaper around him.
    /// </summary>
    public double Glow { get; set; } = 1.0;

    /// <summary>
    /// Draw the dark disc the duck sits on.
    /// </summary>
    /// <remarks>
    /// On by default, and not decoration: a yellow duck laid straight over somebody's
    /// photograph of a sunflower is unreadable, and the disc is what stops the wallpaper
    /// deciding whether the build is passing.
    /// </remarks>
    public bool ShowDisc { get; set; } = true;

    /// <summary>Draw a grey duck when Greenlight is away, rather than nothing at all.</summary>
    public bool ShowWhenOff { get; set; } = true;

    /// <summary>
    /// Whether the duck says anything at all. Off leaves the colour and the pulse, which is
    /// the whole of the information — the balloon is the part that catches the eye.
    /// </summary>
    public bool Quacks { get; set; } = true;

    /// <summary>
    /// Whether a red duck goes on quacking rather than saying it once and settling.
    /// </summary>
    /// <remarks>
    /// On by default, and the one piece of nagging this toy does. A broken pipeline that
    /// announced itself once at 9:15 and then sat there quietly red is a broken pipeline
    /// somebody walks past all afternoon.
    /// </remarks>
    public bool NagsWhenRed { get; set; } = true;

    /// <summary>The colours for a given state. What the canvas asks, every frame.</summary>
    public DuckLook LookFor(DuckState state) => state switch
    {
        DuckState.Green => WhenGreen,
        DuckState.Amber => WhenAmber,
        DuckState.Red => WhenRed,
        _ => WhenOff,
    };

    /// <summary>
    /// Load the file, writing the defaults out first if it is not there. A file that cannot be
    /// read or parsed falls back to the defaults rather than refusing to start: this is a desk
    /// toy, and a stray comma should not cost you the whole thing.
    /// </summary>
    public static DuckConfig Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
            {
                var fresh = new DuckConfig();
                fresh.Save(file);
                return fresh;
            }

            var loaded = JsonSerializer.Deserialize<DuckConfig>(File.ReadAllText(file), Json);
            if (loaded is null) return new DuckConfig();

            // A file written before one of these existed deserializes it as null, and so does a
            // hand edit that deleted a block. Neither should be a crash on the next frame.
            var defaults = new DuckConfig();
            loaded.Duck ??= defaults.Duck;
            loaded.WhenOff ??= defaults.WhenOff;
            loaded.WhenGreen ??= defaults.WhenGreen;
            loaded.WhenAmber ??= defaults.WhenAmber;
            loaded.WhenRed ??= defaults.WhenRed;

            loaded.Opacity = Math.Clamp(loaded.Opacity, 0.1, 1.0);
            loaded.Glow = Math.Clamp(loaded.Glow, 0.0, 2.5);

            // Clamped on the way in, not only on the way out. The file is hand-editable, and an
            // anchor of 12 or a size of -3 should give you a duck you can find and drag back
            // rather than one that is somewhere off the side of the desktop.
            loaded.Duck.AnchorX = Math.Clamp(loaded.Duck.AnchorX, 0, 1);
            loaded.Duck.AnchorY = Math.Clamp(loaded.Duck.AnchorY, 0, 1);
            loaded.Duck.Size = Math.Clamp(loaded.Duck.Size, 0.03, 0.9);

            return loaded;
        }
        catch
        {
            return new DuckConfig();
        }
    }

    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
        }
        catch
        {
            // A toy that cannot write its config still runs perfectly well on the defaults.
        }
    }

    /// <summary>Take on everything from a freshly-read file, in place.</summary>
    /// <remarks>
    /// Copied into this instance rather than swapping it for the new one: the tray is holding
    /// this object, and it is the tray's menu that has to keep agreeing with the file.
    /// </remarks>
    public void CopyFrom(DuckConfig other)
    {
        Duck = other.Duck;
        WhenOff = other.WhenOff;
        WhenGreen = other.WhenGreen;
        WhenAmber = other.WhenAmber;
        WhenRed = other.WhenRed;
        Area = other.Area;
        Opacity = other.Opacity;
        Glow = other.Glow;
        ShowDisc = other.ShowDisc;
        ShowWhenOff = other.ShowWhenOff;
        Quacks = other.Quacks;
        NagsWhenRed = other.NagsWhenRed;
    }
}
