using System;

namespace BS3D.Tools.LatticeProbe
{
    /// <summary>One tile's answer: how far its strongest spectral peak stands over the neighbourhood it sits in, and where.</summary>
    internal readonly record struct TileScore(double Score, double PeriodPixels, double AngleDegrees);

    /// <summary>
    /// <b>The spectral-peak detector for a repeating lattice</b> (#674): does this tile of an image carry a periodic
    /// arrangement, of the kind a person's eye finds in a field of "random" items and the shader's author cannot see?
    /// <para>
    /// A tile is <b>detrended</b> by a least-squares quadratic (a sky gradient leaks into the lowest bins otherwise),
    /// <b>windowed</b> (Hann, so the tile's own edge is not a line of power), transformed, and every local maximum of its
    /// power spectrum inside the band of periods 3–23 pixels is divided by the <b>median of its own 9 x 9
    /// neighbourhood</b>: a lattice is a peak standing out of the spectrum round it, where a coloured noise or a
    /// gradient is high everywhere near it and the ratio comes out about 1. The tile's score is the largest such ratio.
    /// What "large" means is <see cref="NullModel"/>'s: the 99.9th percentile of the same score over pure 1/f² noise.
    /// </para>
    /// <para>
    /// <b>Three kinds of tile are refused instead of scored</b>, each found as a false positive on real captures before
    /// it was a rule: a tile whose detrended texture is under <see cref="TEXTURE_FLOOR"/> levels (8-bit quantisation
    /// steps in a smooth gradient are perfectly periodic and score in the thousands); a tile that holds one
    /// isolated straight edge across it (<see cref="EDGE_ISOLATION"/>: a horizon, a rim, the edge of an object
    /// puts a whole line of power on an axis); and, by the caller, a tile under the crosshair.
    /// </para>
    /// <para>
    /// <b>What it cannot see, said here so a clean result is not read as a clearance:</b> a recurrence longer than a
    /// tile (a texture repeating every fifty units of world), a pattern whose items are jittered by a whole cell (its
    /// spectrum has a zero at the lattice frequency — <see cref="SelfTest"/> prints that row), and anything the band
    /// excludes. It finds a lattice; it does not clear a field.
    /// </para>
    /// </summary>
    internal sealed class TileDetector
    {
        /// <summary>The shortest period, in pixels, that counts (below it a "period" is the pixel grid).</summary>
        public const int MIN_PERIOD = 3;

        /// <summary>The longest period that counts: a tile of 256 holds eleven of these, enough to make a peak.</summary>
        public const int MAX_PERIOD = 23;

        /// <summary>The smallest texture, as the RMS of the detrended tile in 8-bit levels, worth analysing.</summary>
        public const double TEXTURE_FLOOR = 2.0;

        /// <summary>
        /// How many times the gradient energy of the strongest seven-row (seven-column) band of a tile must exceed the
        /// median of the other bands before the tile is an <b>isolated edge</b> — a horizon, a rim, the side of an object.
        /// A lattice has many bands as strong as its strongest (a facade of windows: a dozen rows of them) and a textured
        /// field has all of them alike, so both come out near 1; one straight line across an otherwise quiet tile comes
        /// out in the tens. Measured against the band's own neighbours rather than as a share of the tile's total, because a
        /// noise floor that gives every row a little gradient hides an edge from a share and does nothing to a ratio.
        /// </summary>
        public const double EDGE_ISOLATION = 6.0;

        private readonly int _n;
        private readonly double[] _basis;      //6 planes of n x n: 1, u, v, u2, uv, v2
        private readonly double[] _gramInverse; //6 x 6
        private readonly double[] _window;      //Hann, n
        private readonly bool[] _inBand;
        private readonly double[] _frequency;   //radial frequency of each bin, in cycles per tile
        private readonly double[] _re, _im, _power, _detrended;

