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
        [SerializeField] private float noteScale = 0.32f;
        [SerializeField] private Color noteColor = new Color(0.788f, 0.635f, 0.153f); // #C9A227
        [SerializeField] private Color noteCoreColor = new Color(1f, 0.92f, 0.55f);
        [SerializeField] private bool usePolishedNotes = true;

        private readonly List<ActiveNote> _notes = new List<ActiveNote>(32);
        private Material _noteMaterial;
        private Material _noteCoreMaterial;
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
            var go = usePolishedNotes
                ? GameObject.CreatePrimitive(PrimitiveType.Sphere)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Note";
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = spawnPoint != null ? spawnPoint.position : transform.position;
            go.transform.localScale = usePolishedNotes
                ? new Vector3(noteScale * 1.15f, noteScale * 0.55f, noteScale * 1.35f)
                : Vector3.one * noteScale;

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

            if (usePolishedNotes)
            {
                var core = GameObject.CreatePrimitive(PrimitiveType.Quad);
                core.name = "NoteCore";
                core.transform.SetParent(go.transform, false);
                core.transform.localPosition = Vector3.zero;
                core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                core.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                var coreCol = core.GetComponent<Collider>();
                if (coreCol != null)
                {
                    Destroy(coreCol);
                }

                var coreRenderer = core.GetComponent<MeshRenderer>();
                if (coreRenderer != null)
                {
                    coreRenderer.sharedMaterial = _noteCoreMaterial;
                }
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
            if (_noteMaterial != null && _noteCoreMaterial != null)
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            if (_noteMaterial == null)
            {
                _noteMaterial = BuildMat(shader, noteColor);
            }

            if (_noteCoreMaterial == null)
            {
                _noteCoreMaterial = BuildMat(shader, noteCoreColor);
            }
        }

        private static Material BuildMat(Shader shader, Color color)
        {
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            return mat;
        }
    }
}
