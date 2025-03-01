using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Windows;

public partial class SettingsWindow : Form
{
    private readonly Interfaces.Plugin.PluginInfo _about;
    private Interfaces.Plugin.MusicBeeApiInterface _mbApiInterface;
    private ProfileSettings _settings;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(Interfaces.Plugin.MusicBeeApiInterface mbApiInterface, Interfaces.Plugin.PluginInfo about)
    {
        _mbApiInterface = mbApiInterface;
        _about = about ?? throw new ArgumentNullException(nameof(about));
        InitializeComponent();

        PopulateComboboxes();

        FormClosing += Settings_FormClosing;
        VisibleChanged += OnVisibleChanged;
    }

    private void PopulateComboboxes()
    {
        ComboBoxBitrate.Items.Add("Unlimited");
        ComboBoxBitrate.Items.Add("64K");
        ComboBoxBitrate.Items.Add("80K");
        ComboBoxBitrate.Items.Add("96K");
        ComboBoxBitrate.Items.Add("112K");
        ComboBoxBitrate.Items.Add("128K");
        ComboBoxBitrate.Items.Add("160K");
        ComboBoxBitrate.Items.Add("192K");
        ComboBoxBitrate.Items.Add("256K");
        ComboBoxBitrate.Items.Add("320K");

        ComboBoxProtocol.Items.Add("HTTP");
        ComboBoxProtocol.Items.Add("HTTPS");

        ComboBoxAuth.Items.Add("Token based");
        ComboBoxAuth.Items.Add("Hex enc. password");

        ComboBoxBrowseBy.Items.Add("Tags");
        ComboBoxBrowseBy.Items.Add("Directories");
    }

    private void UpdateProfilesDataSource()
    {
        cmbProfile.DataSource = _settings.Settings.Select(s => s.Profile).ToList();
    }

    private void PopulateFields()
    {
        var currentSettings = _settings.Settings.First(s => s.Profile == _settings.SelectedProfile);

        TextBoxHostname.Text = currentSettings.Host;
        TextBoxPort.Text = currentSettings.Port;
        TextBoxPath.Text = currentSettings.BasePath;
        TextBoxUsername.Text = currentSettings.Username;
        TextBoxPassword.Text = currentSettings.Password;

        CheckBoxTranscode.Checked = currentSettings.Transcode;
        ComboBoxBitrate.SelectedItem =
            string.IsNullOrEmpty(currentSettings.BitRate)
                ? "128K"
                : currentSettings.BitRate;
        ComboBoxBitrate.Enabled = CheckBoxTranscode.Checked;

        ComboBoxProtocol.SelectedItem = currentSettings.Protocol.ToFriendlyString();
        ComboBoxAuth.SelectedIndex = (int)currentSettings.Auth;

        ComboBoxBrowseBy.SelectedIndex = (int)currentSettings.BrowseBy;

        // If we only have one profile, disable the Delete button
        btnProfileDelete.Enabled = _settings.Settings.Count != 1;
    }

    private void OnVisibleChanged(object sender, EventArgs eventArgs)
    {
        if (!Visible) return;

        _settings = Subsonic.LoadSettingsFromFile();
        var profile = _settings.SelectedProfile;
        UpdateProfilesDataSource();
        cmbProfile.SelectedItem = profile;
    }
    
