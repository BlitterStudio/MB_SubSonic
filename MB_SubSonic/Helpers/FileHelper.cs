using System;
using System.IO;
using System.Windows.Forms;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Helpers
{
    public static class FileHelper
    {
        private const string Passphrase = "PeekAndPoke";

        public static SubsonicSettings ReadSettingsFromFile(string settingsFilename)
        {
            var settings = new SubsonicSettings();
            try
            {
                if (!File.Exists(settingsFilename)) return Subsonic.GetCurrentSettings();

                using (var reader = new StreamReader(settingsFilename))
                {
                    var protocolText = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);

                    settings.Protocol = protocolText.Equals("HTTP")
                        ? SubsonicSettings.ConnectionProtocol.Http
                        : SubsonicSettings.ConnectionProtocol.Https;
                    settings.Host = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                    settings.Port = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                    settings.BasePath = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                    settings.Username = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                    settings.Password = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                    settings.Transcode = AesEncryption.Decrypt(reader.ReadLine(), Passphrase) == "Y";
                    settings.Auth = AesEncryption.Decrypt(reader.ReadLine(), Passphrase) == "HexPass"
                        ? SubsonicSettings.AuthMethod.HexPass
                        : SubsonicSettings.AuthMethod.Token;
                    settings.BitRate = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);

                    if (string.IsNullOrEmpty(settings.BitRate))
                        settings.BitRate = "Unlimited";

                    return settings;
                }
            }
            catch (Exception ex)
            {
                const string caption = "Error while trying to load settings";
                MessageBox.Show($@"An error occurred while trying to load the settings file! Reverting to defaults...

Exception: {ex}",
                    caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return Subsonic.GetCurrentSettings();
            }
        }

        public static bool SaveSettingsToFile(SubsonicSettings settings, string filename)
        {
            try
            {
                using (var writer = new StreamWriter(filename))
                {
                    writer.WriteLine(
                        AesEncryption.Encrypt(
                            settings.Protocol == SubsonicSettings.ConnectionProtocol.Http ? "HTTP" : "HTTPS",
                            Passphrase));
                    writer.WriteLine(AesEncryption.Encrypt(settings.Host, Passphrase));
                    writer.WriteLine(AesEncryption.Encrypt(settings.Port, Passphrase));
                    writer.WriteLine(AesEncryption.Encrypt(settings.BasePath, Passphrase));
                    writer.WriteLine(AesEncryption.Encrypt(settings.Username, Passphrase));
                    writer.WriteLine(AesEncryption.Encrypt(settings.Password, Passphrase));
                    writer.WriteLine(settings.Transcode
                        ? AesEncryption.Encrypt("Y", Passphrase)
                        : AesEncryption.Encrypt("N", Passphrase));
                    writer.WriteLine(
                        AesEncryption.Encrypt(
                            settings.Auth == SubsonicSettings.AuthMethod.HexPass ? "HexPass" : "Token",
                            Passphrase));
                    writer.WriteLine(AesEncryption.Encrypt(settings.BitRate, Passphrase));
                    return true;
                }
            }
            catch (Exception ex)
            {
                const string caption = "Error while trying to save settings";
                MessageBox.Show($@"An error occurred while trying to save the settings file!

Exception: {ex}",
                    caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public static void DeleteFile(string path, string filename)
        {
            if (File.Exists(Path.Combine(path, filename)))
            {
                try
                {
                    File.Delete(Path.Combine(path, filename));
                }
                catch (Exception)
                {
                    const string caption = "An error has occurred";
                    MessageBox.Show(
                        @"An error has occurred while trying to delete the Subsonic cache file.\nPlease try deleting the file manually.",
                        caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                const string caption = "Could not delete file: File not found";
                var text = $"The file {filename} was not found in {path}!";
                MessageBox.Show(text, caption, MessageBoxButtons.OK,
MessageBoxIcon.Exclamation);
            }
        }
    }
}