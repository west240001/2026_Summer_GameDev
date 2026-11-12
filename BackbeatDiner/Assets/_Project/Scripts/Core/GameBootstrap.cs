using UnityEngine;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// 起動時の常駐シェル。プロトはシーン遷移・マネージャ登録の起点にする。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            var go = new GameObject(nameof(GameBootstrap));
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log("[BACKBEAT DINER] Bootstrap ready — Unity production scaffold.");
        }
    }
}
