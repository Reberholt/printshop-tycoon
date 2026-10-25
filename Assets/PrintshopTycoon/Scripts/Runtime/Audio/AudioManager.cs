using System.Collections.Generic;
using UnityEngine;

namespace PrintshopTycoon
{
    public enum Sfx { Click, Pickup, Machine, PressHiss, Perfect, Good, Miss, Coin, OrderComplete, Purchase, Footstep, Error, Tick }

    /// <summary>
    /// Plays feedback sounds. All clips are synthesised at startup, so the prototype ships with
    /// zero third-party audio (no licensing questions). Swap in real clips via the inspector later.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int SampleRate = 44100;
        const int Voices = 8;

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioSource[] sources;
        int nextVoice;

        public bool Muted { get; set; }

        void Awake()
        {
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
            clips[Sfx.Click] = Tone("click", new[] { 880f }, 0.05f, 0.35f, Wave.Square);
            clips[Sfx.Tick] = Tone("tick", new[] { 1320f }, 0.03f, 0.25f, Wave.Sine);
            clips[Sfx.Pickup] = Tone("pickup", new[] { 520f, 780f }, 0.12f, 0.4f, Wave.Sine);
            clips[Sfx.Machine] = Tone("machine", new[] { 220f, 330f, 220f, 330f }, 0.4f, 0.3f, Wave.Square);
            clips[Sfx.PressHiss] = Noise("hiss", 0.6f, 0.35f);
            clips[Sfx.Perfect] = Tone("perfect", new[] { 660f, 880f, 1100f, 1320f }, 0.45f, 0.45f, Wave.Sine);
            clips[Sfx.Good] = Tone("good", new[] { 660f, 880f }, 0.25f, 0.4f, Wave.Sine);
            clips[Sfx.Miss] = Tone("miss", new[] { 300f, 200f }, 0.3f, 0.4f, Wave.Square);
            clips[Sfx.Coin] = Tone("coin", new[] { 988f, 1319f }, 0.2f, 0.4f, Wave.Square);
            clips[Sfx.OrderComplete] = Tone("complete", new[] { 523f, 659f, 784f, 1047f }, 0.6f, 0.45f, Wave.Sine);
            clips[Sfx.Purchase] = Tone("purchase", new[] { 392f, 523f, 659f }, 0.35f, 0.4f, Wave.Sine);
            clips[Sfx.Footstep] = Noise("step", 0.05f, 0.2f);
            clips[Sfx.Error] = Tone("error", new[] { 180f, 140f }, 0.22f, 0.35f, Wave.Square);
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (Muted || sources == null || !clips.TryGetValue(sfx, out var clip)) return;
            var src = sources[nextVoice];
            nextVoice = (nextVoice + 1) % sources.Length;
            src.pitch = sfx == Sfx.Footstep ? Random.Range(0.85f, 1.15f) : 1f;
            src.PlayOneShot(clip, volume);
        }

        enum Wave { Sine, Square }

        /// <summary>Sequence of notes with a soft attack/decay envelope.</summary>
        static AudioClip Tone(string name, float[] notes, float seconds, float gain, Wave wave)
        {
            int total = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[total];
            int perNote = Mathf.Max(1, total / notes.Length);
            double phase = 0;
            for (int i = 0; i < total; i++)
            {
                int n = Mathf.Min(notes.Length - 1, i / perNote);
                phase += 2.0 * Mathf.PI * notes[n] / SampleRate;
                float s = (float)System.Math.Sin(phase);
                if (wave == Wave.Square) s = s >= 0f ? 0.6f : -0.6f;
                int local = i - n * perNote;
                float env = Mathf.Clamp01(local / (0.004f * SampleRate)) * Mathf.Clamp01(1f - local / (float)perNote);
                data[i] = s * env * gain;
            }
            var clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Noise(string name, float seconds, float gain)
        {
            int total = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[total];
            var rng = new System.Random(1976);
            float lp = 0f;
            for (int i = 0; i < total; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * 0.25f; // soften the noise
                float env = Mathf.Clamp01(i / (0.01f * SampleRate)) * (1f - i / (float)total);
                data[i] = lp * env * gain * 2f;
            }
            var clip = AudioClip.Create(name, total, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
