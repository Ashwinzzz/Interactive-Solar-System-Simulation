using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Generates every planet, ring and sky texture from scratch (no downloaded assets).</summary>
public static class ProceduralTextures
{
    const float PI = Mathf.PI;

    // ------------------------------------------------------------------ helpers

    static void Dir(int x, int y, int w, int h, out float dx, out float dy, out float dz, out float lat, out float lon)
    {
        lon = (x + 0.5f) / w * 2f * PI;
        lat = (y + 0.5f) / h * PI - PI * 0.5f;
        float c = (float)Math.Cos(lat);
        dx = c * (float)Math.Cos(lon);
        dy = (float)Math.Sin(lat);
        dz = c * (float)Math.Sin(lon);
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Color Grad(Color[] stops, float t)
    {
        t = Mathf.Clamp01(t) * (stops.Length - 1);
        int i = Mathf.Min((int)t, stops.Length - 2);
        return Color.Lerp(stops[i], stops[i + 1], t - i);
    }

    static Color32 ToC32(Color c)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255));
    }

    public static void SavePng(string path, Color32[] px, int w, int h)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.SetPixels32(px);
        tex.Apply(false);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static Color32[] NormalFromHeight(float[] hgt, int w, int h, float strength)
    {
        var res = new Color32[w * h];
        Parallel.For(0, h, y =>
        {
            int y0 = Math.Max(0, y - 1), y1 = Math.Min(h - 1, y + 1);
            float lat = ((y + 0.5f) / h - 0.5f) * PI;
            // equirectangular pixels get narrower towards the poles, so flatten the u gradient there
            float stretch = Mathf.Clamp(1f / Mathf.Max(0.12f, (float)Math.Cos(lat)), 1f, 6f);
            for (int x = 0; x < w; x++)
            {
                int xl = (x - 1 + w) % w, xr = (x + 1) % w;
                float nx = -(hgt[y * w + xr] - hgt[y * w + xl]) * strength / stretch;
                float ny = -(hgt[y1 * w + x] - hgt[y0 * w + x]) * strength;
                float inv = 1f / (float)Math.Sqrt(nx * nx + ny * ny + 1f);
                res[y * w + x] = new Color32(
                    (byte)((nx * inv * 0.5f + 0.5f) * 255f),
                    (byte)((ny * inv * 0.5f + 0.5f) * 255f),
                    (byte)((inv * 0.5f + 0.5f) * 255f), 255);
            }
        });
        return res;
    }

    static void Save(string folder, string name, Color32[] albedo, float[] height, int w, int h, float normalStrength)
    {
        SavePng(Path.Combine(folder, name + "_albedo.png"), albedo, w, h);
        if (height != null)
            SavePng(Path.Combine(folder, name + "_normal.png"), NormalFromHeight(height, w, h, normalStrength), w, h);
    }

    // ------------------------------------------------------------------ rocky worlds

    static void Rocky(string folder, string name, int w, int h, int seed, Func<float, float, float, float, float, float, Color> colorFn, float craterWeight, float normalStrength)
    {
        var albedo = new Color32[w * h];
        var height = new float[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float dx, dy, dz, lat, lon;
                Dir(x, y, w, h, out dx, out dy, out dz, out lat, out lon);
                float cr = NoiseLib.Craters(dx, dy, dz, 3.5f, seed, 0.55f) * 0.55f
                         + NoiseLib.Craters(dx, dy, dz, 8f, seed + 100, 0.6f) * 0.40f
                         + NoiseLib.Craters(dx, dy, dz, 19f, seed + 200, 0.62f) * 0.30f
                         + NoiseLib.Craters(dx, dy, dz, 44f, seed + 300, 0.55f) * 0.2f;
                float rough = NoiseLib.Fbm(dx * 7f + seed, dy * 7f, dz * 7f, 6) * 0.22f
                            + NoiseLib.Fbm(dx * 30f, dy * 30f + seed, dz * 30f, 3) * 0.06f;
                float hh = cr * craterWeight + rough;
                height[y * w + x] = hh;
                albedo[y * w + x] = ToC32(colorFn(dx, dy, dz, hh, cr, lat));
            }
        });
        Save(folder, name, albedo, height, w, h, normalStrength);
    }

    public static void Mercury(string folder)
    {
        Rocky(folder, "mercury", 2048, 1024, 11, (dx, dy, dz, hh, cr, lat) =>
        {
            float tone = 0.34f + 0.12f * NoiseLib.Fbm(dx * 3f, dy * 3f, dz * 3f, 5) + hh * 0.16f + Mathf.Max(0, cr) * 0.35f;
            // faint bright crater rays
            float rays = Smooth(0.35f, 0.6f, NoiseLib.Fbm(dx * 12f + 4f, dy * 12f, dz * 12f, 3)) * 0.07f;
            tone += rays;
            return new Color(tone * 1.02f, tone * 0.96f, tone * 0.90f);
        }, 0.9f, 6f);
    }

    public static void Moon(string folder)
    {
        Rocky(folder, "moon", 2048, 1024, 71, (dx, dy, dz, hh, cr, lat) =>
        {
            float maria = Smooth(-0.02f, 0.18f, NoiseLib.Fbm(dx * 1.4f + 3f, dy * 1.4f, dz * 1.4f, 4));
            float tone = 0.50f + 0.10f * NoiseLib.Fbm(dx * 5f, dy * 5f, dz * 5f, 5) + hh * 0.12f + Mathf.Max(0, cr) * 0.30f;
            tone = Mathf.Lerp(tone, tone * 0.45f, maria * 0.85f);
            return new Color(tone, tone * 0.985f, tone * 0.96f);
        }, 0.95f, 6f);
    }

    public static void Mars(string folder)
    {
        Rocky(folder, "mars", 2048, 1024, 131, (dx, dy, dz, hh, cr, lat) =>
        {
            Color rust = new Color(0.60f, 0.27f, 0.13f);
            Color dust = new Color(0.78f, 0.45f, 0.24f);
            Color dark = new Color(0.27f, 0.13f, 0.09f);
            float large = NoiseLib.Fbm(dx * 2.2f + 9f, dy * 2.2f, dz * 2.2f, 6);
            float detail = NoiseLib.Fbm(dx * 11f, dy * 11f, dz * 11f, 4);
            Color c = Color.Lerp(rust, dust, Smooth(-0.2f, 0.35f, large + detail * 0.25f));
            c = Color.Lerp(c, dark, Smooth(0.12f, 0.42f, -large + 0.05f) * 0.75f);
            float shade = 0.9f + hh * 0.28f + Mathf.Max(0, cr) * 0.16f;
            c *= shade;
            float cap = Smooth(0.86f, 0.93f, Mathf.Abs((float)Math.Sin(lat)) + 0.03f * NoiseLib.Fbm(dx * 20f, dy * 20f, dz * 20f, 3));
            return Color.Lerp(c, new Color(0.93f, 0.93f, 0.95f), cap);
        }, 0.55f, 4f);
    }

    // ------------------------------------------------------------------ Earth

    public static void Earth(string folder)
    {
        int w = 2048, h = 1024;
        var albedo = new Color32[w * h];
        var spec = new Color32[w * h];
        var night = new Color32[w * h];
        var height = new float[w * h];
        var clouds = new Color32[w * h];

        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float dx, dy, dz, lat, lon;
                Dir(x, y, w, h, out dx, out dy, out dz, out lat, out lon);
                float alat = Mathf.Abs(dy);

                // --- continents
                float n = NoiseLib.Fbm(dx * 1.45f + 2.1f, dy * 1.45f, dz * 1.45f, 8)
                        + 0.42f * NoiseLib.Fbm(dx * 0.65f + 5.7f, dy * 0.65f, dz * 0.65f, 3);
                n -= 0.18f * alat * alat;          // slightly less land at the poles
                const float sea = 0.045f;
                bool land = n > sea;

                float elev = Mathf.Clamp01((n - sea) * 3.2f);
                float mount = NoiseLib.Ridged(dx * 5f, dy * 5f, dz * 5f, 5);
                float hgt = land ? elev * 0.65f + elev * mount * 0.55f : 0f;
                height[y * w + x] = hgt;

                Color c;
                if (!land)
                {
                    float depth = Mathf.Clamp01((sea - n) * 3.2f);
                    Color shallow = new Color(0.04f, 0.36f, 0.46f);
                    Color mid = new Color(0.02f, 0.17f, 0.38f);
                    Color deep = new Color(0.01f, 0.06f, 0.21f);
                    c = depth < 0.18f ? Color.Lerp(shallow, mid, depth / 0.18f) : Color.Lerp(mid, deep, Mathf.Clamp01((depth - 0.18f) / 0.6f));
                    c *= 0.92f + 0.12f * NoiseLib.Fbm(dx * 9f, dy * 9f, dz * 9f, 3);
                    // sea ice
                    float ice = Smooth(0.90f, 0.96f, alat + 0.04f * NoiseLib.Fbm(dx * 16f, dy * 16f, dz * 16f, 3));
                    c = Color.Lerp(c, new Color(0.85f, 0.9f, 0.95f), ice);
                    spec[y * w + x] = new Color32((byte)(255f * (1f - ice * 0.9f)), 0, 0, 255);
                }
                else
                {
                    float temp = 1f - Mathf.Pow(alat, 1.15f) - elev * 0.45f;
                    float moist = 0.5f + 0.9f * NoiseLib.Fbm(dx * 2.6f + 40f, dy * 2.6f, dz * 2.6f, 5);
                    float arid = Smooth(0.55f, 0.9f, 1f - moist) * Smooth(0.12f, 0.35f, alat) * Smooth(0.95f, 0.55f, alat);
                    arid = Mathf.Clamp01(arid + Smooth(0.52f, 0.30f, moist) * 0.4f);

                    Color forest = new Color(0.035f, 0.13f, 0.045f);
                    Color grass = new Color(0.16f, 0.27f, 0.08f);
                    Color desert = new Color(0.62f, 0.49f, 0.29f);
                    Color tundra = new Color(0.34f, 0.37f, 0.30f);
                    Color rock = new Color(0.30f, 0.26f, 0.23f);

                    Color veg = Color.Lerp(grass, forest, Smooth(0.35f, 0.9f, moist));
                    c = Color.Lerp(veg, desert, arid);
                    c = Color.Lerp(tundra, c, Smooth(0.18f, 0.5f, temp));
                    c = Color.Lerp(c, rock, Smooth(0.62f, 0.92f, elev));
                    c *= 0.88f + 0.24f * NoiseLib.Fbm(dx * 14f, dy * 14f, dz * 14f, 4);
                    float snow = Smooth(0.86f, 0.94f, alat + 0.06f * NoiseLib.Fbm(dx * 12f, dy * 12f, dz * 12f, 4))
                               + Smooth(0.72f, 0.95f, elev * (1f - temp * 0.5f)) * 0.8f;
                    c = Color.Lerp(c, new Color(0.93f, 0.94f, 0.96f), Mathf.Clamp01(snow));
                    spec[y * w + x] = new Color32(10, 0, 0, 255);
                }
                c.a = 1f;
                albedo[y * w + x] = ToC32(c);

                // --- city lights
                if (land && alat < 0.78f)
                {
                    float pop = Smooth(0.10f, 0.45f, NoiseLib.Fbm(dx * 3.4f + 9f, dy * 3.4f, dz * 3.4f, 5) + 0.18f);
                    float cl = NoiseLib.Fbm(dx * 26f, dy * 26f, dz * 26f, 4);
                    float cl2 = NoiseLib.Perlin(dx * 90f, dy * 90f, dz * 90f);
                    float light = Mathf.Pow(Mathf.Clamp01(cl * 2.2f - 0.15f), 2f) * pop * Mathf.Clamp01(0.6f + cl2 * 0.8f);
                    light *= 1f - Smooth(0.35f, 0.7f, elev);
                    night[y * w + x] = ToC32(new Color(1f * light, 0.72f * light, 0.34f * light, 1f));
                }
                else night[y * w + x] = new Color32(0, 0, 0, 255);

                // --- clouds: swirling frontal bands plus tropical convection
                float wx = NoiseLib.Fbm(dx * 2f + 31f, dy * 2f, dz * 2f, 4) * 0.55f;
                float wy = NoiseLib.Fbm(dx * 2f, dy * 2f + 17f, dz * 2f, 4) * 0.55f;
                float wz = NoiseLib.Fbm(dx * 2f, dy * 2f, dz * 2f + 63f, 4) * 0.55f;
                float cn = NoiseLib.Fbm((dx + wx) * 3.1f, (dy + wy) * 3.1f * 1.7f, (dz + wz) * 3.1f, 8);
                float bandBias = 0.14f * (float)Math.Cos(alat * 9f);       // clearer subtropics, cloudy storm tracks
                float cd = Mathf.Clamp01((cn - 0.03f + bandBias) * 2.1f);
                cd = cd * cd * (3f - 2f * cd);
                byte cb = (byte)(cd * 255f);
                clouds[y * w + x] = new Color32(cb, cb, cb, 255);
            }
        });

        SavePng(Path.Combine(folder, "earth_albedo.png"), albedo, w, h);
        SavePng(Path.Combine(folder, "earth_normal.png"), NormalFromHeight(height, w, h, 5f), w, h);
        SavePng(Path.Combine(folder, "earth_spec.png"), spec, w, h);
        SavePng(Path.Combine(folder, "earth_night.png"), night, w, h);
        SavePng(Path.Combine(folder, "earth_clouds.png"), clouds, w, h);
    }

    // ------------------------------------------------------------------ gas & ice giants

    static void Giant(string folder, string name, int w, int h, int seed, Color[] palette, float bandFreq, float turbulence,
                      float detail, Action<int, int, float, float, float, float, float, Color[]> extra = null)
    {
        var albedo = new Color32[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float dx, dy, dz, lat, lon;
                Dir(x, y, w, h, out dx, out dy, out dz, out lat, out lon);
                float v = lat / (PI * 0.5f);
                float warp = turbulence * (NoiseLib.Fbm(dx * 2.6f + seed, dy * 2.6f, dz * 2.6f, 5)
                           + 0.35f * NoiseLib.Fbm(dx * 9f, dy * 9f + seed, dz * 9f, 4));
                float vv = v + warp;
                float b = 0.5f + 0.5f * (float)Math.Sin(vv * bandFreq) * 0.6f
                               + 0.5f * 0.4f * (float)Math.Sin(vv * bandFreq * 2.71f + 1.3f + seed);
                b = Mathf.Clamp01(b + 0.12f * NoiseLib.Fbm(dx * 5f, dy * 28f, dz * 5f, 4));
                Color c = Grad(palette, b);
                float fine = 1f + detail * NoiseLib.Fbm(dx * 16f, dy * 38f + seed, dz * 16f, 4);
                c *= fine;
                albedo[y * w + x] = ToC32(c);
            }
        });

        if (extra != null)
        {
            // second pass for features such as storms; operates on the finished texels
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    float dx, dy, dz, lat, lon;
                    Dir(x, y, w, h, out dx, out dy, out dz, out lat, out lon);
                    Color c = albedo[y * w + x];
                    var arr = new[] { c };
                    extra(x, y, lat, lon, dx, dy, dz, arr);
                    albedo[y * w + x] = ToC32(arr[0]);
                }
            });
        }
        SavePng(Path.Combine(folder, name + "_albedo.png"), albedo, w, h);
    }

    static void Storm(Color[] px, float lat, float lon, float lat0, float lon0, float rLat, float rLon, Color core, Color collar)
    {
        float dl = lon - lon0;
        while (dl > PI) dl -= 2f * PI;
        while (dl < -PI) dl += 2f * PI;
        float ex = dl * (float)Math.Cos(lat) / rLon;
        float ey = (lat - lat0) / rLat;
        float r2 = ex * ex + ey * ey;
        if (r2 > 2.6f) return;
        float swirl = 0.5f + 0.5f * (float)Math.Sin(Mathf.Atan2(ey, ex) * 2f + Mathf.Sqrt(r2) * 7f);
        float inner = Smooth(1.0f, 0.3f, r2);
        float outer = Smooth(2.4f, 1.0f, r2) * (1f - inner);
        Color c = px[0];
        c = Color.Lerp(c, collar, outer * 0.55f);
        c = Color.Lerp(c, Color.Lerp(core, core * 1.25f, swirl), inner * 0.9f);
        px[0] = c;
    }

    public static void Jupiter(string folder)
    {
        var pal = new[] {
            new Color(0.38f, 0.24f, 0.17f), new Color(0.58f, 0.38f, 0.26f), new Color(0.80f, 0.66f, 0.50f),
            new Color(0.93f, 0.86f, 0.74f), new Color(0.74f, 0.55f, 0.40f), new Color(0.50f, 0.32f, 0.22f) };
        Giant(folder, "jupiter", 2048, 1024, 3, pal, 21f, 0.075f, 0.22f, (x, y, lat, lon, dx, dy, dz, px) =>
        {
            Storm(px, lat, lon, -0.33f, 1.2f, 0.115f, 0.34f, new Color(0.66f, 0.26f, 0.15f), new Color(0.92f, 0.82f, 0.68f));
            Storm(px, lat, lon, 0.62f, 3.3f, 0.035f, 0.07f, new Color(0.92f, 0.88f, 0.82f), new Color(0.7f, 0.55f, 0.4f));
        });
    }

    public static void Saturn(string folder)
    {
        var pal = new[] {
            new Color(0.62f, 0.50f, 0.34f), new Color(0.80f, 0.68f, 0.48f), new Color(0.90f, 0.81f, 0.62f),
            new Color(0.78f, 0.66f, 0.47f), new Color(0.68f, 0.55f, 0.38f) };
        Giant(folder, "saturn", 2048, 1024, 9, pal, 24f, 0.035f, 0.10f);
    }

    public static void Uranus(string folder)
    {
        var pal = new[] { new Color(0.46f, 0.70f, 0.74f), new Color(0.58f, 0.82f, 0.85f), new Color(0.66f, 0.88f, 0.90f) };
        Giant(folder, "uranus", 1024, 512, 14, pal, 12f, 0.02f, 0.05f);
    }

    public static void Neptune(string folder)
    {
        var pal = new[] { new Color(0.08f, 0.16f, 0.50f), new Color(0.14f, 0.30f, 0.82f), new Color(0.22f, 0.42f, 0.92f), new Color(0.12f, 0.24f, 0.66f) };
        Giant(folder, "neptune", 1024, 512, 17, pal, 17f, 0.05f, 0.10f, (x, y, lat, lon, dx, dy, dz, px) =>
        {
            Storm(px, lat, lon, -0.38f, 2.2f, 0.12f, 0.28f, new Color(0.03f, 0.07f, 0.28f), new Color(0.55f, 0.72f, 1f));
            float streak = Smooth(0.5f, 0.75f, NoiseLib.Fbm(dx * 8f, dy * 40f, dz * 8f, 3)) * Smooth(0.15f, 0.3f, Mathf.Abs(lat)) * 0.35f;
            px[0] = Color.Lerp(px[0], new Color(0.85f, 0.92f, 1f), streak);
        });
    }

    public static void Venus(string folder)
    {
        int w = 1024, h = 512;
        var albedo = new Color32[w * h];
        var pal = new[] { new Color(0.55f, 0.38f, 0.20f), new Color(0.78f, 0.60f, 0.34f), new Color(0.92f, 0.80f, 0.55f), new Color(0.96f, 0.90f, 0.72f) };
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float dx, dy, dz, lat, lon;
                Dir(x, y, w, h, out dx, out dy, out dz, out lat, out lon);
                float wx = NoiseLib.Fbm(dx * 2f + 3f, dy * 2f, dz * 2f, 4);
                float wy = NoiseLib.Fbm(dx * 2f, dy * 2f + 7f, dz * 2f, 4);
                float wz = NoiseLib.Fbm(dx * 2f, dy * 2f, dz * 2f + 13f, 4);
                float n = NoiseLib.Fbm((dx + wx * 0.7f) * 2.4f, (dy + wy * 0.7f) * 4.4f, (dz + wz * 0.7f) * 2.4f, 6);
                float t = 0.5f + n * 0.9f;
                // chevron-shaped darker streaks like the UV markings
                t -= 0.12f * Smooth(0.4f, 0.8f, NoiseLib.Fbm(dx * 6f, dy * 14f, dz * 6f, 3));
                albedo[y * w + x] = ToC32(Grad(pal, t));
            }
        });
        SavePng(Path.Combine(folder, "venus_albedo.png"), albedo, w, h);
    }

    // ------------------------------------------------------------------ the Sun

    public static void Sun(string folder)
    {
        int w = 2048, h = 1024;
        var albedo = new Color32[w * h];
        var dark = new Color(0.86f, 0.42f, 0.10f);
        var mid = new Color(1.0f, 0.72f, 0.32f);
        var bright = new Color(1.0f, 0.95f, 0.80f);
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float dx, dy, dz, lat, lon;
                Dir(x, y, w, h, out dx, out dy, out dz, out lat, out lon);
                float cell = NoiseLib.Worley(dx * 22f, dy * 22f, dz * 22f, 5);
                float cell2 = NoiseLib.Worley(dx * 70f + 3f, dy * 70f, dz * 70f, 9);
                float gran = 1f - Smooth(0.05f, 0.85f, cell);
                float gran2 = 1f - Smooth(0.05f, 0.8f, cell2);
                float large = 0.5f + 0.5f * NoiseLib.Fbm(dx * 3.5f, dy * 3.5f, dz * 3.5f, 5);
                float t = Mathf.Clamp01(gran * 0.55f + gran2 * 0.18f + large * 0.5f - 0.1f);
                Color c = t < 0.5f ? Color.Lerp(dark, mid, t * 2f) : Color.Lerp(mid, bright, (t - 0.5f) * 2f);
                // sunspots
                float spot = Smooth(0.48f, 0.66f, NoiseLib.Fbm(dx * 2.3f + 11f, dy * 2.3f, dz * 2.3f, 3) + 0.15f * NoiseLib.Fbm(dx * 14f, dy * 14f, dz * 14f, 2))
                             * (1f - Smooth(0.30f, 0.55f, Mathf.Abs((float)Math.Sin(lat))));
                c = Color.Lerp(c, new Color(0.18f, 0.03f, 0f), spot * 0.8f);
                albedo[y * w + x] = ToC32(c);
            }
        });
        SavePng(Path.Combine(folder, "sun_albedo.png"), albedo, w, h);
    }

    // ------------------------------------------------------------------ rings

    static void RingStrip(string folder, string name, Func<float, Color> fn)
    {
        int w = 2048, h = 4;
        var px = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            Color c = fn(x / (float)(w - 1));
            for (int y = 0; y < h; y++) px[y * w + x] = ToC32(c);
        }
        SavePng(Path.Combine(folder, name + ".png"), px, w, h);
    }

    static float Noise1(float u, float f, int seed) { return NoiseLib.Fbm(u * f + seed, seed * 1.7f, 0.5f, 4); }

    public static void Rings(string folder)
    {
        RingStrip(folder, "saturn_ring", u =>
        {
            // C ring (faint) .. B ring (dense) .. Cassini division .. A ring with the Encke gap
            float d = 0f;
            d += Smooth(0.0f, 0.12f, u) * (1f - Smooth(0.20f, 0.24f, u)) * 0.22f;       // C
            d += Smooth(0.19f, 0.26f, u) * (1f - Smooth(0.60f, 0.62f, u)) * 0.95f;      // B
            d += Smooth(0.66f, 0.69f, u) * (1f - Smooth(0.96f, 1.0f, u)) * 0.72f;       // A
            d *= 1f - Smooth(0.63f, 0.645f, u) * (1f - Smooth(0.655f, 0.67f, u)) * 0.98f; // Cassini
            d *= 1f - Smooth(0.865f, 0.870f, u) * (1f - Smooth(0.875f, 0.88f, u)) * 0.9f; // Encke
            float ringlets = 0.65f + 0.55f * Noise1(u, 70f, 3) + 0.25f * Noise1(u, 260f, 8);
            d = Mathf.Clamp01(d * ringlets);
            float tone = 0.62f + 0.32f * Noise1(u, 24f, 5);
            return new Color(0.80f * tone, 0.70f * tone, 0.55f * tone, d);
        });
        RingStrip(folder, "uranus_ring", u =>
        {
            float d = 0f;
            d += Mathf.Exp(-Mathf.Pow((u - 0.35f) / 0.012f, 2f)) * 0.55f;
            d += Mathf.Exp(-Mathf.Pow((u - 0.55f) / 0.010f, 2f)) * 0.5f;
            d += Mathf.Exp(-Mathf.Pow((u - 0.72f) / 0.014f, 2f)) * 0.6f;
            d += Mathf.Exp(-Mathf.Pow((u - 0.9f) / 0.02f, 2f)) * 0.8f;
            return new Color(0.7f, 0.78f, 0.82f, Mathf.Clamp01(d) * 0.75f);
        });
    }

    // ------------------------------------------------------------------ star field

    public static void Stars(string folder)
    {
        int w = 4096, h = 2048;
        var buf = new float[w * h * 3];

        // Milky Way: a dusty band along a tilted great circle
        Vector3 n = new Vector3(0.25f, 0.88f, 0.40f).normalized;
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float dx, dy, dz, lat, lon;
                Dir(x, y, w, h, out dx, out dy, out dz, out lat, out lon);
                float b = dx * n.x + dy * n.y + dz * n.z;
                float band = (float)Math.Exp(-(b * b) / (2f * 0.16f * 0.16f));
                float core = (float)Math.Exp(-(b * b) / (2f * 0.07f * 0.07f));
                float cloud = 0.5f + 0.5f * NoiseLib.Fbm(dx * 3.3f, dy * 3.3f, dz * 3.3f, 6);
                float fine = 0.5f + 0.5f * NoiseLib.Fbm(dx * 14f, dy * 14f, dz * 14f, 4);
                float lane = Smooth(0.55f, 0.85f, 0.5f + 0.5f * NoiseLib.Fbm(dx * 6f + 8f, dy * 6f, dz * 6f, 5)) * core;
                float lum = (band * 0.55f + core * 0.7f) * cloud * (0.55f + 0.7f * fine) * (1f - lane * 0.8f);
                lum *= 0.15f;
                int i = (y * w + x) * 3;
                buf[i] = lum * 0.95f + core * cloud * 0.012f;
                buf[i + 1] = lum * 0.88f + core * cloud * 0.006f;
                buf[i + 2] = lum * 1.05f;
            }
        });

        // Stars: uniformly distributed on the sphere, Gaussian splats (wider near the poles to compensate for stretching)
        var rnd = new System.Random(2024);
        int count = 0;
        for (int s = 0; s < count; s++)
        {
            float z = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float lonS = (float)(rnd.NextDouble() * Math.PI * 2.0);
            float latS = (float)Math.Asin(z);
            float px = lonS / (2f * PI) * w;
            float py = (latS / PI + 0.5f) * h;

            double r = rnd.NextDouble();
            float mag = (float)Math.Pow(r, 7.0);                 // few bright stars, many faint ones
            float intensity = 0.05f + mag * 2.6f;
            float temp = (float)rnd.NextDouble();
            Color col = temp < 0.15f ? new Color(0.65f, 0.78f, 1f)
                      : temp < 0.55f ? new Color(1f, 0.98f, 0.95f)
                      : temp < 0.85f ? new Color(1f, 0.90f, 0.72f)
                      : new Color(1f, 0.70f, 0.50f);
            float sigma = 0.62f + mag * 0.9f;
            float cosLat = Mathf.Max(0.12f, (float)Math.Cos(latS));
            float sx = sigma / cosLat;
            sx = Mathf.Min(sx, 7f);
            int rx = Mathf.CeilToInt(sx * 3f), ry = Mathf.CeilToInt(sigma * 3f);
            for (int oy = -ry; oy <= ry; oy++)
            {
                int yy = (int)py + oy;
                if (yy < 0 || yy >= h) continue;
                for (int ox = -rx; ox <= rx; ox++)
                {
                    int xx = ((int)px + ox) % w; if (xx < 0) xx += w;
                    float ddx = (xx - px) / sx, ddy = (yy - py) / sigma;
                    // handle wrap for distance
                    float rawDx = xx - px; if (rawDx > w / 2f) rawDx -= w; if (rawDx < -w / 2f) rawDx += w;
                    ddx = rawDx / sx;
                    float g = (float)Math.Exp(-(ddx * ddx + ddy * ddy) * 0.5f) * intensity;
                    int i = (yy * w + xx) * 3;
                    buf[i] += g * col.r; buf[i + 1] += g * col.g; buf[i + 2] += g * col.b;
                }
            }
        }

        var px8 = new Color32[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 3;
                // soft shoulder so very bright stars saturate instead of clipping harshly
                float r = 1f - (float)Math.Exp(-buf[i] * 1.6f);
                float g = 1f - (float)Math.Exp(-buf[i + 1] * 1.6f);
                float b = 1f - (float)Math.Exp(-buf[i + 2] * 1.6f);
                px8[y * w + x] = ToC32(new Color(r, g, b, 1f));
            }
        });
        SavePng(Path.Combine(folder, "stars.png"), px8, w, h);
    }

    public static void GlowDot(string folder)
    {
        // soft white dot used for projectile trails
        int s = 64;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - s / 2f) * (x - s / 2f) + (y - s / 2f) * (y - s / 2f)) / (s / 2f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                px[y * s + x] = ToC32(new Color(1, 1, 1, a));
            }
        SavePng(Path.Combine(folder, "glow_dot.png"), px, s, s);
    }
}
