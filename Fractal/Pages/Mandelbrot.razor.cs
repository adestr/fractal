using Fractal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Runtime.Versioning;

namespace Fractal.Pages;

[SupportedOSPlatform("browser")]
public partial class Mandelbrot : IAsyncDisposable
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

    private const int MinIterationLimit = 10;

    private const int MaxIterationLimit = 1000;

    private const int MinTileSize = 50;

    private const int MaxTileSize = 1000;

    private float MinReal { get; set; } = SettingsService.DefaultMinReal;

    private float MaxReal { get; set; } = SettingsService.DefaultMaxReal;

    private float MinImaginary { get; set; } = SettingsService.DefaultMinImaginary;

    private float MaxImaginary { get; set; } = SettingsService.DefaultMaxImaginary;

    private int _editIterationLimit = SettingsService.IterationLimit;

    private int _editTileSize = SettingsService.TileSize;

    private int _appliedTileSize = SettingsService.TileSize;

    private int _renderVersion;

    private bool _showControls;

    private bool _controlsDragBound;

    private string? _validationMessage;

    private ElementReference _panel;

    private ElementReference _handle;

    private IJSObjectReference? _pageModule;

    private IJSObjectReference? _serviceModule;

    private Dimensions ClientSize { get; set; } = new Dimensions { Width = 0, Height = 0 };

    private record class Dimensions
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    protected override async Task OnInitializedAsync()
    {
        var script = await PageModuleAsync();
        var dimensions = await script.InvokeAsync<Dimensions>("getSize");
        Console.WriteLine($"[initialised] Mandelbrot component initialized at {dimensions.Width}x{dimensions.Height}.");
        ClientSize = dimensions;
        CalculateBounds();

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var script = await PageModuleAsync();

        if (_showControls && !_controlsDragBound)
        {
            await script.InvokeVoidAsync("initControlsPanel", _panel, _handle);
            _controlsDragBound = true;
        }

        if (!_showControls)
        {
            _controlsDragBound = false;
        }

        var dimensions = await script.InvokeAsync<Dimensions>("getSize");
        Console.WriteLine($"[after render] Mandelbrot component initialized at {dimensions.Width}x{dimensions.Height}.");

        if (ClientSize.Width != dimensions.Width || ClientSize.Height != dimensions.Height)
        {
            //ClientSize = dimensions;
            //CalculateBounds();
            //StateHasChanged();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private void ToggleControls()
    {
        _showControls = !_showControls;
    }

    private async Task UpdateViewAsync()
    {
        _validationMessage = ValidateControls();
        if (_validationMessage is not null)
        {
            return;
        }

        SettingsService.IterationLimit = _editIterationLimit;
        SettingsService.TileSize = _editTileSize;
        _appliedTileSize = _editTileSize;

        await RefreshCalculationSettingsAsync();
        RebuildSections();
        _renderVersion++;
    }

    private string? ValidateControls()
    {
        if (float.IsNaN(MinReal) || float.IsNaN(MaxReal) || MinReal >= MaxReal)
        {
            return "Real minimum must be less than real maximum.";
        }

        if (float.IsNaN(MinImaginary) || float.IsNaN(MaxImaginary) || MinImaginary >= MaxImaginary)
        {
            return "Imaginary minimum must be less than imaginary maximum.";
        }

        if (_editIterationLimit < MinIterationLimit || _editIterationLimit > MaxIterationLimit)
        {
            return $"Iteration limit must be between {MinIterationLimit} and {MaxIterationLimit}.";
        }

        if (_editTileSize < MinTileSize || _editTileSize > MaxTileSize)
        {
            return $"Tile size must be between {MinTileSize} and {MaxTileSize} pixels.";
        }

        return null;
    }

    private async Task<IJSObjectReference> PageModuleAsync()
        => _pageModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./pages/mandelbrot.razor.js");

    private async Task RefreshCalculationSettingsAsync()
    {
        _serviceModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./Services/MandelbrotService.razor.js");
        await _serviceModule.InvokeVoidAsync("refreshSettings");
    }

    public async ValueTask DisposeAsync()
    {
        if (_pageModule is not null)
        {
            await _pageModule.DisposeAsync();
        }

        if (_serviceModule is not null)
        {
            await _serviceModule.DisposeAsync();
        }
    }

    /// <summary>
    /// Calculates the bounds of the Mandelbrot set based on the screen dimensions.
    /// </summary>
    /// <remarks>
    /// Ensures that we will fit the default min/max value inside the viewport.
    /// </remarks>
    private void CalculateBounds()
    {
        // Start by resetting to the defaults, so we're not merely adjusting the previous bounds.
        MinReal = SettingsService.DefaultMinReal;
        MaxReal = SettingsService.DefaultMaxReal;
        MinImaginary = SettingsService.DefaultMinImaginary;
        MaxImaginary = SettingsService.DefaultMaxImaginary;

        float deltaReal = MaxReal - MinReal;
        float deltaImaginary = MaxImaginary - MinImaginary;
        float aspect = deltaReal / deltaImaginary;
        float clientAspect = 1.0f * ClientSize.Width / ClientSize.Height;

        float ratio = aspect / clientAspect;

        // If ratio is greater than 1, the viewable area will contain more imaginary space
        // If ratio less than 1, the viewable area will contain more real space
        var targetDeltaImaginary = ratio > 1 ? deltaImaginary * ratio : deltaImaginary;
        var targetDeltaReal = ratio > 1 ? deltaReal : deltaReal / ratio;

        var adjustImaginary = (targetDeltaImaginary - deltaImaginary) / 2;
        var adjustReal = (targetDeltaReal - deltaReal) / 2;

        if (adjustImaginary > 0.01)
        {
            Console.WriteLine($"Adjusting imaginary bounds by {adjustImaginary} to fit aspect ratio.");
            MinImaginary -= adjustImaginary;
            MaxImaginary += adjustImaginary;
        }

        if (adjustReal > 0.01)
        {
            Console.WriteLine($"Adjusting real bounds by {adjustReal} to fit aspect ratio.");
            MinReal -= adjustReal;
            MaxReal += adjustReal;
        }

        RebuildSections();
    }

    /// <summary>
    /// Splits the current real and imaginary limits into tiles.
    /// </summary>
    private void RebuildSections()
    {
        int x = ClientSize.Width;
        int y = ClientSize.Height;
        int tileSize = Math.Max(1, SettingsService.TileSize);
        int xTiles = (int)Math.Ceiling((double)x / tileSize);
        int yTiles = (int)Math.Ceiling((double)y / tileSize);

        RowCount = yTiles;
        ColumnCount = xTiles;

        Sections = new Section[RowCount, ColumnCount];

        if (RowCount == 0 || ColumnCount == 0)
        {
            return;
        }

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

        Console.WriteLine($"Calculated bounds require {xTiles} x {yTiles} tiles.");
    }
}
