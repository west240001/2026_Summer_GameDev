using BackbeatDiner.Core;
using Unity.Netcode;
using UnityEngine;

namespace BackbeatDiner.Net
{
    /// <summary>
    /// Minimal NGO sync for the vertical slice: host writes SalesScore + chart index;
    /// clients receive NetworkVariables (single-role fullscreen unchanged).
    /// </summary>
    public sealed class DaySessionNetSync : NetworkBehaviour
    {
        public static DaySessionNetSync Instance { get; private set; }

        private readonly NetworkVariable<int> _salesScore = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _chartIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private DaySessionState _session;
        private DaySetlistRunner _runner;
        private Text _statusLabel;

        public int SyncedSalesScore => _salesScore.Value;
        public int SyncedChartIndex => _chartIndex.Value;

        public void BindLocalSources(DaySessionState session, DaySetlistRunner runner, Text statusLabel = null)
        {
            _session = session;
            _runner = runner;
            _statusLabel = statusLabel;
            RefreshStatusLabel();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _salesScore.OnValueChanged += HandleScoreChanged;
            _chartIndex.OnValueChanged += HandleChartChanged;
            RefreshStatusLabel();
            Debug.Log(
                $"[Netcode] DaySessionNetSync spawn IsServer={IsServer} score={_salesScore.Value} chart={_chartIndex.Value}");
        }

        public override void OnNetworkDespawn()
        {
            _salesScore.OnValueChanged -= HandleScoreChanged;
            _chartIndex.OnValueChanged -= HandleChartChanged;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            if (_session != null && _salesScore.Value != _session.SalesScore)
            {
                _salesScore.Value = _session.SalesScore;
            }

            var chart = _runner != null ? _runner.CurrentIndex : 0;
            if (_chartIndex.Value != chart)
            {
                _chartIndex.Value = chart;
            }
        }

        private void HandleScoreChanged(int previous, int current)
        {
            _ = previous;
            RefreshStatusLabel();
            Debug.Log($"[Netcode] Synced salesScore → {current}");
        }

        private void HandleChartChanged(int previous, int current)
        {
            _ = previous;
            RefreshStatusLabel();
            Debug.Log($"[Netcode] Synced chartIndex → {current}");
        }

        private void RefreshStatusLabel()
        {
            if (_statusLabel == null)
            {
                return;
            }

            var mode = NetworkManager != null && NetworkManager.IsHost
                ? "Host"
                : NetworkManager != null && NetworkManager.IsServer
                    ? "Server"
                    : NetworkManager != null && NetworkManager.IsClient
                        ? "Client"
                        : "Net";
            _statusLabel.text =
                $"NET {mode} · 売上同期 {_salesScore.Value} · チャート {_chartIndex.Value}";
        }
    }
}
