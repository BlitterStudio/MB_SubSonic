using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Windows.Forms;
using MusicBeePlugin.Domain;
using MusicBeePlugin.Helpers;
using MusicBeePlugin.SubsonicAPI;
using RestSharp;

namespace MusicBeePlugin;

public static class Subsonic
{
    private const string ApiVersion = "1.16.1";
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

    private static readonly Dictionary<string, string> FolderLookup = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> ArtistsLookup = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> AlbumsLookup = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> PlaylistsLookup = new(StringComparer.OrdinalIgnoreCase);


    public static bool Initialize()
    {
        _lastEx = null;
        _errors = 0;

        var settings = FileHelper.ReadSettingsFromFile(SettingsFilename);
        if (settings == null)
        {
            //MessageBox.Show(@"No MB_SubSonic settings were found!
            //The defaults will be set instead...", @"No settings found", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        FolderLookup.Clear();
        ArtistsLookup.Clear();
        AlbumsLookup.Clear();
        PlaylistsLookup.Clear();
        RefreshPanels();
    }

    private static List<KeyValuePair<string, string>> GetArtists()
    {
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
            _lastEx = new Exception($@"An error has occurred: {error.message}");
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
                var cleanName = artist.name?.Trim() ?? "";
                if (!string.IsNullOrEmpty(cleanName) && !ArtistsLookup.ContainsKey(cleanName))
                    ArtistsLookup.Add(cleanName, artist.id);
            }
        }

