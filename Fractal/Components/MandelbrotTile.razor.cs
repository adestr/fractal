using Fractal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Runtime.Versioning;

namespace Fractal.Components;

[SupportedOSPlatform("browser")]
public partial class MandelbrotTile
{
    private readonly string ElementId = $"mandelbrot-tile-{Guid.NewGuid()}";

    /// <summary>
    /// The number of pixels in the width and height of the tile.
    /// </summary>
    [Parameter]
    public int Size { get; set; } = SettingsService.TileSize;
    private int _renderedSize;

    /// <summary>
    /// The real part (x-axis) minimum value of the Mandelbrot set to render.
    /// </summary>
    [Parameter]
    public double RealMin { get; set; }
    private double _renderedMinReal;

    /// <summary>
    /// The real part (x-axis) maximum value of the Mandelbrot set to render.
    /// </summary>
    [Parameter]
    public double RealMax { get; set; }
    private double _renderedMaxReal;

    /// <summary>
    /// The imaginary part (y-axis) minimum value of the Mandelbrot set to render.
    /// </summary>
    [Parameter]
    public double ImaginaryMin { get; set; }
    private double _renderedMinImaginary;

    /// <summary>
    /// The imaginary part (y-axis) maximum value of the Mandelbrot set to render.
    /// </summary>
    [Parameter]
    public double ImaginaryMax { get; set; }
    private double _renderedMaxImaginary;

    private bool _renderRequested;

    private readonly Dictionary<string, object> attrs = new()
    {
        { "width", SettingsService.TileSize },
        { "height", SettingsService.TileSize }
    };

    protected override bool ShouldRender() => _renderRequested;

    protected override void OnParametersSet()
    {
        attrs["width"] = Size;
        attrs["height"] = Size;

        if (_renderedSize != Size || _renderedMinReal != RealMin || _renderedMaxReal != RealMax || _renderedMinImaginary != ImaginaryMin || _renderedMaxImaginary != ImaginaryMax)
        {
            _renderedSize = Size;
            _renderedMinReal = RealMin;
            _renderedMaxReal = RealMax;
            _renderedMinImaginary = ImaginaryMin;
            _renderedMaxImaginary = ImaginaryMax;
            _renderRequested = true;
        }

        base.OnParametersSet();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_renderRequested)
        {
            _renderRequested = false;
            if (Size > 0 && RealMax > RealMin && ImaginaryMax > ImaginaryMin)
            {
                await RenderTileAsync();
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        await MandelbrotService.InitialiseAsync();

        await base.OnInitializedAsync();
    }

    private async Task RenderTileAsync()
    {
        try
        {
            await MandelbrotService.CalculateAsync(ElementId, Size, Size, RealMin, RealMax, ImaginaryMin, ImaginaryMax);
        }
        catch (JSException ex) when (IsCancellation(ex))
        {
        }
    }

    private static bool IsCancellation(JSException ex)
        => ex.Message.Contains("AbortError", StringComparison.OrdinalIgnoreCase)
           || ex.Message.Contains("Cancelled", StringComparison.OrdinalIgnoreCase);
}
