using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace BS3D.Capture
{
    /// <summary>How a camera path eases between one key and the next (<c>"ease"</c> in the file).</summary>
    internal enum CaptureEase
    {
        /// <summary>Slow out of the key and slow into the next: the default, a hand's move.</summary>
        Smooth,

        /// <summary>A constant rate.</summary>
        Linear,

        /// <summary>Slow out of the key, fastest at the next.</summary>
        In,

        /// <summary>Fastest out of the key, slow into the next.</summary>
        Out,
    }

    /// <summary>One key of a camera path: where the lens is and looks at the time, and how it leaves for the next key.</summary>
    internal readonly record struct CaptureKey(float Time, Vector3 Position, Vector3 Target, float FovDegrees, CaptureEase Ease);

    /// <summary>
    /// The lens a capture run films with instead of the play camera's (#946): one pose held (<c>campos=</c>, <c>camtarget=</c>,
    /// <c>fov=</c>, the Testbed's names) or a path of keys (<c>campath=&lt;file&gt;</c>) flown over the window. Pure: a pose is a function
    /// of the seconds into the window, so a clip is the same clip every time.
    /// <para>
    /// <b>The file</b> is JSON: <c>{ "interpolation": "eased", "keys": [ { "t": 0, "position": [x, y, z], "target": [x, y, z],
    /// "fov": 40, "ease": "smooth" }, ... ] }</c>. <c>t</c> is seconds into the captured window (0 is its first frame), strictly
    /// rising; <c>position</c> and <c>target</c> are world coordinates; <c>fov</c> is the vertical field in degrees, given on every
    /// key or none (then the play lens's). Before the first key and after the last the lens holds that key. With
    /// <c>"interpolation": "eased"</c> (the default) each segment is eased by its <b>first</b> key's <c>ease</c>
    /// (<c>smooth</c>, <c>linear</c>, <c>in</c>, <c>out</c>), so a lens comes to rest at every key; with <c>"spline"</c> the path is
    /// one smooth curve through the keys (a cubic Hermite through each, tangents from its neighbours' difference), which
    /// never stops at one, and <c>ease</c> is ignored. Comments and trailing commas are allowed.
    /// </para>
    /// </summary>
    internal sealed class CaptureCamera
    {
        internal const string FORMAT = "bs3d-camera-path";

        private readonly CaptureKey[] _keys;
        private readonly bool _spline;
        private readonly bool _hasFov;

        //For the spline: each key's rate of change (units a second), from its neighbours
        private readonly Vector3[] _positionRate, _targetRate;
        private readonly float[] _fovRate;

        private CaptureCamera(CaptureKey[] keys, bool spline, bool hasFov)
        {
            _keys = keys;
            _spline = spline && keys.Length > 1;
            _hasFov = hasFov;

            if (!_spline) return;

            _positionRate = new Vector3[keys.Length];
            _targetRate = new Vector3[keys.Length];
            _fovRate = new float[keys.Length];

            for (int i = 0; i < keys.Length; i++)
            {
                //The difference across the neighbours, one-sided at the two ends
                CaptureKey before = keys[Math.Max(i - 1, 0)], after = keys[Math.Min(i + 1, keys.Length - 1)];
                float span = after.Time - before.Time;
                _positionRate[i] = (after.Position - before.Position) / span;
                _targetRate[i] = (after.Target - before.Target) / span;
                _fovRate[i] = (after.FovDegrees - before.FovDegrees) / span;
            }
        }

        /// <summary><c>x,y,z</c> as <c>campos=</c> and <c>camtarget=</c> take it, in the invariant culture so a decimal point is always a point (the Testbed's spelling).</summary>
        internal static bool TryParseVector(string text, out Vector3 vector)
        {
            vector = default;
            string[] parts = text?.Split(',');
            if (parts == null || parts.Length != 3) return false;

            float[] values = new float[3];
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !float.IsFinite(values[i])) return false;

            vector = new Vector3(values[0], values[1], values[2]);
            return true;
        }

        /// <summary>The number of keys: 1 for a held pose.</summary>
        internal int KeyCount => _keys.Length;

        /// <summary>Whether the path states a field of view; otherwise the play lens's stands.</summary>
        internal bool HasFov => _hasFov;

        /// <summary>A lens that stays where it is put: <c>campos=</c>, <c>camtarget=</c> and, optionally, <c>fov=</c> (degrees; NaN keeps the play lens's).</summary>
        internal static CaptureCamera Held(Vector3 position, Vector3 target, float fovDegrees)
        {
            if ((target - position).LengthSquared() < 1e-6f) throw new FormatException("campos and camtarget are the same point: there is no direction to look in");

            return new CaptureCamera([new CaptureKey(0f, position, target, fovDegrees, CaptureEase.Linear)], false, !float.IsNaN(fovDegrees));
        }

        /// <summary>Reads a path file (<c>campath=</c>); throws <see cref="FormatException"/> naming what is wrong with it.</summary>
        internal static CaptureCamera Load(string path)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new FormatException($"campath '{path}' cannot be read: {exception.Message}");
            }

            return Parse(text);
        }

        /// <summary>Reads the text of a path file; see the class's remarks for its shape.</summary>
        internal static CaptureCamera Parse(string json)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException exception)
            {
                throw new FormatException($"campath is not JSON: {exception.Message}");
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw new FormatException("campath must be a JSON object with a \"keys\" array");

                if (root.TryGetProperty("format", out JsonElement format) && !(format.ValueKind == JsonValueKind.String && format.GetString() == FORMAT))
                    throw new FormatException($"campath's \"format\" must be \"{FORMAT}\" or absent");

                bool spline = false;
                if (root.TryGetProperty("interpolation", out JsonElement mode))
                {
                    string name = mode.ValueKind == JsonValueKind.String ? mode.GetString() : null;
                    if (string.Equals(name, "spline", StringComparison.OrdinalIgnoreCase)) spline = true;
                    else if (!string.Equals(name, "eased", StringComparison.OrdinalIgnoreCase)) throw new FormatException("campath's \"interpolation\" must be \"eased\" or \"spline\"");
                }

                if (!root.TryGetProperty("keys", out JsonElement keysElement) || keysElement.ValueKind != JsonValueKind.Array || keysElement.GetArrayLength() == 0)
                    throw new FormatException("campath needs a non-empty \"keys\" array");

                List<CaptureKey> keys = new(keysElement.GetArrayLength());
                int withFov = 0;

                foreach (JsonElement element in keysElement.EnumerateArray())
                {
                    int index = keys.Count;
                    if (element.ValueKind != JsonValueKind.Object) throw new FormatException($"campath key {index} must be an object");

                    if (!element.TryGetProperty("t", out JsonElement time) || !TryNumber(time, out float t) || t < 0f)
                        throw new FormatException($"campath key {index} needs \"t\", seconds into the window, 0 or more");

                    if (keys.Count > 0 && t <= keys[^1].Time) throw new FormatException($"campath key {index}: \"t\" must rise ({t.ToString(CultureInfo.InvariantCulture)} after {keys[^1].Time.ToString(CultureInfo.InvariantCulture)})");

                    Vector3 position = ReadVector(element, "position", index);
                    Vector3 target = ReadVector(element, "target", index);
                    if ((target - position).LengthSquared() < 1e-6f) throw new FormatException($"campath key {index}: position and target are the same point");

                    float fov = float.NaN;
                    if (element.TryGetProperty("fov", out JsonElement fovElement))
                    {
                        if (!TryNumber(fovElement, out fov) || !(fov > 1f && fov < 170f)) throw new FormatException($"campath key {index}: \"fov\" is degrees, between 1 and 170");
                        withFov++;
                    }

                    CaptureEase ease = CaptureEase.Smooth;
                    if (element.TryGetProperty("ease", out JsonElement easeElement))
                    {
                        string name = easeElement.ValueKind == JsonValueKind.String ? easeElement.GetString() : null;
                        if (!Enum.TryParse(name, ignoreCase: true, out ease) || !Enum.IsDefined(ease)) throw new FormatException($"campath key {index}: \"ease\" must be smooth, linear, in or out");
                    }

                    keys.Add(new CaptureKey(t, position, target, fov, ease));
                }

                if (withFov != 0 && withFov != keys.Count) throw new FormatException("campath: \"fov\" on every key or on none");

                return new CaptureCamera(keys.ToArray(), spline, withFov != 0);
            }
        }

        //A finite number, and not another kind of value, which TryGetSingle would throw on
        private static bool TryNumber(JsonElement element, out float value)
        {
            value = 0f;
            return element.ValueKind == JsonValueKind.Number && element.TryGetSingle(out value) && float.IsFinite(value);
        }

        private static Vector3 ReadVector(JsonElement key, string name, int index)
        {
            if (!key.TryGetProperty(name, out JsonElement array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 3)
                throw new FormatException($"campath key {index} needs \"{name}\": [x, y, z]");

            Span<float> parts = stackalloc float[3];
            int i = 0;
            foreach (JsonElement part in array.EnumerateArray())
            {
                if (!TryNumber(part, out parts[i])) throw new FormatException($"campath key {index}: \"{name}\" holds a number that is not one");
                i++;
            }

            return new Vector3(parts[0], parts[1], parts[2]);
        }

        /// <summary>
        /// The lens <paramref name="seconds"/> into the window: its position, the point it looks at, and its field of view in
        /// degrees (NaN where the path states none, which leaves the play lens's field).
        /// </summary>
        internal void Pose(float seconds, out Vector3 position, out Vector3 target, out float fovDegrees)
        {
            CaptureKey first = _keys[0];
            if (_keys.Length == 1 || seconds <= first.Time)
            {
                position = first.Position;
                target = first.Target;
                fovDegrees = first.FovDegrees;
                return;
            }

            CaptureKey last = _keys[^1];
            if (seconds >= last.Time)
            {
                position = last.Position;
                target = last.Target;
                fovDegrees = last.FovDegrees;
                return;
            }

            int i = 0;
            while (_keys[i + 1].Time <= seconds) i++;

            CaptureKey from = _keys[i], to = _keys[i + 1];
            float span = to.Time - from.Time;
            float fraction = (seconds - from.Time) / span;

            if (_spline)
            {
                //Hermite wants each tangent as the change over the whole segment, so the rate times its length
                position = Vector3.Hermite(from.Position, _positionRate[i] * span, to.Position, _positionRate[i + 1] * span, fraction);
                target = Vector3.Hermite(from.Target, _targetRate[i] * span, to.Target, _targetRate[i + 1] * span, fraction);
                fovDegrees = _hasFov ? MathHelper.Hermite(from.FovDegrees, _fovRate[i] * span, to.FovDegrees, _fovRate[i + 1] * span, fraction) : float.NaN;
                return;
            }

            float eased = Eased(from.Ease, fraction);
            position = Vector3.Lerp(from.Position, to.Position, eased);
            target = Vector3.Lerp(from.Target, to.Target, eased);
            fovDegrees = _hasFov ? MathHelper.Lerp(from.FovDegrees, to.FovDegrees, eased) : float.NaN;
        }

        /// <summary>The share of a segment covered after <paramref name="fraction"/> of its time, for the ease.</summary>
        internal static float Eased(CaptureEase ease, float fraction) => ease switch
        {
            CaptureEase.Linear => fraction,
            CaptureEase.In => fraction * fraction,
            CaptureEase.Out => 1f - (1f - fraction) * (1f - fraction),
            _ => fraction * fraction * (3f - 2f * fraction),
        };
    }
}
