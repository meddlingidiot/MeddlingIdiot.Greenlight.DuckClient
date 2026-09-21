using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Greenlight.Sdk;
using Greenlight.Sdk.Protocol;

namespace Greenlight.DuckClient;

/// <summary>
/// The whole of the Greenlight integration, which is the point of the sample: attach,
/// translate the colour, and never care whether Greenlight is actually there.
/// </summary>
/// <remarks>
/// The tray icon, the overlay and edit mode are ordinary Avalonia and have nothing to do with
/// Greenlight — the integration is still the twenty-odd lines in
/// <see cref="StartWatchingGreenlight"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class App : Application
{
    private GreenlightClient? _greenlight;
    private DuckWindow? _window;
    private DuckTray? _tray;
    private DuckConfig _config = new();

    /// <summary>
    /// The last thing Greenlight said. Held here rather than only in the scene because the
    /// scene comes and goes — put away, brought back, rebuilt after a reload — and a duck that
    /// came back green after a restart would be the toy lying.
    /// </summary>
    private DuckState _state = DuckState.Off;

    private bool _building;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The overlay is closed and reopened by the tray's put-away/bring-back, and there is
            // no other window — on the default setting, putting the duck away would quit the
            // whole thing and take the tray icon with it.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _config = DuckConfig.Load();

            // If Windows is set to start this, make sure it is still pointed at the right
            // executable. An update moves the versioned copy out from under an older
            // registration, and the symptom is the duck silently not coming back one morning
            // — weeks after anybody touched the setting.
            WindowsStartup.Refresh();

            _tray = new DuckTray(_config)
            {
                IsRunning = () => _window is not null,
                OnSetRunning = running =>
                {
                    if (running) ShowDuck();
                    else HideDuck();
                },
                IsEditing = () => _window?.IsEditing ?? false,

                // Turning the mode off from the menu keeps the arrangement, the same as the
                // tick. Somebody who has dragged the duck and then gone back to the tray has
                // said what they meant; the cross is there for the other answer.
                OnSetEditing = editing =>
                {
                    if (editing) BeginEdit();
                    else EndEdit(keep: true);
                },
                OnConfigChanged = () => _window?.ApplyConfig(),
                OnReloadConfig = ReloadConfig,

                // Deliberately ignores the "let him quack" setting: this is somebody pressing
                // the button marked Quack, and a button that did nothing because of a tick two
                // lines above it would read as broken.
                OnQuack = () => _window?.Scene.QuackNow(),
                OnQuit = () => desktop.Shutdown(),
            };

            ShowDuck();
            StartWatchingGreenlight();

            desktop.Exit += async (_, _) =>
            {
                _tray?.Dispose();
                if (_greenlight is not null) await _greenlight.DisposeAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartWatchingGreenlight()
    {
        _greenlight = new GreenlightClient();

        // Both of these arrive on a background thread — the SDK says so, loudly, and this is
        // what it means in practice. Touching the scene from the pipe's thread would be a race
        // against the render loop reading the same balloon.
        _greenlight.Changed += (_, e) => Apply(Translate(e.Snapshot.Status), e.Snapshot.IsBuilding);
        _greenlight.AvailabilityChanged += (_, e) =>
        {
            // Anything other than Connected means we have nothing to show, and going grey is
            // more honest than leaving a green duck up on stale data.
            if (e.Availability != GreenlightAvailability.Connected) Apply(DuckState.Off, building: false);
        };

        // Deliberately not awaited and deliberately not guarded: StartAsync returns as soon as
        // the background loop is running, and an absent Greenlight is not an error. The duck
        // sits grey until one turns up, then lights on his own.
        _ = _greenlight.StartAsync();
    }

    private void ShowDuck()
    {
        if (_window is not null) return;

        _window = new DuckWindow(_config, new DuckScene(_config.Duck))
        {
            Scene =
            {
                State = _state,
                IsBuilding = _building,
                ShowWhenOff = _config.ShowWhenOff,
                Quacks = _config.Quacks,
                NagsWhenRed = _config.NagsWhenRed,
            },
        };

        // Brought back out to the state he was already in, so he does not announce news that is
        // an hour old. Setting State on a scene whose Fade is still 0 wears it straight away,
        // which is also what queues the quack — this is the one place that is not wanted.
        _window.Scene.Hush();

        _window.EditSaved += (_, _) => EndEdit(keep: true);
        _window.EditCancelled += (_, _) => EndEdit(keep: false);

        _window.Show();
        _tray?.ShowState(_state, _building);
    }

    private void HideDuck()
    {
        // Deliberately before the close, and keeping what was dragged: leaving edit mode is what
        // puts the click-through styles back, and a window destroyed mid-edit would take the
        // desktop's mouse with it until the duck was brought out again.
        if (_window is not null && _window.IsEditing) EndEdit(keep: true);

        _window?.Close();
        _window = null;
        _tray?.ShowState(_state, _building);
    }

    private void BeginEdit()
    {
        // Nothing to move while he is put away, and turning the mode on would leave a closed
        // window holding an interactive overlay nobody can see.
        if (_window is null || _window.IsEditing) return;

        _window.IsEditing = true;
        _tray?.ShowState(_state, _building);
    }

    /// <summary>
    /// Leave edit mode. <paramref name="keep"/> writes the arrangement to the file; without it
    /// the scene puts the duck back where he was when the mode started.
    /// </summary>
    private void EndEdit(bool keep)
    {
        if (_window is null || !_window.IsEditing) return;

        // The scene's snapshot is restored before the mode is turned off, so the frame that
        // draws without the chrome already has the duck back in his old place — otherwise a
        // cancel shows one frame of the dragged position, which reads as the cancel not working.
        if (keep) _window.Scene.CommitEdit();
        else _window.Scene.CancelEdit();

        _window.IsEditing = false;

        // Written once, at the end, rather than on every frame of the drag: the file would
        // otherwise take a few hundred writes to move one duck across the desk.
        if (keep) _config.Save();

        _tray?.ShowState(_state, _building);
    }

    /// <summary>Re-read the file, for colours changed by hand while this was running.</summary>
    private void ReloadConfig()
    {
        _config.CopyFrom(DuckConfig.Load());

        // The placement object is the one thing the overlay cannot absorb: the scene holds the
        // instance it was built with, and a reload hands the config a new one.
        if (_window is null) return;

        HideDuck();
        ShowDuck();
    }

    private static DuckState Translate(GreenlightStatus status) => status switch
    {
        GreenlightStatus.Green => DuckState.Green,
        GreenlightStatus.Yellow => DuckState.Amber,
        GreenlightStatus.Red => DuckState.Red,
        _ => DuckState.Off,
    };

    private void Apply(DuckState state, bool building) =>
        Dispatcher.UIThread.Post(() =>
        {
            _state = state;
            _building = building;

            if (_window is not null)
            {
                _window.Scene.State = state;

                // A build under way makes him breathe rather than recolour: Greenlight's own
                // rule is that a broken pipeline stays red while it rebuilds, and a duck that
                // went yellow the moment the fix started would be contradicting it.
                _window.Scene.IsBuilding = building;
            }

            _tray?.ShowState(state, building);
        });
}