        SetBackgroundTaskMessage("Done running GetArtists");
        return artists;
    }

    private static List<KeyValuePair<string, string>> GetArtist(string artistName)
    {
        artistName = NormalizePath(artistName);
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
            var lookupArtist = artistName.Trim('\u200B').Trim();
            if (ArtistsLookup.TryGetValue(lookupArtist, out var id))
                request.AddParameter("id", id);
            else 
            {
                return [];
            }
        }
        var result = SendRequest(request);
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            _lastEx = new Exception($@"An error has occurred: {error.message}");
            return [];
        }

        if (result.Item is not ArtistWithAlbumsID3 content || content.album == null)
            return [];

        foreach (var album in content.album)
        {
            if (_errors > 0) break;
            
            var displayAlbumName = album.name;
            var cleanArtistName = artistName.Trim('\u200B');
            
            // Disambiguation: If Album name matches Artist name, append a Zero-Width Space for MusicBee's benefit.
            // This makes the folder name UNIQUE in the tree, stopping recursion.
            if (_browseByTags && string.Equals(displayAlbumName, cleanArtistName, StringComparison.OrdinalIgnoreCase))
            {
                displayAlbumName += "\u200B";
            }

            artistAlbums.Add(new KeyValuePair<string, string>(album.id, displayAlbumName));
            
            var storageAlbumName = album.name.Trim('\u200B');
            if (!AlbumsLookup.ContainsKey(storageAlbumName))
                AlbumsLookup.Add(storageAlbumName, album.id);
        }

        return artistAlbums;
    }

    private static KeyValuePair<byte, string>[][] GetAlbumSongs(string albumName)
    {
        SetBackgroundTaskMessage($"Running GetAlbumSongs for: {albumName}");
        _lastEx = null;

        if (!IsInitialized)
            return [];

        var songs = new List<KeyValuePair<byte, string>[]>();
        
        string baseFolderName;
        var backslashIndex = albumName.IndexOf(@"\", StringComparison.Ordinal);
        if (backslashIndex >= 0)
        {
            baseFolderName = albumName.Substring(0, backslashIndex).Trim('\u200B');
            albumName = albumName.TrimEnd('\\').Substring(backslashIndex + 1).Trim('\u200B');
        }
        else
        {
            albumName = albumName.TrimEnd('\\').Trim('\u200B');
            baseFolderName = albumName;
        }

        if (ArtistsLookup.ContainsKey(albumName.Trim('\u200B')))
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
                    _lastEx = new Exception($@"An error has occurred: {error.message}");
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
        else if (AlbumsLookup.TryGetValue(albumName.Trim('\u200B'), out var albumId))
        {
            // This is an Album name
            var request = new RestRequest("getAlbum");
            request.AddParameter("id", albumId);
            var result = SendRequest(request);
            if (result == null)
                return [];

            if (result.Item is Error error)
            {
                _lastEx = new Exception($@"An error has occurred: {error.message}");
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
            _lastEx = new Exception($@"An error has occurred: {error.message}");
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

    private static bool IsRecursion(string path)
    {
        var parts = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        
        // Check for repeated segments anywhere in the path
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            var cleanPart = part.Trim('\u200B');
            if (seen.Contains(cleanPart)) return true;
            seen.Add(cleanPart);
        }
        return false;
    }

    public static string[] GetFolders(string path)
    {
        try
        {
            var originalPath = path ?? "NULL";
            
            // Hard Stop: Detect recursion BEFORE normalization
            if (IsRecursion(originalPath))
            {
                 return [];
            }

            path = NormalizePath(originalPath);
            SetBackgroundTaskMessage($"Loading: {path}");

            if (string.IsNullOrEmpty(path))
            {
                var rootFolders = _browseByTags ? GetArtists() : GetIndexes(null);
                var folderList = (rootFolders?.Select(artist => artist.Value) ?? Enumerable.Empty<string>()).ToList();
                if (!folderList.Any(f => f.Equals("Playlists", StringComparison.OrdinalIgnoreCase)))
                    folderList.Add("Playlists");

                // Pre-seed PlaylistsLookup
                GetPlaylists();

                return folderList.ToArray();
            }

            if (path.Equals("Playlists", StringComparison.OrdinalIgnoreCase))
            {
                var playlists = GetPlaylists();
                return playlists.Select(p => p.Value).ToArray();
            }

            // If it's a known playlist, it has no subfolders
            var lookupName = path.Trim('\u200B');
            if (lookupName.StartsWith("Playlists\\", StringComparison.OrdinalIgnoreCase) || lookupName.StartsWith("Playlists/", StringComparison.OrdinalIgnoreCase))
                lookupName = lookupName.Substring(10);

            if (PlaylistsLookup.ContainsKey(lookupName))
                return [];

            if (_browseByTags)
            {
                // RECURSION STOP: In tag mode, we only support Artist -> Album (2 levels).
                var parts = path.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                
                // 1. If path ends with ZWS, it's an Album. Stop subfolders.
                if (path.EndsWith("\u200B"))
                {
                    return [];
                }

                // 2. Depth check: We only allow Artist (depth 1) to expand into Albums.
                // Anything deeper is a recursion or invalid.
                if (parts.Length > 1)
                {
                    return [];
                }
                
                var albums = GetArtist(path);
                return albums?.Select(album => album.Value).ToArray() ?? [];
            }

            return GetMusicDirectoryDirs(path)?.Select(item => item.Value).ToArray() ?? [];
        }
        catch (Exception ex)
        {
            _lastEx = ex;
            SetBackgroundTaskMessage($"Error in GetFolders: {ex.Message}");
            return [];
        }
    }

    public static KeyValuePair<byte, string>[][] GetFiles(string directoryPath)
    {
        try
        {
            var originalPath = directoryPath ?? "NULL";
            directoryPath = NormalizePath(directoryPath);

            if (string.IsNullOrEmpty(directoryPath))
            {
                return GetRandomSongs();
            }

            if (directoryPath.Equals("Playlists", StringComparison.OrdinalIgnoreCase))
            {
                return GetRandomSongs();
            }

            var lookupPath = directoryPath;
            if (lookupPath.StartsWith("Playlists\\", StringComparison.OrdinalIgnoreCase))
                lookupPath = lookupPath.Substring(10);
            else if (lookupPath.StartsWith("Playlists/", StringComparison.OrdinalIgnoreCase))
                lookupPath = lookupPath.Substring(10);

            if (PlaylistsLookup.TryGetValue(lookupPath, out var playlistId))
            {
                return GetPlaylistFiles(playlistId);
            }

            return _browseByTags
                ? GetAlbumSongs(directoryPath)
                : GetMusicDirectoryFiles(directoryPath);
        }
        catch (Exception ex)
        {
            _lastEx = ex;
            SetBackgroundTaskMessage($"Error in GetFiles: {ex.Message}");
            return [];
        }
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
            _lastEx = new Exception($@"An error has occurred: {error.message}");
            return null;
        }

        if (result.Item is not Indexes content || content.index == null)
            return [];

        foreach (var indexItem in content.index)
        {
            if (_errors > 0) break;
            if (indexItem.artist == null) continue;
            foreach (var artist in indexItem.artist)
            {
                if (_errors > 0) break;
                folders.Add(new KeyValuePair<string, string>(artist.id ?? "", artist.name ?? ""));
                if (!string.IsNullOrEmpty(artist.name) && !FolderLookup.ContainsKey(artist.name))
                    FolderLookup.Add(artist.name, artist.id ?? "");
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
            _lastEx = new Exception($@"An error has occurred: {error.message}");
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

            items.Add(new KeyValuePair<string, string>(child.id ?? "", child.title ?? ""));
            if (!string.IsNullOrEmpty(child.title) && !FolderLookup.ContainsKey(child.title))
                FolderLookup.Add(child.title, child.id ?? "");
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
        SetBackgroundTaskMessage($"Running GetMusicDirectoryFiles for: {path}");
        _lastEx = null;

        if (!IsInitialized)
            return [];

        var songs = new List<KeyValuePair<byte, string>[]>();
        
        string baseFolderName;
        var backslashIndex = path.IndexOf(@"\", StringComparison.Ordinal);
        if (backslashIndex >= 0)
        {
            baseFolderName = path.Substring(0, backslashIndex).Trim('\u200B');
            path = path.TrimEnd('\\').Substring(backslashIndex + 1).Trim('\u200B');
        }
        else
        {
            path = path.TrimEnd('\\').Trim('\u200B');
            baseFolderName = path;
        }

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
            _lastEx = new Exception($@"An error has occurred: {error1.message}");
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
            _lastEx = new Exception($@"An error has occurred: {error.message}");
            return null;
        }

        return result.Item is not Child song 
            ? null 
            : song.coverArt;
    }
    
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        
        // Remove slashes but PRESERVE ZWS for expansion checks
        path = path.TrimEnd('\\', '/').Trim();

        bool stripped;
        do
        {
            stripped = false;
            if (path.StartsWith("Subsonic\\", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Subsonic/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(9).TrimStart('\\', '/');
                stripped = true;
            }
            else if (path.StartsWith("Subsonic Client\\", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Subsonic Client/", StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(16).TrimStart('\\', '/');
                stripped = true;
            }
        } while (stripped && !string.IsNullOrEmpty(path));

        return path;
    }

    public static string NormalizeId(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";

        // 1. Priority: Parse the URL directly if it's our own format
        // This is THE most reliable for streaming/artwork as it's self-contained.
        if (url.StartsWith("subsonic://", StringComparison.OrdinalIgnoreCase))
        {
            var uriPart = url.Substring(11);
            
            // Handle new format: subsonic://path?id=ID
            var queryIndex = uriPart.LastIndexOf("?id=", StringComparison.OrdinalIgnoreCase);
            if (queryIndex >= 0)
            {
                var id = uriPart.Substring(queryIndex + 4);
                return WebUtility.UrlDecode(id);
            }
            
            // Handle alternative format: subsonic://ID/path
            var slashIndex = uriPart.IndexOf('/');
            if (slashIndex >= 0)
            {
                return uriPart.Substring(0, slashIndex);
            }
                
            return uriPart; // Legacy format: subsonic://ID
        }

        // 2. Secondary: Try to get ID from tags (fallback)
        try
        {
            var taggedId = GetFileTag?.Invoke(url, Interfaces.Plugin.MetaDataType.Custom16);
            if (!string.IsNullOrEmpty(taggedId)) return taggedId;
        }
        catch { /* ignore */ }

        return url;
    }

    private static KeyValuePair<byte, string>[] GetTags(Child child, string baseFolderName)
    {
        if (child.isVideo)
            return null;

        // Construct a readable "URL" that MusicBee will show in the Filename column.
        // Format: subsonic://Artist/Album/Title.ext
        var displayPath = child.path ?? (child.title ?? child.id);
        // Replace forward slashes with backslashes for Windows-style display if it's a path
        displayPath = displayPath.Replace('/', '\\').TrimStart('\\');
        
        var urlPath = $"subsonic://{displayPath}?id={Uri.EscapeDataString(child.id ?? "")}";

        return
        [
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Url, urlPath),
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

        var normalizedId = NormalizeId(url);
        var coverArtId = GetCoverArtId(normalizedId);
        
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

        SetBackgroundTaskMessage("Fetching playlists from Subsonic...");
        var request = new RestRequest("getPlaylists");
        var result = SendRequest(request);
        if (result == null)
        {
            SetBackgroundTaskMessage("GetPlaylists: Server sent null response.");
            return [];
        }

        if (result.Item is Error error)
        {
            _lastEx = new Exception($@"An error has occurred: {error.message}");
            SetBackgroundTaskMessage($"Error fetching playlists: {error.message}");
            return [];
        }

        if (result.Item is not Playlists content)
        {
            SetBackgroundTaskMessage($"Unexpected response type for getPlaylists: {result.Item?.GetType().Name ?? "null"}");
            return [];
        }

        if (content.playlist == null)
        {
            SetBackgroundTaskMessage("No playlists element found in Subsonic response");
            return [];
        }

        var playlists = content.playlist.Select(playlistEntry => new KeyValuePair<string, string>(playlistEntry.id ?? "", playlistEntry.name ?? "")).ToArray();
        foreach (var p in playlists)
        {
            if (!PlaylistsLookup.ContainsKey(p.Value))
                PlaylistsLookup.Add(p.Value, p.Key);
        }
        SetBackgroundTaskMessage($"Found {playlists.Length} playlists on Subsonic server");

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
            return [];

        if (result.Item is Error error)
        {
            _lastEx = new Exception($@"An error has occurred: {error.message}");
            return [];
        }

        if (result.Item is not PlaylistWithSongs content)
            return [];

        if (content.entry == null)
            return [];

        files.AddRange(content.entry.Select(playlistEntry => GetTags(playlistEntry, null)).Where(tags => tags != null));
        SetBackgroundTaskMessage($"Found {files.Count} songs in playlist.");

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

    public static void CreatePlaylist(string name, List<string> songIds)
    {
        if (songIds == null || songIds.Count == 0)
        {
            SetBackgroundTaskMessage($"Attempted to create playlist '{name}' but it has no songs.");
        }

        var request = new RestRequest("createPlaylist");
        request.AddParameter("name", name);
        foreach (var songId in songIds) request.AddParameter("songId", songId);

        var response = SendRequest(request);
        if (response?.Item is Error error)
            _lastEx = new Exception($"Failed to create playlist: {error.message}");
        else
            SetBackgroundTaskMessage($"Successfully created playlist '{name}' with {songIds?.Count ?? 0} songs.");
    }

    public static void UpdatePlaylist(string playlistId, string name, List<string> songIdsToAdd,
        List<string> songIdsToRemove)
    {
        //TODO
    }

    public static void DeletePlaylist(string playlistId)
    {
        if (string.IsNullOrEmpty(playlistId)) return;

        var request = new RestRequest("deletePlaylist");
        request.AddParameter("id", playlistId);
        var response = SendRequest(request);
        if (response?.Item is Error error)
            _lastEx = new Exception($"Failed to delete playlist: {error.message}");
    }

    public static Stream GetStream(string url)
    {
        _lastEx = null;

        var fileId = NormalizeId(url);
        if (string.IsNullOrEmpty(fileId))
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
            _lastEx = response.ErrorException;

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
            _lastEx = new Exception($@"An error has occurred: {error.message}");
        }

        return null;
    }

    private static string GenerateSalt()
    {
        const int minSaltSize = 6;
        const int maxSaltSize = 12;

        var saltSize = 0;
        using (var rng = new RNGCryptoServiceProvider())
        {
             var sizeBytes = new byte[4];
             rng.GetBytes(sizeBytes);
             var randomInt = BitConverter.ToInt32(sizeBytes, 0);
             // Ensure positive and within range
             saltSize = (Math.Abs(randomInt) % (maxSaltSize - minSaltSize + 1)) + minSaltSize;
        }

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
        path = NormalizePath(path);
        if (string.IsNullOrEmpty(path)) return true;

        if (path.Equals("Playlists", StringComparison.OrdinalIgnoreCase)) return true;
        
        // Handle playlist subfolders
        if (path.StartsWith("Playlists\\", StringComparison.OrdinalIgnoreCase))
        {
            var playlistName = path.Substring(10);
            return PlaylistsLookup.ContainsKey(playlistName);
        }
        
        if (PlaylistsLookup.ContainsKey(path)) return true;

        if (_browseByTags)
        {
            // In tag mode, we ONLY support Artist or Artist\Album.
            // Any deeper path (Artist\Album\Extra) is invalid.
            var parts = path.Split(new[] { '\\' });
            if (parts.Length > 2) return false;

            var artistPart = parts[0].Trim('\u200B');
            if (!ArtistsLookup.ContainsKey(artistPart)) return false;

            if (parts.Length == 1) return true; // It's a valid Artist

            var albumPart = parts[1].Trim('\u200B');
            // It's Artist\Album, check if it's a known album
            return AlbumsLookup.ContainsKey(albumPart);
        }
        else
        {
            return FolderLookup.ContainsKey(path.Trim('\u200B'));
        }
    }

    public static bool FileExists(string url)
    {
        return GetFile(url) != null;
    }
}