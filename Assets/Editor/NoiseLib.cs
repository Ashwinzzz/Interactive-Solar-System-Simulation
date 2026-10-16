using System;
using UnityEngine;

/// <summary>Thread-safe 3D noise helpers used by the procedural texture generators.</summary>
public static class NoiseLib
{
    static readonly int[] perm = new int[512];

    static NoiseLib()
    {
        var rnd = new System.Random(1337);
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
        for (int i = 255; i > 0; i--) { int j = rnd.Next(i + 1); int t = p[i]; p[i] = p[j]; p[j] = t; }
        for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
    }

    static float Fade(float t) { return t * t * t * (t * (t * 6f - 15f) + 10f); }
    static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

    static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    /// <summary>Improved Perlin noise, roughly in [-1, 1].</summary>
    public static float Perlin(float x, float y, float z)
    {
        int X = (int)Math.Floor(x) & 255, Y = (int)Math.Floor(y) & 255, Z = (int)Math.Floor(z) & 255;
        x -= (float)Math.Floor(x); y -= (float)Math.Floor(y); z -= (float)Math.Floor(z);
        float u = Fade(x), v = Fade(y), w = Fade(z);
        int A = perm[X] + Y, AA = perm[A] + Z, AB = perm[A + 1] + Z;
        int B = perm[X + 1] + Y, BA = perm[B] + Z, BB = perm[B + 1] + Z;
        return Lerp(
            Lerp(Lerp(Grad(perm[AA], x, y, z), Grad(perm[BA], x - 1, y, z), u),
                 Lerp(Grad(perm[AB], x, y - 1, z), Grad(perm[BB], x - 1, y - 1, z), u), v),
            Lerp(Lerp(Grad(perm[AA + 1], x, y, z - 1), Grad(perm[BA + 1], x - 1, y, z - 1), u),
                 Lerp(Grad(perm[AB + 1], x, y - 1, z - 1), Grad(perm[BB + 1], x - 1, y - 1, z - 1), u), v), w);
    }

    /// <summary>Fractal Brownian motion in [-1, 1] (approximately).</summary>
    public static float Fbm(float x, float y, float z, int octaves, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += Perlin(x, y, z) * amp;
            norm += amp;
            x *= lacunarity; y *= lacunarity; z *= lacunarity;
            x += 17.3f; y += 9.1f; z += 4.7f;
            amp *= gain;
        }
        return sum / norm;
    }

    public static float Ridged(float x, float y, float z, int octaves)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            float n = 1f - Math.Abs(Perlin(x, y, z));
            sum += n * n * amp;
            norm += amp;
            x *= 2.03f; y *= 2.03f; z *= 2.03f;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    public static uint Hash(int x, int y, int z, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h;
        }
    }

    public static float Hash01(int x, int y, int z, int seed) { return (Hash(x, y, z, seed) & 0xFFFFFF) / (float)0x1000000; }

    /// <summary>
    /// Impact-crater height field: bowls with raised rims, scattered on a 3D cell grid.
    /// Returns a signed height roughly in [-1, 0.5].
    /// </summary>
    public static float Craters(float x, float y, float z, float cellScale, int seed, float density = 0.65f)
    {
        x *= cellScale; y *= cellScale; z *= cellScale;
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
        float h = 0f;
        for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx, cy = iy + dy, cz = iz + dz;
                    if (Hash01(cx, cy, cz, seed) > density) continue;
                    float px = cx + Hash01(cx, cy, cz, seed + 11);
                    float py = cy + Hash01(cx, cy, cz, seed + 23);
                    float pz = cz + Hash01(cx, cy, cz, seed + 37);
                    float r = 0.18f + 0.34f * Hash01(cx, cy, cz, seed + 51);
                    float ddx = x - px, ddy = y - py, ddz = z - pz;
                    float d = (float)Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz) / r;
                    if (d > 1.5f) continue;
                    float depth = 0.55f + 0.45f * Hash01(cx, cy, cz, seed + 77);
                    float bowl = d < 0.82f ? -(1f - (d / 0.82f) * (d / 0.82f)) * depth : 0f;
                    float rim = (float)Math.Exp(-((d - 0.97f) / 0.13f) * ((d - 0.97f) / 0.13f)) * 0.42f * depth;
                    h += bowl + rim;
                }
        return h;
    }

    /// <summary>Cellular (Worley) distance to nearest feature point, 0..~1.</summary>
    public static float Worley(float x, float y, float z, int seed)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y), iz = (int)Math.Floor(z);
        float best = 9f;
        for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx, cy = iy + dy, cz = iz + dz;
                    float px = cx + Hash01(cx, cy, cz, seed);
                    float py = cy + Hash01(cx, cy, cz, seed + 5);
                    float pz = cz + Hash01(cx, cy, cz, seed + 9);
                    float ddx = x - px, ddy = y - py, ddz = z - pz;
                    float d = ddx * ddx + ddy * ddy + ddz * ddz;
                    if (d < best) best = d;
                }
        return (float)Math.Sqrt(best);
    }
}
