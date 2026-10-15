using Draho.Cat;
using UnityEngine;

namespace Draho.Core
{
    /// <summary>When the cat falls off the world, it comes back at the spawn point.</summary>
    public class FallRespawn : MonoBehaviour
    {
        [SerializeField] CatMotor motor;
        [SerializeField] Transform spawnPoint;
        [SerializeField] float killHeight = -10f;

        void LateUpdate()
        {
            if (motor.transform.position.y < killHeight)
                motor.Teleport(spawnPoint.position, spawnPoint.rotation);
        }
    }
}
