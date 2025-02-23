using System;
using System.Collections.Generic;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Helpers;

public static class SettingsHelper
{
    private const int HttpLength = 7;
    private const int HttpsLength = 8;

    /// <summary>
    /// Returns the default Subsonic settings.
    /// </summary>
    /// <returns>A <see cref="SubsonicSettings"/> object with default values.</returns>
    public static SubsonicSettings DefaultSettings() =>
        new()
        {
            ProfileName = "Default",
            Host = "localhost",
            Port = "80",
            BasePath = "/",
            Username = "admin",
            Password = string.Empty,
            Protocol = SubsonicSettings.ConnectionProtocol.Http,
            Auth = SubsonicSettings.AuthMethod.Token,
            BitRate = string.Empty,
            Transcode = false
        };

    /// <summary>
    /// Sanitizes a list of Subsonic settings.
    /// </summary>
    /// <param name="settings">The list of settings to sanitize.</param>
    /// <returns>The sanitized list of settings.</returns>
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

    /// <summary>
    /// Sanitizes the host string by removing any protocol prefix and trimming whitespace.
    /// </summary>
    /// <param name="host">The host string to sanitize.</param>
    /// <returns>The sanitized host string.</returns>
    private static string SanitizeHost(string host)
    {
        host = host.Trim();
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            host = host.Substring(HttpLength);
        else if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            host = host.Substring(HttpsLength);

        return host;
    }

    /// <summary>
    /// Sanitizes the base path string by ensuring it ends with a slash and trimming whitespace.
    /// </summary>
    /// <param name="basePath">The base path string to sanitize.</param>
    /// <returns>The sanitized base path string.</returns>
    private static string SanitizeBasePath(string basePath)
    {
        basePath = basePath.Trim();
        if (!basePath.EndsWith("/"))
            basePath += "/";

        return basePath;
    }
}