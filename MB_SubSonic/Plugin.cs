using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using MusicBeePlugin.Helpers;
using MusicBeePlugin.Properties;
using MusicBeePlugin.Windows;

namespace MusicBeePlugin
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Plugin
    {
        private readonly Interfaces.Plugin.PluginInfo _about = new Interfaces.Plugin.PluginInfo();
        private Interfaces.Plugin.MusicBeeApiInterface _mbApiInterface;
        private SettingsWindow _settingsWindow;

        // ReSharper disable once UnusedMember.Global
        public Interfaces.Plugin.PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            _mbApiInterface = new Interfaces.Plugin.MusicBeeApiInterface();
            _mbApiInterface.Initialise(apiInterfacePtr);
            Subsonic.SendNotificationsHandler = _mbApiInterface.MB_SendNotification;
            Subsonic.CreateBackgroundTask = _mbApiInterface.MB_CreateBackgroundTask;
            Subsonic.SetBackgroundTaskMessage = _mbApiInterface.MB_SetBackgroundTaskMessage;
            Subsonic.RefreshPanels = _mbApiInterface.MB_RefreshPanels;
            Subsonic.GetFileTag = _mbApiInterface.Library_GetFileTag;
            //Subsonic.GetFileTags = _mbApiInterface.Library_GetFileTags;
            Subsonic.QueryPlaylistFilesEx = _mbApiInterface.Playlist_QueryFilesEx;
            _about.PluginInfoVersion = Interfaces.Plugin.PluginInfoVersion;
            _about.Name = "Subsonic Client";
            _about.Description = "Access files and playlists on a SubSonic (or compatible) Server";
            _about.Author = "Dimitris Panokostas";
            _about.TargetApplication = "Subsonic";
            // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            _about.Type = Interfaces.Plugin.PluginType.Storage;
            _about.VersionMajor = 2; // your plugin version
            _about.VersionMinor = 26;
            _about.Revision = 0;
            _about.MinInterfaceVersion = Interfaces.Plugin.MinInterfaceVersion;
            _about.MinApiRevision = Interfaces.Plugin.MinApiRevision;
            _about.ReceiveNotifications = Interfaces.Plugin.ReceiveNotificationFlags.PlayerEvents |
                                          Interfaces.Plugin.ReceiveNotificationFlags.TagEvents;
            _about.ConfigurationPanelHeight =
                0; // height in pixels that musicbee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            _settingsWindow = new SettingsWindow(_mbApiInterface, _about);
            return _about;
        }

        // ReSharper disable once UnusedMember.Global
        public bool Configure(IntPtr panelHandle)
        {
            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
            //if (panelHandle == IntPtr.Zero) return false;

            _settingsWindow?.Show();
            return true;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // its up to you to figure out whether anything has changed and needs updating
        public void SaveSettings()
        {
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        public void Close(Interfaces.Plugin.PluginCloseReason reason)
        {
            Subsonic.Close();
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
            var path = _mbApiInterface.Setting_GetPersistentStoragePath();
            var filename = "subsonicCache.dat";
            FileHelper.DeleteFile(path, filename);

            filename = "subsonicSettings.dat";
            FileHelper.DeleteFile(path, filename);
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, Interfaces.Plugin.NotificationType type)
        {
            // perform some action depending on the notification type
            switch (type)
            {
                case Interfaces.Plugin.NotificationType.PluginStartup:
                    var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
                    Subsonic.CacheFilename = Path.Combine(dataPath, "subsonicCache.dat");
                    Subsonic.SettingsFilename = Path.Combine(dataPath, "subsonicSettings.dat");

                    Subsonic.SendNotificationsHandler.Invoke(Subsonic.Initialize()
                        ? Interfaces.Plugin.CallbackType.StorageReady
                        : Interfaces.Plugin.CallbackType.StorageFailed);
                    break;

                case Interfaces.Plugin.NotificationType.TagsChanged:
                    //var tags = new List<Interfaces.Plugin.MetaDataType>
                    //{
                    //    Interfaces.Plugin.MetaDataType.Artist,
                    //    Interfaces.Plugin.MetaDataType.TrackTitle,
                    //    Interfaces.Plugin.MetaDataType.Album,
                    //    Interfaces.Plugin.MetaDataType.Year,
                    //    Interfaces.Plugin.MetaDataType.TrackNo,
                    //    Interfaces.Plugin.MetaDataType.Genre,
                    //    Interfaces.Plugin.MetaDataType.Artwork,
                    //    Interfaces.Plugin.MetaDataType.DiscNo,
                    //    Interfaces.Plugin.MetaDataType.RatingLove,
                    //    Interfaces.Plugin.MetaDataType.Rating
                    //};

                    //Subsonic.GetFileTags(sourceFileUrl, tags.ToArray(), out var results);
                    //Subsonic.UpdateTags(results);

                    var rating = Subsonic.GetFileTag(sourceFileUrl, Interfaces.Plugin.MetaDataType.Rating);
                    var starred = Subsonic.GetFileTag(sourceFileUrl, Interfaces.Plugin.MetaDataType.RatingLove);
                    var id = Subsonic.GetFileTag(sourceFileUrl, Interfaces.Plugin.MetaDataType.Custom16);

                    Subsonic.UpdateRating(id, rating);
                    Subsonic.UpdateRatingLove(id, starred);
                    break;

                //case Interfaces.Plugin.NotificationType.PlaylistCreated:
                //    // TODO send new playlist to Subsonic Server
                //    string[] filenames;
                //    Subsonic.QueryPlaylistFilesEx(sourceFileUrl, out filenames);

                //    break;

                //case Interfaces.Plugin.NotificationType.PlaylistUpdated:
                //    //TODO check what happens here
                //    string[] filenames1;
                //    Subsonic.QueryPlaylistFilesEx(sourceFileUrl, out filenames1);

                //    break;

                case Interfaces.Plugin.NotificationType.PlaylistDeleted:
                    //TODO Delete playlist
                    var serverPlaylists = Subsonic.GetPlaylists();
                    var playlistToDelete = serverPlaylists.Where(p => p.Value.Equals(GetPlaylistName(sourceFileUrl)));
                    foreach (var item in playlistToDelete)
                    {
                        Subsonic.DeletePlaylist(int.Parse(item.Key));
                    }
                    break;

                case Interfaces.Plugin.NotificationType.PlaylistMoved:
                    string[] filenames;
                    Subsonic.QueryPlaylistFilesEx(sourceFileUrl, out filenames);

                    // Get Song IDs to add to playlist
                    var songIds = new List<int>();
                    foreach (var filename in filenames)
                    {
                        id = Subsonic.GetFileTag(filename, Interfaces.Plugin.MetaDataType.Custom16);
                        if (int.TryParse(id, out var songId))
                            songIds.Add(songId);
                    }

                    Subsonic.CreatePlaylist(GetPlaylistName(sourceFileUrl), songIds);
                    break;

                //case NotificationType.TrackChanged:
                //    string artist = mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                //    // ...
                //    break;
            }
        }

        private string GetPlaylistName(string playlistUrl)
        {
            // Get Playlist Name
            const string trimEndChars = ".mbp";
            const string trimStartChars = "\\";
            var playlistName = playlistUrl.TrimEnd(trimEndChars.ToCharArray()).Substring(playlistUrl.LastIndexOf('\\'));
            return playlistName.TrimStart(trimStartChars.ToCharArray());
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
                Subsonic.Refresh();
            else
                Subsonic.SendNotificationsHandler.Invoke(Subsonic.Initialize()
                    ? Interfaces.Plugin.CallbackType.StorageReady
                    : Interfaces.Plugin.CallbackType.StorageFailed);
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