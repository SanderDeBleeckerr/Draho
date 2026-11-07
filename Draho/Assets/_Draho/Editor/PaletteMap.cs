using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Draho.EditorTools
{
    /// <summary>
    /// The shared art palette (docs/ART_STYLE.md §1) for code-built materials: every colour that the builders ask
    /// for becomes the nearest palette colour (distance in OKLab, which follows how the eye sees differences, with hue weighted up).
    /// So the 66 hand-picked hex values in apartment_layout.json and the colours in the builders all land on one
    /// harmonious set, without editing each of them. Set <see cref="Enabled"/> to false to get the old colours.
    ///
    /// The palette comes from Art/Palette/DrahoPalette.json, made by Tools/Blender/style_lookdev.py.
    /// </summary>
    public static class PaletteMap
    {
        public const bool Enabled = true;

        const float ChromaWeight = 3f;
        const string JsonPath = "Assets/_Draho/Art/Palette/DrahoPalette.json";

        /// <summary>Materials that keep their own colour: the cat's painted eye and nose, the lid line.</summary>
        static readonly HashSet<string> Keep = new HashSet<string> { "Eye", "Pupil", "Nose", "Glint" };

        [Serializable] class Entry { public string name, hex; }
        [Serializable] class Palette { public Entry[] colors; }

        static List<(string name, Color color, Vector3 lab)> palette;

        /// <summary>The palette colour for a material colour, or the colour itself when it stays (see the rules).</summary>
        public static Color Apply(string materialName, Color color, Texture texture)
        {
            if (!Enabled || Keep.Contains(materialName)) return color;
            if (color.a < 0.99f) return color;                           // glass and other see-through materials
            if (texture != null && Mathf.Min(color.r, Mathf.Min(color.g, color.b)) > 0.9f)
                return color;                                            // a white tint: the texture has the colour
            var p = Load();
            if (p.Count == 0) return color;
            Vector3 lab = OkLab(color);
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < p.Count; i++)
            {
                // Hue and chroma count 3x as much as lightness: a grey that turns green looks wrong, a grey that
                // gets a little darker does not (tested on the 66 layout colours, docs/ART_STYLE.md §1).
                Vector3 d3 = p[i].lab - lab;
                float d = d3.x * d3.x + ChromaWeight * (d3.y * d3.y + d3.z * d3.z);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            Color c = p[best].color;
            c.a = color.a;
            return c;
        }

        /// <summary>A palette colour by name (for code that wants a role, not the nearest match).</summary>
        public static Color Named(string name, Color fallback)
        {
            foreach (var e in Load())
                if (e.name == name) return e.color;
            return fallback;
        }

        static List<(string name, Color color, Vector3 lab)> Load()
        {
            if (palette != null) return palette;
            palette = new List<(string name, Color color, Vector3 lab)>();
            if (!File.Exists(JsonPath)) { Debug.LogWarning($"Draho: palette {JsonPath} not found; colours stay as they are."); return palette; }
            var data = JsonUtility.FromJson<Palette>(File.ReadAllText(JsonPath));
            var seen = new HashSet<string>();
            foreach (var e in data.colors)
            {
                if (!seen.Add(e.hex.ToUpperInvariant())) continue;   // some names share a colour
                if (ColorUtility.TryParseHtmlString(e.hex, out var c)) palette.Add((e.name, c, OkLab(c)));
            }
            Debug.Log($"Draho: palette with {palette.Count} colours");
            return palette;
        }

        /// <summary>sRGB colour (as Unity stores it in a Color) to OKLab (Björn Ottosson, 2020).</summary>
        static Vector3 OkLab(Color c)
        {
            float r = Linear(c.r), g = Linear(c.g), b = Linear(c.b);
            float l = Mathf.Pow(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b, 1f / 3f);
            float m = Mathf.Pow(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b, 1f / 3f);
            float s = Mathf.Pow(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b, 1f / 3f);
            return new Vector3(
                0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
        }

        static float Linear(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    }
}
