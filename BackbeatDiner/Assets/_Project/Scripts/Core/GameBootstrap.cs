using UnityEngine;
using UnityEngine.SceneManagement;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// 起動時の常駐シェル。プロトはシーン遷移・マネージャ登録の起点にする。
    /// Week1: RhythmPrototype シーンでは専用ブートが担当するため軽くログのみ。
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
            var scene = SceneManager.GetActiveScene().name;
            Debug.Log($"[BACKBEAT DINER] Bootstrap ready — scene={scene}. Week1: open RhythmPrototype (Space hit / 1-4 lexicon).");
        }
    }
}
