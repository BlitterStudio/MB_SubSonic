using System;
using System.Collections.Generic;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Helpers;

public static class SettingsHelper
{
    private const int HttpLength = 7;
    private const int HttpsLength = 8;

    public static SubsonicSettings DefaultSettings()
    {
        return new SubsonicSettings
        {
            ProfileName = "Default",
            Host = "localhost",
            Port = "80",
            BasePath = "/",
            Username = "admin",
            Password = "",
            Protocol = SubsonicSettings.ConnectionProtocol.Http,
            Auth = SubsonicSettings.AuthMethod.Token,
            BitRate = string.Empty,
            Transcode = false
        };
    }

    public static List<SubsonicSettings> SanitizeSettings(List<SubsonicSettings> settings)
    {
        foreach (var setting in settings)
        {
            setting.Host = SanitizeHost(setting.Host);
            setting.Port = setting.Port.Trim();
            setting.BasePath = SanitizeBasePath(setting.BasePath);
        }

        return settings;
    }

    private static string SanitizeHost(string host)
    {
        host = host.Trim();
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            host = host.Substring(HttpLength);
        else if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            host = host.Substring(HttpsLength);

        return host;
    }

    private static string SanitizeBasePath(string basePath)
    {
        basePath = basePath.Trim();
        if (!basePath.EndsWith("/"))
            basePath += "/";

        return basePath;
    }
}