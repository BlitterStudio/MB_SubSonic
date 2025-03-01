using System;
using System.Collections.Generic;

namespace MusicBeePlugin.Domain;

public enum AuthMethod
{
    Token,
    HexPass
}

public enum BrowseType
{
    Tags,
    Directories
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

public class SubsonicSettings
{
    public string Profile { get; set; }
    public string Host { get; set; }
    public string Port { get; set; }
    public string BasePath { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool Transcode { get; set; }
    public ConnectionProtocol Protocol { get; set; }
    public AuthMethod Auth { get; set; }
    public string BitRate { get; set; }
    public BrowseType BrowseBy { get; set; }
}

public class ProfileSettings
{
    public string SelectedProfile { get; set; }
    public List<SubsonicSettings> Settings { get; set; }
}

public static class SubsonicSettingsExtensions
{
    public static string ToFriendlyString(this ConnectionProtocol me)
    {
        return me switch
        {
            ConnectionProtocol.Http => "HTTP",
            ConnectionProtocol.Https => "HTTPS",
            _ => throw new ArgumentOutOfRangeException(nameof(me), me, null)
        };
    }
}