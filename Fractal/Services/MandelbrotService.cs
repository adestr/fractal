namespace Fractal.Services;

public class MandelbrotService
{
    public static int GetMandelbrotIterations(double x, double y, int maxIterations)
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
