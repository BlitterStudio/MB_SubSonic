using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private readonly PluginInfo about = new PluginInfo();

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = PluginInfoVersion;
            about.Name = "SubSonic v2";
            about.Description = "Access files and playlists on a SubSonic Server";
            about.Author = "Dimitris Panokostas";
            about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            about.Type = PluginType.Storage;
            about.VersionMajor = 2;  // your plugin version
            about.VersionMinor = 0;
            about.Revision = 1;
            about.MinInterfaceVersion = MinInterfaceVersion;
            about.MinApiRevision = MinApiRevision;
            about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            about.ConfigurationPanelHeight = 80;   // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function
            return about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            // save any persistent settings in a sub-folder of this path
            var dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            if (panelHandle != IntPtr.Zero)
            {
                var configPanel = (Panel)Control.FromHandle(panelHandle);
                var hostPrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(0, 8),
                    Text = "Hostname:"
                };
                var host = new TextBox();
                host.Bounds = new Rectangle(80, 5, 120, host.Height);
                host.Text = "<Host>"; //TODO: needs to be =Subsonic.Host

                var portPrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(212, 8),
                    Text = "Port:"
                };
                var port = new TextBox();
                port.Bounds = new Rectangle(250, 5, 32, port.Height);
                port.Text = "<Port>"; //TODO: needs to be =Subsonic.Port

                var basePathPrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(297, 8),
                    Text = "Path:"
                };
                var basePath = new TextBox();
                basePath.Bounds = new Rectangle(337, 5, 70, basePath.Height);
                basePath.Text = "<BasePath>"; //TODO: needs to be =Subsonic.BasePath

                var usernamePrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(0, 34),
                    Text = "Username:"
                };
                var username = new TextBox();
                username.Bounds = new Rectangle(80, 31, 120, username.Height);
                username.Text = "<Username>"; //TODO: needs to be =Subsonic.Username

                var passwordPrompt = new Label
                {
                    AutoSize = true,
                    Location = new Point(0, 60),
                    Text = "Password:"
                };
                var password = new TextBox();
                password.Bounds = new Rectangle(80, 57, 120, password.Height);
                password.Text = "<Password>"; //TODO: needs to be =Subsonic.Password
                password.PasswordChar = '*';

                var transcode = new CheckBox()
                {
                    AutoSize = true,
                    Checked = false, //TODO: = Subsonic.Transcode
                    Text = "Transcode Streams"
                };

                configPanel.Controls.AddRange(new Control[] { host, hostPrompt, portPrompt, port, basePath, basePathPrompt, username, usernamePrompt, password, passwordPrompt, transcode });
                configPanel.Width = basePath.Right + 10;
                transcode.Location = new Point(basePath.Right - TextRenderer.MeasureText(transcode.Text, configPanel.Font).Width - 12, passwordPrompt.Top -1);
            }
            return false;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
            // save any persistent settings in a sub-folder of this path
            var dataPath = mbApiInterface.Setting_GetPersistentStoragePath();
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(PluginCloseReason reason)
        {
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
            switch (type)
            {
                case NotificationType.PluginStartup:
                    // perform startup initialisation
                    switch (mbApiInterface.Player_GetPlayState())
                    {
                        case PlayState.Playing:
                        case PlayState.Paused:
                            // ...
                            break;
                    }
                    break;
                case NotificationType.TrackChanged:
                    string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                    // ...
                    break;
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

    }
}
