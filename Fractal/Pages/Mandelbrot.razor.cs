using System.Runtime.Versioning;
using Microsoft.JSInterop;
using Fractal.Services;
using Microsoft.AspNetCore.Components;

namespace Fractal.Pages;

[SupportedOSPlatform("browser")]
public partial class Mandelbrot
{
    private float DefaultMinReal { get; set; } = -2.5f;

    private float DefaultMaxReal { get; set; } = 1.0f;

    private float DefaultMinImaginary { get; set; } = -1.5f;

    private float DefaultMaxImaginary { get; set; } = 1.5f;

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
        //CalculateBounds();

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var script = await JS.InvokeAsync<IJSObjectReference>("import", "./pages/mandelbrot.razor.js");
        var dimensions = await script.InvokeAsync<Dimensions>("getSize", DotNetObjectReference.Create(this));
        Console.WriteLine($"[after render] Mandelbrot component initialized at {dimensions.Width}x{dimensions.Height}.");

        if (ClientSize.Width != dimensions.Width || ClientSize.Height != dimensions.Height)
        {
            ClientSize = dimensions;
            //CalculateBounds();
            StateHasChanged();
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
        throw new NotImplementedException();
    }
}
