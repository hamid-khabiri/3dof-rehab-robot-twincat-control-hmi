using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE SPRITE FACTORY
    //  Procedural placeholder shapes for every tangible element in the scene:
    //  the road surface, the shoulders, lane dashes, edge lines, roadside
    //  props, and the cars themselves. Simon's instruction was explicitly
    //  "just use square rectangle cars as placeholders", so that is all these
    //  are -- rectangles generated in code rather than art guessed at and
    //  dragged in. Same reasoning and same shape as
    //  RallyPaddleSpriteFactory.cs / ClearTheView's UISpriteFactory.cs.
    //
    //  Kept separate from those two (rather than reused) for the same reason
    //  they are separate from each other: these are world-space gameplay
    //  shapes sized in world units via pixelsPerUnit, and the set of shapes
    //  needed here (long thin dashes, car bodies) does not overlap theirs.
    //
    //  ORIENTATION CONVENTION: +Y is forward for every car in this scene, and
    //  a Texture2D's row 0 is its BOTTOM, so the FRONT of a car is the HIGH-y
    //  end of its texture.
    //
    //  THE MARKINGS EXIST TO MAKE THAT HEADING UNAMBIGUOUS, which is worth
    //  spelling out because the first version got it wrong in a way that is
    //  easy to repeat. It painted a single light band at 62-82% of the car's
    //  length and called it a windshield. On its own that band reads as a
    //  CABIN ROOF instead -- and a roof sits centre-REAR on a real car, so the
    //  eye infers the nose must be at the other end and the whole car looks
    //  like it is pointing DOWN the screen. Simon reported exactly that: the
    //  car appeared to be driving away from the top rather than towards it.
    //
    //  A single symmetric marking cannot fix this whichever end it sits at, so
    //  the car now carries three cues that only agree on one heading:
    //      - twin bright HEADLIGHTS in the leading edge (high y)
    //      - a windshield band immediately behind them
    //      - a solid red TAILLIGHT bar across the trailing edge (low y)
    //  Warm-white ahead and red behind is the one convention every driver
    //  already knows, so it survives being glanced at in peripheral vision
    //  while the patient is actually watching the road rather than the car.
    //  The two ends also differ in SHAPE, not just colour -- a split pair at
    //  the front against a continuous bar at the back -- so the cue does not
    //  collapse for a red-green colour-blind patient.
    // ─────────────────────────────────────────────────────────────────────────

    public static class HighwayDriveSpriteFactory
    {
        /// <summary>Solid filled rectangle. Road surface, shoulders, lane
        /// dashes, edge lines, roadside props, and UI gauge fills.</summary>
        public static Sprite BuildRectFill(int widthPx, int heightPx, float pixelsPerUnit, Color32 color)
        {
            widthPx = AtLeast(widthPx);
            heightPx = AtLeast(heightPx);

            var pixels = new Color32[widthPx * heightPx];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            return Finish(NewTex(widthPx, heightPx), pixels, widthPx, heightPx, pixelsPerUnit);
        }

        /// <summary>Rectangle outline. Used for the unlit distance lamps and
        /// the speed gauge's frame.</summary>
        public static Sprite BuildRectOutline(int widthPx, int heightPx, float thicknessFraction,
            float pixelsPerUnit, Color32 color)
        {
            widthPx = AtLeast(widthPx);
            heightPx = AtLeast(heightPx);

            float halfW = widthPx * 0.5f;
            float halfH = heightPx * 0.5f;
            float thickness = Mathf.Min(halfW, halfH) * Mathf.Clamp01(thicknessFraction);

            var pixels = new Color32[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                for (int x = 0; x < widthPx; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - halfW);
                    float dy = Mathf.Abs(y + 0.5f - halfH);
                    bool insideInner = dx <= halfW - thickness && dy <= halfH - thickness;
                    pixels[y * widthPx + x] = insideInner ? Transparent(color) : color;
                }
            }
            return Finish(NewTex(widthPx, heightPx), pixels, widthPx, heightPx, pixelsPerUnit);
        }

        /// <summary>Filled circle. Used for the player-car highlight glow
        /// (HighwayDriveController.BuildPlayerCar).</summary>
        public static Sprite BuildDisc(int diameterPx, float pixelsPerUnit, Color32 color)
        {
            diameterPx = AtLeast(diameterPx);
            float r = diameterPx * 0.5f;

            var pixels = new Color32[diameterPx * diameterPx];
            for (int y = 0; y < diameterPx; y++)
            {
                for (int x = 0; x < diameterPx; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    bool inside = (dx * dx + dy * dy) <= r * r;
                    pixels[y * diameterPx + x] = inside ? color : Transparent(color);
                }
            }
            return Finish(NewTex(diameterPx, diameterPx), pixels, diameterPx, diameterPx, pixelsPerUnit);
        }

        // ── Environment theme props (roadside scenery) ───────────────────
        //
        //  Same "plain rectangles, generated in code" philosophy as the car
        //  and the road surface, extended to three silhouettes -- a cactus, a
        //  building, and a pine tree -- so each of Highway Drive's three
        //  environment themes (Desert/City/Snow, see HighwayDriveRoad and
        //  HighwayDriveEnvironmentTheme) reads as visually distinct scenery,
        //  not just a different ground tint. No neighbour-pixel border
        //  detection here, unlike BuildCar's border -- these are small,
        //  distant, constantly-scrolling background elements, so a flat
        //  silhouette plus one internal accent is already legible and keeps
        //  each generator a single pass over the pixel grid.
        //
        //  All three are built ONCE per scene load at a fixed 1x1-world-unit
        //  template size and placed via Transform.localScale, exactly the
        //  technique HighwayDriveRoad.CreateSolid already uses for its shared
        //  white sprite -- see that method's own comment for why (batching:
        //  a handful of unique textures costs nothing extra here since only
        //  one shape is active per theme at a time).

        /// <summary>A single-arm saguaro silhouette: a vertical trunk with two
        /// side arms partway up, plus a darker rib line down the trunk's
        /// centre for a little texture. Desert theme.</summary>
        public static Sprite BuildCactus(int widthPx, int heightPx, float pixelsPerUnit, Color32 color)
        {
            widthPx = AtLeast(widthPx);
            heightPx = AtLeast(heightPx);
            Color32 rib = Shade(color, 0.65f);

            int trunkW = Mathf.Max(2, Mathf.RoundToInt(widthPx * 0.30f));
            int trunkX0 = (widthPx - trunkW) / 2;
            int trunkX1 = trunkX0 + trunkW;

            int armW = Mathf.Max(2, Mathf.RoundToInt(widthPx * 0.16f));
            int armH = Mathf.Max(2, Mathf.RoundToInt(heightPx * 0.22f));
            int armY0 = Mathf.RoundToInt(heightPx * 0.40f);
            int armY1 = armY0 + armH;
            int leftArmX0 = Mathf.RoundToInt(widthPx * 0.06f);
            int leftArmX1 = leftArmX0 + armW;
            int rightArmX1 = widthPx - Mathf.RoundToInt(widthPx * 0.06f);
            int rightArmX0 = rightArmX1 - armW;

            int ribX0 = trunkX0 + trunkW / 2 - 1;
            int ribX1 = ribX0 + 2;

            var pixels = new Color32[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                for (int x = 0; x < widthPx; x++)
                {
                    bool inTrunk = x >= trunkX0 && x < trunkX1;
                    bool inLeftArm = x >= leftArmX0 && x < leftArmX1 && y >= armY0 && y < armY1;
                    bool inRightArm = x >= rightArmX0 && x < rightArmX1 && y >= armY0 && y < armY1;

                    if (!inTrunk && !inLeftArm && !inRightArm)
                    {
                        pixels[y * widthPx + x] = Transparent(color);
                        continue;
                    }

                    bool onRib = inTrunk && x >= ribX0 && x < ribX1;
                    pixels[y * widthPx + x] = onRib ? rib : color;
                }
            }
            return Finish(NewTex(widthPx, heightPx), pixels, widthPx, heightPx, pixelsPerUnit);
        }

        /// <summary>A flat-fronted building: a solid body with a bordered
        /// outline and a grid of lit windows. City theme.</summary>
        public static Sprite BuildBuilding(int widthPx, int heightPx, float pixelsPerUnit, Color32 body, Color32 window)
        {
            widthPx = AtLeast(widthPx);
            heightPx = AtLeast(heightPx);
            Color32 border = Shade(body, 0.55f);

            int borderPx = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(widthPx, heightPx) * 0.05f));

            const int cols = 2;
            const int rows = 4;
            int marginX = Mathf.RoundToInt(widthPx * 0.14f);
            int gapX = Mathf.RoundToInt(widthPx * 0.10f);
            int gapY = Mathf.RoundToInt(heightPx * 0.06f);
            int topMargin = Mathf.RoundToInt(heightPx * 0.08f);
            int bottomMargin = Mathf.RoundToInt(heightPx * 0.10f);

            int cellW = Mathf.Max(1, (widthPx - marginX * 2 - gapX * (cols - 1)) / cols);
            int cellH = Mathf.Max(1, (heightPx - topMargin - bottomMargin - gapY * (rows - 1)) / rows);

            var pixels = new Color32[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                for (int x = 0; x < widthPx; x++)
                {
                    bool onBorder = x < borderPx || x >= widthPx - borderPx
                                 || y < borderPx || y >= heightPx - borderPx;
                    if (onBorder)
                    {
                        pixels[y * widthPx + x] = border;
                        continue;
                    }

                    bool onWindow = false;
                    for (int row = 0; row < rows && !onWindow; row++)
                    {
                        int cellY0 = heightPx - topMargin - (row + 1) * cellH - row * gapY;
                        int cellY1 = cellY0 + cellH;
                        if (y < cellY0 || y >= cellY1) continue;

                        for (int col = 0; col < cols; col++)
                        {
                            int cellX0 = marginX + col * (cellW + gapX);
                            if (x >= cellX0 && x < cellX0 + cellW) { onWindow = true; break; }
                        }
                    }
                    pixels[y * widthPx + x] = onWindow ? window : body;
                }
            }
            return Finish(NewTex(widthPx, heightPx), pixels, widthPx, heightPx, pixelsPerUnit);
        }

        /// <summary>A conifer silhouette: a single tapering triangle of
        /// foliage over a short trunk, with a lighter snow-dusted tint on the
        /// tip. Snow theme.</summary>
        public static Sprite BuildPineTree(int widthPx, int heightPx, float pixelsPerUnit, Color32 foliage)
        {
            widthPx = AtLeast(widthPx);
            heightPx = AtLeast(heightPx);
            Color32 trunk = new Color32(90, 62, 40, 255);
            Color32 cap = Shade(foliage, 1.5f);

            int trunkW = Mathf.Max(2, Mathf.RoundToInt(widthPx * 0.14f));
            int trunkX0 = (widthPx - trunkW) / 2;
            int trunkX1 = trunkX0 + trunkW;
            int trunkH = Mathf.Max(1, Mathf.RoundToInt(heightPx * 0.16f));

            float maxHalfW = widthPx * 0.42f;
            int capLowY = Mathf.RoundToInt(heightPx * 0.86f);
            int cx = widthPx / 2;
            int triangleSpan = Mathf.Max(1, heightPx - trunkH);

            var pixels = new Color32[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                if (y < trunkH)
                {
                    for (int x = 0; x < widthPx; x++)
                        pixels[y * widthPx + x] = (x >= trunkX0 && x < trunkX1) ? trunk : Transparent(foliage);
                    continue;
                }

                float t = (float)(y - trunkH) / triangleSpan;
                int halfW = Mathf.RoundToInt(maxHalfW * (1f - t));
                int xLo = cx - halfW;
                int xHi = cx + halfW;
                Color32 fill = y >= capLowY ? cap : foliage;

                for (int x = 0; x < widthPx; x++)
                    pixels[y * widthPx + x] = (x >= xLo && x < xHi) ? fill : Transparent(foliage);
            }
            return Finish(NewTex(widthPx, heightPx), pixels, widthPx, heightPx, pixelsPerUnit);
        }

        /// <summary>A placeholder car pointing towards +Y: filled rectangle,
        /// darker border, and the three directional cues described in this
        /// class's header -- headlights at the leading edge, windshield behind
        /// them, red taillight bar at the trailing edge.
        ///
        /// The border does real work beyond outlining: it keeps two cars of
        /// similar colour from merging into one blob when they sit side by
        /// side in neighbouring lanes.</summary>
        public static Sprite BuildCar(int widthPx, int heightPx, float pixelsPerUnit, Color32 body)
        {
            widthPx = AtLeast(widthPx);
            heightPx = AtLeast(heightPx);

            Color32 border = Shade(body, 0.45f);
            Color32 glass = Shade(body, 1.9f);
            Color32 headlight = new Color32(255, 246, 214, 255);  // warm white, front
            Color32 taillight = new Color32(198, 46, 38, 255);    // red, rear

            int borderPx = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(widthPx, heightPx) * 0.06f));

            // Bands as fractions of the car's LENGTH. Front is high y.
            int headlightLowY = Mathf.RoundToInt(heightPx * 0.86f);
            int glassLowY = Mathf.RoundToInt(heightPx * 0.58f);
            int glassHighY = Mathf.RoundToInt(heightPx * 0.78f);
            int taillightHighY = Mathf.RoundToInt(heightPx * 0.12f);

            int lampInsetX = Mathf.RoundToInt(widthPx * 0.13f);
            int lampWidthX = Mathf.RoundToInt(widthPx * 0.27f);
            int glassInsetX = Mathf.RoundToInt(widthPx * 0.18f);

            var pixels = new Color32[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                for (int x = 0; x < widthPx; x++)
                {
                    Color32 c;
                    bool onBorder = x < borderPx || x >= widthPx - borderPx
                                 || y < borderPx || y >= heightPx - borderPx;

                    if (onBorder)
                    {
                        c = border;
                    }
                    else if (y >= headlightLowY
                             && ((x >= lampInsetX && x < lampInsetX + lampWidthX)
                              || (x >= widthPx - lampInsetX - lampWidthX && x < widthPx - lampInsetX)))
                    {
                        c = headlight;
                    }
                    else if (y < taillightHighY && x >= lampInsetX && x < widthPx - lampInsetX)
                    {
                        c = taillight;
                    }
                    else if (y >= glassLowY && y < glassHighY
                             && x >= glassInsetX && x < widthPx - glassInsetX)
                    {
                        c = glass;
                    }
                    else
                    {
                        c = body;
                    }

                    pixels[y * widthPx + x] = c;
                }
            }
            return Finish(NewTex(widthPx, heightPx), pixels, widthPx, heightPx, pixelsPerUnit);
        }

        // ── Hearts (Simon's request, 2026-08-19) ──────────────────────────
        //
        //  Two circular lobes plus a tapering triangular point, the same
        //  primitive-composition style as BuildCactus/BuildPineTree above
        //  rather than an implicit heart-curve formula, which looks worse at
        //  small pixel sizes than it does in a textbook. One filled variant
        //  (the world-space pickup, HighwayDriveHearts, and the patient UI's
        //  EARNED heart slots) and one outline variant (the UI's UNEARNED
        //  slots) -- same Fill/Outline pairing this file already uses for
        //  rects (BuildRectFill/BuildRectOutline), so the "earned = solid,
        //  unearned = hollow" convention reads the same way it already does
        //  everywhere else in this project (RallyPaddle's progress lamps,
        //  this project's own now-removed distance lamps).

        /// <summary>Filled heart, with a small highlight accent in the upper-
        /// left lobe for a "collectible" gloss -- same one-accent-per-shape
        /// restraint as BuildCactus's rib / BuildPineTree's snow cap.</summary>
        public static Sprite BuildHeart(int widthPx, int heightPx, float pixelsPerUnit, Color32 color)
        {
            widthPx = AtLeast(widthPx);
            heightPx = AtLeast(heightPx);

            HeartGeometry g = new HeartGeometry(widthPx, heightPx);
            Color32 highlight = Shade(color, 1.6f);
            float hlCx = g.LeftLobeCx - g.LobeRadius * 0.28f;
            float hlCy = g.LobeCy + g.LobeRadius * 0.30f;
            float hlR = g.LobeRadius * 0.32f;

            var pixels = new Color32[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                for (int x = 0; x < widthPx; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    if (!g.Contains(px, py, 0f))
                    {
                        pixels[y * widthPx + x] = Transparent(color);
                        continue;
                    }

                    float dhx = px - hlCx, dhy = py - hlCy;
                    bool onHighlight = dhx * dhx + dhy * dhy <= hlR * hlR;
                    pixels[y * widthPx + x] = onHighlight ? highlight : color;
                }
            }
            return Finish(NewTex(widthPx, heightPx), pixels, widthPx, heightPx, pixelsPerUnit);
        }

        /// <summary>Outline-only heart -- same silhouette as BuildHeart with an
        /// inset copy of itself cut out of the middle, the same "outer AND NOT
        /// inner" technique BuildRectOutline uses.</summary>
        public static Sprite BuildHeartOutline(int widthPx, int heightPx, float thicknessFraction,
            float pixelsPerUnit, Color32 color)
        {
            widthPx = AtLeast(widthPx);
            heightPx = AtLeast(heightPx);

            HeartGeometry g = new HeartGeometry(widthPx, heightPx);
            float thickness = Mathf.Min(widthPx, heightPx) * Mathf.Clamp01(thicknessFraction);

            var pixels = new Color32[widthPx * heightPx];
            for (int y = 0; y < heightPx; y++)
            {
                for (int x = 0; x < widthPx; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    bool onOutline = g.Contains(px, py, 0f) && !g.Contains(px, py, thickness);
                    pixels[y * widthPx + x] = onOutline ? color : Transparent(color);
                }
            }
            return Finish(NewTex(widthPx, heightPx), pixels, widthPx, heightPx, pixelsPerUnit);
        }

        /// <summary>Shared geometry for BuildHeart/BuildHeartOutline: two
        /// circular lobes (top) plus a triangular wedge tapering to a point
        /// (bottom), unioned. Contains(x, y, inset) shrinks both the lobe
        /// radius and the wedge's half-width by inset before testing -- an
        /// approximate but visually adequate uniform erosion, the same
        /// "inset by a flat amount" approach BuildRectOutline already uses
        /// rather than a true signed-distance stroke, which would be a lot of
        /// machinery for a handful of small placeholder icons.</summary>
        private readonly struct HeartGeometry
        {
            public readonly float LeftLobeCx, RightLobeCx, LobeCy, LobeRadius, Cx, WedgeHalfWidthAtLobes;

            public HeartGeometry(int widthPx, int heightPx)
            {
                LobeRadius = widthPx * 0.28f;
                LobeCy = heightPx * 0.66f;
                LeftLobeCx = widthPx * 0.30f;
                RightLobeCx = widthPx * 0.70f;
                Cx = widthPx * 0.5f;
                WedgeHalfWidthAtLobes = widthPx * 0.42f;
            }

            public bool Contains(float px, float py, float inset)
            {
                float r = LobeRadius - inset;
                if (r > 0f)
                {
                    float dxL = px - LeftLobeCx, dyL = py - LobeCy;
                    if (dxL * dxL + dyL * dyL <= r * r) return true;
                    float dxR = px - RightLobeCx, dyR = py - LobeCy;
                    if (dxR * dxR + dyR * dyR <= r * r) return true;
                }

                float halfWAtLobes = WedgeHalfWidthAtLobes - inset;
                if (py <= LobeCy && halfWAtLobes > 0f)
                {
                    float t = Mathf.Clamp01(py / LobeCy);
                    float halfW = halfWAtLobes * t;
                    if (Mathf.Abs(px - Cx) <= halfW) return true;
                }
                return false;
            }
        }

        // ── shared helpers ──────────────────────────────────────────────

        private static int AtLeast(int px) => px < 2 ? 2 : px;

        private static Texture2D NewTex(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        private static Color32 Transparent(Color32 c) => new Color32(c.r, c.g, c.b, 0);

        /// <summary>Multiplies a colour's brightness, clamped to the byte
        /// range. Factors below 1 darken (the car border), above 1 lighten
        /// (the windshield band).</summary>
        private static Color32 Shade(Color32 c, float factor)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * factor), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * factor), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * factor), 0, 255),
                c.a);
        }

        private static Sprite Finish(Texture2D tex, Color32[] pixels, int w, int h, float pixelsPerUnit)
        {
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        // ── Real-art sizing (car_icons) ───────────────────────────────────
        //
        //  The procedural sprites above are always generated at exactly the
        //  target world size (ppu chosen specifically so the texture already
        //  renders at that size), so nothing has ever needed to scale one
        //  after assignment. Imported art (car_icons/*.png) has its own fixed
        //  pixel dimensions that do not know about any particular gameplay
        //  box, so this is the one place both paths meet: FitToWorldSize
        //  computes whatever Transform.localScale makes a Sprite -- real or
        //  procedural -- render at a given world size, using the sprite's own
        //  rect and pixelsPerUnit rather than any hardcoded aspect. Calling it
        //  on a procedural sprite is a harmless no-op (scale comes out at
        //  (1,1) by construction); calling it on real art is what actually
        //  does the work.

        /// <summary>Scales sr's Transform so its sprite renders at exactly
        /// worldSize (world units), regardless of the sprite's own pixel
        /// dimensions or pixelsPerUnit. Safe to call every time a
        /// SpriteRenderer's sprite is (re)assigned -- e.g. HighwayDriveTraffic
        /// reassigning a pooled car's sprite to a different colour variant on
        /// every respawn.</summary>
        public static void FitToWorldSize(SpriteRenderer sr, Vector2 worldSize)
        {
            if (sr == null || sr.sprite == null) return;

            Vector2 native = sr.sprite.rect.size / sr.sprite.pixelsPerUnit;
            if (native.x <= 0f || native.y <= 0f) return;

            sr.transform.localScale = new Vector3(worldSize.x / native.x, worldSize.y / native.y, 1f);
        }
    }
}
