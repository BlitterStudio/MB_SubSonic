using System;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Helpers
{
    public static class SettingsHelper
    {
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

        public static SubsonicSettings SanitizeSettings(SubsonicSettings settings)
        {
            settings.Host = settings.Host.Trim();
            if (settings.Host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                settings.Host = settings.Host.Substring(7);
            else if (settings.Host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                settings.Host = settings.Host.Substring(8);
            settings.Port = settings.Port.Trim();
            settings.BasePath = settings.BasePath.Trim();
            if (!settings.BasePath.EndsWith("/"))
                settings.BasePath += "/";
            return settings;
        }
    }
}