using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BackbeatDiner.Net
{
    /// <summary>
    /// Creates NetworkManager + UnityTransport at runtime, starts Host/Client on localhost,
    /// and registers the DaySessionNetSync network prefab (fixed GlobalObjectIdHash).
    /// </summary>
    public static class NetcodeSessionBootstrap
    {
        private static GameObject _syncPrefab;
        private static FieldInfo _globalObjectIdHashField;

        public static NetworkManager EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                RegisterSyncPrefab(NetworkManager.Singleton);
                return NetworkManager.Singleton;
            }

            var go = new GameObject("NetworkManager");
            var nm = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null)
            {
                nm.NetworkConfig = new NetworkConfig();
            }

            nm.NetworkConfig.EnableSceneManagement = false;
            nm.NetworkConfig.ForceSamePrefabs = true;
            nm.NetworkConfig.NetworkTransport = transport;

            RegisterSyncPrefab(nm);
            Object.DontDestroyOnLoad(go);
            Debug.Log("[Netcode] NetworkManager + UnityTransport ready (scene management off).");
            return nm;
        }

        public static void ApplyConnectionData(string address, ushort port, bool listenAll)
        {
            var nm = EnsureNetworkManager();
            var transport = nm.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null)
            {
                transport = nm.gameObject.GetComponent<UnityTransport>();
                nm.NetworkConfig.NetworkTransport = transport;
            }

            if (transport == null)
            {
                Debug.LogError("[Netcode] UnityTransport missing.");
                return;
            }

            var listen = listenAll ? "0.0.0.0" : address;
            transport.SetConnectionData(address, port, listen);
            Debug.Log($"[Netcode] Connection → {address}:{port} (listen={listen})");
        }

        public static bool StartHost(string address = NetSessionConfig.DefaultAddress, ushort port = NetSessionConfig.DefaultPort)
        {
            NetSessionConfig.ConfigureHost(address, port);
            ApplyConnectionData(NetSessionConfig.Address, NetSessionConfig.Port, listenAll: true);
            var nm = NetworkManager.Singleton;
            if (nm.IsListening)
            {
                return nm.IsHost;
            }

            var ok = nm.StartHost();
            Debug.Log(ok
                ? $"[Netcode] Host started on {NetSessionConfig.Address}:{NetSessionConfig.Port}"
                : "[Netcode] StartHost FAILED");
            return ok;
        }

        public static bool StartClient(string address = NetSessionConfig.DefaultAddress, ushort port = NetSessionConfig.DefaultPort)
        {
            NetSessionConfig.ConfigureClient(address, port);
            ApplyConnectionData(NetSessionConfig.Address, NetSessionConfig.Port, listenAll: false);
            var nm = NetworkManager.Singleton;
            if (nm.IsListening)
            {
                return nm.IsClient;
            }

            var ok = nm.StartClient();
            Debug.Log(ok
                ? $"[Netcode] Client connecting to {NetSessionConfig.Address}:{NetSessionConfig.Port}"
                : "[Netcode] StartClient FAILED");
            return ok;
        }

        public static GameObject GetOrCreateSyncPrefab()
        {
            if (_syncPrefab != null)
            {
                return _syncPrefab;
            }

            _syncPrefab = new GameObject("DaySessionNetSyncPrefab");
            _syncPrefab.SetActive(false);
            Object.DontDestroyOnLoad(_syncPrefab);

            var netObj = _syncPrefab.AddComponent<NetworkObject>();
            SetGlobalObjectIdHash(netObj, NetSessionConfig.DaySessionSyncPrefabHash);
            _syncPrefab.AddComponent<DaySessionNetSync>();
            return _syncPrefab;
        }

        public static DaySessionNetSync SpawnSyncOnServerIfNeeded()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
            {
                return DaySessionNetSync.Instance;
            }

            if (DaySessionNetSync.Instance != null && DaySessionNetSync.Instance.IsSpawned)
            {
                return DaySessionNetSync.Instance;
            }

            var prefab = GetOrCreateSyncPrefab();
            var instance = Object.Instantiate(prefab);
            instance.name = "DaySessionNetSync";
            instance.SetActive(true);
            var netObj = instance.GetComponent<NetworkObject>();
            SetGlobalObjectIdHash(netObj, NetSessionConfig.DaySessionSyncPrefabHash);
            netObj.Spawn(destroyWithScene: false);
            Debug.Log("[Netcode] DaySessionNetSync spawned (beat phase + role input sync).");
            return instance.GetComponent<DaySessionNetSync>();
        }

        private static void RegisterSyncPrefab(NetworkManager nm)
        {
            if (nm == null)
            {
                return;
            }

            var prefab = GetOrCreateSyncPrefab();
            if (nm.NetworkConfig.Prefabs.Contains(prefab))
            {
                return;
            }

            try
            {
                nm.AddNetworkPrefab(prefab);
                Debug.Log($"[Netcode] Registered DaySessionNetSync prefab hash=0x{NetSessionConfig.DaySessionSyncPrefabHash:X8}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Netcode] AddNetworkPrefab: " + ex.Message);
            }
        }

        private static void SetGlobalObjectIdHash(NetworkObject netObj, uint hash)
        {
            if (netObj == null)
            {
                return;
            }

            _globalObjectIdHashField ??= typeof(NetworkObject).GetField(
                "GlobalObjectIdHash",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (_globalObjectIdHashField == null)
            {
                Debug.LogError("[Netcode] Could not set NetworkObject.GlobalObjectIdHash via reflection.");
                return;
            }

            _globalObjectIdHashField.SetValue(netObj, hash);
        }
    }
}
