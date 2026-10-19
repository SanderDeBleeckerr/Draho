using Draho.Story;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The humans watch TV in the evening (the epilogue at night): the screen and a cool light on the walls flicker as
    /// the picture changes (research round 77: TV light reads as cool blue-white and changes brightness and colour
    /// at every cut, every few seconds, with small changes between). Off by day and before the humans are home.
    /// </summary>
    public class TvGlow : MonoBehaviour
    {
        [SerializeField] Light glow;
        [SerializeField] Renderer screen;
        [SerializeField] float intensity = 1.4f;

        Material screenMat;
        Color shot = new Color(0.6f, 0.75f, 1f);
        float level, target, nextCut;

        public void Configure(Light tvLight, Renderer tvScreen)
        {
            glow = tvLight;
            screen = tvScreen;
        }

        void Start()
        {
            if (screen != null) screenMat = screen.material;
        }

        void Update()
        {
            bool on = StoryDirector.InEpilogue && SkyMood.NightAmount > 0.45f;
            nextCut -= Time.deltaTime;
            if (nextCut <= 0f)
            {
                // A cut: a new shot with its own brightness and colour.
                nextCut = Random.Range(0.6f, 4f);
                target = Random.Range(0.35f, 1f);
                shot = Color.Lerp(new Color(0.55f, 0.7f, 1f), new Color(1f, 0.9f, 0.8f), Random.value * 0.6f);
            }
            float flicker = 1f + 0.06f * Mathf.Sin(Time.time * 23f) * Mathf.Sin(Time.time * 7f);
            level = Mathf.MoveTowards(level, on ? target * flicker : 0f, Time.deltaTime * 6f);
            if (glow != null)
            {
                glow.enabled = level > 0.01f;
                glow.intensity = intensity * level;
                glow.color = shot;
            }
            if (screenMat != null) screenMat.SetColor("_EmissionColor", shot * (level * 2.2f));
        }
    }
}
