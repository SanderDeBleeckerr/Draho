using System;
using System.Collections.Generic;
using Draho.Cat;
using UnityEngine;

namespace Draho.Core
{
    /// <summary>
    /// Save data: the story beat, the last safe place of the cat, and one-time story moments already played
    /// (the epilogue gifts, "gift_..."). Stored with PlayerPrefs (in the web build: the browser's storage), so the
    /// game continues where it stopped. Add ?fresh=1 to the URL to start a new game.
    /// </summary>
    public class Progress : MonoBehaviour
    {
        [Serializable]
        class Data
        {
            public int beat = -1;
            public bool hasPosition;
            public float x, y, z, yaw;
            // Older saves also hold coins, feathers, nap spots and bounty counts: JsonUtility skips those fields.
            public List<string> collected = new List<string>();
        }

        const string Key = "draho_save_v1";
        public static Progress Instance { get; private set; }

        [SerializeField] float autosaveInterval = 8f;
        [Tooltip("Save the position only after the cat moved this far (a save writes to the browser storage).")]
        [SerializeField] float autosaveDistance = 2f;

        Data data = new Data();
        CatMotor cat;
        float autosave;

        public event Action Changed;
        public int SavedBeat => data.beat;

        void Awake()
        {
            Instance = this;
            string url = Application.absoluteURL ?? "";
            if (url.Contains("fresh=1")) PlayerPrefs.DeleteKey(Key);
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<Data>(json) ?? new Data(); }
                catch (Exception) { data = new Data(); }
            }
            cat = FindAnyObjectByType<CatMotor>();
            Debug.Log($"SAVE loaded: beat {data.beat}");
        }

        void Update()
        {
            if (cat == null) return;
            autosave -= Time.deltaTime;
            if (autosave > 0f || !cat.IsGrounded || cat.IsClimbing) return;
            autosave = autosaveInterval;
            Vector3 p = cat.transform.position;
            if (data.hasPosition && Vector3.Distance(p, new Vector3(data.x, data.y, data.z)) < autosaveDistance) return;
            data.hasPosition = true;
            data.x = p.x; data.y = p.y; data.z = p.z; data.yaw = cat.transform.eulerAngles.y;
            Save();
        }

        public bool TryGetSavedPosition(out Vector3 position, out float yaw)
        {
            position = new Vector3(data.x, data.y, data.z);
            yaw = data.yaw;
            return data.hasPosition;
        }

        public void BeatReached(int beat)
        {
            if (beat > data.beat) data.beat = beat;
            Save();
        }

        /// <summary>True when this one-time story moment has already played.</summary>
        public bool IsDone(string id) => data.collected.Contains(id);

        /// <summary>Remember that a one-time story moment has played.</summary>
        public void MarkDone(string id)
        {
            if (IsDone(id)) return;
            data.collected.Add(id);
            Save();
        }

        /// <summary>Delete the save and start the game again at the beginning (the "New game" button).</summary>
        public void NewGame()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            data = new Data();
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
