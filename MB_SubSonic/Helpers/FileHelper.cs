using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Helpers
{
    public static class FileHelper
    {
        private const string Passphrase = "PeekAndPoke";

        public static SubsonicSettings ReadSettingsFromOldFile(string settingsFilename)
        {
            try
            {
                var settings = new SubsonicSettings();
                using var reader = new StreamReader(settingsFilename);
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

                settings.ProfileName = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                if (string.IsNullOrEmpty(settings.ProfileName))
                    settings.ProfileName = "Default";
                
                return settings;
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

        public static List<SubsonicSettings> ReadSettingsFromFile(string settingsFilename)
        {
            try
            {
                if (!File.Exists(settingsFilename)) return null;

                var fileContents = File.ReadAllText(settingsFilename);
                if (string.IsNullOrWhiteSpace(fileContents)) return new List<SubsonicSettings>{ Subsonic.GetCurrentSettings() };

                var decrypted = AesEncryption.Decrypt(fileContents, Passphrase);
                var settings = JsonSerializer.Deserialize<List<SubsonicSettings>>(decrypted);
                //var settings = JsonSerializer.Deserialize<List<SubsonicSettings>>(fileContents);
                if (settings is { Count: 1 } && string.IsNullOrWhiteSpace(settings.First().ProfileName))
                    settings.First().ProfileName = "Default";
                return settings;
            }
            catch (Exception ex)
            {
                const string caption = "Error while trying to load settings";
                MessageBox.Show($@"An error occurred while trying to load the settings file! Reverting to defaults...

Exception: {ex}",
                    caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return new List<SubsonicSettings> { Subsonic.GetCurrentSettings() };
            }
        }

        public static bool SaveSettingsToFile(List<SubsonicSettings> settings, string filename)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(filename, AesEncryption.Encrypt(json, Passphrase));
                //File.WriteAllText(filename, json);
                return true;
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