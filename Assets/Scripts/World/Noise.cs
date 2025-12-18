using UnityEngine;

/// <summary>
/// Simple noise utilities: 2D Perlin with octaves + a cheap 3D noise helper.
/// Not a replacement for true 3D simplex/perlin, but good enough for caves prototyping.
/// </summary>
public static class Noise
{
    // 2D fractal PerlinNoise
    public static float PerlinNoise2D(float x, float y, int octaves, float persistence, float lacunarity, float scale, Vector2 offset)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float noiseHeight = 0f;
        float maxAmplitude = 0f;

        for (int o = 0; o < Mathf.Max(1, octaves); o++)
        {
            float sampleX = (x + offset.x) / Mathf.Max(0.0001f, scale) * frequency;
            float sampleY = (y + offset.y) / Mathf.Max(0.0001f, scale) * frequency;

            float perlin = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f; // -1..1
            noiseHeight += perlin * amplitude;

            maxAmplitude += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // normalize to -1..1
        if (maxAmplitude == 0) return 0f;
        return noiseHeight / maxAmplitude;
    }

    // Cheap 3D noise composed from Perlin pairs. Output roughly -1..1.
    public static float PerlinNoise3D(float x, float y, float z, int octaves, float persistence, float lacunarity, float scale, Vector3 offset)
    {
        // combine three 2D slices to approximate 3D behaviour
        float xy = PerlinNoise2D(x + offset.x, y + offset.y, octaves, persistence, lacunarity, scale, new Vector2(0, 0));
        float yz = PerlinNoise2D(y + offset.y, z + offset.z, octaves, persistence, lacunarity, scale, new Vector2(13.37f, 7.13f));
        float zx = PerlinNoise2D(z + offset.z, x + offset.x, octaves, persistence, lacunarity, scale, new Vector2(42.42f, 21.21f));

        // average and clamp
        float value = (xy + yz + zx) / 3f;
        return Mathf.Clamp(value, -1f, 1f);
    }
}