    private void Settings_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.UserClosing) return;
        Hide();
        e.Cancel = true;
    }

    private void PersistValues()
    {
        var saved = Subsonic.SaveSettings(_settings);
        if (Subsonic.IsInitialized)
            Subsonic.Refresh();
        else
            Subsonic.SendNotificationsHandler.Invoke(Subsonic.Initialize()
                ? Interfaces.Plugin.CallbackType.StorageReady
                : Interfaces.Plugin.CallbackType.StorageFailed);
    }

    private void ButtonAbout_Click(object sender, EventArgs e)
    {
        Debug.Assert(_about != null, "_about != null");
        const string caption = "About Subsonic Client";
        MessageBox.Show(this,
            $@"{_about.Name} v{_about.VersionMajor}.{_about.VersionMinor}.{_about.Revision}
{_about.Description}

Author: {_about.Author}

https://github.com/BlitterStudio/MB_SubSonic", caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ButtonCancel_Click(object sender, EventArgs e)
    {
        Hide();
    }

    private void ButtonSave_Click(object sender, EventArgs e)
    {
        StoreCurrentSettings();
        Subsonic.ChangeServerProfile(GetCurrentSettings());
        PersistValues();
        Hide();
    }

    private void btnApply_Click(object sender, EventArgs e)
    {
        StoreCurrentSettings();
        Subsonic.ChangeServerProfile(GetCurrentSettings());
    }

    private void ButtonPing_Click(object sender, EventArgs e)
    {
        var currentSettings = GetCurrentSettings();
        var pingResult = Subsonic.PingServer(currentSettings);
        if (pingResult)
        {
            const string text = "The server responded normally";
            const string caption = "Ping response OK";
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            const string text = "The server did not respond to Ping as expected!";
            const string caption = "Ping response not OK";
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private SubsonicSettings GetCurrentSettings()
    {
        var settings = new SubsonicSettings
        {
            Host = TextBoxHostname.Text,
            Port = TextBoxPort.Text,
            BasePath = TextBoxPath.Text,
            Username = TextBoxUsername.Text,
            Password = TextBoxPassword.Text,
            Transcode = CheckBoxTranscode.Checked,
            Protocol = ComboBoxProtocol.SelectedItem.ToString().Equals("HTTP")
                ? ConnectionProtocol.Http
                : ConnectionProtocol.Https,
            Auth = ComboBoxAuth.SelectedIndex.Equals(0)
                ? AuthMethod.Token
                : AuthMethod.HexPass,
            BitRate = string.IsNullOrEmpty(ComboBoxBitrate.SelectedItem.ToString())
                ? "128K"
                : ComboBoxBitrate.SelectedItem.ToString(),
            BrowseBy = ComboBoxBrowseBy.SelectedIndex.Equals(0)
                ? BrowseType.Tags
                : BrowseType.Directories,
            Profile = cmbProfile.SelectedItem.ToString()
        };

        return settings;
    }

    private void StoreCurrentSettings()
    {
        var settings = _settings.Settings.Find(s => s.Profile == _settings.SelectedProfile);
        if (settings == null) return;

        settings.Host = TextBoxHostname.Text;
        settings.Port = TextBoxPort.Text;
        settings.BasePath = TextBoxPath.Text;
        settings.Username = TextBoxUsername.Text;
        settings.Password = TextBoxPassword.Text;
        settings.Transcode = CheckBoxTranscode.Checked;
        settings.Protocol = ComboBoxProtocol.SelectedItem.ToString().Equals("HTTP")
            ? ConnectionProtocol.Http
            : ConnectionProtocol.Https;
        settings.Auth = ComboBoxAuth.SelectedIndex.Equals(0)
            ? AuthMethod.Token
            : AuthMethod.HexPass;
        settings.BitRate = string.IsNullOrEmpty(ComboBoxBitrate.SelectedItem.ToString())
            ? "128K"
            : ComboBoxBitrate.SelectedItem.ToString();
        settings.BrowseBy = ComboBoxBrowseBy.SelectedIndex.Equals(0)
            ? BrowseType.Tags
            : BrowseType.Directories;
        settings.Profile = cmbProfile.SelectedItem.ToString();

        _settings.SelectedProfile = cmbProfile.SelectedItem.ToString();
    }

    private void cmbProfile_SelectedIndexChanged(object sender, EventArgs e)
    {
        _settings.SelectedProfile = cmbProfile.SelectedItem.ToString();
        PopulateFields();
    }

    private void btnProfileNew_Click(object sender, EventArgs e)
    {
        StoreCurrentSettings();
        var newProfileName = Prompt.ShowDialog("Profile Name:", "Profile Name");
        _settings.Settings.Add(new SubsonicSettings { Profile = newProfileName });
        UpdateProfilesDataSource();
        _settings.SelectedProfile = newProfileName;
        cmbProfile.SelectedItem = newProfileName;
    }

    private void btnProfileRename_Click(object sender, EventArgs e)
    {
        var newProfileName = Prompt.ShowDialog("Change Profile Name to:", "Profile Name");
        var settings = _settings.Settings.Find(s => s.Profile == _settings.SelectedProfile);
        settings.Profile = newProfileName;
        UpdateProfilesDataSource();
        _settings.SelectedProfile = newProfileName;
        cmbProfile.SelectedItem = newProfileName;
    }

    private void btnProfileDelete_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show(@"Are you sure you want to delete the current Profile?", @"Deleting Profile",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            var settingToRemove = _settings.Settings.Find(s => s.Profile == _settings.SelectedProfile);
            _settings.Settings.Remove(settingToRemove);
            UpdateProfilesDataSource();
            _settings.SelectedProfile = _settings.Settings.First().Profile;
            PopulateFields();
        }
    }

    private void CheckBoxTranscode_CheckedChanged(object sender, EventArgs e)
    {
        ComboBoxBitrate.Enabled = CheckBoxTranscode.Checked;
    }
}