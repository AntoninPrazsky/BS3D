using BS3D.Lighting;
using System;
using System.Threading;
using Windows.Devices.Enumeration;
using Windows.Devices.Lights;
using Windows.System;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;

namespace BS3D.Platform
{
    /// <summary>
    /// The player's backlit keyboard through Windows' own lighting API (#987): <c>Windows.Devices.Lights.LampArray</c>, the HID
    /// LampArray standard behind Windows 11's Dynamic Lighting. No vendor's SDK and no DLL: the owner's Logitech G513 reports itself
    /// to Windows as a LampArray keyboard (104 lamps, every key addressable by its virtual key, 33 ms between updates, measured
    /// 2026-10-28), and so does any keyboard Dynamic Lighting lists. It is on the same Windows SDK the Game's TFM already carries for
    /// <see cref="PadMotors"/>.
    /// <para>
    /// <b>Windows decides who drives the lamps</b>: the app in the foreground, unless the player has said otherwise under Settings,
    /// Personalization, Dynamic Lighting. So the game owns the board while its window is active and the player's own effect comes
    /// back when it is not, with nothing to do here. Writes made while another app has the board are dropped by Windows, which is why
    /// <see cref="Submit"/> resends the whole board when the window comes back.
    /// </para>
    /// <para>
    /// <b>Nothing of it runs on the frame.</b> The device is found by a <see cref="DeviceWatcher"/> (plugged in later, pulled out,
    /// another keyboard), and the writes are a worker thread's: the frame composes the colours into its own array
    /// (<c>KeyLightModel.Compose</c>, no allocation), and <see cref="Submit"/> copies them under a lock. The worker sends at the
    /// device's own pace (<see cref="LampArray.MinUpdateInterval"/>), and only what changed. The first keyboard found is the one
    /// lit; a chassis lamp or a mouse is left alone.
    /// </para>
    /// <para>
    /// Turned off in Settings, the worker closes the device, which hands it back to Windows; turned on again, it opens it again.
    /// Windows-only by construction: GamePi compiles a namesake (<c>GamePi/Platform/KeyboardLights.cs</c>) that finds nothing.
    /// </para>
    /// </summary>
    internal static class KeyboardLights
    {
        //How often the whole board is sent again although nothing changed, in case Windows dropped a write while another app
        //had the lamps (a focus change the game did not see as one, Dynamic Lighting turned off and on)
        private const int RESEND_MILLISECONDS = 2000;

        private static readonly object _lock = new();
        private static DeviceWatcher _watcher;
        private static Thread _worker;
        private static readonly AutoResetEvent _wake = new(false);

        //Written by the watcher's thread and the worker under _lock; read by the frame (Layout) without it
        private static volatile KeyLightLayout _layout;
        private static string _deviceId;
        private static LampArray _lamps;
        private static TimeSpan _interval = TimeSpan.FromMilliseconds(33);

        //The frame's last colours (under _lock), the worker's own copy, and what it last sent
        private static XnaVector3[] _pending = Array.Empty<XnaVector3>();
        private static bool _hasPending, _resend;
        private static XnaVector3[] _working = Array.Empty<XnaVector3>();
        private static Windows.UI.Color[] _colours = Array.Empty<Windows.UI.Color>();
        private static Windows.UI.Color[] _sent = Array.Empty<Windows.UI.Color>();
        private static int[] _indices = Array.Empty<int>();

        private static volatile bool _wanted = true;
        private static bool _started;

        /// <summary>The keyboard's lamps, or null while none is open: the frame composes for this one.</summary>
        internal static KeyLightLayout Layout => _layout;

        /// <summary>Whether a keyboard is lit: the Settings row says so when none is.</summary>
        internal static bool HasKeyboard => _layout != null;

