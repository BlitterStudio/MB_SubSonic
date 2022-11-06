using System;
using System.Collections.Generic;
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

        public static List<SubsonicSettings> SanitizeSettings(List<SubsonicSettings> settings)
        {
            foreach (var setting in settings)
            {
                setting.Host = setting.Host.Trim();
                if (setting.Host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    setting.Host = setting.Host.Substring(7);
                else if (setting.Host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    setting.Host = setting.Host.Substring(8);
                setting.Port = setting.Port.Trim();
                setting.BasePath = setting.BasePath.Trim();
                if (!setting.BasePath.EndsWith("/"))
                    setting.BasePath += "/";
            }
            
            return settings;
        }
    }
}