using System;
using System.Linq;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace BS3D
{
    /// <summary>
    /// The Android app's one activity (#1048): the game in landscape over the whole screen. It takes the place of
    /// <c>Program.Main</c>, which it shares everything before the game object with (<see cref="Program.Start"/>).
    /// Modelled on TW3D's (the owner's other game), which runs on the same phone.
    /// </summary>
    /// <remarks>
    /// The launch arguments come in the intent's <c>args</c> extra, split at spaces, so a scripted run is
    /// <c>adb shell am start -n io.github.antoninprazsky.bs3d/io.github.antoninprazsky.bs3d.MainActivity --es args "level=Girandole logfps"</c>.
    /// </remarks>
    [Activity(
        Name = "io.github.antoninprazsky.bs3d.MainActivity",
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@drawable/icon",
        Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden
            | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode)]
    public class MainActivity : AndroidGameActivity
    {
        private BS3DGame _game;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            WindowManagerLayoutParams attributes = Window.Attributes;
            //Draw beside the camera's cutout too, as on a monitor the whole panel is the game's
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
                attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
            //The display's fastest mode at its own resolution, as the desktop follows the monitor: Android would hold
            //the game at 60 Hz otherwise
            if (OperatingSystem.IsAndroidVersionAtLeast(30) && Display != null)
            {
                Display.Mode current = Display.GetMode();
                Display.Mode fastest = Display.GetSupportedModes()
                    .Where(mode => mode.PhysicalWidth == current.PhysicalWidth && mode.PhysicalHeight == current.PhysicalHeight)
                    .OrderByDescending(mode => mode.RefreshRate)
                    .First();
                attributes.PreferredDisplayModeId = fastest.ModeId;
            }
            Window.Attributes = attributes;

            //Before Program.Start: its [build] lines and the Game's readers look in the base directory
            AssetFiles.Prepare(this);
            //Before the first frame: MonoGame's GLES backend leaves an instanced draw's divisors to the next SpriteBatch
            Platform.GlesInstancing.Install();

            string line = Intent?.GetStringExtra("args");
            string[] args = string.IsNullOrWhiteSpace(line)
                ? Array.Empty<string>()
                : line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            LaunchOptions options = Program.Start(args);
            if (options == null)
            {
                Finish();
                return;
            }

            _game = new BS3DGame(options);
            SetContentView((View)_game.Services.GetService(typeof(View)));
            HideSystemBars();
            _game.Run();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            //Android shows the bars again on returning to the app
            if (hasFocus)
                HideSystemBars();
        }

        /// <summary>Hides the status and navigation bars; a swipe from the edge shows them for a while.</summary>
        private void HideSystemBars()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(30))
                return;

            //Since Android 15 an app is drawn under the bars anyway
            if (!OperatingSystem.IsAndroidVersionAtLeast(35))
                Window.SetDecorFitsSystemWindows(false);
            IWindowInsetsController controller = Window.InsetsController;
            if (controller == null)
                return;
            controller.Hide(WindowInsets.Type.SystemBars());
            controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
        }
    }
}
