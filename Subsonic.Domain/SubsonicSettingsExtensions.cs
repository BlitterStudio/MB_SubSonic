using System;

namespace Subsonic.Domain
{
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
                    return "Token based";
                case SubsonicSettings.AuthMethod.HexPass:
                    return "Hex enc. password";
                default:
                    throw new ArgumentOutOfRangeException(nameof(me), me, null);
            }
        }

        public static string ToFriendlyString(this SubsonicSettings.ApiVersion me)
        {
            switch (me)
            {
                case SubsonicSettings.ApiVersion.V14:
                    return "1.14.0";
                case SubsonicSettings.ApiVersion.V13:
                    return "1.13.0";
                case SubsonicSettings.ApiVersion.V12:
                    return "1.12.0";
                case SubsonicSettings.ApiVersion.V11:
                    return "1.11.0";
                default:
                    throw new ArgumentOutOfRangeException(nameof(me), me, null);
            }
        }
    }
}
