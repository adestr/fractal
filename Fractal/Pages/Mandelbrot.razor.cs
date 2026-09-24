using Fractal.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Runtime.Versioning;

namespace Fractal.Pages;

[SupportedOSPlatform("browser")]
public partial class Mandelbrot : IAsyncDisposable
{
    private readonly List<TileModel> _tiles = new();

    private sealed class TileModel
    {
        public int LayoutVersion { get; init; }
        public int Column { get; init; }
        public int Row { get; init; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double MinReal { get; init; }
        public double MaxReal { get; init; }
        public double MinImaginary { get; init; }
        public double MaxImaginary { get; init; }
        public string Key => $"{LayoutVersion}:{Column}:{Row}";
    }

    private const int MinIterationLimit = 10;

    private const int MaxIterationLimit = 1000;

    private const int MinTileSize = 50;

    private const int MaxTileSize = 1000;

    private double MinReal { get; set; } = SettingsService.DefaultMinReal;

    private double MaxReal { get; set; } = SettingsService.DefaultMaxReal;

    private double MinImaginary { get; set; } = SettingsService.DefaultMinImaginary;

    private double MaxImaginary { get; set; } = SettingsService.DefaultMaxImaginary;

    private int _editIterationLimit = SettingsService.IterationLimit;

    private int _editTileSize = SettingsService.TileSize;

    private int _appliedTileSize = SettingsService.TileSize;

    private int _layoutVersion;

    private bool _viewReady;

    private bool _showControls;

    private bool _controlsDragBound;

    private string? _validationMessage;

    private double _originX;

    private double _originY;

    private double _snapshotX;

    private double _snapshotY;

    private string SnapshotStyle
        => Math.Abs(_snapshotX) < 0.001 && Math.Abs(_snapshotY) < 0.001
            ? "transform:none"
            : FormattableString.Invariant($"transform:translate({_snapshotX:0.####}px,{_snapshotY:0.####}px)");

    private ElementReference _viewport;

    private ElementReference _stage;

    private ElementReference _snapshot;

    private ElementReference _panel;

    private ElementReference _handle;

    private IJSObjectReference? _pageModule;

    private IJSObjectReference? _serviceModule;

    private DotNetObjectReference<Mandelbrot>? _self;

    private Dimensions ClientSize { get; set; } = new Dimensions { Width = 0, Height = 0 };

    private record class Dimensions
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var script = await PageModuleAsync();

        if (firstRender)
        {
            _self = DotNetObjectReference.Create(this);
            await script.InvokeVoidAsync("initViewport", _viewport, _stage, _snapshot, _self);
        }

        if (_showControls && !_controlsDragBound)
        {
            await script.InvokeVoidAsync("initControlsPanel", _panel, _handle);
            _controlsDragBound = true;
        }

        if (!_showControls)
        {
            _controlsDragBound = false;
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
        await MandelbrotService.CancelAsync();
        if (_pageModule is not null)
        {
            await _pageModule.InvokeVoidAsync("prepareForRelayout");
        }

        _snapshotX = 0;
        _snapshotY = 0;
        LayoutTilesForView();
    }