        public TileDetector(int size)
        {
            if (size < 32 || (size & (size - 1)) != 0) throw new ArgumentException("The tile size must be a power of two of at least 32.");

            _n = size;
            _basis = new double[6 * size * size];
            _window = new double[size];
            _inBand = new bool[size * size];
            _frequency = new double[size * size];
            _re = new double[size * size];
            _im = new double[size * size];
            _power = new double[size * size];
            _detrended = new double[size * size];

            double half = (size - 1) / 2.0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double u = (x - half) / half, v = (y - half) / half;
                    int i = y * size + x, plane = size * size;
                    _basis[i] = 1.0;
                    _basis[plane + i] = u;
                    _basis[2 * plane + i] = v;
                    _basis[3 * plane + i] = u * u;
                    _basis[4 * plane + i] = u * v;
                    _basis[5 * plane + i] = v * v;
                }

            for (int i = 0; i < size; i++) _window[i] = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * (i + 0.5) / size);

            double[] gram = new double[36];
            for (int a = 0; a < 6; a++)
                for (int b = a; b < 6; b++)
                {
                    double sum = 0;
                    for (int i = 0; i < size * size; i++) sum += _basis[a * size * size + i] * _basis[b * size * size + i];
                    gram[a * 6 + b] = gram[b * 6 + a] = sum;
                }

            _gramInverse = Invert6(gram);

