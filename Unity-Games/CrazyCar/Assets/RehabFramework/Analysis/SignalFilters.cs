using System;

namespace RehabFramework.Analysis
{
    // ─────────────────────────────────────────────────────────────────────────
    //  SIGNAL FILTERS (design §6.2)
    //  Two deliberately separate regimes:
    //    • Real-time (causal): CausalMovingAverage — drives the state machine
    //      and balloon only. Cheap, ~20 ms lag, never used for reported metrics.
    //    • Offline (zero-phase): 2nd-order Butterworth low-pass applied
    //      forward-then-backward over a complete repetition — zero phase shift,
    //      so onset/peak timestamps are unbiased. Used for every clinical number.
    //  Plain C#, no UnityEngine. No external dependencies.
    // ─────────────────────────────────────────────────────────────────────────

    public static class SignalFilters
    {
        // ─────────────────────────────────────────────────────────────────────
        //  BUTTERWORTH DESIGN (bilinear transform, 2nd order low-pass)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Coefficients of a 2nd-order Butterworth low-pass (bilinear transform,
        /// prewarped), normalized so a0 = 1:
        ///   y[n] = b0·x[n] + b1·x[n−1] + b2·x[n−2] − a1·y[n−1] − a2·y[n−2]
        /// DC gain is exactly 1 by construction.
        /// </summary>
        public static void ButterworthLowPass(
            double fs, double fc,
            out double b0, out double b1, out double b2,
            out double a1, out double a2)
        {
            if (fc <= 0.0 || fc >= fs * 0.5)
                throw new ArgumentException("Cutoff must satisfy 0 < fc < fs/2.");

            double wc = Math.Tan(Math.PI * fc / fs);   // prewarped analog cutoff
            double k1 = Math.Sqrt(2.0) * wc;           // 2·cos(π/4)·wc
            double k2 = wc * wc;
            double a0 = 1.0 + k1 + k2;

            b0 = k2 / a0;
            b1 = 2.0 * k2 / a0;
            b2 = k2 / a0;
            a1 = 2.0 * (k2 - 1.0) / a0;
            a2 = (1.0 - k1 + k2) / a0;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZERO-PHASE FILTERING (filtfilt)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Zero-phase Butterworth low-pass: forward pass, time-reverse, second
        /// pass, reverse back (filtfilt). Edge handling: odd-reflection padding
        /// of <paramref name="padLen"/> samples at each end plus steady-state
        /// initialization of the filter delay line — for reach data, which is
        /// at rest at both ends of a repetition slice, this makes edge
        /// transients negligible.
        /// Effective magnitude response is the square of the single-pass
        /// response (−6 dB at fc instead of −3 dB), which is the standard
        /// filtfilt trade and is fine here: everything physiological in a
        /// reach sits well below 5 Hz.
        /// </summary>
        public static float[] ZeroPhaseLowPass(float[] x, float fs, float fc, int padLen = 25)
        {
            if (x == null) throw new ArgumentNullException("x");
            int n = x.Length;
            if (n < 3) return (float[])x.Clone();

            double b0, b1, b2, a1, a2;
            ButterworthLowPass(fs, fc, out b0, out b1, out b2, out a1, out a2);

            if (padLen > n - 1) padLen = n - 1;
            int m = n + 2 * padLen;
            double[] ext = new double[m];

            // Odd reflection: ext continues the signal's slope through each end,
            // so the filter warm-up happens on plausible data, not on a cliff.
            for (int i = 0; i < padLen; i++)
                ext[i] = 2.0 * x[0] - x[padLen - i];
            for (int i = 0; i < n; i++)
                ext[padLen + i] = x[i];
            for (int i = 0; i < padLen; i++)
                ext[padLen + n + i] = 2.0 * x[n - 1] - x[n - 2 - i];

            FilterForwardInPlace(ext, b0, b1, b2, a1, a2);
            Array.Reverse(ext);
            FilterForwardInPlace(ext, b0, b1, b2, a1, a2);
            Array.Reverse(ext);

            float[] result = new float[n];
            for (int i = 0; i < n; i++) result[i] = (float)ext[padLen + i];
            return result;
        }

        /// <summary>Single causal IIR pass (direct form I). The delay line is
        /// initialized to steady state at the first sample (valid because the
        /// filter's DC gain is 1), so a signal that starts at rest produces no
        /// start-up transient at all.</summary>
        private static void FilterForwardInPlace(
            double[] x, double b0, double b1, double b2, double a1, double a2)
        {
            double x1 = x[0], x2 = x[0];
            double y1 = x[0], y2 = x[0];
            for (int i = 0; i < x.Length; i++)
            {
                double xi = x[i];
                double yi = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = xi;
                y2 = y1; y1 = yi;
                x[i] = yi;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  DERIVATIVES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Central-difference derivative (one-sided at the endpoints).
        /// Always applied to already-filtered signals; chain three times for
        /// velocity → acceleration → jerk (design §6.2).</summary>
        public static float[] Derivative(float[] x, float dt)
        {
            if (x == null) throw new ArgumentNullException("x");
            if (dt <= 0f) throw new ArgumentException("dt must be positive.");
            int n = x.Length;
            float[] d = new float[n];
            if (n == 1) { d[0] = 0f; return d; }

            d[0]     = (x[1] - x[0]) / dt;
            d[n - 1] = (x[n - 1] - x[n - 2]) / dt;
            float twoDt = 2f * dt;
            for (int i = 1; i < n - 1; i++)
                d[i] = (x[i + 1] - x[i - 1]) / twoDt;
            return d;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  REAL-TIME HELPER
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Fixed-length causal moving average over a ring buffer.
        /// Used by the live state machine and by RobotInterface's default
        /// velocity derivation. Never used for reported metrics.</summary>
        public sealed class CausalMovingAverage
        {
            private readonly float[] _buf;
            private int   _count;
            private int   _head;
            private float _sum;

            public CausalMovingAverage(int length)
            {
                if (length < 1) throw new ArgumentException("length must be ≥ 1.");
                _buf = new float[length];
            }

            /// <summary>Push a sample; returns the mean of the last N samples
            /// (or of all samples pushed so far, while still filling).</summary>
            public float Push(float v)
            {
                if (_count < _buf.Length)
                {
                    _buf[_head] = v;
                    _sum += v;
                    _count++;
                }
                else
                {
                    _sum -= _buf[_head];
                    _buf[_head] = v;
                    _sum += v;
                }
                _head = (_head + 1) % _buf.Length;
                return _sum / _count;
            }

            public void Reset()
            {
                Array.Clear(_buf, 0, _buf.Length);
                _count = 0; _head = 0; _sum = 0f;
            }
        }
    }
}
