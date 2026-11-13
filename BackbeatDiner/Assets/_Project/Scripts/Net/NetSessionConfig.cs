namespace BackbeatDiner.Net
{
    /// <summary>
    /// Session intent chosen on TitleMenu before VerticalSlice loads.
    /// Offline = existing single-device role fullscreen; Host/Client = NGO localhost scaffold.
    /// </summary>
    public enum NetSessionMode
    {
        Offline = 0,
        Host = 1,
        Client = 2
    }

    public static class NetSessionConfig
    {
        public const string DefaultAddress = "127.0.0.1";
        public const ushort DefaultPort = 7777;

        /// <summary>Fixed GlobalObjectIdHash for runtime DaySessionNetSync prefab (host + client must match).</summary>
        public const uint DaySessionSyncPrefabHash = 0xB34C7E51u;

        public static NetSessionMode Mode { get; private set; } = NetSessionMode.Offline;

        public static string Address { get; private set; } = DefaultAddress;

        public static ushort Port { get; private set; } = DefaultPort;

        public static bool IsNetworked => Mode == NetSessionMode.Host || Mode == NetSessionMode.Client;

        public static void ConfigureOffline()
        {
            Mode = NetSessionMode.Offline;
        }

        public static void ConfigureHost(string address = DefaultAddress, ushort port = DefaultPort)
        {
            Mode = NetSessionMode.Host;
            Address = string.IsNullOrWhiteSpace(address) ? DefaultAddress : address.Trim();
            Port = port == 0 ? DefaultPort : port;
        }

        public static void ConfigureClient(string address = DefaultAddress, ushort port = DefaultPort)
        {
            Mode = NetSessionMode.Client;
            Address = string.IsNullOrWhiteSpace(address) ? DefaultAddress : address.Trim();
            Port = port == 0 ? DefaultPort : port;
        }
    }
}
