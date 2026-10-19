using UnityEngine;

namespace Draho.UI
{
    /// <summary>
    /// One-time hints for the rings (research round 95: teach a verb in context, the first time it can be used,
    /// once, in a few words). The first time the cat comes near a ring of a kind that is not found yet, a line says
    /// what to do there.
    /// </summary>
    public static class RingHints
    {
        static readonly System.Collections.Generic.HashSet<string> shown = new System.Collections.Generic.HashSet<string>();

        /// <summary>Call while the cat is near a ring: shows the hint for that kind once per session.</summary>
        public static void Near(string kind)
        {
            if (!shown.Add(kind)) return;
            string line = kind switch
            {
                "nap" => "A warm, golden ring: a perfect nap spot. Stand still in it, and you sit down for a nap.",
                "scratch" => "A tan ring at the foot of something rough: jump without moving to rear up and scratch it.",
                "view" => "A pale blue ring up high: a viewpoint. Stand still in it a moment and look round.",
                _ => null,
            };
            if (line == null) return;
            ProgressHud.Toast(line);
            Debug.Log($"STORY hint: {kind}");
        }
    }
}
