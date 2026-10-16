using System;
using System.IO;
using UnityEngine;

/// <summary>Synthesises a seamless, slow ambient space-music loop (pads, sub bass, bell sparkles, soft echo).</summary>
public static class MusicGenerator
{
    const int SampleRate = 32000;

    static float Hz(float semitonesFromA4) { return 440f * Mathf.Pow(2f, semitonesFromA4 / 12f); }

    public static void Generate(string wavPath)
    {
        float duration = 96f;
        int n = (int)(SampleRate * duration);
        var L = new float[n];
        var R = new float[n];
        var rnd = new System.Random(7);

        // 4 chords, 24 s each. Semitone offsets from A4.
        float[][] chords = {
            new[] { -24f, -17f, -12f, -9f, -5f, 2f },     // Am9   A1.. E3 A3 C4 E4 B4
            new[] { -28f, -21f, -16f, -12f, -9f, -5f },   // Fmaj7 F1.. C2 F2 A3 C4 E4
            new[] { -21f, -14f, -9f, -5f, -2f, 5f },      // Cmaj9
            new[] { -26f, -19f, -14f, -10f, -5f, 0f },    // Gsus-ish add9
        };
        float[] bassRoot = { -36f, -40f, -33f, -38f };    // A0.. F0.. C1.. G0..
        float chordLen = duration / chords.Length;
        float fade = 6.5f;

        double twoPi = Math.PI * 2.0;

        for (int c = 0; c < chords.Length; c++)
        {
            float start = c * chordLen;
            // window: raised cosine ramps wrap around the loop so the transition across the loop point is seamless
            int i0 = (int)((start - fade) * SampleRate);
            int i1 = (int)((start + chordLen + fade) * SampleRate);
            var notes = chords[c];
            for (int k = 0; k < notes.Length; k++)
            {
                float f = Hz(notes[k]);
                float pan = Mathf.Lerp(-0.55f, 0.55f, k / (float)(notes.Length - 1)) * (c % 2 == 0 ? 1f : -1f);
                float gL = Mathf.Cos((pan * 0.5f + 0.5f) * Mathf.PI * 0.5f);
                float gR = Mathf.Sin((pan * 0.5f + 0.5f) * Mathf.PI * 0.5f);
                float det = 1.0035f;
                double p1 = rnd.NextDouble() * twoPi, p2 = rnd.NextDouble() * twoPi, p3 = rnd.NextDouble() * twoPi;
                double w1 = twoPi * f / SampleRate, w2 = twoPi * f * det / SampleRate, w3 = twoPi * f / det / SampleRate;
                float lfoRate = 0.05f + 0.04f * k;
                float amp = 0.075f / (1f + 0.15f * k);
                for (int i = i0; i < i1; i++)
                {
                    float t = (i - (start - fade) * SampleRate) / SampleRate;           // time into window
                    float total = chordLen + 2f * fade;
                    float env = t < fade * 2f ? 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(t / (fade * 2f)))
                                              : 1f;
                    float tail = total - t;
                    if (tail < fade * 2f) env *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(tail / (fade * 2f)));
                    int idx = ((i % n) + n) % n;
                    int j = i - i0;
                    double a1 = p1 + w1 * j, a2 = p2 + w2 * j, a3 = p3 + w3 * j;
                    float s = (float)(Math.Sin(a1) + Math.Sin(a2) + Math.Sin(a3)) * 0.34f
                            + (float)(Math.Sin(a1 * 2.0) * 0.28 + Math.Sin(a2 * 3.0) * 0.12 + Math.Sin(a3 * 4.0) * 0.06);
                    float lfo = 0.78f + 0.22f * Mathf.Sin(twoPiF() * lfoRate * (idx / (float)SampleRate) + k);
                    float v = s * amp * env * lfo;
                    L[idx] += v * gL * 1.4f;
                    R[idx] += v * gR * 1.4f;
                }
            }

