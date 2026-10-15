using UnityEngine;

namespace Draho.World
{
    /// <summary>Slowly moves the ripple texture of the river: the water flows.</summary>
    public class WaterRipples : MonoBehaviour
    {
        [SerializeField] Vector2 flow = new Vector2(0.01f, -0.035f);

        Material material;
        Vector2 offset;

        void Awake() => material = GetComponent<Renderer>().material;

        void Update()
        {
            offset += flow * Time.deltaTime;
            material.SetTextureOffset("_BaseMap", offset);
        }
    }
}
