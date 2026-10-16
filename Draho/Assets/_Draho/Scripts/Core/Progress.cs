using System;
using System.Collections.Generic;
using Draho.Cat;
using Draho.World;
using UnityEngine;

namespace Draho.Core
{
    /// <summary>
    /// Save data: story beat, the last safe place of the cat, coins, collected feathers and bounty counts.
    /// Stored with PlayerPrefs (in the web build: the browser's storage), so the game continues where it stopped.
    /// Add ?fresh=1 to the URL to start a new game.
    /// </summary>
    public class Progress : MonoBehaviour
    {
        [Serializable]
        class Data
        {
            public int beat = -1;
            public bool hasPosition;
            public float x, y, z, yaw;
            public int coins;
            public List<string> collected = new List<string>();
            public List<string> bountyNames = new List<string>();
            public List<int> bountyCounts = new List<int>();
        }

        const string Key = "draho_save_v1";
        public static Progress Instance { get; private set; }

        [SerializeField] int coinsPerBeat = 5;
        [SerializeField] float autosaveInterval = 6f;

        Data data = new Data();
        CatMotor cat;
        float autosave;

        public event Action Changed;
        public int Coins => data.coins;
        public int CollectedCount => data.collected.Count;
        public int TotalCollectables { get; private set; }
        public int SavedBeat => data.beat;
        public int CoinsPerBeat => coinsPerBeat;

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
            TotalCollectables = FindObjectsByType<Collectable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            Debug.Log($"SAVE loaded: beat {data.beat}, coins {data.coins}, feathers {data.collected.Count}/{TotalCollectables}");
        }

        void Start()
        {
            // Every mouse territory pays its bounty when its mouse is caught.
            foreach (var m in FindObjectsByType<Mouse>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mouse = m;
                mouse.Caught += () =>
                {
                    AddCoins(mouse.Bounty, $"bounty: {mouse.DisplayName}");
                    int i = data.bountyNames.IndexOf(mouse.DisplayName);
                    if (i < 0) { data.bountyNames.Add(mouse.DisplayName); data.bountyCounts.Add(0); i = data.bountyNames.Count - 1; }
                    data.bountyCounts[i]++;
                    Save();
                };
            }
        }

        void Update()
        {
            if (cat == null) return;
            autosave -= Time.deltaTime;
            if (autosave > 0f || !cat.IsGrounded || cat.IsClimbing) return;
            autosave = autosaveInterval;
            Vector3 p = cat.transform.position;
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
            if (beat > data.beat)
            {
                if (beat > 0) AddCoins(coinsPerBeat, $"story beat {beat}");
                data.beat = beat;
            }
            Save();
        }

        public void AddCoins(int amount, string reason)
        {
            data.coins += amount;
            Debug.Log($"COINS +{amount} ({reason}) = {data.coins}");
            Changed?.Invoke();
        }

        public bool IsCollected(string id) => data.collected.Contains(id);

        public void Collect(string id, int coins)
        {
            if (IsCollected(id)) return;
            data.collected.Add(id);
            AddCoins(coins, $"feather {id}");
            Debug.Log($"COLLECT {id}: {data.collected.Count}/{TotalCollectables}");
            Save();
        }

        public int BountyCount(string name)
        {
            int i = data.bountyNames.IndexOf(name);
            return i < 0 ? 0 : data.bountyCounts[i];
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
