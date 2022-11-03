﻿using System;
using System.Diagnostics;
using System.Windows.Forms;
using MusicBeePlugin.Domain;

namespace MusicBeePlugin.Windows
{
    public partial class SettingsWindow : Form
    {
        private readonly Interfaces.Plugin.PluginInfo _about;
        private Interfaces.Plugin.MusicBeeApiInterface _mbApiInterface;

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
            Shown += Settings_OnShown;
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
        }

        private void UpdateAll()
        {
            var currentSettings = Subsonic.GetCurrentSettings();
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
        }

        private void OnVisibleChanged(object sender, EventArgs eventArgs)
        {
            if (Visible) UpdateAll();
        }

        private void Settings_OnShown(object sender, EventArgs eventArgs)
        {
            UpdateAll();
        }

        private void Settings_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            Hide();
            e.Cancel = true;
        }

        private void PersistValues()
        {
            var settings = GetFormSettings();
            var isChanged = Subsonic.IsSettingChanged(settings);
            if (!isChanged) return;

            var saved = Subsonic.SaveSettings(settings);
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
https://github.com/midwan/MB_SubSonic", caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            Hide();
        }

        private void ButtonSave_Click(object sender, EventArgs e)
        {
            PersistValues();
            Hide();
        }

        private void ButtonPing_Click(object sender, EventArgs e)
        {
            var settings = GetFormSettings();

            var pingResult = Subsonic.PingServer(settings);
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

        private SubsonicSettings GetFormSettings()
        {
            return new SubsonicSettings
            {
                Host = TextBoxHostname.Text,
                Port = TextBoxPort.Text,
                BasePath = TextBoxPath.Text,
                Username = TextBoxUsername.Text,
                Password = TextBoxPassword.Text,
                Transcode = CheckBoxTranscode.Checked,
                Protocol = ComboBoxProtocol.SelectedItem.ToString().Equals("HTTP")
                    ? SubsonicSettings.ConnectionProtocol.Http
                    : SubsonicSettings.ConnectionProtocol.Https,
                Auth = ComboBoxAuth.SelectedIndex.Equals(0)
                    ? SubsonicSettings.AuthMethod.Token
                    : SubsonicSettings.AuthMethod.HexPass,
                BitRate = string.IsNullOrEmpty(ComboBoxBitrate.SelectedItem.ToString())
                    ? "128K"
                    : ComboBoxBitrate.SelectedItem.ToString()
            };
        }

        private void cmbProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            //TODO
        }

        private void btnProfileNew_Click(object sender, EventArgs e)
        {
            //TODO
        }

        private void btnProfileDelete_Click(object sender, EventArgs e)
        {
            //TODO
        }
    }
}