using System;
using UnityEngine;

namespace BackbeatDiner.Rhythm
{
    /// <summary>
    /// Authored chart data: BPM, length in beats, and timed notes with lanes.
    /// Create via Assets → Create → BACKBEAT DINER → Chart Definition.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ChartDefinition",
        menuName = "BACKBEAT DINER/Chart Definition",
        order = 10)]
    public sealed class ChartDefinition : ScriptableObject
    {
        [Tooltip("Japanese HUD label (チャート名). Falls back to asset name when empty.")]
        [SerializeField] private string displayName = "";

        [SerializeField] private float bpm = 120f;
        [SerializeField] private float lengthBeats = 32f;
        [Tooltip("Optional demo/song bed. Played by RhythmClock from chart start dspTime at this BPM.")]
        [SerializeField] private AudioClip songClip;
        [SerializeField] private ChartNote[] notes = Array.Empty<ChartNote>();

        /// <summary>HUD-facing chart name (Japanese preferred).</summary>
        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();

        public float Bpm => Mathf.Max(1f, bpm);

        public float LengthBeats => Mathf.Max(1f, lengthBeats);

        /// <summary>Optional royalty-free song bed for this chart (may be null).</summary>
        public AudioClip SongClip => songClip;

        public ChartNote[] Notes => notes ?? Array.Empty<ChartNote>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            bpm = Mathf.Max(1f, bpm);
            lengthBeats = Mathf.Max(1f, lengthBeats);
            if (notes == null)
            {
                notes = Array.Empty<ChartNote>();
            }
        }
#endif
    }

    /// <summary>Single chart hit: beat time from chart start and lane index.</summary>
    [Serializable]
    public struct ChartNote
    {
        [Tooltip("Hit time in beats from chart start (0 = first beat).")]
        public float timeBeats;

        [Tooltip("Lane index (0 = center / primary).")]
        public int lane;

        public ChartNote(float timeBeats, int lane)
        {
            this.timeBeats = timeBeats;
            this.lane = lane;
        }
    }
}
