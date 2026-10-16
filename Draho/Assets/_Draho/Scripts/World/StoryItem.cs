using Draho.Cat;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>An object the story needs (Drahomil's toy mouse): the cat picks it up by touching it.</summary>
    public class StoryItem : MonoBehaviour
    {
        [SerializeField] string message = "Got it!";
        [SerializeField] float pickRadius = 0.4f;

        CatMotor cat;
        public bool Taken { get; private set; }

        public void Configure(string pickMessage) => message = pickMessage;

        void Awake() => cat = FindAnyObjectByType<CatMotor>();

        void Update()
        {
            if (Taken || cat == null) return;
            if (Vector3.Distance(cat.transform.position, transform.position) > pickRadius) return;
            Taken = true;
            ProgressHud.Toast(message);
            Debug.Log($"STORY took {name}");
            gameObject.SetActive(false);
        }
    }
}
