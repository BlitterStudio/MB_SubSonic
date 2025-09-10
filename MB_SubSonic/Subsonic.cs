using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using MusicBeePlugin.Domain;
using MusicBeePlugin.Helpers;
using MusicBeePlugin.SubsonicAPI;
using RestSharp;

namespace MusicBeePlugin;

public static class Subsonic
{
    private const string ApiVersion = "1.13.0";
    private const string ApiVersionOlder = "1.12.0";
    private const string CaptionServerError = "Error reported from Server";
    private static SubsonicSettings _currentSettings;
    private static ServerType _serverType;
    public static bool IsInitialized;
    public static string SettingsFilename;

    public static Interfaces.Plugin.MB_SendNotificationDelegate SendNotificationsHandler;
    public static Interfaces.Plugin.MB_SetBackgroundTaskMessageDelegate SetBackgroundTaskMessage;
    public static Interfaces.Plugin.MB_RefreshPanelsDelegate RefreshPanels;
    public static Interfaces.Plugin.Library_GetFileTagDelegate GetFileTag;
    //public static Interfaces.Plugin.Library_GetFileTagsDelegate GetFileTags;
    public static Interfaces.Plugin.Playlist_QueryFilesExDelegate QueryPlaylistFilesEx;

    private static string _serverName;
    private static Exception _lastEx;
    private static bool _validSettings;
    private static bool _browseByTags;
    private static int _errors;

    private static readonly Dictionary<string, string> FolderLookup = [];
    private static readonly Dictionary<string, string> ArtistsLookup = [];
    private static readonly Dictionary<string, string> AlbumsLookup = [];


