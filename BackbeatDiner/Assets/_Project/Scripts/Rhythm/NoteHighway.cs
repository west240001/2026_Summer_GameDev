using System.Collections.Generic;
using UnityEngine;

namespace BackbeatDiner.Rhythm
{
    /// <summary>
    /// One-lane scrolling note highway. Notes spawn on beat schedule and move toward a hit line.
    /// </summary>
    public sealed class NoteHighway : MonoBehaviour
    {
        [SerializeField] private RhythmClock clock;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform hitLine;
        [SerializeField] private float approachBeats = 2f;
        [SerializeField] private float noteScale = 0.35f;
        [SerializeField] private Color noteColor = new Color(0.788f, 0.635f, 0.153f); // #C9A227

        private readonly List<ActiveNote> _notes = new List<ActiveNote>(32);
        private Material _noteMaterial;
        private double _approachSeconds;

        private struct ActiveNote
        {
            public GameObject Go;
            public double TargetDsp;
            public bool Consumed;
        }

        public IReadOnlyList<ActiveNoteProxy> Notes
        {
            get
            {
                var list = new List<ActiveNoteProxy>(_notes.Count);
                for (var i = 0; i < _notes.Count; i++)
                {
                    var n = _notes[i];
                    if (n.Consumed || n.Go == null)
                    {
                        continue;
                    }

                    list.Add(new ActiveNoteProxy(n.Go, n.TargetDsp));
                }

                return list;
            }
        }

        public readonly struct ActiveNoteProxy
        {
            public readonly GameObject Go;
            public readonly double TargetDsp;

            public ActiveNoteProxy(GameObject go, double targetDsp)
            {
                Go = go;
                TargetDsp = targetDsp;
            }
        }

        public void Configure(RhythmClock rhythmClock, Transform spawn, Transform hit)
        {
            if (clock != null)
            {
                clock.OnBeat -= HandleBeat;
            }

            clock = rhythmClock;
            spawnPoint = spawn;
            hitLine = hit;

            if (isActiveAndEnabled && clock != null)
            {
                clock.OnBeat += HandleBeat;
            }
        }

        private void OnEnable()
        {
            if (clock != null)
            {
                clock.OnBeat -= HandleBeat;
                clock.OnBeat += HandleBeat;
            }
        }

        private void OnDisable()
        {
            if (clock != null)
            {
                clock.OnBeat -= HandleBeat;
            }
        }

        private void Start()
        {
            EnsureMaterial();
            RefreshApproach();
        }

        private void Update()
        {
            if (clock == null || spawnPoint == null || hitLine == null)
            {
                return;
            }

            RefreshApproach();
            var now = AudioSettings.dspTime;
            var spawnPos = spawnPoint.position;
            var hitPos = hitLine.position;

            for (var i = _notes.Count - 1; i >= 0; i--)
            {
                var note = _notes[i];
                if (note.Go == null)
                {
                    _notes.RemoveAt(i);
                    continue;
                }

                var start = note.TargetDsp - _approachSeconds;
                var t = _approachSeconds <= 0.0001
                    ? 1.0
                    : (now - start) / _approachSeconds;
                t = System.Math.Max(0.0, System.Math.Min(1.4, t));
                note.Go.transform.position = Vector3.LerpUnclamped(spawnPos, hitPos, (float)t);

                // Despawn shortly after passing the hit line if not hit.
                if (!note.Consumed && now > note.TargetDsp + 0.25)
                {
                    Destroy(note.Go);
                    _notes.RemoveAt(i);
                }
            }
        }

        public bool TryConsumeNearest(double inputDsp, float maxWindowMs, out double targetDsp, out GameObject noteGo)
        {
            targetDsp = 0;
            noteGo = null;
            var bestAbs = double.MaxValue;
            var bestIndex = -1;

            for (var i = 0; i < _notes.Count; i++)
            {
                var n = _notes[i];
                if (n.Consumed || n.Go == null)
                {
                    continue;
                }

                var abs = System.Math.Abs(inputDsp - n.TargetDsp) * 1000.0;
                if (abs <= maxWindowMs && abs < bestAbs)
                {
                    bestAbs = abs;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            var hit = _notes[bestIndex];
            hit.Consumed = true;
            _notes[bestIndex] = hit;
            targetDsp = hit.TargetDsp;
            noteGo = hit.Go;
            Destroy(hit.Go, 0.02f);
            return true;
        }

        private void HandleBeat(int beatIndex, double scheduledDsp)
        {
            // Spawn a note that will arrive `approachBeats` later.
            var target = scheduledDsp + approachBeats * clock.SecondsPerBeat;
            SpawnNote(target);
        }

        private void SpawnNote(double targetDsp)
        {
            EnsureMaterial();
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Note";
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = spawnPoint != null ? spawnPoint.position : transform.position;
            go.transform.localScale = Vector3.one * noteScale;

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = _noteMaterial;
            }

            _notes.Add(new ActiveNote
            {
                Go = go,
                TargetDsp = targetDsp,
                Consumed = false
            });
        }

        private void RefreshApproach()
        {
            if (clock != null)
            {
                _approachSeconds = approachBeats * clock.SecondsPerBeat;
            }
        }

        private void EnsureMaterial()
        {
            if (_noteMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            _noteMaterial = new Material(shader);
            if (_noteMaterial.HasProperty("_BaseColor"))
            {
                _noteMaterial.SetColor("_BaseColor", noteColor);
            }
            else if (_noteMaterial.HasProperty("_Color"))
            {
                _noteMaterial.SetColor("_Color", noteColor);
            }
        }
    }
}
