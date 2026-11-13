using System;
using BackbeatDiner.Coop;
using BackbeatDiner.Core;
using BackbeatDiner.Front;
using BackbeatDiner.Rhythm;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace BackbeatDiner.Net
{
    /// <summary>
    /// Host-authoritative vertical-slice Netcode sync:
    /// song start (ServerTime) / beat phase / chart+gap, prep+sales,
    /// plus ServerRpc→ClientRpc for back hits, serve open/resolve, C1/C2/C3.
    /// Offline / solo F8 paths leave this unspawned.
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

        private readonly NetworkVariable<int> _prepUnits = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _servedCount = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _inGap = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _clockRunning = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _bpm = new NetworkVariable<float>(
            120f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _beatIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>Host ServerTime corresponding to local RhythmClock.StartDspTime.</summary>
        private readonly NetworkVariable<double> _songStartServerTime = new NetworkVariable<double>(
            0d,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private DaySessionState _session;
        private DaySetlistRunner _runner;
        private RhythmClock _clock;
        private HitInput _hitInput;
        private ServeRhythmWindow _serveRhythm;
        private Text _statusLabel;
        private Text _judgementLabel;

        private bool _suppressRelay;
        private bool _bound;
        private double _lastPublishedStartDsp = double.NaN;
        private int _lastAppliedChart = int.MinValue;
        private bool _lastAppliedGap;
        private double _lastAlignServerStart = double.NaN;

        public int SyncedSalesScore => _salesScore.Value;
        public int SyncedChartIndex => _chartIndex.Value;
        public int SyncedPrepUnits => _prepUnits.Value;
        public int SyncedBeatIndex => _beatIndex.Value;
        public bool SyncedClockRunning => _clockRunning.Value;

        public void BindLocalSources(
            DaySessionState session,
            DaySetlistRunner runner,
            RhythmClock clock,
            HitInput hitInput,
            ServeRhythmWindow serveRhythm,
            Text statusLabel = null,
            Text judgementLabel = null)
        {
            UnbindLocalEvents();
            _session = session;
            _runner = runner;
            _clock = clock;
            _hitInput = hitInput;
            _serveRhythm = serveRhythm;
            _statusLabel = statusLabel;
            _judgementLabel = judgementLabel;
            _bound = true;
            BindLocalEvents();
            RefreshStatusLabel();
            if (IsSpawned && !IsServer)
            {
                ApplyClientChartFromNet(force: true);
                TryAlignClientClock(force: true);
                MirrorSessionCountersFromNet();
            }
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _salesScore.OnValueChanged += HandleScoreChanged;
            _chartIndex.OnValueChanged += HandleChartChanged;
            _prepUnits.OnValueChanged += HandlePrepChanged;
            _servedCount.OnValueChanged += HandleServedChanged;
            _inGap.OnValueChanged += HandleGapChanged;
            _clockRunning.OnValueChanged += HandleClockRunningChanged;
            _songStartServerTime.OnValueChanged += HandleSongStartChanged;
            _bpm.OnValueChanged += HandleBpmChanged;
            _beatIndex.OnValueChanged += HandleBeatIndexChanged;
            RefreshStatusLabel();
            Debug.Log(
                $"[Netcode] DaySessionNetSync spawn IsServer={IsServer} score={_salesScore.Value} " +
                $"chart={_chartIndex.Value} prep={_prepUnits.Value}");

            if (_bound && !IsServer)
            {
                ApplyClientChartFromNet(force: true);
                TryAlignClientClock(force: true);
                MirrorSessionCountersFromNet();
            }
        }

        public override void OnNetworkDespawn()
        {
            _salesScore.OnValueChanged -= HandleScoreChanged;
            _chartIndex.OnValueChanged -= HandleChartChanged;
            _prepUnits.OnValueChanged -= HandlePrepChanged;
            _servedCount.OnValueChanged -= HandleServedChanged;
            _inGap.OnValueChanged -= HandleGapChanged;
            _clockRunning.OnValueChanged -= HandleClockRunningChanged;
            _songStartServerTime.OnValueChanged -= HandleSongStartChanged;
            _bpm.OnValueChanged -= HandleBpmChanged;
            _beatIndex.OnValueChanged -= HandleBeatIndexChanged;
            UnbindLocalEvents();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void BindLocalEvents()
        {
            if (_hitInput != null)
            {
                _hitInput.OnJudged += HandleLocalBackHit;
            }

            if (_serveRhythm != null)
            {
                _serveRhythm.OnServeOpened += HandleLocalServeOpened;
                _serveRhythm.OnServeResolved += HandleLocalServeResolved;
            }

            BeatLexiconDemo.BeatSent += HandleLocalBeatSent;
            QuestRequestController.QuestRequested += HandleLocalQuest;
            SnapshotSendController.SnapshotSent += HandleLocalSnapshot;
        }

        private void UnbindLocalEvents()
        {
            if (_hitInput != null)
            {
                _hitInput.OnJudged -= HandleLocalBackHit;
            }

            if (_serveRhythm != null)
            {
                _serveRhythm.OnServeOpened -= HandleLocalServeOpened;
                _serveRhythm.OnServeResolved -= HandleLocalServeResolved;
            }

            BeatLexiconDemo.BeatSent -= HandleLocalBeatSent;
            QuestRequestController.QuestRequested -= HandleLocalQuest;
            SnapshotSendController.SnapshotSent -= HandleLocalSnapshot;
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            if (_session != null)
            {
                if (_salesScore.Value != _session.SalesScore)
                {
                    _salesScore.Value = _session.SalesScore;
                }

                if (_prepUnits.Value != _session.PrepUnits)
                {
                    _prepUnits.Value = _session.PrepUnits;
                }

                if (_servedCount.Value != _session.ServedCount)
                {
                    _servedCount.Value = _session.ServedCount;
                }
            }

            var chart = _runner != null ? _runner.CurrentIndex : 0;
            if (_chartIndex.Value != chart)
            {
                _chartIndex.Value = chart;
            }

            var gap = _runner != null && _runner.IsInGap;
            if (_inGap.Value != gap)
            {
                _inGap.Value = gap;
            }

            PublishClockFromHost();
            RefreshStatusLabel();
        }

        private void PublishClockFromHost()
        {
            if (_clock == null)
            {
                if (_clockRunning.Value)
                {
                    _clockRunning.Value = false;
                }

                return;
            }

            if (_clock.IsRunning)
            {
                var startDsp = _clock.StartDspTime;
                if (!_clockRunning.Value ||
                    double.IsNaN(_lastPublishedStartDsp) ||
                    Math.Abs(startDsp - _lastPublishedStartDsp) > 0.0001)
                {
                    _lastPublishedStartDsp = startDsp;
                    var elapsedLocal = AudioSettings.dspTime - startDsp;
                    _songStartServerTime.Value = NetworkManager.ServerTime.Time - elapsedLocal;
                    _bpm.Value = _clock.Bpm;
                    _clockRunning.Value = true;
                }

                if (_beatIndex.Value != _clock.BeatIndex)
                {
                    _beatIndex.Value = _clock.BeatIndex;
                }

                if (Math.Abs(_bpm.Value - _clock.Bpm) > 0.01f)
                {
                    _bpm.Value = _clock.Bpm;
                }
            }
            else if (_clockRunning.Value)
            {
                _clockRunning.Value = false;
                _lastPublishedStartDsp = double.NaN;
            }
        }

        private void HandleLocalBackHit(HitJudgement judgement, double delta, int combo, int laneScore)
        {
            _ = laneScore;
            if (!IsSpawned || _suppressRelay)
            {
                return;
            }

            var deltaMs = (float)(delta * 1000.0);
            if (IsServer)
            {
                NotifyBackHitClientRpc((byte)judgement, deltaMs, combo, NetworkManager.LocalClientId);
            }
            else
            {
                ReportBackHitServerRpc((byte)judgement, deltaMs, combo);
            }
        }

        private void HandleLocalServeOpened(float patienceRatio)
        {
            if (!IsSpawned || _suppressRelay)
            {
                return;
            }

            if (IsServer)
            {
                NotifyServeOpenedClientRpc(patienceRatio, NetworkManager.LocalClientId);
            }
            else
            {
                ReportServeOpenedServerRpc(patienceRatio);
            }
        }

        private void HandleLocalServeResolved(HitJudgement judgement, float tipMult, float patienceRatio)
        {
            if (!IsSpawned || _suppressRelay)
            {
                return;
            }

            // Patience already applied locally on sender; relay for peer FX / host SoT.
            if (IsServer)
            {
                NotifyServeResolvedClientRpc(
                    (byte)judgement, tipMult, patienceRatio, NetworkManager.LocalClientId);
            }
            else
            {
                ReportServeResolvedServerRpc((byte)judgement, tipMult, patienceRatio);
            }
        }

        private void HandleLocalBeatSent(BeatDefinition def)
        {
            if (!IsSpawned || _suppressRelay)
            {
                return;
            }

            var role = def.Id >= BeatId.F1 ? BeatRole.Front : BeatRole.Back;
            var pad = role == BeatRole.Front
                ? (int)def.Id - (int)BeatId.F1
                : (int)def.Id - (int)BeatId.B1;
            if (IsServer)
            {
                NotifyBeatMotifClientRpc((byte)role, (byte)pad, NetworkManager.LocalClientId);
            }
            else
            {
                ReportBeatMotifServerRpc((byte)role, (byte)pad);
            }
        }

        private void HandleLocalQuest(QuestRequestPayload payload)
        {
            if (!IsSpawned || _suppressRelay)
            {
                return;
            }

            if (IsServer)
            {
                NotifyQuestClientRpc((byte)payload.Slot, NetworkManager.LocalClientId);
            }
            else
            {
                ReportQuestServerRpc((byte)payload.Slot);
            }
        }

        private void HandleLocalSnapshot(SnapshotPayload payload)
        {
            if (!IsSpawned || _suppressRelay)
            {
                return;
            }

            var customerLine = payload.CustomerLine ?? string.Empty;
            var stock = payload.StockSummary ?? string.Empty;
            if (IsServer)
            {
                NotifySnapshotClientRpc(
                    payload.ActiveCustomers,
                    customerLine,
                    stock,
                    payload.GapPreferred,
                    NetworkManager.LocalClientId);
            }
            else
            {
                ReportSnapshotServerRpc(
                    payload.ActiveCustomers,
                    customerLine,
                    stock,
                    payload.GapPreferred);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportBackHitServerRpc(byte judgement, float deltaMs, int combo, ServerRpcParams rpcParams = default)
        {
            var j = (HitJudgement)judgement;
            _session?.AddPrepFromJudgement(j);
            NotifyBackHitClientRpc(judgement, deltaMs, combo, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void NotifyBackHitClientRpc(byte judgement, float deltaMs, int combo, ulong senderClientId)
        {
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId)
            {
                return;
            }

            ShowRemoteHitFeedback((HitJudgement)judgement, deltaMs, combo);
            Debug.Log($"[Netcode] Remote back hit {(HitJudgement)judgement} Δ={deltaMs:0.0}ms combo={combo}");
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportServeOpenedServerRpc(float patienceRatio, ServerRpcParams rpcParams = default)
        {
            // Host SoT: reserve prep when Front client opens S1 (local client already reserved).
            if (_session != null && rpcParams.Receive.SenderClientId != NetworkManager.LocalClientId)
            {
                _session.TryReservePrepForServe();
            }

            NotifyServeOpenedClientRpc(patienceRatio, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void NotifyServeOpenedClientRpc(float patienceRatio, ulong senderClientId)
        {
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId)
            {
                return;
            }

            Debug.Log($"[Netcode] Remote serve opened patience={patienceRatio:0.00}");
            RefreshStatusLabel();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportServeResolvedServerRpc(
            byte judgement,
            float tipMult,
            float patienceRatio,
            ServerRpcParams rpcParams = default)
        {
            var j = (HitJudgement)judgement;
            if (_session != null && rpcParams.Receive.SenderClientId != NetworkManager.LocalClientId)
            {
                // Prep already reserved via ReportServeOpenedServerRpc when possible.
                _session.CompleteServeWithRhythm(patienceRatio, tipMult, j);
            }

            NotifyServeResolvedClientRpc(judgement, tipMult, patienceRatio, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void NotifyServeResolvedClientRpc(
            byte judgement,
            float tipMult,
            float patienceRatio,
            ulong senderClientId)
        {
            _ = tipMult;
            _ = patienceRatio;
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId)
            {
                return;
            }

            ShowRemoteHitFeedback((HitJudgement)judgement, 0f, 0);
            Debug.Log($"[Netcode] Remote serve resolved {(HitJudgement)judgement}");
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportBeatMotifServerRpc(byte role, byte padIndex, ServerRpcParams rpcParams = default)
        {
            NotifyBeatMotifClientRpc(role, padIndex, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void NotifyBeatMotifClientRpc(byte role, byte padIndex, ulong senderClientId)
        {
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId)
            {
                return;
            }

            _suppressRelay = true;
            try
            {
                BeatLexiconDemo.RaiseRemoteMotif((BeatRole)role, padIndex);
            }
            finally
            {
                _suppressRelay = false;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportQuestServerRpc(byte slot, ServerRpcParams rpcParams = default)
        {
            NotifyQuestClientRpc(slot, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void NotifyQuestClientRpc(byte slot, ulong senderClientId)
        {
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId)
            {
                return;
            }

            _suppressRelay = true;
            try
            {
                QuestRequestController.RaiseRemoteQuest((QuestSlot)slot);
            }
            finally
            {
                _suppressRelay = false;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportSnapshotServerRpc(
            int customers,
            string customerLine,
            string stockSummary,
            bool gapPreferred,
            ServerRpcParams rpcParams = default)
        {
            NotifySnapshotClientRpc(
                customers,
                customerLine ?? string.Empty,
                stockSummary ?? string.Empty,
                gapPreferred,
                rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void NotifySnapshotClientRpc(
            int customers,
            string customerLine,
            string stockSummary,
            bool gapPreferred,
            ulong senderClientId)
        {
            if (NetworkManager != null && NetworkManager.LocalClientId == senderClientId)
            {
                return;
            }

            var payload = new SnapshotPayload(
                customers,
                customerLine ?? string.Empty,
                stockSummary ?? string.Empty,
                Array.Empty<string>(),
                Array.Empty<float>(),
                gapPreferred);

            _suppressRelay = true;
            try
            {
                SnapshotSendController.RaiseRemoteSnapshot(payload);
            }
            finally
            {
                _suppressRelay = false;
            }
        }

        private void HandleScoreChanged(int previous, int current)
        {
            _ = previous;
            if (!IsServer)
            {
                _session?.ApplyNetworkSalesScore(current);
            }

            RefreshStatusLabel();
            Debug.Log($"[Netcode] Synced salesScore → {current}");
        }

        private void HandlePrepChanged(int previous, int current)
        {
            _ = previous;
            if (!IsServer)
            {
                _session?.ApplyNetworkPrepUnits(current);
            }

            RefreshStatusLabel();
        }

        private void HandleServedChanged(int previous, int current)
        {
            _ = previous;
            if (!IsServer)
            {
                _session?.ApplyNetworkServedCount(current);
            }

            RefreshStatusLabel();
        }

        private void HandleChartChanged(int previous, int current)
        {
            _ = previous;
            if (!IsServer)
            {
                ApplyClientChartFromNet(force: true);
            }

            RefreshStatusLabel();
            Debug.Log($"[Netcode] Synced chartIndex → {current}");
        }

        private void HandleGapChanged(bool previous, bool current)
        {
            _ = previous;
            if (!IsServer)
            {
                ApplyClientChartFromNet(force: true);
            }

            RefreshStatusLabel();
            Debug.Log($"[Netcode] Synced inGap → {current}");
        }

        private void HandleClockRunningChanged(bool previous, bool current)
        {
            _ = previous;
            if (!IsServer)
            {
                if (!current)
                {
                    _clock?.StopClock();
                }
                else
                {
                    TryAlignClientClock(force: true);
                }
            }

            RefreshStatusLabel();
        }

        private void HandleSongStartChanged(double previous, double current)
        {
            _ = previous;
            _ = current;
            if (!IsServer)
            {
                TryAlignClientClock(force: true);
            }
        }

        private void HandleBpmChanged(float previous, float current)
        {
            _ = previous;
            if (!IsServer && _clock != null)
            {
                _clock.Bpm = current;
            }
        }

        private void HandleBeatIndexChanged(int previous, int current)
        {
            _ = previous;
            _ = current;
            if (!IsServer)
            {
                TryAlignClientClock(force: false);
            }

            RefreshStatusLabel();
        }

        private void ApplyClientChartFromNet(bool force)
        {
            if (IsServer || _runner == null)
            {
                return;
            }

            var index = _chartIndex.Value;
            var gap = _inGap.Value;
            if (!force && index == _lastAppliedChart && gap == _lastAppliedGap)
            {
                return;
            }

            _lastAppliedChart = index;
            _lastAppliedGap = gap;
            _runner.ApplyNetworkChartIndex(index, gap);
            if (_clock != null && _bpm.Value > 1f)
            {
                _clock.Bpm = _bpm.Value;
            }
        }

        private void TryAlignClientClock(bool force)
        {
            if (IsServer || _clock == null)
            {
                return;
            }

            if (!_clockRunning.Value || _inGap.Value)
            {
                if (_clock.IsRunning)
                {
                    _clock.StopClock();
                }

                return;
            }

            if (NetworkManager == null)
            {
                return;
            }

            var serverStart = _songStartServerTime.Value;
            if (!force &&
                !double.IsNaN(_lastAlignServerStart) &&
                Math.Abs(serverStart - _lastAlignServerStart) < 0.0001 &&
                _clock.IsRunning)
            {
                return;
            }

            _lastAlignServerStart = serverStart;
            if (_bpm.Value > 1f)
            {
                _clock.Bpm = _bpm.Value;
            }

            var elapsed = NetworkManager.ServerTime.Time - serverStart;
            var localStartDsp = AudioSettings.dspTime - elapsed;
            var beat = Mathf.Max(0, _beatIndex.Value);
            _clock.AlignToStartDsp(localStartDsp, beat);
            Debug.Log(
                $"[Netcode] Client clock aligned elapsed={elapsed:F3}s beat={beat} bpm={_bpm.Value}");
        }

        private void MirrorSessionCountersFromNet()
        {
            if (IsServer || _session == null)
            {
                return;
            }

            _session.ApplyNetworkSalesScore(_salesScore.Value);
            _session.ApplyNetworkPrepUnits(_prepUnits.Value);
            _session.ApplyNetworkServedCount(_servedCount.Value);
        }

        private void ShowRemoteHitFeedback(HitJudgement judgement, float deltaMs, int combo)
        {
            if (_judgementLabel == null)
            {
                return;
            }

            var name = judgement switch
            {
                HitJudgement.Perfect => "Perfect",
                HitJudgement.Good => "Good",
                _ => "Miss"
            };
            _judgementLabel.text = combo > 0
                ? $"相手判定: {name} ({deltaMs:0}ms) c{combo}"
                : $"相手配膳: {name}";
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
            var clock = _clockRunning.Value
                ? $"♪ b{_beatIndex.Value}"
                : _inGap.Value
                    ? "間隙"
                    : "停止";
            _statusLabel.text =
                $"NET {mode} · 売上 {_salesScore.Value} · 仕込み {_prepUnits.Value} · " +
                $"チャート {_chartIndex.Value} · {clock}";
        }
    }
}