    public static bool Initialize()
    {
        _lastEx = null;
        _errors = 0;

        var settings = FileHelper.ReadSettingsFromFile(SettingsFilename);
        if (settings == null)
        {
            MessageBox.Show(@"No MB_SubSonic settings were found!
The defaults will be set instead...", @"No settings found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _currentSettings = SettingsHelper.DefaultSettings().Settings.First();
            _serverName = BuildServerUri(_currentSettings);
            _browseByTags = true;
            // No need to try a Ping in this case
            IsInitialized = true;
            _validSettings = false;
        }
        else
        {
            _currentSettings = settings.Settings.Find(s => s.Profile == settings.SelectedProfile);
            _browseByTags = _currentSettings.BrowseBy == BrowseType.Tags;
            _validSettings = true;
            IsInitialized = PingServer(_currentSettings);
        }
            
        if (_lastEx != null)
            IsInitialized = false;

        return IsInitialized;
    }
    
    public static ProfileSettings LoadSettingsFromFile()
    {
        var settings = FileHelper.ReadSettingsFromFile(SettingsFilename);
        return settings ?? SettingsHelper.DefaultSettings();
    }

    public static void ChangeServerProfile(SubsonicSettings settings)
    {
        _currentSettings = SettingsHelper.SanitizeSettings(settings);
        _serverName = BuildServerUri(_currentSettings);
        _browseByTags = _currentSettings.BrowseBy == BrowseType.Tags;
        _validSettings = true;
    }

    public static bool PingServer(SubsonicSettings settings)
    {
        _currentSettings = SettingsHelper.SanitizeSettings(settings);
        _serverName = BuildServerUri(_currentSettings);
        _validSettings = true;

        SetBackgroundTaskMessage("Subsonic server configured, attempting to Ping it...");
        try
        {
            var request = new RestRequest("ping");
            var result = SendRequest(request);
            _serverType = result != null ? ServerType.Subsonic : ServerType.None;
            _validSettings = IsPingOk(result);
            return _validSettings;
        }
        catch (Exception ex)
        {
            _lastEx = ex;
            return false;
        }
    }

    private static bool IsPingOk(Response response)
    {
        switch (_serverType)
        {
            case ServerType.Subsonic:
            {
                SetBackgroundTaskMessage($"Detected a Subsonic server, Ping response was {response.status}");
                return response.status == SubsonicAPI.ResponseStatus.ok;
            }
            case ServerType.None:
            default:
            {
                SetBackgroundTaskMessage("Could not get a valid response to Ping from the Subsonic server");
                return false;
            }
        }
    }

    private static string BuildServerUri(SubsonicSettings settings)
    {
        return $"{settings.Protocol.ToFriendlyString()}://{settings.Host}:{settings.Port}{settings.BasePath}";
    }

    public static void Close()
    {
    }

    public static bool SaveSettings(ProfileSettings settings)
    {
        settings = SettingsHelper.SanitizeSettings(settings);
        var savedResult = FileHelper.SaveSettingsToFile(settings, SettingsFilename);
        if (!savedResult)
            return false;

        _currentSettings = settings.Settings.Find(s => s.Profile == settings.SelectedProfile);
        IsInitialized = true;
        try
        {
            SendNotificationsHandler.Invoke(Interfaces.Plugin.CallbackType.SettingsUpdated);
        }
        catch (Exception ex)
        {
            _lastEx = ex;
            return false;
        }

        return true;
    }

    public static void Refresh()
    {
        RefreshPanels();
    }

    private static List<KeyValuePair<string, string>> GetArtists()
    {
        SetBackgroundTaskMessage("Running GetArtists...");
        _lastEx = null;

        if (!IsInitialized)
        {
            return [];
        }

        var artists = new List<KeyValuePair<string, string>>();
        var request = new RestRequest("getArtists");
        var result = SendRequest(request);
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (result.Item is not ArtistsID3 content || content.index == null)
            return [];

        foreach (var indexItem in content.index)
        {
            if (_errors > 0) break;
            foreach (var artist in indexItem.artist)
            {
                if (_errors > 0) break;
                artists.Add(new KeyValuePair<string, string>(artist.id, artist.name));
                if (!ArtistsLookup.ContainsKey(artist.name))
                    ArtistsLookup.Add(artist.name, artist.id);
            }
        }

        SetBackgroundTaskMessage("Done running GetArtists");
        return artists;
    }

    private static List<KeyValuePair<string, string>> GetArtist(string artistName)
    {
        SetBackgroundTaskMessage("Running GetArtist...");
        _lastEx = null;

        if (!IsInitialized)
        {
            return [];
        }

        var artistAlbums = new List<KeyValuePair<string, string>>();
        var request = new RestRequest("getArtist");
        if (!string.IsNullOrEmpty(artistName))
        {
            // MusicBee would send in the artist name instead of the artist ID
            // We need to use the ArtistsLookup Dictionary to find the relevant ID
            artistName = artistName.TrimEnd('\\');
            if (ArtistsLookup.TryGetValue(artistName, out var id))
                request.AddParameter("id", id);
            else return [];
        }
        var result = SendRequest(request);
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (result.Item is not ArtistWithAlbumsID3 content || content.album == null)
            return [];

        foreach (var album in content.album)
        {
            if (_errors > 0) break;
            artistAlbums.Add(new KeyValuePair<string, string>(album.id, album.name));
            if (!AlbumsLookup.ContainsKey(album.name))
                AlbumsLookup.Add(album.name, album.id);
        }

        SetBackgroundTaskMessage("Done running GetArtist");
        return artistAlbums;
    }

    private static KeyValuePair<byte, string>[][] GetAlbumSongs(string albumName)
    {
        // The parameter can be the Album name, or the Artist name
        // If an Artist is selected, we return all the songs from all the albums of that artist

        SetBackgroundTaskMessage("Running GetAlbumSongs...");
        _lastEx = null;

        if (!IsInitialized)
        {
            return [];
        }

        var songs = new List<KeyValuePair<byte, string>[]>();
        var baseFolderName = albumName.Substring(0, albumName.IndexOf(@"\", StringComparison.Ordinal));
        albumName = albumName.TrimEnd('\\');

        // If we have a format of Artist\Album, split them and get the Album part only
        if (albumName.Contains(@"\"))
            albumName = albumName.Substring(albumName.LastIndexOf(@"\", StringComparison.Ordinal) + 1);

        if (ArtistsLookup.ContainsKey(albumName))
        {
            // This is an Artist name
            var artistAlbums = GetArtist(albumName);
            foreach (var album in artistAlbums)
            {
                if (_errors > 0) break;
                var request = new RestRequest("getAlbum");
                request.AddParameter("id", album.Key);
                var result = SendRequest(request);
                if (result == null)
                    continue;

                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                if (result.Item is not AlbumWithSongsID3 content || content.song == null)
                    continue;

                foreach (var song in content.song)
                {
                    if (_errors > 0) break;
                    var tags = GetTags(song, baseFolderName);
                    if (tags != null)
                        songs.Add(tags);
                }
            }
        }
        else if (AlbumsLookup.TryGetValue(albumName, out var albumId))
        {
            // This is an Album name
            var request = new RestRequest("getAlbum");
            request.AddParameter("id", albumId);
            var result = SendRequest(request);
            if (result == null)
                return [];

            if (result.Item is Error error)
            {
                MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            if (result.Item is not AlbumWithSongsID3 content || content.song == null)
                return [];

            foreach (var song in content.song)
            {
                if (_errors > 0) break;
                var tags = GetTags(song, baseFolderName);
                if (tags != null)
                    songs.Add(tags);
            }
        }

        SetBackgroundTaskMessage("Done running GetAlbumSongs");
        return [.. songs];
    }

    private static KeyValuePair<byte, string>[][] GetRandomSongs(int size = 100)
    {
        SetBackgroundTaskMessage("Running GetRandomSongs...");
        _lastEx = null;

        if (!IsInitialized)
        {
            return [];
        }

        var songs = new List<KeyValuePair<byte, string>[]>();
        var request = new RestRequest("getRandomSongs");
        request.AddParameter("size", size);
        var result = SendRequest(request);
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return [];
        }

        if (result.Item is not Songs content || content.song == null)
            return [];

        foreach (var song in content.song)
        {
            if (_errors > 0) break;
            var tags = GetTags(song, null);
            if (tags != null)
                songs.Add(tags);
        }

        SetBackgroundTaskMessage("Done running GetRandomSongs");
        return [.. songs];
    }

    public static string[] GetFolders(string path)
    {
        // If the path is empty, we should return the root-level folders (Artists or Directories)
        if (string.IsNullOrEmpty(path))
        {
            var rootFolders = _browseByTags ? GetArtists() : GetIndexes(null);
            return rootFolders?.Select(artist => artist.Value).ToArray() ?? [];
        }

        // Otherwise, the user selected an existing item (Artist or Directory)
        // If we have a format of Artist\Album, there are no subfolders
        path = path.TrimEnd('\\');
        if (path.Contains(@"\"))
        {
            return [];
        }

        return _browseByTags 
            ? GetArtist(path)?.Select(album => album.Value).ToArray() ?? [] 
            : GetMusicDirectoryDirs(path)?.Select(item => item.Value).ToArray() ?? [];
    }

    public static KeyValuePair<byte, string>[][] GetFiles(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            // Root level selected
            // Let's return some random songs
            return GetRandomSongs();
        }
        return _browseByTags 
            ? GetAlbumSongs(directoryPath) 
            : GetMusicDirectoryFiles(directoryPath);
    }

    private static List<KeyValuePair<string, string>> GetIndexes(string musicFolderId)
    {
        SetBackgroundTaskMessage("Running GetIndexes...");
        _lastEx = null;

        if (!IsInitialized)
        {
            return [];
        }

        var folders = new List<KeyValuePair<string, string>>();
        var request = new RestRequest("getIndexes");
        if (!string.IsNullOrEmpty(musicFolderId))
            request.AddParameter("musicFolderId", musicFolderId);
        var result = SendRequest(request);
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (result.Item is not Indexes content || content.index == null)
            return [];

        foreach (var indexItem in content.index)
        {
            if (_errors > 0) break;
            foreach (var artist in indexItem.artist)
            {
                if (_errors > 0) break;
                folders.Add(new KeyValuePair<string, string>(artist.id, artist.name));
                if (!FolderLookup.ContainsKey(artist.name))
                    FolderLookup.Add(artist.name, artist.id);
            }
        }

        SetBackgroundTaskMessage("Done Running GetIndexes");
        return folders;
    }

    private static SubsonicAPI.Directory GetMusicDirectory(string id)
    {
        var request = new RestRequest("getMusicDirectory");
        request.AddParameter("id", id);
        var result = SendRequest(request);
        if (result == null)
            return null;

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (result.Item is not SubsonicAPI.Directory content || content.child == null)
            return null;

        return content;
    }

    /// <summary>
    /// Get the directory contents of a music directory.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static List<KeyValuePair<string, string>> GetMusicDirectoryDirs(string path)
    {
        SetBackgroundTaskMessage("Running GetMusicDirectory...");
        _lastEx = null;

        if (!IsInitialized)
        {
            return [];
        }

        var items = new List<KeyValuePair<string, string>>();
        if (!FolderLookup.TryGetValue(path, out var id))
        {
            return [];
        }

        var content = GetMusicDirectory(id);
        if (content?.child == null)
            return [];

        foreach (var child in content.child)
        {
            if (_errors > 0) break;
            // Only add directories
            if (!child.isDir) continue;

            items.Add(new KeyValuePair<string, string>(child.id, child.title));
            if (!FolderLookup.ContainsKey(child.title))
                FolderLookup.Add(child.title, child.id);
        }

        SetBackgroundTaskMessage("Done Running GetMusicDirectory");
        return items;
    }

    /// <summary>
    /// Get the file contents of a music directory.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static KeyValuePair<byte, string>[][] GetMusicDirectoryFiles(string path)
    {
        SetBackgroundTaskMessage("Running GetMusicDirectory...");
        _lastEx = null;

        if (!IsInitialized)
        {
            return [];
        }

        var songs = new List<KeyValuePair<byte, string>[]>();
        var baseFolderName = path.Substring(0, path.IndexOf(@"\", StringComparison.Ordinal));
        path = path.TrimEnd('\\');

        // If we have a format of Artist\Album, split them and get the Album part only
        if (path.Contains(@"\"))
            path = path.Substring(path.LastIndexOf(@"\", StringComparison.Ordinal) + 1);

        if (FolderLookup.TryGetValue(path, out var id))
        {
            RetrieveFilesFromDirectory(id, baseFolderName, songs);
        }

        SetBackgroundTaskMessage("Done running GetMusicDirectory");
        return songs.ToArray();
    }

    private static void RetrieveFilesFromDirectory(string directoryId, string baseFolderName, List<KeyValuePair<byte, string>[]> songs)
    {
        var content = GetMusicDirectory(directoryId);
        if (content?.child == null)
            return;

        foreach (var child in content.child)
        {
            if (_errors > 0) break;

            if (child.isDir)
            {
                // Recursively retrieve files from subdirectories
                RetrieveFilesFromDirectory(child.id, baseFolderName, songs);
            }
            else
            {
                var tags = GetTags(child, baseFolderName);
                if (tags != null)
                    songs.Add(tags);
            }
        }
    }

    public static KeyValuePair<byte, string>[] GetFile(string url)
    {
        // the url will be the song ID (e.g. "tr-12")
        var request = new RestRequest("getSong");
        request.AddParameter("id", url);
        var result = SendRequest(request);
        if (result == null)
            return null;

        if (result.Item is Error error1)
        {
            MessageBox.Show($@"An error has occurred:
{error1.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (result.Item is not Child song)
            return null;

        return GetTags(song, null);
    }

    private static string GetCoverArtId(string url)
    {
        if (url == null) return null;

        // the url will be the song ID (e.g. "tr-12")
        var request = new RestRequest("getSong");
        request.AddParameter("id", url);
        var result = SendRequest(request);
        if (result == null)
            return null;

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        return result.Item is not Child song 
            ? null 
            : song.coverArt;
    }
    
    private static KeyValuePair<byte, string>[] GetTags(Child child, string baseFolderName)
    {
        if (child.isVideo)
            return null;

        var path = child.id;

        return
        [
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Url, path),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Artist, child.artist ?? ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.TrackTitle, child.title ?? ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Album, child.album ?? ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Year, child.year.ToString()),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.TrackNo, child.track.ToString()),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Genre, child.genre ?? ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Duration, (child.duration * 1000).ToString()),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Bitrate, child.bitRate.ToString()),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Size, child.size.ToString()),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Artwork, child.coverArt ?? ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.DiscNo, child.discNumber.ToString()),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.RatingLove, child.starred != default ? "L" : ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Custom16, child.id ?? ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Rating, child.userRating.ToString())
        ];
    }

    public static byte[] GetFileArtwork(string url)
    {
        _lastEx = null;
        if (url == null)
            return null;

        var coverArtId = GetCoverArtId(url);
        
        try
        {
            var request = new RestRequest("getCoverArt");
            request.AddParameter("id", coverArtId);
            return DownloadData(request);
        }
        catch (Exception ex)
        {
            _lastEx = ex;
            return null;
        }
    }

    public static KeyValuePair<string, string>[] GetPlaylists()
    {
        _lastEx = null;

        var request = new RestRequest("getPlaylists");
        var result = SendRequest(request);
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        var content = result.Item as Playlists;
        if (content?.playlist == null)
            return [];

        var playlists = content.playlist.Select(playlistEntry => new KeyValuePair<string, string>(playlistEntry.id, playlistEntry.name)).ToArray();

        return playlists;
    }

    public static KeyValuePair<byte, string>[][] GetPlaylistFiles(string id)
    {
        _lastEx = null;
        var files = new List<KeyValuePair<byte, string>[]>();
        var request = new RestRequest("getPlaylist");
        request.AddParameter("id", id);
        var result = SendRequest(request);
        if (result == null)
            return null;

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (result.Item is not PlaylistWithSongs content || content.entry == null)
            return [.. files];

        files.AddRange(content.entry.Select(playlistEntry => GetTags(playlistEntry, null)).Where(tags => tags != null));

        return [.. files];
    }

    public static void UpdateRating(string id, string rating)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(rating))
            throw new ArgumentException("Id and rating parameters cannot be null or empty.");

        if (!int.TryParse(rating, out var parsedRating))
            throw new ArgumentException("Rating must be an integer.");

        var request = new RestRequest("setRating");
        request.AddParameter("id", id);
        request.AddParameter("rating", parsedRating);

        var response = SendRequest(request);
        if (response?.Item is Error)
            throw new Exception("Failed to update rating.");
    }

    public static void UpdateRatingLove(string id, string starred)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Id parameter cannot be null or empty.");

        var resource = starred.Equals("L", StringComparison.OrdinalIgnoreCase) ? "star" : "unstar";
        var request = new RestRequest { Resource = resource };
        request.AddParameter("id", id);
        var response = SendRequest(request);

        if (response?.Item is Error)
            throw new Exception("Failed to update rating love.");
    }

    public static void CreatePlaylist(string name, List<int> songIds)
    {
        //TODO
        var request = new RestRequest("createPlaylist");
        request.AddParameter("name", name);
        foreach (var songId in songIds) request.AddParameter("songId", songId);

        SendRequest(request);
    }

    public static void UpdatePlaylist(int playlistId, string name, List<int> songIdsToAdd,
        List<int> songIdsToRemove)
    {
        //TODO
    }

    public static void DeletePlaylist(int playlistId)
    {
        //TODO
        var request = new RestRequest("deletePlaylist");
        request.AddParameter("id", playlistId);
        SendRequest(request);
    }

    public static Stream GetStream(string url)
    {
        _lastEx = null;

        var fileId = url;
        if (fileId == null)
        {
            _lastEx = new FileNotFoundException();
            return null;
        }

        var salt = GenerateSalt();
        var token = Md5(_currentSettings.Password + salt);
        var transcodeAndBitRate = GetTranscodeAndBitRate();
        var uriLine = _currentSettings.Auth == AuthMethod.HexPass
            ? $"{_serverName}rest/stream?u={_currentSettings.Username}&p=enc:{BitConverter.ToString(Encoding.Default.GetBytes(_currentSettings.Password)).Replace("-", "")}&v={ApiVersionOlder}&c=MusicBee&id={fileId}&{transcodeAndBitRate}"
            : $"{_serverName}rest/stream?u={_currentSettings.Username}&t={token}&s={salt}&v={ApiVersion}&c=MusicBee&id={fileId}&{transcodeAndBitRate}";

        var uri = new Uri(uriLine);
        var stream = new ConnectStream(uri);
        if (stream.ContentType.StartsWith("text/xml"))
        {
            _lastEx = new InvalidDataException();
            stream.Dispose();
            return null;
        }

        return stream;
    }

    private static string GetTranscodeAndBitRate()
    {
        /* If the Transcode is set, then there must be a bit rate that you would want to set.
         * ... and if the max bit rate is already set at the server side, this would not
         * cause any harm.
         */
        if (_currentSettings.Transcode)
            return "maxBitRate=" + GetBitRate(_currentSettings.BitRate);
        return "format=raw";
    }

    private static string GetBitRate(string bitRate)
    {
        return bitRate.Equals("Unlimited") ? "0" : bitRate.TrimEnd('K');
    }

    public static Exception GetError()
    {
        return _lastEx;
    }

    private static Response SendRequest(RestRequest request)
    {
        if (!_validSettings)
            return null;

        var client = new RestClient($"{_serverName}rest/");
        request.AddParameter("u", _currentSettings.Username);

        if (_currentSettings.Auth == AuthMethod.HexPass)
        {
            var hexPass = $"enc:{BitConverter.ToString(Encoding.Default.GetBytes(_currentSettings.Password)).Replace("-", "")}";
            request.AddParameter("p", hexPass);
            request.AddParameter("v", ApiVersionOlder);
        }
        else
        {
            var salt = GenerateSalt();
            var token = Md5(_currentSettings.Password + salt);
            request.AddParameter("t", token);
            request.AddParameter("s", salt);
            request.AddParameter("v", ApiVersion);
        }

        request.AddParameter("c", "MusicBee");

        var response = client.ExecuteAsync<Response>(request).Result;
        if (!response.IsSuccessful)
        {
            if (_errors > 0) return response.Data;
            MessageBox.Show($@"Error retrieving response from Subsonic server:

{response.ErrorException}",
                @"Subsonic Plugin Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            _errors++;
            return response.Data;
        }

        _errors = 0;
        return response.Data;
    }

    private static byte[] DownloadData(RestRequest request)
    {
        var client = new RestClient($"{_serverName}rest/");
        request.AddParameter("u", _currentSettings.Username);

        if (_currentSettings.Auth == AuthMethod.HexPass)
        {
            var hexPass = $"enc:{BitConverter.ToString(Encoding.Default.GetBytes(_currentSettings.Password)).Replace("-", "")}";
            request.AddParameter("p", hexPass);
            request.AddParameter("v", ApiVersionOlder);
        }
        else
        {
            var salt = GenerateSalt();
            var token = Md5(_currentSettings.Password + salt);
            request.AddParameter("t", token);
            request.AddParameter("s", salt);
            request.AddParameter("v", ApiVersion);
        }

        request.AddParameter("c", "MusicBee");
        var response = client.ExecuteAsync<Response>(request).Result;

        if (response.ContentType != null && !response.ContentType.StartsWith("text/xml"))
            return response.RawBytes;

        if (response.Data?.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return null;
    }

    private static string GenerateSalt()
    {
        const int minSaltSize = 6;
        const int maxSaltSize = 12;

        // Use a single instance of Random, rather than creating a new one every time this method is called.
        var random = new Random();
        var saltSize = random.Next(minSaltSize, maxSaltSize);

        // Use a cryptographic random number generator for generating the salt bytes.
        var saltBytes = new byte[saltSize];
        using (var rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(saltBytes);
        }

        // Convert the byte array to a hexadecimal string.
        return BitConverter.ToString(saltBytes).Replace("-", "").ToLowerInvariant();
    }

    private static string Md5(string saltedPassword)
    {
        // Use using statement to ensure the disposal of the MD5CryptoServiceProvider.
        using var md5 = MD5.Create();
        var inputBytes = Encoding.ASCII.GetBytes(saltedPassword);
        var hashBytes = md5.ComputeHash(inputBytes);

        // Convert the byte array to hexadecimal string.
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public static bool FolderExists(string path)
    {
        path = path.TrimEnd('\\');
        return _browseByTags 
            ? FolderLookup.ContainsKey(path)
            : ArtistsLookup.ContainsKey(path) || AlbumsLookup.ContainsKey(path);
    }

    public static bool FileExists(string url)
    {
        return GetFile(url) != null;
    }
}