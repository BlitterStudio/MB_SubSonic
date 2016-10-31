namespace Subsonic.Domain
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

        public ApiVersion Api { get; set; } = ApiVersion.V13;

        public enum ApiVersion
        {
            V14,
            V13,
            V12,
            V11
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
