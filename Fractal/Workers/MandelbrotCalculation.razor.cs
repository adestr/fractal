using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Fractal.Workers;

[SupportedOSPlatform("browser")]
public partial class MandelbrotCalculation
{
    [JSExport]
    public static int[] Calculate(int x, int y, float realMin, float realMax, float imaginaryMin, float imaginaryMax)
    {
        const int maxIterations = 100;
        Console.WriteLine($"Calculating Mandelbrot heights for tile of size ({x}, {y}) with real range ({realMin}, {realMax}) and imaginary range ({imaginaryMin}, {imaginaryMax}) with max iterations {maxIterations}");

        int[] result = new int[x * y];

        float deltaReal = (realMax - realMin) / x;
        float deltaImaginary = (imaginaryMax - imaginaryMin) / y;
        for (int i = 0; i < x; i++)
        {
            for (int j = 0; j < y; j++)
            {
                float real = realMin + (i + 0.5f) * deltaReal;
                float imaginary = imaginaryMin + (j + 0.5f) * deltaImaginary;
                result[i * y + j] = CalculatePoint(real, imaginary, maxIterations);
            }
        }

        return result;
    }

    private static int CalculatePoint(double x, double y, int maxIterations)
    {
        double zx = 0.0;
        double zy = 0.0;
        int iterations = 0;
        while (zx * zx + zy * zy < 4.0 && iterations < maxIterations)
        {
            double temp = zx * zx - zy * zy + x;
            zy = 2.0 * zx * zy + y;
            zx = temp;
            iterations++;
        }
        return iterations;
    }
}