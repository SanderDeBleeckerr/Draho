using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Light inside the flat (docs/APARTMENT_GRAPHICS.md): the room is lit by its window and its lamps, not by an even
    /// ambient glow. While Drahomil is inside, SkyMood dims the ambient light; daylight comes in through the sliding
    /// glass door (a soft spot light outside it, with shadows, so the floor is bright by the glass and darker at the back)
    /// and through the court glass; the ceiling lamps come on from the evening, the spots above the sink are on all day.
    /// The colours and strengths follow the time of day (SkyMood.NightAmount: 0 day, 0.5 evening, 1 night).
    /// </summary>
    public class IndoorLight : MonoBehaviour
    {
        [SerializeField] Rect flat = Rect.MinMaxRect(0f, 0f, 7.6f, 5f);
        [Tooltip("Open-air parts inside the flat's outline (the inner court): outdoors.")]
        [SerializeField] Rect[] open = new Rect[0];
        [SerializeField] float ceiling = 2.6f;
        [SerializeField] Light window;
        [SerializeField] Light court;
        [SerializeField] Light[] lamps;
        [SerializeField] Light[] sinkSpots;
        [SerializeField] Renderer[] sinkFittings;

        static readonly Color Daylight = new Color(0.86f, 0.9f, 0.97f);
        static readonly Color Dusk = new Color(0.98f, 0.78f, 0.56f);
        static readonly Color Moonlight = new Color(0.55f, 0.64f, 0.85f);

        CatMotor cat;
        float inside;
        float[] lampBase;

        public void Configure(Light windowLight, Light courtLight, Light[] roomLamps, Light[] spots, Renderer[] fittings, Rect[] openAir)
        {
            open = openAir ?? new Rect[0];
            window = windowLight;
            court = courtLight;
            lamps = roomLamps;
            sinkSpots = spots;
            sinkFittings = fittings;
        }

        void Init()
        {
            if (lampBase != null) return;
            cat = FindAnyObjectByType<CatMotor>();
            lampBase = new float[lamps != null ? lamps.Length : 0];
            for (int i = 0; i < lampBase.Length; i++) lampBase[i] = lamps[i] != null ? lamps[i].intensity : 0f;
        }

        void Update()
        {
            Init();
            Refresh(cat != null ? cat.transform.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero), false);
        }

        /// <summary>Sets the lights for someone at <paramref name="viewer"/> (ArtCapture calls this in edit mode).</summary>
        public void Refresh(Vector3 viewer, bool instant)
        {
            Init();
            var at = new Vector2(viewer.x, viewer.z);
            bool inFlat = flat.Contains(at) && viewer.y < ceiling;
            foreach (var r in open) if (r.Contains(at)) inFlat = false;
            inside = instant ? (inFlat ? 1f : 0f) : Mathf.MoveTowards(inside, inFlat ? 1f : 0f, Time.deltaTime * 1.5f);
            SkyMood.Indoors = inside;

            float night = SkyMood.NightAmount;
            float evening = Mathf.Clamp01(1f - Mathf.Abs(night - 0.5f) * 2f);           // 1 at 0.5 (evening)
            Color sky = night < 0.5f ? Color.Lerp(Daylight, Dusk, night * 2f) : Color.Lerp(Dusk, Moonlight, (night - 0.5f) * 2f);
            if (window != null)
            {
                window.color = sky;
                // Spot lights fall off with the square of the distance: about 14 gives a bright patch 2-3 m inside.
                window.intensity = Mathf.Lerp(15f, 1.7f, night) + 2f * evening;
            }
            if (court != null)
            {
                court.color = sky;
                court.intensity = Mathf.Lerp(6f, 0.8f, night);
            }
            float lampsOn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.6f, night));
            for (int i = 0; i < lampBase.Length; i++)
                if (lamps[i] != null)
                {
                    lamps[i].intensity = lampBase[i] * lampsOn;
                    lamps[i].enabled = lampsOn > 0.01f;
                }
            float spots = Mathf.Lerp(4f, 7f, lampsOn);   // 2.6 m from the counter: a warm pool on the sink
            if (sinkSpots != null)
                foreach (var s in sinkSpots)
                    if (s != null) s.intensity = spots;
        }
    }
}
