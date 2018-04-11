using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MusicBeePlugin.Domain;
using MusicBeePlugin.Properties;

namespace MusicBeePlugin
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public partial class Plugin
    {
        private readonly PluginInfo _about = new PluginInfo();
        private TextBox _basePath;
        private TextBox _host;
        private MusicBeeApiInterface _mbApiInterface;
        private TextBox _password;
        private TextBox _port;
        private CheckBox _transcode;
        private ComboBox _bitRate;
        private TextBox _username;
        private ComboBox _protocol;
        private ComboBox _authMethodBox;
        private Label bitRateLabel;

        // ReSharper disable once UnusedMember.Global
        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            _mbApiInterface = new MusicBeeApiInterface();
            _mbApiInterface.Initialise(apiInterfacePtr);
            Subsonic.SendNotificationsHandler = _mbApiInterface.MB_SendNotification;
            Subsonic.CreateBackgroundTask = _mbApiInterface.MB_CreateBackgroundTask;
            Subsonic.SetBackgroundTaskMessage = _mbApiInterface.MB_SetBackgroundTaskMessage;
            Subsonic.RefreshPanels = _mbApiInterface.MB_RefreshPanels;
            _about.PluginInfoVersion = PluginInfoVersion;
            _about.Name = "Subsonic v2.13";
            _about.Description = "Access files and playlists on a SubSonic Server";
            _about.Author = "Dimitris Panokostas";
            _about.TargetApplication = "Subsonic";
            // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            _about.Type = PluginType.Storage;
            _about.VersionMajor = 2; // your plugin version
            _about.VersionMinor = 13;
            _about.Revision = 0;
            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;
            _about.ReceiveNotifications = ReceiveNotificationFlags.StartupOnly;
            _about.ConfigurationPanelHeight = TextRenderer.MeasureText("FirstRowText", SystemFonts.DefaultFont).Height * 12;
            // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function
            return _about;
        }

        // ReSharper disable once UnusedMember.Global
        public bool Configure(IntPtr panelHandle)
        {
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            if (panelHandle == IntPtr.Zero) return false;

            var configPanel = (Panel) Control.FromHandle(panelHandle);
            var protocolWidth = TextRenderer.MeasureText(@"HTTPS", configPanel.Font).Width*2;
            var hostTextBoxWidth = TextRenderer.MeasureText(@"my-server-name.subsonic.org", configPanel.Font).Width;
            var portTextBoxWidth = TextRenderer.MeasureText(@"844345", configPanel.Font).Width;
            var authMethodWidth = TextRenderer.MeasureText(@"Hex enc. password", configPanel.Font).Width;
            var bitRateWidth = TextRenderer.MeasureText(@"Unlimited", configPanel.Font).Width;
            var spacer = TextRenderer.MeasureText("X", configPanel.Font).Width;
            const int firstRowPosY = 0;
            var secondRowPosY = TextRenderer.MeasureText("FirstRowText", configPanel.Font).Height*2;
            var thirdRowPosY = TextRenderer.MeasureText("FirstRowText", configPanel.Font).Height*4;
            var fourthRowPosY = TextRenderer.MeasureText("FirstRowText", configPanel.Font).Height*6;
            var fifthRowPosY = TextRenderer.MeasureText("FirstRowText", configPanel.Font).Height*8;

            var hostPrompt = new Label
            {
                AutoSize = true,
                Location = new Point(0, secondRowPosY + 2),
                Text = @"Hostname:"
            };
            _host = new TextBox();
            _host.Bounds =
                new Rectangle(hostPrompt.Left + TextRenderer.MeasureText(hostPrompt.Text, configPanel.Font).Width,
                    secondRowPosY, hostTextBoxWidth, _host.Height);
            _host.Text = Subsonic.Host;

            var portPrompt = new Label
            {
                AutoSize = true,
                Location = new Point(_host.Left + _host.Width + spacer, hostPrompt.Top),
                Text = @"Port:"
            };
            _port = new TextBox();
            _port.Bounds =
                new Rectangle(portPrompt.Left + TextRenderer.MeasureText(portPrompt.Text, configPanel.Font).Width,
                    secondRowPosY, portTextBoxWidth, _port.Height);
            _port.Text = Subsonic.Port;

            var basePathPrompt = new Label
            {
                AutoSize = true,
                Location = new Point(hostPrompt.Left, thirdRowPosY + 2),
                Text = @"Path:"
            };
            _basePath = new TextBox();
            _basePath.Bounds = new Rectangle(_host.Left, thirdRowPosY, hostTextBoxWidth, _basePath.Height);
            _basePath.Text = Subsonic.BasePath;

            var usernamePrompt = new Label
            {
                AutoSize = true,
                Location = new Point(hostPrompt.Left, fourthRowPosY + 2),
                Text = @"Username:"
            };
            _username = new TextBox();
            _username.Bounds = new Rectangle(_host.Left, fourthRowPosY, hostTextBoxWidth, _username.Height);
            _username.Text = Subsonic.Username;

            var passwordPrompt = new Label
            {
                AutoSize = true,
                Location = new Point(hostPrompt.Left, fifthRowPosY + 2),
                Text = @"Password:"
            };
            _password = new TextBox();
            _password.Bounds = new Rectangle(_host.Left, fifthRowPosY, hostTextBoxWidth, _password.Height);
            _password.Text = Subsonic.Password;
            _password.PasswordChar = '*';

            _transcode = new CheckBox();
            //Register a checked change event and move the assignments down
            _transcode.CheckedChanged += _transcode_CheckedChanged;

            bitRateLabel = new Label
            {
                AutoSize = true,
                Text = @"Max. Bitrate: ",
                Visible = false
            };

            _bitRate = new ComboBox();
            _bitRate.Items.Add("Unlimited");
            _bitRate.Items.Add("64K");
            _bitRate.Items.Add("80K");
            _bitRate.Items.Add("96K");
            _bitRate.Items.Add("112K");
            _bitRate.Items.Add("128K");
            _bitRate.Items.Add("160K");
            _bitRate.Items.Add("192K");
            _bitRate.Items.Add("256K");
            _bitRate.Items.Add("320K");
            _bitRate.SelectedItem = Subsonic.BitRate;
            _bitRate.DropDownStyle = ComboBoxStyle.DropDownList;
            _bitRate.Visible = false;

            /* Moving the assignments after the creation of the abive BitRate fields.
             * We would need the checked event to fire and set the visibility of the added
             * controls for BitRates.
             */
            _transcode.AutoSize = true;
            _transcode.Checked = Subsonic.Transcode;
            _transcode.Text = @"Transcode as mp3";

            var protocolLabel = new Label
            {
                AutoSize = true,
                Location = new Point(hostPrompt.Left, firstRowPosY + 2),
                Text = @"Protocol:"
            };
            _protocol = new ComboBox();
            _protocol.Bounds = new Rectangle(_host.Left, firstRowPosY, protocolWidth, _protocol.Height);
            _protocol.Items.Add("HTTP");
            _protocol.Items.Add("HTTPS");
            _protocol.SelectedItem = Subsonic.Protocol.ToFriendlyString();
            _protocol.DropDownStyle = ComboBoxStyle.DropDownList;

            var authMethodLabel = new Label
            {
                AutoSize = true,
                Location = new Point(_password.Left + _password.Width + spacer, fifthRowPosY + 2),
                Text = @"Auth:"
            };

            _authMethodBox = new ComboBox();
            _authMethodBox.Bounds = new Rectangle(authMethodLabel.Left + TextRenderer.MeasureText(authMethodLabel.Text, configPanel.Font).Width, fifthRowPosY, authMethodWidth, _authMethodBox.Height);
            _authMethodBox.Items.Add("Token based");
            _authMethodBox.Items.Add("Hex enc. password");
            _authMethodBox.SelectedIndex = (int)Subsonic.AuthMethod;
            _authMethodBox.DropDownStyle = ComboBoxStyle.DropDownList;

            configPanel.Controls.AddRange(new Control[]
            {
                protocolLabel, _protocol, _host, hostPrompt, portPrompt, _port, _basePath, basePathPrompt, _username, usernamePrompt, _password,
                passwordPrompt, _transcode, bitRateLabel, _bitRate, _authMethodBox, authMethodLabel
            });
            _transcode.Location = new Point(_port.Left, basePathPrompt.Top - 2);
            bitRateLabel.Location = new Point(_port.Left - 3, fourthRowPosY + 2);
            _bitRate.Bounds = new Rectangle(bitRateLabel.Left +
                TextRenderer.MeasureText(bitRateLabel.Text, configPanel.Font).Width, fourthRowPosY, bitRateWidth + spacer, _bitRate.Height);
            configPanel.Width = _bitRate.Right + spacer;
            return true;
        }

        private void _transcode_CheckedChanged(object sender, EventArgs e)
        {
            bitRateLabel.Visible = _bitRate.Visible = _transcode.Checked;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            var settings = new SubsonicSettings
            {
                Host = _host.Text,
                Port = _port.Text,
                BasePath = _basePath.Text,
                Username = _username.Text,
                Password = _password.Text,
                Transcode = _transcode.Checked,
                Protocol =
                    _protocol.SelectedItem.ToString().Equals("HTTP")
                        ? SubsonicSettings.ConnectionProtocol.Http
                        : SubsonicSettings.ConnectionProtocol.Https,
                Auth =
                    _authMethodBox.SelectedIndex.Equals(0)
                        ? SubsonicSettings.AuthMethod.Token
                        : SubsonicSettings.AuthMethod.HexPass,
                BitRate = _bitRate.SelectedItem.ToString()
            };

            var setHostSuccess = Subsonic.SetHost(settings);
            if (setHostSuccess)
            {
                DeleteCacheFile();
                Refresh();
                return;
            }

            var error = Subsonic.GetError();
            var message = error?.Message;
            if (!string.IsNullOrEmpty(message))
            {
                MessageBox.Show(_host, $@"Error: {message}", @"Subsonic Plugin", MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
        }

        private void DeleteCacheFile()
        {
            var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
            if (File.Exists(Path.Combine(dataPath, "subsonicCache.dat")))
            {
                try
                {
                    File.Delete(Path.Combine(dataPath, "subsonicCache.dat"));
                }
                catch (Exception)
                {
                    MessageBox.Show(@"An error has occurred while trying to delete the Subsonic cache file.\nPlease try deleting the file manually.", @"An error has occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
            Subsonic.Close();
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
            DeleteCacheFile();

            var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
            if (File.Exists(Path.Combine(dataPath, "subsonicSettings.dat")))
            {
                try
                {
                    File.Delete(Path.Combine(dataPath, "subsonicSettings.dat"));
                }
                catch (Exception)
                {
                    MessageBox.Show(@"An error has occurred while trying to delete the Subsonic settings file.\nPlease try deleting the file manually.", @"An error has occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            //switch (type)
            if (type != NotificationType.PluginStartup) return;

            var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
            Subsonic.CacheUrl = Path.Combine(dataPath, "subsonicCache.dat");
            Subsonic.SettingsUrl = Path.Combine(dataPath, "subsonicSettings.dat");

            Subsonic.SendNotificationsHandler.Invoke(Subsonic.Initialize()
                ? CallbackType.StorageReady
                : CallbackType.StorageFailed);

            //case NotificationType.TrackChanged:
            //    string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
            //    // ...
            //    break;
        }


        // return an array of lyric or artwork provider names this plugin supports
        // the providers will be iterated through one by one and passed to the RetrieveLyrics/ RetrieveArtwork function in order set by the user in the MusicBee Tags(2) preferences screen until a match is found
        public string[] GetProviders()
        {
            return null;
        }

        // return lyrics for the requested artist/title from the requested provider
        // only required if PluginType = LyricsRetrieval
        // return null if no lyrics are found
        public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album,
            bool synchronisedPreferred, string provider)
        {
            return null;
        }

        // return Base64 string representation of the artwork binary data from the requested provider
        // only required if PluginType = ArtworkRetrieval
        // return null if no artwork is found
        public string RetrieveArtwork(string sourceFileUrl, string albumArtist, string album, string provider)
        {
            //Return Convert.ToBase64String(artworkBinaryData)
            return null;
        }

        public void Refresh()
        {
            if (Subsonic.IsInitialized)
            {
                Subsonic.Refresh();
            }
            else
            {
                Subsonic.SendNotificationsHandler.Invoke(Subsonic.Initialize()
                    ? CallbackType.StorageReady
                    : CallbackType.StorageFailed);
            }
        }

        public bool IsReady()
        {
            return Subsonic.IsInitialized;
        }

        public Image GetIcon()
        {
            var icon = Resources.SubSonic;
            return icon;
        }

        public bool FolderExists(string path)
        {
            return Subsonic.FolderExists(path);
        }

        public string[] GetFolders(string path)
        {
            return Subsonic.GetFolders(path);
        }

        public KeyValuePair<byte, string>[][] GetFiles(string path)
        {
            return Subsonic.GetFiles(path);
        }

        public KeyValuePair<byte, string>[] GetFile(string url)
        {
            return Subsonic.GetFile(url);
        }

        public bool FileExists(string url)
        {
            return Subsonic.FileExists(url);
        }

        public byte[] GetFileArtwork(string url)
        {
            return Subsonic.GetFileArtwork(url);
        }

        public KeyValuePair<string, string>[] GetPlaylists()
        {
            return Subsonic.GetPlaylists();
        }

        public KeyValuePair<byte, string>[][] GetPlaylistFiles(string id)
        {
            return Subsonic.GetPlaylistFiles(id);
        }

        public Stream GetStream(string url)
        {
            return Subsonic.GetStream(url);
        }

        public Exception GetError()
        {
            return Subsonic.GetError();
        }
    }
}