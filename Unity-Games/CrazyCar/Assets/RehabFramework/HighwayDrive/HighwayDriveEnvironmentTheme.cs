namespace RehabFramework.HighwayDrive
{
    /// <summary>The roadside environments HighwayDriveRoad can build. Each one
    /// is a complete modular art set under Art/ -- asphalt tile, dashed lane
    /// divider, edge lines, left/right verge strips, horizon backdrop, and its
    /// own trees/rocks/flora/signs.
    ///
    /// GREEN IS FIRST, and not just alphabetically: it is the reference
    /// environment (Simon, 2026-08-24) because the dark asphalt has the
    /// strongest contrast against the grass verge, so the drivable road reads
    /// unambiguously. Order also matters mechanically -- it is the order
    /// HighwayDriveControlMath.ThemeIndexForDay cycles through.
    ///
    /// The old City theme is gone: the replacement art pack ships Green,
    /// Desert and Snow only, and a theme with no art set behind it would fall
    /// back to procedural placeholder shapes in the middle of an otherwise
    /// finished-looking scene.</summary>
    public enum HighwayDriveEnvironmentTheme
    {
        Green,
        Desert,
        Snow,
    }

    /// <summary>How HighwayDriveRoad picks which HighwayDriveEnvironmentTheme
    /// is active for a session.</summary>
    public enum HighwayDriveEnvironmentThemeMode
    {
        /// <summary>Rotates by calendar day (HighwayDriveControlMath.
        /// ThemeIndexForDay) -- the "every day or so" behaviour.</summary>
        AutoRotateDaily,

        /// <summary>Always the same theme, from FixedTheme. The default while
        /// the three-lane green reference build is being finalised.</summary>
        Fixed,
    }
}
