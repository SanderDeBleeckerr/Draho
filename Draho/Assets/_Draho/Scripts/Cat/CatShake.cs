using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// The wet-cat shake (research round 68: wet mammals shake their skin at a tuned frequency, a cat at about 6 Hz;
    /// the twist starts at the head and runs back along the body; drops fly off sideways). Called after the river and
    /// when the cat comes home from the storm. Added after the Animator, round the cat's own forward axis.
    /// </summary>
    public class CatShake : MonoBehaviour
    {
        [SerializeField] ParticleSystem dust;
        [SerializeField] float frequency = 6f;
        [SerializeField] float duration = 0.9f;
        [SerializeField] float angle = 26f;

        Transform head, neck, chest, spine, hips;
        ParticleSystem drops;
        float t = -1f, delay;
        int emitted;

        void Start()
        {
            foreach (var b in GetComponentsInChildren<Transform>())
            {
                switch (b.name)
                {
                    case "Head": head = b; break;
                    case "Neck": neck = b; break;
                    case "Chest": chest = b; break;
                    case "Spine": spine = b; break;
                    case "Hips": hips = b; break;
                }
            }
            if (dust != null) drops = MakeDrops(dust);
        }

        /// <summary>Shake off the water, after a short pause (the cat finds its feet first).</summary>
        public void Shake(float after = 0.4f)
        {
            delay = after;
            t = 0f;
            Debug.Log("CAT shakes off the water");
        }

        void LateUpdate()
        {
            if (t < 0f) return;
            if (delay > 0f) { delay -= Time.deltaTime; return; }
            t += Time.deltaTime;
            if (t > duration) { t = -1f; return; }
            float env = Mathf.Sin(Mathf.PI * t / duration); // up and down again
            Vector3 axis = transform.forward;
            // The wave starts at the head and runs back: each part a little later.
            Twist(head, 0f, env, axis, 0.7f);
            Twist(neck, 0.03f, env, axis, 0.6f);
            Twist(chest, 0.06f, env, axis, 1f);
            Twist(spine, 0.09f, env, axis, 0.9f);
            Twist(hips, 0.12f, env, axis, 0.5f);
            if (drops != null && Random.value < 0.6f)
            {
                int n = Mathf.CeilToInt(5 * env);
                drops.Emit(n);
                emitted += n;
            }
        }

        void Twist(Transform bone, float lag, float env, Vector3 axis, float weight)
        {
            if (bone == null) return;
            float a = Mathf.Sin((t - lag) * frequency * Mathf.PI * 2f) * angle * env * weight;
            bone.rotation = Quaternion.AngleAxis(a, axis) * bone.rotation;
        }

        static ParticleSystem MakeDrops(ParticleSystem dust)
        {
            // A copy of the dust puff, tuned to small, fast, falling drops of water.
            var go = Instantiate(dust.gameObject, dust.transform.parent);
            go.name = "Drops";
            go.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            main.startColor = new Color(0.85f, 0.92f, 1f, 0.95f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 120;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            shape.rotation = Vector3.zero;
            var grow = ps.sizeOverLifetime;
            grow.enabled = false;
            var emission = ps.emission;
            emission.enabled = false;
            ps.Play(); // a stopped copy does not simulate what Emit() adds; with emission off, Play() adds nothing itself
            return ps;
        }

        /// <summary>Test hook: SendMessage('Drahomil', 'TestShake').</summary>
        public void TestShake()
        {
            Shake(0.1f);
            Invoke(nameof(Report), 0.6f);
        }

        void Report() => Debug.Log($"STATE shake drops: {(drops != null ? drops.particleCount : -1)} alive, {emitted} emitted, playing {(drops != null && drops.isPlaying)}, head {(head != null)}, chest {(chest != null)}");
    }
}