            double lowest = size / (double)MAX_PERIOD, highest = size / (double)MIN_PERIOD;
            for (int ky = 0; ky < size; ky++)
                for (int kx = 0; kx < size; kx++)
                {
                    double fx = kx < size / 2 ? kx : kx - size, fy = ky < size / 2 ? ky : ky - size;
                    double f = Math.Sqrt(fx * fx + fy * fy);
                    _frequency[ky * size + kx] = f;
                    _inBand[ky * size + kx] = f >= lowest && f <= highest;
                }
        }

        public int Size => _n;

        /// <summary>
        /// Scores <paramref name="tile"/> (size x size luminance, 0–255) or says why not. <paramref name="rejected"/> is
        /// "flat" or "edge" when the tile was refused.
        /// </summary>
        public bool TryScore(float[] tile, out TileScore score, out string rejected)
        {
            score = default;
            rejected = null;

            if (IsEdge(tile))
            {
                rejected = "edge";
                return false;
            }

            double rms = Detrend(tile);
            if (rms < TEXTURE_FLOOR)
            {
                rejected = "flat";
                return false;
            }

            score = Peak();
            return true;
        }

        /// <summary>Scores a tile with no refusals — the null model's route, whose tiles are synthetic and known to be textured.</summary>
        public TileScore Score(float[] tile)
        {
            Detrend(tile);
            return Peak();
        }

        //Whether the gradient energy of the tile is one isolated line across it, horizontal or vertical: the strongest band
        //of seven rows (columns) against the median of the bands elsewhere
        private bool IsEdge(float[] tile)
        {
            double[] rows = new double[_n], columns = new double[_n];

            for (int y = 1; y < _n - 1; y++)
                for (int x = 1; x < _n - 1; x++)
                {
                    int i = y * _n + x;
                    double gy = Math.Abs(tile[i + _n] - tile[i - _n]);
                    double gx = Math.Abs(tile[i + 1] - tile[i - 1]);
                    rows[y] += gy;
                    columns[x] += gx;
                }

            return Isolated(rows) || Isolated(columns);
        }

        private bool Isolated(double[] energy)
        {
            const int HALF = 3;
            double[] band = new double[_n];

            //The energy of the seven consecutive rows (columns) centred on each position, the ends left out
            double best = 0;
            int at = HALF;
            for (int i = HALF; i < _n - HALF; i++)
            {
                double sum = 0;
                for (int k = -HALF; k <= HALF; k++) sum += energy[i + k];

                band[i] = sum;
                if (sum > best)
                {
                    best = sum;
                    at = i;
                }
            }

            var others = new System.Collections.Generic.List<double>();
            for (int i = HALF; i < _n - HALF; i++)
                if (Math.Abs(i - at) > 2 * HALF + 1) others.Add(band[i]);

            if (others.Count == 0) return false;

            others.Sort();
            double median = others[others.Count / 2];
            return best > EDGE_ISOLATION * Math.Max(median, 1e-9);
        }

        //Subtracts the least-squares quadratic from the tile into _detrended and returns the residual's RMS
        private double Detrend(float[] tile)
        {
            int plane = _n * _n;
            double[] b = new double[6];

            for (int k = 0; k < 6; k++)
            {
                double sum = 0;
                for (int i = 0; i < plane; i++) sum += _basis[k * plane + i] * tile[i];
                b[k] = sum;
            }

            double[] c = new double[6];
            for (int r = 0; r < 6; r++)
                for (int k = 0; k < 6; k++) c[r] += _gramInverse[r * 6 + k] * b[k];

            double square = 0;
            for (int i = 0; i < plane; i++)
            {
                double fit = 0;
                for (int k = 0; k < 6; k++) fit += c[k] * _basis[k * plane + i];

                double residual = tile[i] - fit;
                _detrended[i] = residual;
                square += residual * residual;
            }

            return Math.Sqrt(square / plane);
        }

        //Windows and transforms _detrended, then returns the strongest peak-over-neighbourhood ratio in the band
        private TileScore Peak()
        {
            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    int i = y * _n + x;
                    _re[i] = _detrended[i] * _window[x] * _window[y];
                    _im[i] = 0.0;
                }

            Fft.Transform2D(_re, _im, _n, inverse: false);

            for (int i = 0; i < _power.Length; i++) _power[i] = _re[i] * _re[i] + _im[i] * _im[i];

            double best = 0;
            int bestKx = 0, bestKy = 0;
            Span<double> around = stackalloc double[80];

            for (int ky = 0; ky < _n; ky++)
                for (int kx = 0; kx < _n; kx++)
                {
                    int i = ky * _n + kx;
                    if (!_inBand[i]) continue;

                    double p = _power[i];
                    if (p <= 0) continue;

                    //A local maximum of its 3 x 3 first: only a peak can be a lattice's, and it spares the median on the other eight
                    bool peak = true;
                    for (int dy = -1; dy <= 1 && peak; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (_power[Wrap(ky + dy) * _n + Wrap(kx + dx)] > p) { peak = false; break; }
                        }

                    if (!peak) continue;

                    int count = 0;
                    for (int dy = -4; dy <= 4; dy++)
                        for (int dx = -4; dx <= 4; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            around[count++] = _power[Wrap(ky + dy) * _n + Wrap(kx + dx)];
                        }

                    around.Sort();
                    double median = 0.5 * (around[39] + around[40]);
                    double ratio = p / Math.Max(median, 1e-12);

                    if (ratio > best)
                    {
                        best = ratio;
                        bestKx = kx;
                        bestKy = ky;
                    }
                }

            double fx = bestKx < _n / 2 ? bestKx : bestKx - _n, fy = bestKy < _n / 2 ? bestKy : bestKy - _n;
            double f = Math.Max(Math.Sqrt(fx * fx + fy * fy), 1e-9);
            double angle = Math.Atan2(fy, fx) * 180.0 / Math.PI;
            angle = ((angle % 180.0) + 180.0) % 180.0;

            return new TileScore(best, _n / f, angle);
        }

        private int Wrap(int k) => (k + _n) & (_n - 1);

        private static double[] Invert6(double[] m)
        {
            double[] a = new double[6 * 12];
            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 6; c++) a[r * 12 + c] = m[r * 6 + c];
                a[r * 12 + 6 + r] = 1.0;
            }

            for (int col = 0; col < 6; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < 6; r++)
                    if (Math.Abs(a[r * 12 + col]) > Math.Abs(a[pivot * 12 + col])) pivot = r;

                if (pivot != col)
                    for (int c = 0; c < 12; c++) (a[col * 12 + c], a[pivot * 12 + c]) = (a[pivot * 12 + c], a[col * 12 + c]);

                double diagonal = a[col * 12 + col];
                for (int c = 0; c < 12; c++) a[col * 12 + c] /= diagonal;

                for (int r = 0; r < 6; r++)
                {
                    if (r == col) continue;

                    double factor = a[r * 12 + col];
                    for (int c = 0; c < 12; c++) a[r * 12 + c] -= factor * a[col * 12 + c];
                }
            }

            double[] inverse = new double[36];
            for (int r = 0; r < 6; r++)
                for (int c = 0; c < 6; c++) inverse[r * 6 + c] = a[r * 12 + 6 + c];

            return inverse;
        }
    }
}
