using Draho.Story;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Time goes round again in the free-roam epilogue (research round 72: open-world games with a day-night cycle
    /// compress a day into tens of minutes, and spend most of it in daylight), so the night things come back after
    /// the story: the lamps and their cones, the moths, the glow-worms, the stars and shooting stars, the eyeshine.
    /// A day of about 16 minutes: day, evening, night, dawn.
    /// </summary>
    public class DayCycle : MonoBehaviour
    {
        static readonly (SkyMood.Mood mood, float minutes, float blend, string line)[] Day =
        {
            (SkyMood.Mood.Day, 8f, 20f, null),
            (SkyMood.Mood.Evening, 2f, 40f, "The sun sinks behind the roofs. Evening comes to the town."),
            (SkyMood.Mood.Night, 5f, 40f, "Night. The lamps come on, the moths are back, and something glows in the wild grass."),
            (SkyMood.Mood.Dawn, 1.5f, 30f, "Birds sing: a new morning."),
        };

        int phase = -1;
        float left;
        bool running;

        void Update()
        {
            if (!StoryDirector.InEpilogue) { running = false; return; }
            if (!running) { running = true; phase = 0; left = Day[0].minutes * 60f; } // the epilogue starts by day
            left -= Time.deltaTime;
            if (left > 0f) return;
            Advance(false);
        }

        void Advance(bool quick)
        {
            phase = (phase + 1) % Day.Length;
            var p = Day[phase];
            left = p.minutes * 60f;
            SkyMood.Set(p.mood, quick ? 2f : p.blend);
            if (p.line != null) ProgressHud.Toast(p.line);
            Debug.Log($"SKY cycle: {p.mood} for {p.minutes} min");
        }

        /// <summary>Test hook: SendMessage('DayCycle', 'TestAdvance'): skip to the next part of the day.</summary>
        public void TestAdvance() { if (running) Advance(true); }
    }
}
