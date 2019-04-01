using System;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Helpers
{
    public static class SettingsHelper
    {
        public static SubsonicSettings SetDefaultSettings()
        {
            return new SubsonicSettings
            {
                Host = "localhost",
                Port = "80",
                BasePath = "/",
                Username = "admin",
                Password = "",
                Protocol = SubsonicSettings.ConnectionProtocol.Http,
                Auth = SubsonicSettings.AuthMethod.Token,
                BitRate = string.Empty,
                Transcode = false,
                UseIndexCache = true,
                PreCacheAll = false
            };
        }

        public static bool IsSettingChanged(SubsonicSettings newSettings, SubsonicSettings oldSettings)
        {
            newSettings = SanitizeSettings(newSettings);
            var result = !newSettings.Host.Equals(oldSettings.Host) ||
                         !newSettings.Port.Equals(oldSettings.Port) ||
                         !newSettings.BasePath.Equals(oldSettings.BasePath) ||
                         !newSettings.Username.Equals(oldSettings.Username) ||
                         !newSettings.Password.Equals(oldSettings.Password) ||
                         !newSettings.Protocol.Equals(oldSettings.Protocol) ||
                         !newSettings.Auth.Equals(oldSettings.Auth) ||
                         !newSettings.Transcode.Equals(oldSettings.Transcode) ||
                         !newSettings.BitRate.Equals(oldSettings.BitRate) ||
                         !newSettings.UseIndexCache.Equals(oldSettings.UseIndexCache) ||
                         !newSettings.PreCacheAll.Equals(oldSettings.PreCacheAll);
            return result;
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
            if (!settings.BasePath.EndsWith(@"/"))
                settings.BasePath += @"/";
            return settings;
        }
    }
}