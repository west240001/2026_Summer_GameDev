using System;

namespace BackbeatDiner.Rhythm
{
    /// <summary>
    /// Hit timing windows in milliseconds relative to the scheduled beat.
    /// Perfect ≤ ±PerfectMs, Good ≤ ±GoodMs, otherwise Miss.
    /// </summary>
    [Serializable]
    public struct JudgeWindow
    {
        public float PerfectMs;
        public float GoodMs;

        public static JudgeWindow Default => new JudgeWindow
        {
            PerfectMs = 40f,
            GoodMs = 80f
        };

        public HitJudgement Evaluate(double deltaSeconds)
        {
            var absMs = Math.Abs(deltaSeconds) * 1000.0;
            if (absMs <= PerfectMs)
            {
                return HitJudgement.Perfect;
            }

            if (absMs <= GoodMs)
            {
                return HitJudgement.Good;
            }

            return HitJudgement.Miss;
        }
    }

    public enum HitJudgement
    {
        Perfect = 0,
        Good = 1,
        Miss = 2
    }
}