        /// <summary>Starts looking for a keyboard, once. Any failure (no Dynamic Lighting, an older Windows) leaves it dark.</summary>
        internal static void Start()
        {
            if (_started) return;
            _started = true;

            try
            {
                _watcher = DeviceInformation.CreateWatcher(LampArray.GetDeviceSelector());
                _watcher.Added += (_, info) => Found(info.Id, info.Name);
                _watcher.Removed += (_, update) => Lost(update.Id);
                _watcher.EnumerationCompleted += (_, _) =>
                {
                    if (_deviceId == null) Console.WriteLine("[lights] No backlit keyboard (Windows Dynamic Lighting lists none)");
                };
                _watcher.Start();

                _worker = new Thread(Work) { IsBackground = true, Name = "Keyboard lights", Priority = ThreadPriority.BelowNormal };
                _worker.Start();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[lights] Not available: {exception.GetType().Name}: {exception.Message}");
            }
        }

        /// <summary>The Settings switch: off closes the keyboard and hands it back to Windows, on opens it again.</summary>
        internal static void SetWanted(bool wanted)
        {
            if (_wanted == wanted) return;

            _wanted = wanted;
            _wake.Set();
        }

        /// <summary>
        /// Hands the frame's colours over (one a lamp of <see cref="Layout"/>, authored sRGB 0 to 1) to be sent at the device's
        /// pace. <paramref name="resend"/> sends the whole board even where nothing changed: the window has just come back.
        /// </summary>
        internal static void Submit(XnaVector3[] lamps, bool resend)
        {
            lock (_lock)
            {
                if (_pending.Length != lamps.Length) return; //composed for a keyboard that has just gone

                Array.Copy(lamps, _pending, lamps.Length);
                _hasPending = true;
                _resend |= resend;
            }
        }

        //The watcher's thread: the first LampArray that is a keyboard becomes the one lit
        private static void Found(string id, string name)
        {
            try
            {
                LampArray lamps = LampArray.FromIdAsync(id).AsTask().GetAwaiter().GetResult();
                if (lamps == null || lamps.LampArrayKind != LampArrayKind.Keyboard) return;

                string said = $"[lights] Keyboard '{name}' ({lamps.HardwareVendorId:X4}:{lamps.HardwareProductId:X4}): {lamps.LampCount} lamps, "
                    + $"{lamps.MinUpdateInterval.TotalMilliseconds:0} ms between updates, keys by name {lamps.SupportsVirtualKeys}";

                lock (_lock)
                {
                    if (_deviceId != null) { Release(lamps); return; }

                    _deviceId = id;
                    Open(lamps);
                    if (!_wanted) Close();
                }
                Console.WriteLine(said);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[lights] '{name}' could not be opened: {exception.GetType().Name}: {exception.Message}");
            }
        }

        private static void Lost(string id)
        {
            lock (_lock)
            {
                if (id != _deviceId) return;

                _deviceId = null;
                Close();
            }
            Console.WriteLine("[lights] The keyboard went away");
        }

        //Under _lock: the device opened, its layout read once and the buffers sized to it
        private static void Open(LampArray lamps)
        {
            _lamps = lamps;
            _interval = lamps.MinUpdateInterval > TimeSpan.Zero ? lamps.MinUpdateInterval : TimeSpan.FromMilliseconds(33);

            int count = lamps.LampCount;
            float width = Math.Max(lamps.BoundingBox.X, 1e-3f);
            var x = new float[count];
            for (int i = 0; i < count; i++) x[i] = Math.Clamp(lamps.GetLampInfo(i).Position.X / width, 0f, 1f);

            var keyLamps = new int[KeyLightModel.KEY_COUNT][];
            for (int key = 0; key < KeyLightModel.KEY_COUNT; key++)
                keyLamps[key] = lamps.SupportsVirtualKeys ? lamps.GetIndicesForKey(VirtualKeyOf((LitKey)key)) : Array.Empty<int>();

            _pending = new XnaVector3[count];
            _working = new XnaVector3[count];
            _colours = new Windows.UI.Color[count];
            _sent = new Windows.UI.Color[count];
            _indices = new int[count];
            for (int i = 0; i < count; i++) _indices[i] = i;

            _hasPending = false;
            _resend = true;
            _layout = new KeyLightLayout(x, keyLamps);
        }

