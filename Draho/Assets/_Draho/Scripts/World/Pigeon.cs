using System.Collections.Generic;
using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A pigeon in the inner court. It walks and pecks on the round stones and flies up to a window sill or
    /// the gutter when the cat comes too close. Like real birds, it flees sooner from a cat that comes
    /// straight at it than from a cat that walks past to the side (direct versus tangential approach).
    /// Pigeons watch each other: when one flies up, the rest of the flock follows a moment later.
    /// <see cref="WasTouched"/> becomes true when the cat gets close enough to touch one on the ground.
    /// </summary>
    public class Pigeon : MonoBehaviour
    {
        enum State { Ground, Flying, Perched }

        static readonly List<Pigeon> flock = new List<Pigeon>();

        [SerializeField] Transform body;
        [SerializeField] Transform head;
        [SerializeField] Transform wingLeft;
        [SerializeField] Transform wingRight;
        [SerializeField] Vector3[] perches;
        [Tooltip("Where the pigeon walks and lands: x0, z0, x1, z1 (the court stones).")]
        [SerializeField] Vector4 yard = new Vector4(4.9f, 3.5f, 7.3f, 4.7f);

        [Header("Senses (m)")]
        [SerializeField] float fleeRunning = 2.6f;
        [SerializeField] float fleeWalking = 1.3f;
        [SerializeField] float fleeSneaking = 0.45f;
        [Tooltip("Multiplier on the flee distance when the cat comes straight at the pigeon.")]
        [SerializeField] float directFactor = 1.6f;
        [Tooltip("Multiplier on the flee distance when the cat walks past to the side.")]
        [SerializeField] float sideFactor = 0.6f;
        [SerializeField] float touchRadius = 0.32f;

        [Tooltip("Birds watch only their own flock (the court pigeons, the bakery gull).")]
        [SerializeField] string group = "court";

        [Header("Moves")]
        [SerializeField] float flySpeed = 3f;
        [SerializeField] float walkSpeed = 0.25f;

        CatMotor cat;
        State state = State.Ground;
        Vector3 from, to, walkTarget;
        float moveTime, moveDuration, timer, phase, alarmDelay = -1f;

        public bool OnGround => state == State.Ground;
        public bool WasTouched { get; private set; }
        public static bool AnyTouched { get; private set; }

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Transform bodyPart, Transform headPart, Transform left, Transform right, Vector3[] perchPoints, Vector4 yardRect)
        {
            body = bodyPart;
            head = headPart;
            wingLeft = left;
            wingRight = right;
            perches = perchPoints;
            yard = yardRect;
        }

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            walkTarget = transform.position;
            phase = Random.value * 10f;
            timer = Random.Range(0.5f, 2f);
        }

        void OnEnable() => flock.Add(this);
        void OnDisable() => flock.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            switch (state)
            {
                case State.Ground: UpdateGround(dt); break;
                case State.Flying: UpdateFlight(dt); break;
                case State.Perched: UpdatePerched(dt); break;
            }
        }

        void UpdateGround(float dt)
        {
            if (alarmDelay >= 0f)
            {
                // Another pigeon flew up: follow it after a short look.
                alarmDelay -= dt;
                if (alarmDelay < 0f) TakeOff("follows the flock");
                return;
            }
            if (cat != null)
            {
                Vector3 toPigeon = Flat(transform.position - cat.transform.position);
                float d = toPigeon.magnitude;
                if (d < touchRadius && !WasTouched)
                {
                    WasTouched = true;
                    if (group == "court") AnyTouched = true;
                    Debug.Log($"STORY {group} bird touched at {d:0.00} m");
                    TakeOff("touched");
                    return;
                }
                if (d < FleeDistance(toPigeon, out string how))
                {
                    TakeOff($"cat at {d:0.00} m, {how}");
                    return;
                }
            }

            // Walk and peck: the head nods with each step, like a real pigeon.
            Vector3 step = Flat(walkTarget - transform.position);
            if (step.magnitude > 0.03f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(step.normalized), 240f * dt);
                transform.position += transform.forward * walkSpeed * dt;
                if (head != null) head.localPosition = new Vector3(0f, 0.15f, 0.06f + Mathf.Sin(phase * 14f) * 0.02f);
                return;
            }
            timer -= dt;
            if (head != null)
                head.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(phase * 6f)) * 60f * (timer % 2f > 1.2f ? 1f : 0f), 0f, 0f);
            if (timer > 0f) return;
            if (head != null) head.localRotation = Quaternion.identity;
            walkTarget = RandomSpot();
            timer = Random.Range(0.8f, 2.5f);
        }

        float FleeDistance(Vector3 toPigeon, out string how)
        {
            float radius = cat.Speed < 0.05f ? fleeSneaking * 0.5f // a cat that sits still is part of the court
                : cat.IsCrouching ? fleeSneaking
                : cat.Speed > cat.Stats.walkSpeed * 1.5f ? fleeRunning
                : fleeWalking;
            if (cat.IsAirborne || cat.IsClimbing) radius = Mathf.Max(radius, fleeWalking);
            how = "still";
            if (cat.Speed > 0.05f && toPigeon.sqrMagnitude > 1e-4f)
            {
                // Straight at me, or past me?
                float facing = Vector3.Dot(Flat(cat.transform.forward).normalized, toPigeon.normalized);
                if (facing > 0.8f) { radius *= directFactor; how = "straight at it"; }
                else if (facing < 0.5f) { radius *= sideFactor; how = "from the side"; }
                else how = "at an angle";
            }
            return radius;
        }

        void TakeOff(string why)
        {
            Debug.Log($"STORY {(group == "court" ? "pigeon" : group)} flies up ({why})");
            FlyTo(PickPerch());
            timer = Random.Range(8f, 13f);
            // Alarm: the rest of the flock follows one by one.
            foreach (var other in flock)
                if (other != this && other.group == group && other.state == State.Ground && other.alarmDelay < 0f)
                    other.alarmDelay = Random.Range(0.15f, 0.5f);
        }

        void UpdateFlight(float dt)
        {
            moveTime += dt;
            float t = Mathf.Clamp01(moveTime / moveDuration);
            // Up the shaft: climb first, then glide across to the perch (or the reverse when landing).
            float rise = to.y > from.y ? Mathf.Sqrt(t) : t * t;
            Vector3 p = new Vector3(Mathf.Lerp(from.x, to.x, t), Mathf.Lerp(from.y, to.y, rise), Mathf.Lerp(from.z, to.z, t));
            Vector3 dir = Flat(p - transform.position);
            if (dir.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), 8f * dt);
            transform.position = p;
            Flap(t < 0.92f ? 1f : 0.3f);
            if (t < 1f) return;
            Flap(0f);
            state = to.y > 0.5f ? State.Perched : State.Ground;
            walkTarget = transform.position;
            if (state == State.Ground) timer = Random.Range(0.5f, 1.5f);
        }

        void UpdatePerched(float dt)
        {
            timer -= dt;
            if (head != null) head.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * 1.1f) * 50f, 0f); // looks down at the cat
            if (timer > 0f) return;
            // Come back down to a spot away from the cat; wait while the cat is right under the perch.
            Vector3 best = RandomSpot();
            for (int i = 0; i < 6; i++)
            {
                Vector3 spot = RandomSpot();
                if (CatDistance(spot) > CatDistance(best)) best = spot;
            }
            if (CatDistance(best) < 1.1f) { timer = 2f; return; }
            FlyTo(best);
        }

        float CatDistance(Vector3 p) => cat != null ? Flat(p - cat.transform.position).magnitude : 9f;

        Vector3 RandomSpot() => new Vector3(Random.Range(yard.x, yard.z), 0f, Random.Range(yard.y, yard.w));

        Vector3 PickPerch()
        {
            if (perches == null || perches.Length == 0) return transform.position + Vector3.up * 3f;
            // A free perch, the higher the safer.
            Vector3 best = perches[0];
            float bestScore = float.MinValue;
            foreach (var p in perches)
            {
                float taken = 0f;
                foreach (var other in flock)
                    if (other != this && Vector3.Distance(other.to, p) < 0.2f) taken = 3f;
                float score = p.y * 0.3f - taken + Random.Range(0f, 1.5f);
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        void FlyTo(Vector3 point)
        {
            state = State.Flying;
            alarmDelay = -1f;
            from = transform.position;
            to = point;
            moveTime = 0f;
            moveDuration = Mathf.Max(0.8f, Vector3.Distance(from, to) / flySpeed);
            if (head != null) head.localRotation = Quaternion.identity;
        }

        void Flap(float amount)
        {
            float angle = amount > 0f ? (55f + Mathf.Sin(phase * 30f) * 55f) * amount : 0f;
            if (wingLeft != null) wingLeft.localRotation = Quaternion.Euler(0f, 0f, -angle);
            if (wingRight != null) wingRight.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (body != null) body.localRotation = Quaternion.Euler(amount > 0f ? -35f : 0f, 0f, 0f);
        }

        /// <summary>Test hook: SendMessage('Pigeon_1', 'TestStartle') from the playtest.</summary>
        public void TestStartle() { if (state == State.Ground) TakeOff("test"); }

        /// <summary>Test hook: SendMessage('Pigeon_1', 'TestTouch'): as if the cat touched this pigeon.</summary>
        public void TestTouch()
        {
            WasTouched = true;
            if (group == "court") AnyTouched = true;
            Debug.Log($"STORY {group} bird touched (test)");
            if (state == State.Ground) TakeOff("touched");
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
