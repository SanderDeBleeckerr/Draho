using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A mouse that nibbles near its hole and runs to the safest hole when it hears the cat.
    /// A running cat is loud, a walking cat less, a crouching (sneaking) cat is almost silent.
    /// The cat catches the mouse when it touches it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Mouse : MonoBehaviour
    {
        enum State { Hidden, Wander, Flee, Caught }

        [SerializeField] Transform[] holes;
        [SerializeField] Transform body;
        [SerializeField] Transform tail;
        [SerializeField] float wanderSpeed = 0.22f;
        [SerializeField] float fleeSpeed = 1.3f;
        [SerializeField] float wanderRadius = 0.7f;
        [Header("Hearing distance (m)")]
        [SerializeField] float hearRunning = 2.4f;
        [SerializeField] float hearWalking = 1.2f;
        [SerializeField] float hearSneaking = 0.4f;
        [SerializeField] float hearStill = 0.25f;
        [SerializeField] float catchRadius = 0.28f;
        [SerializeField] float hideTime = 4f;

        CharacterController controller;
        CatMotor cat;
        State state = State.Hidden;
        Transform hole;      // the hole the mouse lives at now
        Vector3 target;
        float timer;
        float stuckTimer;
        float verticalVelocity;
        float phase;

        public bool IsCaught => state == State.Caught;
        public bool IsVisible => state == State.Wander || state == State.Flee;
        /// <summary>Raised once, when the cat catches the mouse.</summary>
        public event System.Action Caught;

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Transform[] mouseHoles, Transform bodyVisual, Transform tailVisual)
        {
            holes = mouseHoles;
            body = bodyVisual;
            tail = tailVisual;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            cat = FindAnyObjectByType<CatMotor>();
            // The mouse and the cat share a layer; their capsules must not push each other.
            Physics.IgnoreLayerCollision(gameObject.layer, gameObject.layer, true);
        }

        void OnEnable()
        {
            if (holes != null && holes.Length > 0) HideIn(holes[0], 1.5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Hidden:
                    timer -= dt;
                    if (timer <= 0f) ComeOut(PickHole(exclude: hole));
                    return;
                case State.Caught:
                    return;
            }

            if (CanBeCaught())
            {
                state = State.Caught;
                SetVisible(false);
                Debug.Log("STORY mouse caught");
                Caught?.Invoke();
                return;
            }

            if (state == State.Wander && HearsCat())
            {
                state = State.Flee;
                hole = PickHole(exclude: null);
                target = hole.position;
                stuckTimer = 0f;
            }

            if (state == State.Wander)
            {
                timer -= dt;
                if (timer <= 0f || Flat(target - transform.position).magnitude < 0.05f)
                {
                    // Nibble for a moment, then go to a new spot near the hole.
                    Vector2 r = Random.insideUnitCircle * wanderRadius;
                    target = hole.position + hole.forward * (wanderRadius + 0.1f) + new Vector3(r.x, 0f, r.y);
                    timer = Random.Range(1.5f, 3.5f);
                }
            }
            else if (Flat(hole.position - transform.position).magnitude < 0.12f)
            {
                HideIn(hole, hideTime);
                return;
            }

            float speed = state == State.Flee ? fleeSpeed : wanderSpeed;
            bool nibbling = state == State.Wander && timer > 0f && Flat(target - transform.position).magnitude < 0.05f;
            Move(nibbling ? 0f : speed, dt);
        }

        void Move(float speed, float dt)
        {
            Vector3 to = Flat(target - transform.position);
            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            if (speed > 0f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up), 720f * dt);

            verticalVelocity = controller.isGrounded ? -0.5f : verticalVelocity - 9.81f * dt;
            Vector3 before = transform.position;
            controller.Move((transform.forward * speed + Vector3.up * verticalVelocity) * dt);

            // Blocked by furniture: try another hole (fleeing) or another spot (wandering).
            float moved = Flat(transform.position - before).magnitude;
            stuckTimer = speed > 0f && moved < speed * dt * 0.25f ? stuckTimer + dt : 0f;
            if (stuckTimer > 0.4f)
            {
                stuckTimer = 0f;
                if (state == State.Flee) { hole = PickHole(exclude: hole); target = hole.position; }
                else timer = 0f;
            }

            // Scurry: quick body bob and tail swing.
            phase += dt * (speed > 0f ? 40f : 6f);
            if (body != null) body.localPosition = new Vector3(0f, speed > 0f ? Mathf.Abs(Mathf.Sin(phase)) * 0.006f : 0f, 0f);
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * 0.5f) * 25f, 0f);
        }

        bool HearsCat()
        {
            if (cat == null) return false;
            float radius = cat.IsCrouching ? hearSneaking
                : cat.Speed < 0.05f ? hearStill
                : cat.Speed > cat.Stats.walkSpeed * 1.5f ? hearRunning
                : hearWalking;
            if (cat.IsAirborne) radius = Mathf.Max(radius, hearWalking); // landing makes noise
            return Flat(cat.transform.position - transform.position).magnitude < radius;
        }

        bool CanBeCaught()
        {
            if (cat == null) return false;
            Vector3 d = cat.transform.position - transform.position;
            return Flat(d).magnitude < catchRadius && Mathf.Abs(d.y) < 0.3f;
        }

        /// <summary>The hole that is far from the cat and not too far from the mouse.</summary>
        Transform PickHole(Transform exclude)
        {
            Transform best = holes[0];
            float bestScore = float.MinValue;
            foreach (var h in holes)
            {
                if (h == exclude && holes.Length > 1) continue;
                float fromCat = cat != null ? Flat(h.position - cat.transform.position).magnitude : 0f;
                float fromMouse = Flat(h.position - transform.position).magnitude;
                float score = fromCat - 0.6f * fromMouse + Random.Range(0f, 0.3f);
                if (score > bestScore) { bestScore = score; best = h; }
            }
            return best;
        }

        void HideIn(Transform h, float seconds)
        {
            state = State.Hidden;
            hole = h;
            timer = seconds;
            SetVisible(false);
        }

        void ComeOut(Transform h)
        {
            hole = h;
            controller.enabled = false;
            transform.SetPositionAndRotation(h.position, h.rotation);
            controller.enabled = true;
            target = h.position + h.forward * wanderRadius;
            timer = 2f;
            state = State.Wander;
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
