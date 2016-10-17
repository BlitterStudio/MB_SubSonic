using System;
using System.Security.Policy;

namespace MusicBeePlugin.Domain
{
    public class SubsonicSettings
    {
        public string Host { get; set; }
        public string Port { get; set; }
        public string BasePath { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Transcode { get; set; }
        public ConnectionProtocol Protocol { get; set; }
        public AuthMethod Auth { get; set; }

        public ApiVersion Api { get; set; } = ApiVersion.V113;

        public enum ApiVersion
        {
            V114,
            V113,
            V112,
            V111
        }

        public enum ConnectionProtocol
        {
            Http,
            Https
        }

        public enum AuthMethod
        {
            Token,
            HexPass
        }
    }
}
