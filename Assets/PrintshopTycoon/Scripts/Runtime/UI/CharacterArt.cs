using System.Collections.Generic;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Hue &amp; Silke artwork (Resources/PrintshopTycoon/Characters): transparent cut-outs of the
    /// reference art with all Tex-Tryk logos removed (design rule: no real-world branding).
    /// Poses whose motif was the old logo are not shipped; their constants map to clean poses.
    /// Missing files return null so the UI falls back.
    /// </summary>
    public static class CharacterArt
    {
        public const string HueNeutral = "hue_neutral";
        public const string HueWave = "hue_wave";
        public const string HuePoint = "hue_point";
        public const string HueTransfer = "hue_point";       // old transfer pose carried the logo
        public const string HueClock = "hue_clock";
        public const string HueHeadset = "hue_headset";
        public const string SilkeNeutral = "silke_neutral";
        public const string SilkeWave = "silke_wave";
        public const string SilkeClipboard = "silke_clipboard";
        public const string SilkeQuestion = "silke_question";
        public const string SilkeMagnifier = "silke_question";
        public const string SilkeMeasure = "silke_clipboard";
        public const string SilkeMonitor = "silke_wave";
        public const string DuoWelcome = "duo_welcome";
        public const string DuoDesk = "duo_desk";

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var s) && s != null) return s;
            var tex = Resources.Load<Texture2D>("PrintshopTycoon/Characters/" + name);
            if (tex == null) return null;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
            s.name = name;
            cache[name] = s;
            return s;
        }
    }
}
