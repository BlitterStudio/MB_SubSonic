using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface _mbApiInterface;
        private readonly PluginInfo _about = new PluginInfo();
        private TextBox _host;
        private TextBox _port;
        private TextBox _basePath;
        private TextBox _username;
        private TextBox _password;
        private CheckBox _transcode;

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            _mbApiInterface = new MusicBeeApiInterface();
            _mbApiInterface.Initialise(apiInterfacePtr);
            Subsonic.SendNotificationsHandler = _mbApiInterface.MB_SendNotification;
            _about.PluginInfoVersion = PluginInfoVersion;
            _about.Name = "Subsonic v2";
            _about.Description = "Access files and playlists on a SubSonic Server";
            _about.Author = "Dimitris Panokostas";
            _about.TargetApplication = "Subsonic";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            _about.Type = PluginType.Storage;
            _about.VersionMajor = 2;  // your plugin version
            _about.VersionMinor = 0;
            _about.Revision = 1;
            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;
            _about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            _about.ConfigurationPanelHeight = 130;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function
            return _about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            // save any persistent settings in a sub-folder of this path
            var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            if (panelHandle != IntPtr.Zero)
            {
                var configPanel = (Panel)Control.FromHandle(panelHandle);
                var hostPrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(0, 7),
                    Text = @"Hostname:"
                };
                _host = new TextBox();
                _host.Bounds = new Rectangle(hostPrompt.Left + TextRenderer.MeasureText(hostPrompt.Text, configPanel.Font).Width, 5, 200, _host.Height);
                _host.Text = Subsonic.Host;

                var portPrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(_host.Left + _host.Width + 5, 7),
                    Text = @"Port:"
                };
                _port = new TextBox();
                _port.Bounds = new Rectangle(portPrompt.Left + TextRenderer.MeasureText(portPrompt.Text, configPanel.Font).Width, 5, 60, _port.Height);
                _port.Text = Subsonic.Port;

                var basePathPrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(_port.Left + _port.Width + 5, 7),
                    Text = @"Path:"
                };
                _basePath = new TextBox();
                _basePath.Bounds = new Rectangle(basePathPrompt.Left + TextRenderer.MeasureText(basePathPrompt.Text, configPanel.Font).Width, 5, 70, _basePath.Height);
                _basePath.Text = Subsonic.BasePath;

                var usernamePrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(hostPrompt.Left, 52),
                    Text = @"Username:"
                };
                _username = new TextBox();
                _username.Bounds = new Rectangle(_host.Left, 50, 120, _username.Height);
                _username.Text = Subsonic.Username;

                var passwordPrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(hostPrompt.Left, 97),
                    Text = @"Password:"
                };
                _password = new TextBox();
                _password.Bounds = new Rectangle(_host.Left, 95, 120, _password.Height);
                _password.Text = Subsonic.Password;
                _password.PasswordChar = '*';

                _transcode = new CheckBox
                {
                    AutoSize = true,
                    Checked = Subsonic.Transcode,
                    Text = @"Transcode Streams"
                };

                configPanel.Controls.AddRange(new Control[] { _host, hostPrompt, portPrompt, _port, _basePath, basePathPrompt, _username, usernamePrompt, _password, passwordPrompt, _transcode });
                configPanel.Width = _basePath.Right + 10;
                _transcode.Location = new Point(_port.Left, passwordPrompt.Top -2);
            }
            return false;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            // save any persistent settings in a sub-folder of this path
            var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();

            var setHostSuccess = Subsonic.SetHost(_host.Text.Trim(), _port.Text.Trim(), _basePath.Text.Trim(), _username.Text.Trim(), _password.Text.Trim(), _transcode.Checked);
            if (!setHostSuccess)
            {
                var message = Subsonic.GetError().Message;
                if (!string.IsNullOrEmpty(message))
                {
                    MessageBox.Show(_host, $"Error: {message}     ", @"Subsonic Plugin", MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
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
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            //switch (type)
            if (type == NotificationType.PluginStartup)
            {
                //case NotificationType.PluginStartup:
                // perform startup initialisation
                //switch (mbApiInterface.Player_GetPlayState())
                //{
                //    case PlayState.Playing:
                //    case PlayState.Paused:
                //        // ...
                //        break;
                //}
                //break;
                Subsonic.CacheUrl = _mbApiInterface.Setting_GetPersistentStoragePath() + @"\subsonicCache.dat";
                Subsonic.SettingsUrl = _mbApiInterface.Setting_GetPersistentStoragePath() + @"\subsonicSettings.dat";
                Subsonic.SendNotificationsHandler.Invoke(Subsonic.Initialize()
                    ? CallbackType.StorageReady
                    : CallbackType.StorageFailed);

                //case NotificationType.TrackChanged:
                //    string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                //    // ...
                //    break;
            }
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
        public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album, bool synchronisedPreferred, string provider)
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
            var assembly = Assembly.GetExecutingAssembly();
            var resourceManager = new ResourceManager("Resources.Images", assembly);
            var icon = resourceManager.GetObject("Subsonic");
            return (Image) icon;
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
