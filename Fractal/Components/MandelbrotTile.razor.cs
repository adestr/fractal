using Fractal.Services;
using Microsoft.AspNetCore.Components;
using System.Runtime.Versioning;

namespace Fractal.Components;

[SupportedOSPlatform("browser")]
public partial class MandelbrotTile
{
    private readonly string ElementId = $"mandelbrot-tile-{Guid.NewGuid()}";

    public const int DefaultTileSize = 100;

    /// <summary>
    /// The number of pixels in the width and height of the tile.
    /// </summary>
    [Parameter]
    public int Size { get; set; } = DefaultTileSize;
    private int _renderedSize;

    /// <summary>
    /// The real part (x-axis) minimum value of the Mandelbrot set to render.
    /// </summary>
    [Parameter]
    public float RealMin { get; set; }
    private float _renderedMinReal;

    /// <summary>
    /// The real part (x-axis) maximum value of the Mandelbrot set to render.
    /// </summary>
    [Parameter]
    public float RealMax { get; set; }
    private float _renderedMaxReal;

    /// <summary>
    /// The imaginary part (y-axis) minimum value of the Mandelbrot set to render.
    /// </summary>
    [Parameter]
    public float ImaginaryMin { get; set; }
    private float _renderedMinImaginary;

    /// <summary>
    /// The imaginary part (y-axis) maximum value of the Mandelbrot set to render.
    /// </summary>
    [Parameter]
    public float ImaginaryMax { get; set; }
    private float _renderedMaxImaginary;

    private readonly Dictionary<string, object> attrs = new()
    {
        { "width", DefaultTileSize },
        { "height", DefaultTileSize }
    };

    protected override async Task OnParametersSetAsync()
    {
        if ((RealMin != default || RealMax != default) && (ImaginaryMin != default || ImaginaryMax != default))
        {
            await RenderTileAsync();
        }

        await base.OnParametersSetAsync();
    }

    protected override bool ShouldRender()
    {
        if (_renderedSize != Size || _renderedMinReal != RealMin || _renderedMaxReal != RealMax || _renderedMinImaginary != ImaginaryMin || _renderedMaxImaginary != ImaginaryMax)
        {
            _renderedSize = Size;
            _renderedMinReal = RealMin;
            _renderedMaxReal = RealMax;
            _renderedMinImaginary = ImaginaryMin;
            _renderedMaxImaginary = ImaginaryMax;

            // What we really want to do in this case is to re-generate the bitmap, then only re-render once the bitmap is ready.

            return true;
        }
        return false;
    }

    protected override void OnParametersSet()
    {
        // update attributes and pixel size whenever parameters change
        attrs["width"] = Size;
        attrs["height"] = Size;

        base.OnParametersSet();
    }

    protected override async Task OnInitializedAsync()
    {
        await MandelbrotService.InitialiseAsync();

        await base.OnInitializedAsync();
    }

    private async Task RenderTileAsync()
    {
        await MandelbrotService.CalculateAsync(ElementId, Size, Size, RealMin, RealMax, ImaginaryMin, ImaginaryMax);
    }
}
