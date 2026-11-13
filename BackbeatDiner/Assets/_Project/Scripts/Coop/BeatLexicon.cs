using System;

namespace BackbeatDiner.Coop
{
    /// <summary>KG-3 C3 beat lexicon roles (裏 = Beat-B*, 表 = Beat-F*).</summary>
    public enum BeatRole
    {
        Back = 0,
        Front = 1
    }

    /// <summary>KG-3 v1 motif ids Beat-B1..B4 / Beat-F1..F4.</summary>
    public enum BeatId
    {
        B1 = 0,
        B2 = 1,
        B3 = 2,
        B4 = 3,
        F1 = 4,
        F2 = 5,
        F3 = 6,
        F4 = 7
    }

    /// <summary>Immutable beat motif definition (static lexicon; ScriptableObject optional later).</summary>
    public readonly struct BeatDefinition
    {
        public BeatId Id { get; }
        public string Code { get; }
        public string JapaneseLabel { get; }
        public string EnglishName { get; }
        public float[] FrequenciesHz { get; }
        public float NoteSeconds { get; }

        public BeatDefinition(
            BeatId id,
            string code,
            string japaneseLabel,
            string englishName,
            float[] frequenciesHz,
            float noteSeconds)
        {
            Id = id;
            Code = code;
            JapaneseLabel = japaneseLabel;
            EnglishName = englishName;
            FrequenciesHz = frequenciesHz;
            NoteSeconds = noteSeconds;
        }

        public string PadLabel => $"{JapaneseLabel}";

        public string DisplayName => $"{Code}「{JapaneseLabel}（{EnglishName}）」";
    }

    /// <summary>
    /// KG-3 C3 beat lexicon: static defs Beat-B1..F4.
    /// Cooldown policy: <see cref="AnyMotifCooldownSeconds"/> / person,
    /// <see cref="SameMotifCooldownSeconds"/> for same-motif spam.
    /// </summary>
    public static class BeatLexicon
    {
        public const float AnyMotifCooldownSeconds = 8f;
        public const float SameMotifCooldownSeconds = 20f;

        private static readonly BeatDefinition[] All =
        {
            new BeatDefinition(BeatId.B1, "Beat-B1", "プッシュ", "Push",
                new[] { 261.63f, 329.63f, 392.00f }, 0.11f),
            new BeatDefinition(BeatId.B2, "Beat-B2", "クリア", "Clear",
                new[] { 523.25f, 523.25f, 523.25f, 523.25f }, 0.07f),
            new BeatDefinition(BeatId.B3, "Beat-B3", "スナップ前", "Snap cue",
                new[] { 196.00f, 392.00f }, 0.14f),
            new BeatDefinition(BeatId.B4, "Beat-B4", "あとで", "Later",
                new[] { 220.00f }, 0.28f),
            new BeatDefinition(BeatId.F1, "Beat-F1", "ビジー", "Busy",
                new[] { 440.00f, 466.16f, 493.88f, 523.25f }, 0.06f),
            new BeatDefinition(BeatId.F2, "Beat-F2", "ビッグオーダー", "Big order",
                new[] { 110.00f, 110.00f, 659.25f }, 0.12f),
            new BeatDefinition(BeatId.F3, "Beat-F3", "在庫赤", "Low stock",
                new[] { 349.23f, 277.18f, 349.23f }, 0.09f),
            new BeatDefinition(BeatId.F4, "Beat-F4", "ラスト客", "Last guest",
                new[] { 392.00f, 329.63f, 261.63f, 196.00f }, 0.13f)
        };

        public static BeatDefinition Get(BeatId id) => All[(int)id];

        public static BeatDefinition GetPad(BeatRole role, int padIndex)
        {
            if (padIndex < 0 || padIndex > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(padIndex), "Pad index must be 0–3.");
            }

            var id = role == BeatRole.Front
                ? (BeatId)((int)BeatId.F1 + padIndex)
                : (BeatId)((int)BeatId.B1 + padIndex);
            return All[(int)id];
        }

        public static BeatDefinition[] PadsFor(BeatRole role)
        {
            return new[]
            {
                GetPad(role, 0),
                GetPad(role, 1),
                GetPad(role, 2),
                GetPad(role, 3)
            };
        }
    }
}
