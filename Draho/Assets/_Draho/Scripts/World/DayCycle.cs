using Draho.Story;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The day goes round all game now: SkyMood's clock runs everywhere (24 game hours in SkyMood.DayMinutes real
    /// minutes, the sun on an arc, the moon at night; docs/DAY_NIGHT_PLAN.md). This only says what is happening in the
    /// free-roam epilogue, where no story text does: a line when evening, night and morning come.
    /// The night things (the lamps and their cones, the moths, the glow-worms, the stars, the eyeshine) follow the clock.
    /// </summary>
    public class DayCycle : MonoBehaviour
    {
        static string LineFor(SkyMood.Mood mood) => mood switch
        {
            SkyMood.Mood.Evening => "The sun sinks behind the roofs. Evening comes to the town.",
            SkyMood.Mood.Night => "Night. The lamps come on, the moths are back, and something glows in the wild grass.",
            SkyMood.Mood.Dawn => "Birds sing: a new morning.",
            _ => null,
        };

        SkyMood.Mood last = SkyMood.Mood.Day;

        void Update()
        {
            var mood = SkyMood.Current;
            if (mood == last) return;
            last = mood;
            Debug.Log($"SKY cycle: {mood} at {SkyMood.Hours:00.0} h");
            if (!StoryDirector.InEpilogue) return;
            string line = LineFor(mood);
            if (line != null) ProgressHud.Toast(line);
        }

        /// <summary>Test hook: SendMessage('DayCycle', 'TestAdvance'): skip to the next part of the day.</summary>
        public void TestAdvance()
        {
            var next = SkyMood.Current switch
            {
                SkyMood.Mood.Day => SkyMood.Mood.Evening,
                SkyMood.Mood.Evening => SkyMood.Mood.Night,
                SkyMood.Mood.Night => SkyMood.Mood.Dawn,
                _ => SkyMood.Mood.Day,
            };
            SkyMood.Set(next, 2f);
        }
    }
}
