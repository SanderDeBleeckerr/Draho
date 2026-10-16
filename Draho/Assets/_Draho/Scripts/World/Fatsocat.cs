using Draho.Cat;
using Draho.Core;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Fatsocat, the big white rival. Now and then he comes over the end of the garden and walks at Drahomil.
    /// Stand your ground (stay close and face him) and he gives up and leaves: the garden is defended.
    /// Run away, and he sits in the middle of the lawn for a while, very pleased with himself.
    /// </summary>
    public class Fatsocat : MonoBehaviour
    {
        enum State { Away, Arrive, Standoff, Retreat, Gloat }

        [SerializeField] Animator animator;
        [SerializeField] Rect garden = new Rect(-23.4f, 0.4f, 22.8f, 4.2f); // x, z of the lawn and terrace
        [SerializeField] Vector3 entry = new Vector3(-23.6f, 0f, 2.5f);
        [SerializeField] Vector2 awayTime = new Vector2(90f, 180f);
        [SerializeField] float walkSpeed = 0.45f;
        [SerializeField] float standoffDistance = 2.2f;
        [SerializeField] float holdTime = 4f;
        [SerializeField] int reward = 10;
        [SerializeField] int firstBeat = 5; // he shows up once the garden is part of the story

        CatMotor cat;
        State state = State.Away;
        float timer, hold;

        public void Configure(Animator catAnimator) => animator = catAnimator;

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            timer = Random.Range(25f, 45f);
            SetVisible(false);
        }

        /// <summary>Test hook: SendMessage('Fatsocat', 'TestArrive').</summary>
        public void TestArrive() => timer = 0f;

        void Update()
        {
            if (animator != null) animator.SetBool("Grounded", true);
            float dt = Time.deltaTime;
            Vector3 p = transform.position;
            Vector3 catPos = cat != null ? cat.transform.position : p;
            bool catInGarden = cat != null && garden.Contains(new Vector2(catPos.x, catPos.z)) && catPos.y < 1.2f;
            Vector3 toCat = new Vector3(catPos.x - p.x, 0f, catPos.z - p.z);
            float d = toCat.magnitude;

            switch (state)
            {
                case State.Away:
                    timer -= dt;
                    int beat = Progress.Instance != null ? Progress.Instance.SavedBeat : 0;
                    if (timer > 0f || !catInGarden || beat < firstBeat) { Walk(0f); return; }
                    transform.position = entry;
                    SetVisible(true);
                    state = State.Arrive;
                    ProgressHud.Toast("Fatsocat jumps over the fence! Stand your ground: face him, do not run.");
                    Debug.Log("STORY Fatsocat arrives");
                    break;

                case State.Arrive:
                    if (!catInGarden) { state = State.Retreat; break; }
                    if (d > standoffDistance) { Face(toCat); Move(toCat.normalized, walkSpeed * 2f, dt); } // a purposeful trot
                    else { state = State.Standoff; hold = 0f; }
                    break;

                case State.Standoff:
                    Face(toCat);
                    Walk(0f);
                    bool facing = Vector3.Dot(cat.transform.forward, -toCat.normalized) > 0.6f;
                    bool fled = d > standoffDistance * 2.2f || (cat.Speed > cat.Stats.walkSpeed * 1.5f && Vector3.Dot(cat.transform.forward, toCat.normalized) > 0.3f);
                    if (fled)
                    {
                        state = State.Gloat;
                        timer = 25f;
                        ProgressHud.Toast("Drahomil ran. Fatsocat sits on the lawn like he owns it.");
                        Debug.Log("STORY Fatsocat wins the standoff");
                        break;
                    }
                    hold = facing && d < standoffDistance * 1.4f ? hold + dt : Mathf.Max(0f, hold - dt * 0.5f);
                    if (hold >= holdTime)
                    {
                        state = State.Retreat;
                        Progress.Instance?.AddCoins(reward, "garden defended");
                        Progress.Instance?.Save();
                        ProgressHud.Toast($"Fatsocat backs off and leaves. Garden defended! +{reward} coins");
                        Debug.Log("STORY Fatsocat retreats: garden defended");
                    }
                    break;

                case State.Gloat:
                    Walk(0f);
                    timer -= dt;
                    if (timer <= 0f) state = State.Retreat;
                    break;

                case State.Retreat:
                    Vector3 toEntry = new Vector3(entry.x - p.x, 0f, entry.z - p.z);
                    if (toEntry.magnitude < 0.3f)
                    {
                        SetVisible(false);
                        state = State.Away;
                        timer = Random.Range(awayTime.x, awayTime.y);
                        break;
                    }
                    Face(toEntry);
                    Move(toEntry.normalized, walkSpeed * 1.6f, dt);
                    break;
            }
        }

        void Move(Vector3 dir, float speed, float dt)
        {
            Vector3 next = transform.position + dir * speed * dt;
            next.x = Mathf.Clamp(next.x, garden.xMin - 0.5f, garden.xMax);
            next.z = Mathf.Clamp(next.z, garden.yMin, garden.yMax);
            transform.position = new Vector3(next.x, 0f, next.z);
            Walk(speed);
        }

        void Face(Vector3 dir)
        {
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized), 240f * Time.deltaTime);
        }

        void Walk(float speed) { if (animator != null) animator.SetFloat("Speed", speed); }

        void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }
    }
}
