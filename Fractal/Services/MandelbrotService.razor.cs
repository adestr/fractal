using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Fractal.Services;

[SupportedOSPlatform("browser")]
public partial class MandelbrotService
{
    private static bool IsInitialized = false;

    private const string ModuleName = nameof(MandelbrotService);

    public static async Task InitialiseAsync()
    {
        if (!IsInitialized && OperatingSystem.IsBrowser())
        {
            await JSHost.ImportAsync(ModuleName, "/Services/MandelbrotService.razor.js");
            IsInitialized = true;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await InitialiseAsync();

        await base.OnInitializedAsync();
    }

    public static async Task<int[]> CalculateAsync(int nr,
        int ni,
        double rMin,
        double rMax,
        double iMin,
        double iMax
)
    {
        var jsObject = await Mandelbrot(nr, ni, rMin, rMax, iMin, iMax);
        return UnwrapJSObjectAsIntArray(jsObject);
    }

    /// <summary>
    /// Calculates the Mandelbrot set for the specified region, using a Web Worker, and returns the result as a JSON string.
    /// </summary>
    /// <remarks>
    /// The result is returned as a JSON string because .NET cannot directly handle asynchronous array responses from the Web Worker.
    /// </remarks>
    /// <param name="nr">The number of steps along the real axis</param>
    /// <param name="ni">Number of steps along the imaginary axis</param>
    /// <param name="rMin">The minimum value along the real axis</param>
    /// <param name="rMax">The maximum value along the real axis</param>
    /// <param name="iMin">The minimum value along the imaginary axis</param>
    /// <param name="iMax">The maximum value along the imaginary axis</param>
    /// <returns>A JSON object, which can be unwrapped using <see cref="UnwrapJsObject"/> to obtain an array of the Mandelbrot heights for the specified region</returns>
    [JSImport("mandelbrot", ModuleName)]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    internal static partial Task<JSObject> Mandelbrot(
        int nr,
        int ni,
        double rMin,
        double rMax,
        double iMin,
        double iMax
    );

    [JSImport("unwrapJsObjectAsIntArray", ModuleName)]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    internal static partial int[] UnwrapJSObjectAsIntArray(JSObject jsObject);
}