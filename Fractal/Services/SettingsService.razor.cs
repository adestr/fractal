using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Fractal.Services;

[SupportedOSPlatform("browser")]
public partial class SettingsService
{
    /// <summary>
    /// All rendered tiles should be squares with sides <see cref="TileSize" /> long.
    /// </summary>
    public static int TileSize { get; set; } = 200;

    public static int IterationLimit { get; set; } = 200;

    /// <summary>
    /// True if you want purples and pinks to be at the top of the palette, false if you want them at the bottom.
    /// </summary>
    public static bool ReversePalette { get; set; } = false;

    /// <summary>
    /// The rendered area must include the range from <see cref="DefaultMinReal"/>
    ///  to <see cref="DefaultMaxReal"/> within the real space.
    /// </summary>
    public const float DefaultMinReal = -2.0f;

    /// <summary>
    /// The rendered area must include the range from <see cref="DefaultMinReal"/>
    ///  to <see cref="DefaultMaxReal"/> within the real space.
    /// </summary>
    public const float DefaultMaxReal = 1.0f;

    /// <summary>
    /// The rendered area must include the range from <see cref="DefaultMinImaginary"/>
    ///  to <see cref="DefaultMaxImaginary"/> within the imaginary space.
    /// </summary>
    public const float DefaultMinImaginary = -1.5f;

    /// <summary>
    /// The rendered area must include the range from <see cref="DefaultMinImaginary"/>
    ///  to <see cref="DefaultMaxImaginary"/> within the imaginary space.
    /// </summary>
    public const float DefaultMaxImaginary = 1.5f;

    [JSExport]
    public static int GetTileSize() => TileSize;

    [JSExport]
    public static int GetIterationLimit() => IterationLimit;
}
