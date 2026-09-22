using Fractal.Services;
using Microsoft.AspNetCore.Components;
using SkiaSharp;
using SkiaSharp.Views.Blazor;
using System.Runtime.Versioning;

namespace Fractal.Components;

[SupportedOSPlatform("browser")]
public partial class MandelbrotTile
{
    public const int DefaultTileSize = 100;

    private const int MaxIterations = 200;

    SKCanvasView? CanvasView { get; set; }

    private SKBitmap? bitmap = null;

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

    private void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        if (bitmap == null)
        {
            // TODO: Dark mode!
            canvas.Clear(SKColors.White);
        }
        else
        {
            canvas.DrawBitmap(bitmap, new SKRect(0, 0, e.Info.Width, e.Info.Height), new SKRect(0, 0, Size, Size), SKSamplingOptions.Default);
        }
    }

    private async Task RenderTileAsync()
    {
        if (bitmap == null)
        {
            int[] heights = await MandelbrotService.CalculateAsync(Size, Size, RealMin, RealMax, ImaginaryMin, ImaginaryMax);

            SKBitmap tmp = new(Size, Size);
            for (int i = 0; i < Size; i++)
            {
                for (int j = 0; j < Size; j++)
                {
                    int height = heights?[i * Size + j] ?? 0;
                    var (r, g, b) = GetColour(height);
                    tmp.SetPixel(i, j, new SKColor(r, g, b));
                }
            }
            bitmap = tmp;
            CanvasView?.Invalidate();
        }
    }

    private static (byte r, byte g, byte b) GetColour(int mandelbrotValue)
    {
        byte r = (byte)(mandelbrotValue % 256);
        byte g = (byte)(mandelbrotValue * 2 % 256);
        byte b = (byte)(mandelbrotValue * 3 % 256);

        return mandelbrotValue < MaxIterations ? (r, g, b) : ((byte)0, (byte)0, (byte)0);
    }
}
