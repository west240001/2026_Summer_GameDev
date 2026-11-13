using UnityEditor;
using UnityEngine;

namespace BackbeatDiner.Editor
{
    /// <summary>
    /// Docs / sanity check for the NGO host-join scaffold (runtime NetworkManager; no scene bake required).
    /// Menu: BACKBEAT DINER / Netcode Scaffold Info
    /// </summary>
    public static class NetcodeScaffoldBuilder
    {
        [MenuItem("BACKBEAT DINER/Netcode Scaffold Info")]
        public static void ShowInfo()
        {
            EditorUtility.DisplayDialog(
                "BACKBEAT DINER — Netcode scaffold",
                "TitleMenu: 役選択 → ホスト作成 / 参加 localhost (127.0.0.1:7777).\n" +
                "VerticalSlice: single-role fullscreen.\n" +
                "DaySessionNetSync: host beat phase + prep/sales + RPC hits/C1/C2/C3.\n" +
                "NetworkManager is created at runtime (UnityTransport).\n" +
                "Scripts: Assets/_Project/Scripts/Net/",
                "OK");
            Debug.Log("[NetcodeScaffoldBuilder] See Assets/_Project/Scripts/Net/README.md");
        }
    }
}
