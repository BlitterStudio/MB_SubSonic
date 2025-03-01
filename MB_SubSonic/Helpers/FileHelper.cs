using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Helpers;

public static class FileHelper
{
    private const string Passphrase = "PeekAndPoke";

    public static ProfileSettings ReadSettingsFromFile(string settingsFilename)
    {
        try
        {
            if (!File.Exists(settingsFilename)) return null;

            var fileContents = File.ReadAllText(settingsFilename);
            if (string.IsNullOrWhiteSpace(fileContents)) return SettingsHelper.DefaultSettings();

            var decrypted = AesEncryption.Decrypt(fileContents, Passphrase);
            var settings = JsonSerializer.Deserialize<ProfileSettings>(decrypted);
            return settings;
        }
        catch (Exception ex)
        {
            ShowErrorMessage("Error while trying to load settings, using defaults", ex);
            return SettingsHelper.DefaultSettings();
        }
    }

    public static bool SaveSettingsToFile(ProfileSettings settings, string filename)
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
            try
            {
                File.Delete(fullPath);
            }
            catch (Exception)
            {
                ShowErrorMessage("An error has occurred",
                    new Exception(
                        "An error has occurred while trying to delete the Subsonic cache file.\nPlease try deleting the file manually."));
            }
        else
            MessageBox.Show($@"The file {filename} was not found in {path}!", @"Could not delete file: File not found",
                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
    }

    private static void ShowErrorMessage(string caption, Exception ex)
    {
        MessageBox.Show($@"An error occurred while trying to load the settings file! Reverting to defaults...", 
            caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}