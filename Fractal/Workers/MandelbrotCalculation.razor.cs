using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Fractal.Workers;

[SupportedOSPlatform("browser")]
public partial class MandelbrotCalculation
{
    [JSExport]
    public static byte[] Calculate(int requestId, int x, int y, double realMin, double realMax, double imaginaryMin, double imaginaryMax)
    {
        const int maxIterations = 200;
        var watch = Stopwatch.StartNew();
        Console.WriteLine($"[{requestId}] Calculating Mandelbrot heights for tile of size ({x}, {y}) with real range ({realMin}, {realMax}) and imaginary range ({imaginaryMin}, {imaginaryMax}) with max iterations {maxIterations}");

        int[] result = new int[x * y];

        double deltaReal = (realMax - realMin) / x;
        double deltaImaginary = (imaginaryMax - imaginaryMin) / y;
        for (int i = 0; i < x; i++)
        {
            for (int j = 0; j < y; j++)
            {
                double real = realMin + (i + 0.5) * deltaReal;
                double imaginary = imaginaryMin + (j + 0.5) * deltaImaginary;
                result[i * y + j] = CalculatePoint(real, imaginary, maxIterations);
            }
        }

        var compressed = Compression.Compress(result);
        Console.WriteLine($"[{requestId}] Compressed Mandelbrot heights from {result.Length * sizeof(int)} bytes to {compressed.Length} bytes");
        Console.WriteLine($"[{requestId}] Finished calculating Mandelbrot heights for range ({realMin} {imaginaryMin}i, {realMax} {imaginaryMax}i) in {watch.ElapsedMilliseconds} ms");

        return compressed;
    }

    /// <summary>
    /// Calculates the number of iterations for a given point in the complex plane to determine if it belongs to the Mandelbrot set.
    /// </summary>
    /// <param name="real">The real part of the complex number.</param>
    /// <param name="imaginary">The imaginary part of the complex number.</param>
    /// <param name="maxIterations">The maximum number of iterations to perform.</param>
    /// <remarks>
    /// For each iteration, the function is <c>z_n = z_{n-1}^2 + c</c> where <c>c</c> is the complex number represented by the <paramref name="real"/> and <paramref name="imaginary"/> parameters.
    /// </remarks>
    /// <returns>The number of iterations it took for the point to escape, or maxIterations if it did not escape.</returns>
    private static int CalculatePoint(double real, double imaginary, int maxIterations)
    {
        double zx = 0.0;
        double zy = 0.0;
        int iterations = 0;
        while (zx * zx + zy * zy < 4.0 && iterations < maxIterations)
        {
            // Calculate the next iteration of z = z^2 + c
            double temp = zx * zx - zy * zy + real;
            zy = 2.0 * zx * zy + imaginary;
            zx = temp;
            iterations++;
        }
        return iterations;
    }
}
