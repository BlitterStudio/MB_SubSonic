using System;

namespace MusicBeePlugin.Domain
{
    public class SubsonicSettings
    {
        public enum AuthMethod
        {
            Token,
            HexPass
        }

        public enum ConnectionProtocol
        {
            Http,
            Https
        }

        public enum ServerType
        {
            Subsonic,
            LibreSonic
        }

        public string Host { get; set; }
        public string Port { get; set; }
        public string BasePath { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Transcode { get; set; }
        public ConnectionProtocol Protocol { get; set; }
        public AuthMethod Auth { get; set; }
        public string BitRate { get; set; }
        public bool UseIndexCache { get; set; }
        public bool PreCacheAll { get; set; }
    }

    public static class SubsonicSettingsExtensions
    {
        public static string ToFriendlyString(this SubsonicSettings.ConnectionProtocol me)
        {
            switch (me)
            {
                case SubsonicSettings.ConnectionProtocol.Http:
                    return "HTTP";
                case SubsonicSettings.ConnectionProtocol.Https:
                    return "HTTPS";
                default:
                    throw new ArgumentOutOfRangeException(nameof(me), me, null);
            }
        }

        public static string ToFriendlyString(this SubsonicSettings.AuthMethod me)
        {
            switch (me)
            {
                case SubsonicSettings.AuthMethod.Token:
                    return "Token";
                case SubsonicSettings.AuthMethod.HexPass:
                    return "HexPass";
                default:
                    throw new ArgumentOutOfRangeException(nameof(me), me, null);
            }
        }
    }
}