    private string? ValidateControls()
    {
        if (double.IsNaN(MinReal) || double.IsNaN(MaxReal) || MinReal >= MaxReal)
        {
            return "Real minimum must be less than real maximum.";
        }

        if (double.IsNaN(MinImaginary) || double.IsNaN(MaxImaginary) || MinImaginary >= MaxImaginary)
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

    [JSInvokable]
    public Task CancelRendering() => MandelbrotService.CancelAsync();

    /// <summary>
    /// Shifts the visible window with the pointer and keeps tiles covering the viewport.
    /// </summary>
    [JSInvokable]
    public void ApplyPan(double dx, double dy)
    {
        if (!_viewReady || ClientSize.Width <= 0 || ClientSize.Height <= 0 || _tiles.Count == 0)
        {
            return;
        }

        if (dx == 0 && dy == 0)
        {
            return;
        }

        double spanReal = MaxReal - MinReal;
        double spanImag = MaxImaginary - MinImaginary;
        MinReal += -dx / ClientSize.Width * spanReal;
        MaxReal += -dx / ClientSize.Width * spanReal;
        MinImaginary += -dy / ClientSize.Height * spanImag;
        MaxImaginary += -dy / ClientSize.Height * spanImag;

        _originX += dx;
        _originY += dy;
        _snapshotX += dx;
        _snapshotY += dy;

        foreach (var tile in _tiles)
        {
            tile.Left += dx;
            tile.Top += dy;
        }

        _validationMessage = null;
        EnsureTileCoverage();
        StateHasChanged();
    }

    /// <summary>
    /// Replaces the visible window after a zoom gesture. The anchor pixel stays on the same complex value.
    /// </summary>
    [JSInvokable]
    public void CommitZoom(double anchorX, double anchorY, double scale, double translateX, double translateY)
    {
        if (!_viewReady || ClientSize.Width <= 0 || ClientSize.Height <= 0 || scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return;
        }

        double oldMinReal = MinReal;
        double oldMinImag = MinImaginary;
        double spanReal = MaxReal - MinReal;
        double spanImag = MaxImaginary - MinImaginary;
        if (spanReal <= 0 || spanImag <= 0)
        {
            return;
        }

        double ContentX(double screenX) => (screenX - anchorX - translateX) / scale + anchorX;
        double ContentY(double screenY) => (screenY - anchorY - translateY) / scale + anchorY;
        double RealFromPixel(double pixelX) => oldMinReal + pixelX / ClientSize.Width * spanReal;
        double ImagFromPixel(double pixelY) => oldMinImag + pixelY / ClientSize.Height * spanImag;

        double nextMinReal = RealFromPixel(ContentX(0));
        double nextMaxReal = RealFromPixel(ContentX(ClientSize.Width));
        double nextMinImag = ImagFromPixel(ContentY(0));
        double nextMaxImag = ImagFromPixel(ContentY(ClientSize.Height));
        if (nextMaxReal <= nextMinReal || nextMaxImag <= nextMinImag || double.IsNaN(nextMinReal) || double.IsNaN(nextMinImag))
        {
            return;
        }

        MinReal = nextMinReal;
        MaxReal = nextMaxReal;
        MinImaginary = nextMinImag;
        MaxImaginary = nextMaxImag;
        _snapshotX = 0;
        _snapshotY = 0;
        _validationMessage = null;
        LayoutTilesForView();
        StateHasChanged();
    }

    [JSInvokable]
    public void OnViewportResize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (_viewReady && ClientSize.Width == width && ClientSize.Height == height)
        {
            return;
        }

        if (!_viewReady)
        {
            ClientSize = new Dimensions { Width = width, Height = height };
            CalculateBounds();
            _viewReady = true;
        }
        else
        {
            double centerReal = (MinReal + MaxReal) / 2;
            double centerImag = (MinImaginary + MaxImaginary) / 2;
            double realPerPixel = (MaxReal - MinReal) / Math.Max(1, ClientSize.Width);
            double imagPerPixel = (MaxImaginary - MinImaginary) / Math.Max(1, ClientSize.Height);
            ClientSize = new Dimensions { Width = width, Height = height };
            MinReal = centerReal - realPerPixel * width / 2;
            MaxReal = centerReal + realPerPixel * width / 2;
            MinImaginary = centerImag - imagPerPixel * height / 2;
            MaxImaginary = centerImag + imagPerPixel * height / 2;
            _snapshotX = 0;
            _snapshotY = 0;
            LayoutTilesForView();
        }

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_pageModule is not null)
        {
            try
            {
                await _pageModule.InvokeVoidAsync("disposeViewport");
            }
            catch (JSDisconnectedException)
            {
            }

            await _pageModule.DisposeAsync();
        }

