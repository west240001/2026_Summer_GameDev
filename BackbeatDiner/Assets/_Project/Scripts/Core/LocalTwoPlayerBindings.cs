using UnityEngine.InputSystem;

namespace BackbeatDiner.Core
{
    /// <summary>
    /// Local couch 2P keyboard split for <c>VerticalSlicePrototype</c>.
    /// Back (left): WASD+Space hits, A/S/D/F = Beat-B1..B4.
    /// Front (right): Enter cook, J/K serve, arrows = Beat-F1..F4.
    /// </summary>
    public static class LocalTwoPlayerBindings
    {
        public const string HudHelp =
            "【ローカル2P】裏: WASD+Space=ヒット / A·S·D·F=Beat-B1..B4　　表: Enter=クック→配膳 / J·K=配膳ヒット / ←↓→↑=Beat-F1..F4 / Tab長押し=C2スナップ（配膳中は表送信ロック）";

        public const string HudBackHint =
            "裏: WASD+Space = ヒット　　A / S / D / F = Beat-B1..B4　　C2受信=Snap-In";

        public const string HudFrontCookHint =
            "Enter = インスタントクック → 配膳リズム";

        public const string HudFrontServeHint =
            "配膳リズム: J·K（Perfect×1.5 / Good×1 / Miss=チップなし）";

        public const string HudFrontBeatHint =
            "表ビート: ←↓→↑ = F1..F4（8s / 同一20s・配膳中ロック）";

        public const string HudFrontSnapHint =
            "C2スナップ: Tab 長押し 0.8s → 送信 1.2s（CD 25s・間隙推奨）";

        public static bool WasBackHitPressed()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            return kb.spaceKey.wasPressedThisFrame
                   || kb.wKey.wasPressedThisFrame
                   || kb.aKey.wasPressedThisFrame
                   || kb.sKey.wasPressedThisFrame
                   || kb.dKey.wasPressedThisFrame;
        }

        public static bool WasFrontCookPressed()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        }

        public static bool WasFrontServeHitPressed()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            return kb.jKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame;
        }

        /// <summary>Back Beat-B1..B4 via A/S/D/F. Returns pad index 0–3, or -1.</summary>
        public static int ReadBackBeatPadIndex()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return -1;
            }

            if (kb.aKey.wasPressedThisFrame) return 0;
            if (kb.sKey.wasPressedThisFrame) return 1;
            if (kb.dKey.wasPressedThisFrame) return 2;
            if (kb.fKey.wasPressedThisFrame) return 3;
            return -1;
        }

        /// <summary>Front Beat-F1..F4 via ←↓→↑. Returns pad index 0–3, or -1.</summary>
        public static int ReadFrontBeatPadIndex()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return -1;
            }

            if (kb.leftArrowKey.wasPressedThisFrame) return 0;
            if (kb.downArrowKey.wasPressedThisFrame) return 1;
            if (kb.rightArrowKey.wasPressedThisFrame) return 2;
            if (kb.upArrowKey.wasPressedThisFrame) return 3;
            return -1;
        }

        /// <summary>Legacy solo shells: digit / numpad 1–4.</summary>
        public static int ReadDigitBeatPadIndex()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return -1;
            }

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 3;
            return -1;
        }
    }

    /// <summary>Which keys drive BeatLexicon pads for this sender instance.</summary>
    public enum BeatPadKeyScheme
    {
        /// <summary>1–4 / numpad (RhythmPrototype / FrontShell solo).</summary>
        Digits = 0,
        /// <summary>A/S/D/F → Beat-B* (VerticalSlice back).</summary>
        BackAsdf = 1,
        /// <summary>←↓→↑ → Beat-F* (VerticalSlice front).</summary>
        FrontArrows = 2
    }
}
