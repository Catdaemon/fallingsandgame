using System;

namespace FallingSandWorld;

public class NoiseGenerator
{
    private readonly int[] permutation;

    public NoiseGenerator(Random random)
    {
        // Generate permutation table
        permutation = new int[512];
        for (int i = 0; i < 256; i++)
        {
            permutation[i] = i;
        }

        // Shuffle the permutation table
        for (int i = 0; i < 256; i++)
        {
            int j = random.Next(256);
            (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
        }

        // Copy to upper half
        for (int i = 0; i < 256; i++)
        {
            permutation[i + 256] = permutation[i];
        }
    }

    // Simple 2D Perlin noise implementation
    public float PerlinNoise(float x, float y)
    {
        // Find unit grid cell containing point
        int X = (int)Math.Floor(x) & 255;
        int Y = (int)Math.Floor(y) & 255;

        // Get relative x,y of point in cell
        x -= (float)Math.Floor(x);
        y -= (float)Math.Floor(y);

        // Compute fade curves
        float u = Fade(x);
        float v = Fade(y);

        // Hash coordinates of the 4 square corners
        int A = permutation[X] + Y;
        int AA = permutation[A];
        int AB = permutation[A + 1];
        int B = permutation[X + 1] + Y;
        int BA = permutation[B];
        int BB = permutation[B + 1];

        // Add blended results from 4 corners of the square
        float res = Lerp(
            v,
            Lerp(u, Grad(permutation[AA], x, y, 0), Grad(permutation[BA], x - 1, y, 0)),
            Lerp(u, Grad(permutation[AB], x, y - 1, 0), Grad(permutation[BB], x - 1, y - 1, 0))
        );

        // Scale result to [0, 1]
        return (res + 1) / 2;
    }

    // Get noise at different octaves for more natural looking terrain
    public float OctaveNoise(float x, float y, int octaves, float persistence = 0.5f)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;

        for (int i = 0; i < octaves; i++)
        {
            total += PerlinNoise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }

        return total / maxValue;
    }

    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    private static float Lerp(float t, float a, float b)
    {
        return a + t * (b - a);
    }

    private static float Grad(int hash, float x, float y, float z)
    {
        // Convert lower 4 bits of hash into 12 gradient directions
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : ((h == 12 || h == 14) ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}
