using System.Collections.Generic;
using UnityEngine;

namespace Draho.Core
{
    /// <summary>
    /// Combines all scenery that never moves (walls, roofs, houses, fences, trees...) into a few big meshes
    /// when the level loads (static batching at run time), so the phone draws far fewer objects one by one.
    /// The list comes from the level builder; moving things (animals, the door, feathers) are not in it.
    /// </summary>
    public class StaticScenery : MonoBehaviour
    {
        [SerializeField] GameObject[] parts;

        public void Configure(GameObject[] staticParts) => parts = staticParts;

        void Awake()
        {
            var list = new List<GameObject>(parts.Length);
            foreach (var go in parts)
            {
                if (go == null) continue;
                var r = go.GetComponent<MeshRenderer>();
                var f = go.GetComponent<MeshFilter>();
                if (r == null || f == null || f.sharedMesh == null || !f.sharedMesh.isReadable || r.isPartOfStaticBatch) continue;
                list.Add(go);
            }
            float t = Time.realtimeSinceStartup;
            StaticBatchingUtility.Combine(list.ToArray(), gameObject);
            Debug.Log($"PERF static batching: {list.Count} objects combined in {(Time.realtimeSinceStartup - t) * 1000f:0} ms");
        }
    }
}
