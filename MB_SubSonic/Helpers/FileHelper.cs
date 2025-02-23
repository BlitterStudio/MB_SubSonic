using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Helpers;

public static class FileHelper
{
    private const string Passphrase = "PeekAndPoke";

    public static SubsonicSettings ReadSettingsFromOldFile(string settingsFilename)
    {
        try
        {
            var settings = new SubsonicSettings();
            using var reader = new StreamReader(settingsFilename);
            settings.Protocol = AesEncryption.Decrypt(reader.ReadLine(), Passphrase) == "HTTP"
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
            settings.BitRate = string.IsNullOrWhiteSpace(settings.BitRate) ? "Unlimited" : settings.BitRate;
            settings.ProfileName = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
            settings.ProfileName = string.IsNullOrWhiteSpace(settings.ProfileName) ? "Default" : settings.ProfileName;

            return settings;
        }
        catch (Exception ex)
        {
            ShowErrorMessage("Error while trying to load settings", ex);
            return Subsonic.GetCurrentSettings();
        }
    }

    public static List<SubsonicSettings> ReadSettingsFromFile(string settingsFilename)
    {
        try
        {
            if (!File.Exists(settingsFilename)) return null;

            var fileContents = File.ReadAllText(settingsFilename);
            if (string.IsNullOrWhiteSpace(fileContents)) return new List<SubsonicSettings> { Subsonic.GetCurrentSettings() };

            var decrypted = AesEncryption.Decrypt(fileContents, Passphrase);
            var settings = JsonSerializer.Deserialize<List<SubsonicSettings>>(decrypted);

            if (settings is { Count: 1 } && string.IsNullOrWhiteSpace(settings.First().ProfileName))
                settings.First().ProfileName = "Default";

            return settings;
        }
        catch (Exception ex)
        {
            ShowErrorMessage("Error while trying to load settings", ex);
            return [Subsonic.GetCurrentSettings()];
        }
    }

    public static bool SaveSettingsToFile(List<SubsonicSettings> settings, string filename)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(filename, AesEncryption.Encrypt(json, Passphrase));
            return true;
        }
        catch (Exception ex)
        {
            ShowErrorMessage("Error while trying to save settings", ex);
            return false;
        }
    }

    public static void DeleteFile(string path, string filename)
    {
        var fullPath = Path.Combine(path, filename);
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (Exception)
            {
                ShowErrorMessage("An error has occurred", new Exception("An error has occurred while trying to delete the Subsonic cache file.\nPlease try deleting the file manually."));
            }
        }
        else
        {
            MessageBox.Show($@"The file {filename} was not found in {path}!", @"Could not delete file: File not found", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }

    private static void ShowErrorMessage(string caption, Exception ex)
    {
        MessageBox.Show($@"An error occurred while trying to load the settings file! Reverting to defaults...

Exception: {ex}", caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}