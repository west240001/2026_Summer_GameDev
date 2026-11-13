using System;
using BackbeatDiner.Rhythm;
using UnityEngine;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Day setlist: ordered <see cref="ChartDefinition"/> list plus inter-chart kitchen gap.
    /// Create via Assets → Create → BACKBEAT DINER → Setlist Definition.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SetlistDefinition",
        menuName = "BACKBEAT DINER/Setlist Definition",
        order = 11)]
    public sealed class SetlistDefinition : ScriptableObject
    {
        [SerializeField] private ChartDefinition[] charts = Array.Empty<ChartDefinition>();

        [Tooltip("Kitchen gap between charts (seconds). Design range ~5–15.")]
        [SerializeField] private float gapSeconds = 8f;

        public ChartDefinition[] Charts => charts ?? Array.Empty<ChartDefinition>();

        public float GapSeconds => Mathf.Max(0f, gapSeconds);

        public int ChartCount
        {
            get
            {
                var list = Charts;
                var n = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    if (list[i] != null)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            gapSeconds = Mathf.Max(0f, gapSeconds);
            if (charts == null)
            {
                charts = Array.Empty<ChartDefinition>();
            }
        }
#endif
    }
}
