using System.Collections.Generic;
using UnityEngine;

// Efectos de sonido generados por codigo (el proyecto no trae explosiones ni golpes).
public static class ProceduralSfx
{
    public enum Kind { Explosion, Hit, Thud, Engine }

    const int SampleRate = 44100;
    static readonly Dictionary<Kind, AudioClip> clips = new Dictionary<Kind, AudioClip>();

    public static void Play(Kind kind, Vector3 position, float volume = 1f)
    {
        AudioSource.PlayClipAtPoint(Get(kind), position, volume);
    }

    public static AudioClip Get(Kind kind)
    {
        if (clips.TryGetValue(kind, out var clip) && clip != null)
            return clip;

        switch (kind)
        {
            case Kind.Explosion: clip = Noise("Explosion", 1.6f, 4f, 0.04f, 0.9f); break;
            case Kind.Thud: clip = Noise("Thud", 0.5f, 9f, 0.08f, 0.8f); break;
            case Kind.Engine: clip = EngineLoop(); break;
            default: clip = Noise("Hit", 0.18f, 25f, 0.35f, 0.7f); break;
        }
        clips[kind] = clip;
        return clip;
    }

    // Zumbido de motor en bucle (se sube el pitch con la velocidad)
    static AudioClip EngineLoop()
    {
        const float baseFrequency = 55f;
        int length = SampleRate; // 1 s: 55 ciclos exactos, el bucle no salta
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float phase = (float)i / SampleRate * baseFrequency;
            float saw = 2f * (phase - Mathf.Floor(phase + 0.5f));
            float pulse = Mathf.Sin(phase * 2f * Mathf.PI * 2f) * 0.4f;
            data[i] = (saw * 0.5f + pulse) * 0.5f;
        }
        var clip = AudioClip.Create("Engine", length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Ruido filtrado con caida exponencial; 'smoothing' bajo = mas grave
    static AudioClip Noise(string name, float seconds, float decay, float smoothing, float gain)
    {
        int length = (int)(seconds * SampleRate);
        var data = new float[length];
        var random = new System.Random(name.GetHashCode());
        float filtered = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered += (white - filtered) * smoothing;
            float attack = Mathf.Clamp01(t / 0.005f);
            data[i] = Mathf.Clamp(filtered * 4f, -1f, 1f) * Mathf.Exp(-decay * t) * attack * gain;
        }
        var clip = AudioClip.Create(name, length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
