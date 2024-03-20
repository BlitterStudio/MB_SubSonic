using System;

namespace MusicBeePlugin.Domain;

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
        None,
        Subsonic
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
    public string ProfileName { get; set; }
}

public static class SubsonicSettingsExtensions
{
    public static string ToFriendlyString(this SubsonicSettings.ConnectionProtocol me)
    {
        return me switch
        {
            SubsonicSettings.ConnectionProtocol.Http => "HTTP",
            SubsonicSettings.ConnectionProtocol.Https => "HTTPS",
            _ => throw new ArgumentOutOfRangeException(nameof(me), me, null),
        };
    }
}