            // sub bass
            {
                float f = Hz(bassRoot[c]) * 2f;
                double w = twoPi * f / SampleRate;
                for (int i = i0; i < i1; i++)
                {
                    float t = (i - (start - fade) * SampleRate) / SampleRate;
                    float total = chordLen + 2f * fade;
                    float env = Mathf.Min(1f, t / (fade * 2f));
                    env = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * env);
                    float tail = Mathf.Min(1f, (total - t) / (fade * 2f));
                    env *= 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(tail));
                    int idx = ((i % n) + n) % n;
                    float v = (float)(Math.Sin(w * (i - i0)) + 0.35 * Math.Sin(2.0 * w * (i - i0))) * 0.15f * env;
                    L[idx] += v; R[idx] += v;
                }
            }
        }

        // bell sparkles: A minor pentatonic, inharmonic partial, wrap-around tails
        float[] penta = { 0f, 3f, 5f, 7f, 10f, 12f, 15f, 17f };
        float tNote = 1.5f;
        while (tNote < duration - 3f)
        {
            float semi = penta[rnd.Next(penta.Length)];
            float f = Hz(semi);
            float vel = 0.025f + 0.03f * (float)rnd.NextDouble();
            float pan = (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.8f;
            AddBell(L, R, n, tNote, f, vel, pan);
            // soft echoes
            AddBell(L, R, n, tNote + 0.52f, f, vel * 0.45f, -pan);
            AddBell(L, R, n, tNote + 1.10f, f, vel * 0.2f, pan * 0.5f);
            tNote += 2.2f + (float)rnd.NextDouble() * 3.4f;
        }

        // airy noise bed
        {
            float lpL = 0, lpR = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float cut = 0.012f + 0.008f * Mathf.Sin(twoPiF() * t / duration * 2f);
                float a = (float)(rnd.NextDouble() * 2.0 - 1.0), b = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lpL += (a - lpL) * cut; lpR += (b - lpR) * cut;
                float swell = 0.5f + 0.5f * Mathf.Sin(twoPiF() * t / duration * 3f);
                L[i] += lpL * 0.55f * (0.3f + swell * 0.7f);
                R[i] += lpR * 0.55f * (0.3f + swell * 0.7f);
            }
        }

        // long feedback echo / diffuse tail (run twice around the loop so it is seamless)
        ApplyEcho(L, n, (int)(0.83f * SampleRate), 0.42f);
        ApplyEcho(R, n, (int)(0.97f * SampleRate), 0.42f);

        // normalise
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(L[i]), Mathf.Abs(R[i]));
        float gain = 0.82f / peak;

        WriteWav(wavPath, L, R, n, gain);
    }

    static float twoPiF() { return Mathf.PI * 2f; }

    static void AddBell(float[] L, float[] R, int n, float time, float f, float vel, float pan)
    {
        int start = (int)(time * SampleRate);
        int len = (int)(5.5f * SampleRate);
        float gL = Mathf.Cos((pan * 0.5f + 0.5f) * Mathf.PI * 0.5f);
        float gR = Mathf.Sin((pan * 0.5f + 0.5f) * Mathf.PI * 0.5f);
        double w1 = Math.PI * 2.0 * f / SampleRate;
        double w2 = w1 * 2.76, w3 = w1 * 5.4;
        for (int j = 0; j < len; j++)
        {
            float t = j / (float)SampleRate;
            float env = (1f - Mathf.Exp(-t * 400f)) * Mathf.Exp(-t * 1.15f);
            float v = ((float)Math.Sin(w1 * j) + 0.28f * (float)Math.Sin(w2 * j) * Mathf.Exp(-t * 1.5f) + 0.1f * (float)Math.Sin(w3 * j) * Mathf.Exp(-t * 2.4f)) * vel * env;
            int idx = (start + j) % n;
            L[idx] += v * gL; R[idx] += v * gR;
        }
    }

    static void ApplyEcho(float[] ch, int n, int delay, float feedback)
    {
        var dry = (float[])ch.Clone();
        float lp = 0;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                int src = (i - delay + n) % n;
                lp += (ch[src] - lp) * 0.35f;
                ch[i] = dry[i] + lp * feedback;
            }
        }
    }

    static void WriteWav(string path, float[] L, float[] R, int n, float gain)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int dataSize = n * 4;
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataSize);
            bw.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            bw.Write(16); bw.Write((short)1); bw.Write((short)2);
            bw.Write(SampleRate); bw.Write(SampleRate * 4); bw.Write((short)4); bw.Write((short)16);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);
            for (int i = 0; i < n; i++)
            {
                bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(L[i] * gain * 32767f), -32768, 32767));
                bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(R[i] * gain * 32767f), -32768, 32767));
            }
        }
    }
}
