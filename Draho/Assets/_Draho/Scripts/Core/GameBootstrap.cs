using UnityEngine;

namespace Draho.Core
{
    /// <summary>Global runtime settings. iOS runs at 30 fps unless we ask for more.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] int targetFrameRate = 60;

        void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
