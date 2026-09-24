using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Fractal.Services;

[SupportedOSPlatform("browser")]
public partial class MandelbrotService
{
    private static bool IsInitialized = false;

    private const string ModuleName = nameof(MandelbrotService);

    private static LazyAsync<JSObject> initOnlyOnce = new(async () =>
    {
        Console.WriteLine("[MandelbrotService] Importing JavaScript module for Mandelbrot calculations...");
        await JSHost.ImportAsync(ModuleName, "/Services/MandelbrotService.razor.js");

        Console.WriteLine($"[{nameof(MandelbrotService)}] Initializing Web Worker for Mandelbrot calculations...");
        return await InitialiseWorkerAsync();
    });

    public static async Task InitialiseAsync()
    {
        if (!IsInitialized && OperatingSystem.IsBrowser())
        {
            await initOnlyOnce.Value;
            IsInitialized = true;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await InitialiseAsync();

        await base.OnInitializedAsync();
    }

    private static int counter = 0;

    [JSImport("initializeWorker", ModuleName)]
    [return: JSMarshalAs<JSType.Promise<JSType.Object>>]
    internal static partial Task<JSObject> InitialiseWorkerAsync();

    [JSImport("cancelRenders", ModuleName)]
    internal static partial void CancelRenders();

    public static async Task CancelAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        await initOnlyOnce.Value;
        CancelRenders();
    }

    public static async Task<int[]> CalculateAsync(string elementId, int nr, int ni, double rMin, double rMax, double iMin, double iMax)
    {
        int batch = counter++;
        var watch = Stopwatch.StartNew();
        Console.WriteLine($"[{batch}] Sending request to calculate Mandelbrot heights for range ({rMin} {iMin}i, {rMax} {iMax}i) in {watch.ElapsedMilliseconds} ms");

        var jsObject = await Mandelbrot(elementId, batch, nr, ni, rMin, rMax, iMin, iMax);

        watch.Stop();
        Console.WriteLine($"[{batch}] Received Mandelbrot heights for range ({rMin} {iMin}i, {rMax} {iMax}i) in {watch.ElapsedMilliseconds} ms");
        watch.Start();

        var array = UnwrapJSObjectAsArraySegment(jsObject);
        Console.WriteLine($"[{batch}] Unwrapped Mandelbrot heights for range ({rMin} {iMin}i, {rMax} {iMax}i) in {watch.ElapsedMilliseconds} ms");

        return array;
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
        string elementId,
        int requestId,
        int nr,
        int ni,
        double rMin,
        double rMax,
        double iMin,
        double iMax
    );

    [JSImport("unwrapJsObject", ModuleName)]
    //[return: JSMarshalAs<JSType.MemoryView>]
    internal static partial int[] UnwrapJSObjectAsArraySegment(JSObject jsObject);

    //[JSImport("unwrapJsObjectAsByteArray", ModuleName)]
    //[return: JSMarshalAs<JSType.Array<JSType.Number>>]
    //internal static partial byte[] UnwrapJSObjectAsByteArray(JSObject jsObject);
}
