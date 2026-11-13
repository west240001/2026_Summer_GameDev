namespace BackbeatDiner.Core
{
    /// <summary>
    /// Single-role fullscreen selection for two-device VerticalSlice play.
    /// Each device picks 裏 (Back) or 表 (Front); shared DaySessionState still runs locally.
    /// </summary>
    public enum VerticalSlicePlayRole
    {
        Back = 0,
        Front = 1
    }

    public static class VerticalSliceRoleSelection
    {
        private const string PrefKey = "BackbeatDiner.VerticalSlicePlayRole";

        /// <summary>True after TitleMenu / in-scene pick / explicit Select this session.</summary>
        public static bool HasUserSelection { get; private set; }

        public static VerticalSlicePlayRole Selected { get; private set; } = VerticalSlicePlayRole.Back;

        public static bool IsBack => Selected == VerticalSlicePlayRole.Back;
        public static bool IsFront => Selected == VerticalSlicePlayRole.Front;

        public static string DisplayName =>
            Selected == VerticalSlicePlayRole.Back ? "裏" : "表";

        public static void Select(VerticalSlicePlayRole role)
        {
            Selected = role;
            HasUserSelection = true;
            UnityEngine.PlayerPrefs.SetInt(PrefKey, (int)role);
            UnityEngine.PlayerPrefs.Save();
        }

        public static void LoadSavedOrDefault()
        {
            Selected = (VerticalSlicePlayRole)UnityEngine.PlayerPrefs.GetInt(
                PrefKey, (int)VerticalSlicePlayRole.Back);
        }

        public static VerticalSlicePlayRole Toggle()
        {
            var next = Selected == VerticalSlicePlayRole.Back
                ? VerticalSlicePlayRole.Front
                : VerticalSlicePlayRole.Back;
            Select(next);
            return Selected;
        }
    }
}