        //Under _lock: the device closed, which gives it back to Windows, and the layout withdrawn from the frame
        private static void Close()
        {
            _layout = null;
            Release(_lamps);
            _lamps = null;
        }

        //A LampArray is not IClosable on the Windows SDK the Game targets (19041), so the projection gives it no Dispose: the
        //COM reference under it is released here rather than whenever the collector gets to it, which is what hands the
        //keyboard back to Windows now
        private static void Release(LampArray lamps)
        {
            try
            {
                if (lamps is WinRT.IWinRTObject winrt) winrt.NativeObject.Dispose();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[lights] Release failed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        private static VirtualKey VirtualKeyOf(LitKey key) => key switch
        {
            LitKey.Space => VirtualKey.Space,
            LitKey.E => VirtualKey.E,
            LitKey.Q => VirtualKey.Q,
            LitKey.R => VirtualKey.R,
            LitKey.W => VirtualKey.W,
            LitKey.A => VirtualKey.A,
            LitKey.S => VirtualKey.S,
            LitKey.D => VirtualKey.D,
            LitKey.F1 => VirtualKey.F1,
            LitKey.F2 => VirtualKey.F2,
            LitKey.F3 => VirtualKey.F3,
            _ => VirtualKey.F4,
        };

        //The worker: opens and closes on the switch, and sends the frame's colours at the device's pace
        private static void Work()
        {
            int sinceSent = 0;
            bool failing = false;

            while (true)
            {
                int wait = (int)Math.Max(_interval.TotalMilliseconds, 10);
                _wake.WaitOne(wait);
                sinceSent += wait;

                try
                {
                    LampArray lamps;
                    bool resend;

                    //The device and its buffers as one set, taken under the lock: the watcher's thread replaces all of them
                    //when a keyboard arrives, and the write below runs outside it
                    XnaVector3[] working;
                    Windows.UI.Color[] colours, sent;
                    int[] indices;

                    lock (_lock)
                    {
                        if (!_wanted && _lamps != null) Close();
                        if (_wanted && _lamps == null && _deviceId != null
                            && LampArray.FromIdAsync(_deviceId).AsTask().GetAwaiter().GetResult() is LampArray reopened)
                            Open(reopened);

                        lamps = _lamps;
                        if (lamps == null || !_hasPending) continue;

                        working = _working;
                        colours = _colours;
                        sent = _sent;
                        indices = _indices;
                        Array.Copy(_pending, working, working.Length);
                        _hasPending = false;
                        resend = _resend || sinceSent >= RESEND_MILLISECONDS;
                        _resend = false;
                    }

                    bool changed = false;
                    for (int i = 0; i < working.Length; i++)
                    {
                        Windows.UI.Color colour = ToColour(working[i]);
                        colours[i] = colour;
                        changed |= !colour.Equals(sent[i]);
                    }
                    if (!changed && !resend) continue;

                    lamps.SetColorsForIndices(colours, indices);
                    Array.Copy(colours, sent, colours.Length);
                    sinceSent = 0;
                    failing = false;
                }
                catch (Exception exception)
                {
                    //A keyboard pulled mid-write, a device Windows has taken away: said once until a write goes through again
                    if (!failing) Console.WriteLine($"[lights] Write failed: {exception.GetType().Name}: {exception.Message}");
                    failing = true;
                    Thread.Sleep(1000);
                }
            }
        }

        private static Windows.UI.Color ToColour(XnaVector3 c) => Windows.UI.Color.FromArgb(255, Channel(c.X), Channel(c.Y), Channel(c.Z));

        private static byte Channel(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
    }
}
