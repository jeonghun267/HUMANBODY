using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외부 효과음 파일 없이 런타임에 합성하는 효과음 라이브러리 (라이선스 걱정 없음).
/// gulp(꿀꺽), crunch(씹기), whoomp(연동파 통과), snip(효소 절단), ting(흡수), drip(물 흡수), gurgle(위 꾸르륵 루프), pulse(혈류 맥박 루프)
/// </summary>
public static class Sfx
{
    const int Rate = 24000;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static System.Random rng = new System.Random(7);

    public static AudioClip Get(string name)
    {
        if (cache.TryGetValue(name, out var c)) return c;
        float[] d = name switch
        {
            "gulp" => Gulp(),
            "crunch" => Crunch(),
            "whoomp" => Whoomp(),
            "snip" => Snip(),
            "ting" => Ting(),
            "drip" => Drip(),
            "gurgle" => Gurgle(),
            "pulse" => Pulse(),
            "whoosh" => Whoosh(),
            _ => Ting(),
        };
        c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        cache[name] = c;
        return c;
    }

    /// <summary>위치에서 한 번 재생 (3D).</summary>
    public static void Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f)
    {
        var go = new GameObject("Sfx_" + name);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = Get(name); src.spatialBlend = 1f; src.volume = volume; src.pitch = pitch; src.minDistance = 1f; src.maxDistance = 25f;
        src.Play();
        Object.Destroy(go, src.clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    /// <summary>루프 앰비언스 소스 만들기.</summary>
    public static AudioSource Loop(string name, Transform parent, float volume = 0.4f, bool spatial = false)
    {
        var go = new GameObject("Ambience_" + name);
        go.transform.SetParent(parent, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = Get(name); src.loop = true; src.spatialBlend = spatial ? 1f : 0f; src.volume = volume; src.Play();
        return src;
    }

    // ───────── 합성기 ─────────
    static float Noise() => (float)(rng.NextDouble() * 2 - 1);
    static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

    static float[] Gulp()
    {
        int n = (int)(Rate * 0.5f); var d = new float[n]; float ph = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float f = Mathf.Lerp(420f, 90f, t / 0.5f);
            ph += f / Rate; float s = Mathf.Sin(ph * Mathf.PI * 2) * 0.7f + Noise() * 0.15f;
            lp += (s - lp) * 0.25f;
            d[i] = lp * Env(t, 0.02f, 0.18f);
        }
        return d;
    }

    static float[] Crunch()
    {
        int n = (int)(Rate * 0.25f); var d = new float[n]; float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float s = Noise(); lp += (s - lp) * 0.6f;
            float crack = (i % 700 < 40) ? Noise() * 0.8f : 0f;
            d[i] = (lp * 0.5f + crack) * Env(t, 0.005f, 0.09f);
        }
        return d;
    }

    static float[] Whoomp()
    {
        int n = (int)(Rate * 0.9f); var d = new float[n]; float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float f = Mathf.Lerp(150f, 40f, t / 0.9f);
            ph += f / Rate;
            d[i] = (Mathf.Sin(ph * Mathf.PI * 2) * 0.8f + Noise() * 0.05f) * Env(t, 0.08f, 0.35f);
        }
        return d;
    }

    static float[] Snip()
    {
        int n = (int)(Rate * 0.22f); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float e1 = Env(t, 0.002f, 0.02f), e2 = t > 0.09f ? Env(t - 0.09f, 0.002f, 0.03f) : 0f;
            d[i] = Noise() * (e1 + e2) * 0.8f;
        }
        return d;
    }

    static float[] Ting()
    {
        int n = (int)(Rate * 0.6f); var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            d[i] = (Mathf.Sin(t * 1320f * Mathf.PI * 2) * 0.5f + Mathf.Sin(t * 1980f * Mathf.PI * 2) * 0.3f) * Env(t, 0.003f, 0.2f);
        }
        return d;
    }

    static float[] Drip()
    {
        int n = (int)(Rate * 0.35f); var d = new float[n]; float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float f = Mathf.Lerp(600f, 1400f, Mathf.Clamp01(t / 0.08f));
            ph += f / Rate;
            d[i] = Mathf.Sin(ph * Mathf.PI * 2) * 0.6f * Env(t, 0.004f, 0.08f);
        }
        return d;
    }

    static float[] Gurgle()
    {
        int n = Rate * 4; var d = new float[n]; float lp = 0f, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float bub = Mathf.PerlinNoise(t * 3.1f, 0.3f);
            float f = 60f + bub * 120f;
            ph += f / Rate;
            float s = Mathf.Sin(ph * Mathf.PI * 2) * (0.3f + bub * 0.4f) + Noise() * 0.08f;
            lp += (s - lp) * 0.2f;
            d[i] = lp * 0.6f;
        }
        return Loopable(d);
    }

    static float[] Pulse()
    {
        int n = (int)(Rate * 1.0f); var d = new float[n]; float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float e = Env(t, 0.01f, 0.12f) + (t > 0.28f ? Env(t - 0.28f, 0.01f, 0.1f) * 0.7f : 0f);
            ph += 55f / Rate;
            d[i] = Mathf.Sin(ph * Mathf.PI * 2) * e * 0.8f;
        }
        return d;
    }

    static float[] Whoosh()
    {
        int n = (int)(Rate * 0.8f); var d = new float[n]; float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float s = Noise(); float k = Mathf.Lerp(0.05f, 0.5f, Mathf.Sin(t / 0.8f * Mathf.PI));
            lp += (s - lp) * k;
            d[i] = lp * Mathf.Sin(t / 0.8f * Mathf.PI) * 0.7f;
        }
        return d;
    }

    static float[] Loopable(float[] d)
    {
        int fade = Rate / 10;
        for (int i = 0; i < fade; i++) { float k = (float)i / fade; d[i] *= k; d[d.Length - 1 - i] *= k; }
        return d;
    }
}
