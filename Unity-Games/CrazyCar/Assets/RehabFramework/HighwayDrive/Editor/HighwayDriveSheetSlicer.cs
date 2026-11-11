using System;
using System.Collections.Generic;

namespace RehabFramework.HighwayDrive.EditorTools
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE SHEET SLICER (pure pixel logic, no UnityEngine)
    //  Turns the ChatGPT-generated asset sheets -- single sprites padded with
    //  transparency, paired vertical strips, and multi-sprite prop sheets --
    //  into crop rectangles. Deliberately engine-free (plain arrays in,
    //  plain rects out) so the exact algorithm that will run inside the
    //  editor was first validated OFFLINE against the real PNGs, via a Mono
    //  harness using System.Drawing, before Unity ever executed it. Unity's
    //  editor-side wrapper (HighwayDriveArtImporter) supplies the alpha mask
    //  from Texture2D and writes the cropped PNGs; this class never touches
    //  either end.
    //
    //  Three modes, matching the three kinds of sheet in the pack:
    //   - ContentBounds: one sprite in a sea of transparent padding (cars,
    //     halo, every HUD element). One rect: the tight bounding box.
    //   - ColumnBands: vertically-continuous strips laid out side by side
    //     (the left/right verge strips, the paired edge lines, the dashed
    //     lane divider). Component analysis would shred a dashed line into
    //     one blob per dash, which is exactly wrong -- the dash RHYTHM is the
    //     asset. Projecting alpha onto the X axis and splitting on wide
    //     empty column gaps keeps each strip whole.
    //   - Components: genuinely separate props on one sheet (trees, rocks,
    //     flowers, landmark tiles). Flood fill with a gap tolerance, so a
    //     tree whose loose leaf pixels sit a few pixels off the trunk still
    //     comes out as one sprite, then size-filtered and reading-ordered.
    // ─────────────────────────────────────────────────────────────────────────

    public struct SliceRect
    {
        public int X, Y, W, H;
        public int Area;
        public SliceRect(int x, int y, int w, int h, int area) { X = x; Y = y; W = w; H = h; Area = area; }
    }

    public static class HighwayDriveSheetSlicer
    {
        /// <summary>Tight bounding box of every pixel whose alpha exceeds
        /// <paramref name="alphaThreshold"/>. Returns false when the image is
        /// entirely below the threshold. The threshold is per-call because the
        /// player car ships with a baked-in glow: cropping that car at the
        /// usual faint threshold would include the glow halo in the rect, and
        /// since the collision box is fitted to the rect, the VISIBLE car
        /// would then sit smaller than the box that can hit things -- the
        /// exact "collisions that visually did not occur" failure the design
        /// brief calls out. A high threshold crops to the solid body instead.</summary>
        public static bool TryContentBounds(byte[] alpha, int w, int h, byte alphaThreshold, out SliceRect rect)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1, area = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (alpha[row + x] <= alphaThreshold) continue;
                    area++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < 0)
            {
                rect = default;
                return false;
            }
            rect = new SliceRect(minX, minY, maxX - minX + 1, maxY - minY + 1, area);
            return true;
        }

        /// <summary>Splits a sheet into vertical bands wherever a run of at
        /// least <paramref name="minGapColumns"/> consecutive columns is
        /// (nearly) empty, then tight-crops each band. Left-to-right order.</summary>
        public static List<SliceRect> ColumnBands(byte[] alpha, int w, int h, byte alphaThreshold, int minGapColumns)
        {
            var columnHasContent = new bool[w];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (alpha[y * w + x] > alphaThreshold) { columnHasContent[x] = true; break; }
                }
            }

            var bands = new List<SliceRect>();
            int bandStart = -1;
            int gapRun = 0;
            for (int x = 0; x <= w; x++)
            {
                bool content = x < w && columnHasContent[x];
                if (content)
                {
                    if (bandStart < 0) bandStart = x;
                    gapRun = 0;
                    continue;
                }

                gapRun++;
                if (bandStart >= 0 && (gapRun >= minGapColumns || x == w))
                {
                    int bandEnd = x - gapRun; // last content column
                    if (bandEnd >= bandStart)
                        bands.Add(CropBandToContent(alpha, w, h, alphaThreshold, bandStart, bandEnd));
                    bandStart = -1;
                }
            }
            // A band still open at the right edge (gap never reached
            // minGapColumns before the loop ended at x == w handles it above
            // via x == w) -- nothing further needed.
            return bands;
        }

        private static SliceRect CropBandToContent(byte[] alpha, int w, int h, byte alphaThreshold, int x0, int x1)
        {
            int minY = h, maxY = -1, area = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = x0; x <= x1; x++)
                {
                    if (alpha[row + x] <= alphaThreshold) continue;
                    area++;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            return new SliceRect(x0, minY, x1 - x0 + 1, maxY - minY + 1, area);
        }

        /// <summary>Connected components over the alpha mask, where two pixels
        /// connect if they sit within <paramref name="gapTolerance"/> pixels of
        /// each other (Chebyshev) -- the gap-jump is what keeps one prop's
        /// slightly detached pixels (loose leaves, glow specks) in one blob.
        /// Components smaller than <paramref name="minArea"/> are dropped, the
        /// rest come back in reading order (row bands, then left to right),
        /// optionally truncated to the <paramref name="keepLargest"/> biggest
        /// before ordering (0 = keep all).</summary>
        public static List<SliceRect> Components(byte[] alpha, int w, int h, byte alphaThreshold,
            int gapTolerance, int minArea, int keepLargest)
        {
            var labels = new int[w * h]; // 0 = unvisited
            var result = new List<SliceRect>();
            var stack = new Stack<int>();
            int nextLabel = 1;

            for (int seed = 0; seed < alpha.Length; seed++)
            {
                if (labels[seed] != 0 || alpha[seed] <= alphaThreshold) continue;

                int minX = w, minY = h, maxX = -1, maxY = -1, area = 0;
                labels[seed] = nextLabel;
                stack.Push(seed);

                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    int px = p % w, py = p / w;
                    area++;
                    if (px < minX) minX = px;
                    if (px > maxX) maxX = px;
                    if (py < minY) minY = py;
                    if (py > maxY) maxY = py;

                    int yLo = Math.Max(0, py - gapTolerance), yHi = Math.Min(h - 1, py + gapTolerance);
                    int xLo = Math.Max(0, px - gapTolerance), xHi = Math.Min(w - 1, px + gapTolerance);
                    for (int ny = yLo; ny <= yHi; ny++)
                    {
                        int row = ny * w;
                        for (int nx = xLo; nx <= xHi; nx++)
                        {
                            int n = row + nx;
                            if (labels[n] != 0 || alpha[n] <= alphaThreshold) continue;
                            labels[n] = nextLabel;
                            stack.Push(n);
                        }
                    }
                }

                nextLabel++;
                if (area >= minArea)
                    result.Add(new SliceRect(minX, minY, maxX - minX + 1, maxY - minY + 1, area));
            }

            if (keepLargest > 0 && result.Count > keepLargest)
            {
                result.Sort((a, b) => b.Area.CompareTo(a.Area));
                result.RemoveRange(keepLargest, result.Count - keepLargest);
            }

            SortReadingOrder(result);
            return result;
        }

        /// <summary>Reading order: components whose vertical extents overlap
        /// meaningfully count as one row and sort left-to-right; otherwise
        /// top-to-bottom. A plain (Y, X) sort would interleave rows whenever
        /// one prop sits a few pixels higher than its row-mates, which these
        /// hand-arranged sheets do constantly.</summary>
        private static void SortReadingOrder(List<SliceRect> rects)
        {
            rects.Sort((a, b) =>
            {
                int aMid = a.Y + a.H / 2, bMid = b.Y + b.H / 2;
                bool sameRow = aMid < b.Y + b.H && bMid < a.Y + a.H;
                if (sameRow) return (a.X + a.W / 2).CompareTo(b.X + b.W / 2);
                return aMid.CompareTo(bMid);
            });
        }
    }
}