        _self?.Dispose();

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
        MinReal = SettingsService.DefaultMinReal;
        MaxReal = SettingsService.DefaultMaxReal;
        MinImaginary = SettingsService.DefaultMinImaginary;
        MaxImaginary = SettingsService.DefaultMaxImaginary;

        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        double deltaReal = MaxReal - MinReal;
        double deltaImaginary = MaxImaginary - MinImaginary;
        double aspect = deltaReal / deltaImaginary;
        double clientAspect = (double)ClientSize.Width / ClientSize.Height;
        double ratio = aspect / clientAspect;

        double targetDeltaImaginary = ratio > 1 ? deltaImaginary * ratio : deltaImaginary;
        double targetDeltaReal = ratio > 1 ? deltaReal : deltaReal / ratio;
        double adjustImaginary = (targetDeltaImaginary - deltaImaginary) / 2;
        double adjustReal = (targetDeltaReal - deltaReal) / 2;

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

        LayoutTilesForView();
    }

    /// <summary>
    /// Places a tile lattice over the viewport so the leftover pixels are split equally on the left and right,
    /// and equally on the top and bottom.
    /// </summary>
    private void LayoutTilesForView()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            _tiles.Clear();
            return;
        }

        _layoutVersion++;
        int tileSize = Math.Max(1, _appliedTileSize);
        int columns = Math.Max(1, (int)Math.Ceiling(ClientSize.Width / (double)tileSize));
        int rows = Math.Max(1, (int)Math.Ceiling(ClientSize.Height / (double)tileSize));
        _originX = -(columns * (double)tileSize - ClientSize.Width) / 2;
        _originY = -(rows * (double)tileSize - ClientSize.Height) / 2;
        _tiles.Clear();
        EnsureTileCoverage();
        Console.WriteLine($"Calculated bounds require {columns} x {rows} tiles.");
    }

    private void EnsureTileCoverage()
    {
        int tileSize = Math.Max(1, _appliedTileSize);
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        int colMin = (int)Math.Floor(-_originX / tileSize);
        int colMax = (int)Math.Floor((ClientSize.Width - 1e-6 - _originX) / tileSize);
        int rowMin = (int)Math.Floor(-_originY / tileSize);
        int rowMax = (int)Math.Floor((ClientSize.Height - 1e-6 - _originY) / tileSize);

        _tiles.RemoveAll(tile => tile.Column < colMin || tile.Column > colMax || tile.Row < rowMin || tile.Row > rowMax);

        var present = new HashSet<(int Column, int Row)>(_tiles.Count);
        foreach (var tile in _tiles)
        {
            present.Add((tile.Column, tile.Row));
        }

        for (int row = rowMin; row <= rowMax; row++)
        {
            for (int column = colMin; column <= colMax; column++)
            {
                if (present.Contains((column, row)))
                {
                    continue;
                }

                _tiles.Add(CreateTile(column, row, tileSize));
            }
        }
    }

    private TileModel CreateTile(int column, int row, int tileSize)
    {
        double left = _originX + column * tileSize;
        double top = _originY + row * tileSize;
        return new TileModel
        {
            LayoutVersion = _layoutVersion,
            Column = column,
            Row = row,
            Left = left,
            Top = top,
            MinReal = RealAt(left),
            MaxReal = RealAt(left + tileSize),
            MinImaginary = ImagAt(top),
            MaxImaginary = ImagAt(top + tileSize),
        };
    }

    private double RealAt(double pixelX)
        => MinReal + pixelX / ClientSize.Width * (MaxReal - MinReal);

    private double ImagAt(double pixelY)
        => MinImaginary + pixelY / ClientSize.Height * (MaxImaginary - MinImaginary);

    private static string TileStyle(TileModel tile)
        => FormattableString.Invariant($"left:{tile.Left:0.####}px;top:{tile.Top:0.####}px");
}
