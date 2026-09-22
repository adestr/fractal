using Microsoft.JSInterop;
using System.Runtime.Versioning;

namespace Fractal.Pages;

[SupportedOSPlatform("browser")]
public partial class Mandelbrot
{
    private int RowCount { get; set; } = 0;

    private int ColumnCount { get; set; } = 0;

    private record class Section
    {
        public float MinReal { get; set; }
        public float MaxReal { get; set; }
        public float MinImaginary { get; set; }
        public float MaxImaginary { get; set; }
    }

    private Section[,] Sections { get; set; } = new Section[0, 0];

    private float MinReal { get; set; } = -2.5f;

    private float MaxReal { get; set; } = 1.0f;

    private float MinImaginary { get; set; } = -1.5f;

    private float MaxImaginary { get; set; } = 1.5f;

    private int TileSize { get; set; } = 100;

    private Dimensions ClientSize { get; set; } = new Dimensions { Width = 0, Height = 0 };

    private record class Dimensions
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    protected override async Task OnInitializedAsync()
    {
        var script = await JS.InvokeAsync<IJSObjectReference>("import", "./pages/mandelbrot.razor.js");
        var dimensions = await script.InvokeAsync<Dimensions>("getSize", DotNetObjectReference.Create(this));
        Console.WriteLine($"[initialised] Mandelbrot component initialized at {dimensions.Width}x{dimensions.Height}.");
        ClientSize = dimensions;
        CalculateBounds();

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var script = await JS.InvokeAsync<IJSObjectReference>("import", "./pages/mandelbrot.razor.js");
        var dimensions = await script.InvokeAsync<Dimensions>("getSize", DotNetObjectReference.Create(this));
        Console.WriteLine($"[after render] Mandelbrot component initialized at {dimensions.Width}x{dimensions.Height}.");

        if (ClientSize.Width != dimensions.Width || ClientSize.Height != dimensions.Height)
        {
            //ClientSize = dimensions;
            //CalculateBounds();
            //StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    /// <summary>
    /// Calculates the bounds of the Mandelbrot set based on the screen dimensions.
    /// </summary>
    /// <remarks>
    /// Ensures that we will fit the default min/max value inside the viewport.
    /// </remarks>
    private void CalculateBounds()
    {
        int x = ClientSize.Width;
        int y = ClientSize.Height;
        int xTiles = (int)Math.Ceiling((double)x / TileSize);
        int yTiles = (int)Math.Ceiling((double)y / TileSize);

        RowCount = yTiles;
        ColumnCount = xTiles;
        Sections = new Section[RowCount, ColumnCount];

        for (int i = 0; i < RowCount; i++)
        {
            for (int j = 0; j < ColumnCount; j++)
            {
                float rmin = MinReal + (j * (MaxReal - MinReal) / ColumnCount);
                float rmax = MinReal + ((j + 1) * (MaxReal - MinReal) / ColumnCount);
                float imin = MinImaginary + (i * (MaxImaginary - MinImaginary) / RowCount);
                float imax = MinImaginary + ((i + 1) * (MaxImaginary - MinImaginary) / RowCount);

                Sections[i, j] = new Section
                {
                    MinReal = rmin,
                    MaxReal = rmax,
                    MinImaginary = imin,
                    MaxImaginary = imax
                };
            }
        }

        Console.WriteLine($"Calculating bounds for {xTiles}x{yTiles} tiles.");
    }
}
