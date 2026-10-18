using Draho.Cat;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// An old fisherman on the quay with a bucket of fish: "Grandmother's footsteps" (red light, green light).
    /// He watches his float; now and then he hums (the warning), then turns to look along the quay. A cat that
    /// moves near him while he looks is shooed back up the quay (a setback, not a game over, like being sent back
    /// to the start line). A cat that reaches the bucket while he watches the float takes a fish (research round 46).
    /// </summary>
    public class Fisherman : MonoBehaviour
    {
        enum State { Fishing, Humming, Looking }

        [SerializeField] Transform torso;
        [SerializeField] Transform bucket;
        [SerializeField] Transform bobber;
        [SerializeField] Vector3 shooPoint = new Vector3(-33f, 0.05f, -14f);
        [SerializeField] float watchDistance = 7f;
        [SerializeField] float grabDistance = 0.55f;
        [SerializeField] float warningTime = 1.3f;

        CatMotor cat;
        State state = State.Fishing;
        float timer, phase, turn;
        Vector3 bobberBase;
        int shoos;

        public bool FishTaken { get; private set; }

        public void Configure(Transform torsoPivot, Transform bucketPoint, Transform floatObject)
        {
            torso = torsoPivot;
            bucket = bucketPoint;
            bobber = floatObject;
        }

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            timer = Random.Range(4f, 6f);
            if (bobber != null) bobberBase = bobber.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            timer -= dt;
            if (bobber != null) bobber.position = bobberBase + Vector3.up * (Mathf.Sin(phase * 2.1f) * 0.02f);
            if (cat == null) return;
            Vector3 toCat = cat.transform.position - transform.position;
            toCat.y = 0f;
            bool near = toCat.magnitude < watchDistance;

            switch (state)
            {
                case State.Fishing:
                    if (timer <= 0f)
                    {
                        state = State.Humming;
                        timer = warningTime;
                        if (near) ProgressHud.Toast("\"Hmm-hmm-hmmm...\" The fisherman is going to look round. Freeze!");
                        Debug.Log("STORY fisherman hums");
                    }
                    break;
                case State.Humming:
                    if (timer <= 0f) { state = State.Looking; timer = Random.Range(2.2f, 3f); }
                    break;
                case State.Looking:
                    if (near && (cat.Speed > 0.05f || cat.IsAirborne) && !FishTaken)
                    {
                        shoos++;
                        Debug.Log($"STORY fisherman shoos the cat ({shoos})");
                        ProgressHud.Toast("\"Hey! Shoo, cat!\" Back up the quay you go. Move only while he watches his float.");
                        cat.Teleport(shooPoint, Quaternion.Euler(0f, 180f, 0f));
                        state = State.Fishing;
                        timer = Random.Range(3f, 5f);
                        break;
                    }
                    if (timer <= 0f) { state = State.Fishing; timer = Random.Range(3.5f, 7f); }
                    break;
            }

            // He turns his upper body towards the quay while he looks; back to the float after.
            float target = state == State.Looking ? 1f : state == State.Humming ? 0.25f : 0f;
            turn = Mathf.MoveTowards(turn, target, dt * (state == State.Looking ? 3f : 1.5f));
            if (torso != null)
            {
                float yaw = Vector3.SignedAngle(transform.forward, toCat.sqrMagnitude > 0.01f ? toCat : transform.forward, Vector3.up);
                torso.localRotation = Quaternion.Euler(0f, Mathf.Clamp(yaw, -150f, 150f) * turn, 0f);
            }

            if (!FishTaken && bucket != null && state != State.Looking)
            {
                Vector3 toBucket = bucket.position - cat.transform.position;
                toBucket.y = 0f;
                if (toBucket.magnitude < grabDistance)
                {
                    FishTaken = true;
                    ProgressHud.Toast("A fish! Cold, wet and wonderful.");
                    Debug.Log("STORY took a fish from the bucket");
                }
            }
        }

        /// <summary>Test hook: SendMessage('Fisherman', 'TestLook'): hum and look now.</summary>
        public void TestLook() { state = State.Humming; timer = warningTime; }
    }
}
