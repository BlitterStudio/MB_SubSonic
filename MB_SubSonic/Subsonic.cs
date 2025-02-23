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
    private const int TagCount = 14;
    private const string ApiVersion = "1.13.0";
    private const string ApiVersionOlder = "1.12.0";
    private const string CaptionServerError = "Error reported from Server";
    private static SubsonicSettings _currentSettings;
    private static SubsonicSettings.ServerType _serverType;
    public static bool IsInitialized;
    public static string SettingsFilename;
    public static Interfaces.Plugin.MB_SendNotificationDelegate SendNotificationsHandler;
    public static Interfaces.Plugin.MB_SetBackgroundTaskMessageDelegate SetBackgroundTaskMessage;
    public static Interfaces.Plugin.MB_RefreshPanelsDelegate RefreshPanels;

    public static Interfaces.Plugin.Library_GetFileTagDelegate GetFileTag;

    //public static Interfaces.Plugin.Library_GetFileTagsDelegate GetFileTags;
    public static Interfaces.Plugin.Playlist_QueryFilesExDelegate QueryPlaylistFilesEx;
    public static string CurrentProfile = "Default";
    private static string _serverName;
    private static Exception _lastEx;
    private static readonly object CacheFileLock = new();
    private static string[] _collectionNames;
    private static readonly Dictionary<string, ulong> LastModified = [];
    private static readonly object FolderLookupLock = new();
    private static readonly Dictionary<string, string> FolderLookup = [];
    private static bool _validSettings;

    public static bool Initialize()
    {
        _lastEx = null;

        var settings = FileHelper.ReadSettingsFromFile(SettingsFilename);
        if (settings == null)
        {
            MessageBox.Show(@"No MB_SubSonic settings were found!
The defaults will be set instead...", @"No settings found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _currentSettings = SettingsHelper.DefaultSettings();
            _serverName = BuildServerUri(_currentSettings);
            // No need to try a Ping in this case
            IsInitialized = true;
            _validSettings = false;
        }
        else
        {
            _currentSettings = settings.Find(s => s.ProfileName.Equals(CurrentProfile));
            _validSettings = true;
            IsInitialized = PingServer(_currentSettings);
        }
            
        if (_lastEx != null)
            IsInitialized = false;

        return IsInitialized;
    }

    public static void MigrateOldSettings(string oldSettingsFilename, string newSettingsFilename)
    {
        if (!File.Exists(oldSettingsFilename)) return;

        var result = MessageBox.Show(
            @"Detected an older MB_SubSonic settings file.
Should it be migrated to the new format and then deleted?

Note: This operation cannot be reversed!
",
            @"Old settings file detected", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.No) return;

        var settings = FileHelper.ReadSettingsFromOldFile(oldSettingsFilename);
        FileHelper.SaveSettingsToFile([settings], newSettingsFilename);
        File.Delete(oldSettingsFilename);
    }

    public static SubsonicSettings GetCurrentSettings()
    {
        return _currentSettings ?? SettingsHelper.DefaultSettings();
    }

    public static List<SubsonicSettings> LoadSettingsFromFile()
    {
        var settings = FileHelper.ReadSettingsFromFile(SettingsFilename);
        return settings ?? [SettingsHelper.DefaultSettings()];
    }

    public static void ChangeServerProfile(SubsonicSettings settings)
    {
        _currentSettings = SettingsHelper.SanitizeSettings([settings]).First();
        _serverName = BuildServerUri(_currentSettings);
        _validSettings = true;
    }

    public static bool PingServer(SubsonicSettings settings)
    {
        _currentSettings = SettingsHelper.SanitizeSettings([settings]).First();
        _serverName = BuildServerUri(_currentSettings);
        _validSettings = true;

        SetBackgroundTaskMessage("Subsonic server configured, attempting to Ping it...");
        try
        {
            var request = new RestRequest("ping");
            var result = SendRequest(request);
            _serverType = result != null ? SubsonicSettings.ServerType.Subsonic : SubsonicSettings.ServerType.None;
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
            case SubsonicSettings.ServerType.Subsonic:
            {
                SetBackgroundTaskMessage($"Detected a Subsonic server, Ping response was {response.status}");
                return response.status == SubsonicAPI.ResponseStatus.ok;
            }
            case SubsonicSettings.ServerType.None:
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

    public static bool SaveSettings(List<SubsonicSettings> settings)
    {
        settings = SettingsHelper.SanitizeSettings(settings);
        var savedResult = FileHelper.SaveSettingsToFile(settings, SettingsFilename);
        if (!savedResult)
            return false;

        _currentSettings = settings.Find(s => s.ProfileName.Equals(CurrentProfile));
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

    public static bool FolderExists(string directoryPath)
    {
        return string.IsNullOrEmpty(directoryPath)
               || directoryPath.Equals(@"\")
               || GetFolderId(directoryPath) != null;
    }

    public static string[] GetFolders(string path)
    {
        SetBackgroundTaskMessage("Running GetFolders...");
        _lastEx = null;

        if (!IsInitialized)
        {
            return [];
        }

        if (string.IsNullOrEmpty(path))
        {
            var rootFolders = GetRootFolders(true, true, false);
            return rootFolders?.Select(folder => folder.Value).ToArray() ?? [];
        }

        var list = new List<string>();
        var folderId = GetFolderId(path);

        if (path.IndexOf(@"\", StringComparison.Ordinal) == path.LastIndexOf(@"\", StringComparison.Ordinal))
        {
            if (folderId != null)
            {
                var alwaysFalse = false;
                list.AddRange(
                    GetIndexes(folderId, path.Substring(0, path.Length - 1), false, false, ref alwaysFalse)
                        .Select(folder => folder.Key));
            }

            return [.. list];
        }

        if (!path.EndsWith(@"\"))
            path += @"\";

        if (string.IsNullOrEmpty(folderId))
        {
            return [];
        }

        var request = new RestRequest("getMusicDirectory");
        request.AddParameter("id", folderId);
        var result = SendRequest(request);
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (result.Item is SubsonicAPI.Directory { child: not null } content)
        {
            var total = content.child.Length;
            for (var index = 0; index < total; index++)
            {
                var dirChild = content.child[index];
                SetBackgroundTaskMessage($"Processing {index} of {total} Folders...");

                if (!dirChild.isDir) continue;

                folderId = dirChild.id;
                var folderName = path + dirChild.title;
                list.Add(folderName);
                if (!FolderLookup.ContainsKey(folderName))
                    FolderLookup.Add(folderName, folderId);
            }
            SetBackgroundTaskMessage("");
        }

        SetBackgroundTaskMessage("Done processing GetFolders");
        return [.. list];
    }

    public static KeyValuePair<byte, string>[][] GetFiles(string directoryPath)
    {
        SetBackgroundTaskMessage("Running GetFiles...");
        _lastEx = null;

        if (!IsInitialized || string.IsNullOrEmpty(directoryPath))
        {
            return [];
        }

        return GetFolderFiles(directoryPath);
    }

    private static List<KeyValuePair<string, string>> GetRootFolders(bool collectionOnly, bool refresh, bool dirtyOnly)
    {
        SetBackgroundTaskMessage("Running GetMusicFolders");
        var folders = new List<KeyValuePair<string, string>>();
        var collection = new List<KeyValuePair<string, string>>();

        if (!refresh && FolderLookup.Any())
            return [.. FolderLookup];

        var result = SendRequest(new RestRequest("getMusicFolders"));
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        var content = (MusicFolders)result.Item;
        if (content.musicFolder == null)
            return [];

        lock (FolderLookupLock)
        {
            var total = content.musicFolder.Length;
            for (var index = 0; index < total; index++)
            {
                SetBackgroundTaskMessage($"Processing {index} of {total} music folders");
                var folder = content.musicFolder[index];
                var folderId = folder.id.ToString();
                var folderName = folder.name;

                if (folderName == null)
                    continue;

                FolderLookup[folderName] = folderId;
                collection.Add(new KeyValuePair<string, string>(folderId, folderName));
            }

            SetBackgroundTaskMessage("");

            _collectionNames = collection.Select(c => c.Value + @"\").ToArray();

            if (collectionOnly)
                return collection;

            var isDirty = false;
            foreach (var collectionItem in collection)
            {
                folders.AddRange(GetIndexes(collectionItem.Key, collectionItem.Value, true, refresh && dirtyOnly, ref isDirty));
            }

            if (dirtyOnly && !isDirty)
                return null;
        }

        SetBackgroundTaskMessage("Done running GetMusicFolders");
        return folders;
    }

    private static IEnumerable<KeyValuePair<string, string>> GetIndexes(string collectionId,
        string collectionName,
        bool indices, bool updateIsDirty, ref bool isDirty)
    {
        SetBackgroundTaskMessage("Running GetIndexes...");
        var folders = new List<KeyValuePair<string, string>>();

        var request = new RestRequest("getIndexes");
        request.AddParameter("musicFolderId", collectionId);
        var result = SendRequest(request);
        if (result == null)
            return [];

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        if (result.Item is not Indexes content)
            return [];

        if (updateIsDirty)
        {
            var serverLastModified = (ulong)content.lastModified;
            lock (CacheFileLock)
            {
                if (!LastModified.TryGetValue(collectionName, out var clientLastModified) || serverLastModified > clientLastModified)
                {
                    isDirty = true;
                    LastModified[collectionName] = serverLastModified;
                }
            }
        }

        if (content.index == null)
            return [];

        foreach (var indexChild in content.index)
        {
            foreach (var artistChild in indexChild.artist)
            {
                var folderId = artistChild.id;
                var folderName = $"{collectionName}\\{artistChild.name}";
                FolderLookup[folderName] = folderId;
                folders.Add(new KeyValuePair<string, string>(indices ? folderId : folderName, collectionName));
            }
        }

        SetBackgroundTaskMessage("Done Running GetIndexes");
        return folders;
    }

    private static void GetFolderFiles(string baseFolderName, string folderPath, string folderId,
        ICollection<KeyValuePair<byte, string>[]> files)
    {
        // Workaround for MusicBee calling GetFile on root folder(s)
        var rootFolders = GetRootFolders(true, true, false);
        if (rootFolders.Any(x => x.Key.Equals(folderId)))
            return;

        var request = new RestRequest("getMusicDirectory");
        request.AddParameter("id", folderId);
        var result = SendRequest(request);
        if (result == null)
            return;

        if (result.Item is Error error)
        {
            MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (result.Item is not SubsonicAPI.Directory content || content.child == null)
            return;

        var total = content.child.Length;
        for (var index = 0; index < total; index++)
        {
            SetBackgroundTaskMessage($"Processing MusicDirectory {index} of {total}...");
            var childEntry = content.child[index];
            if (childEntry.isDir)
                continue;

            // Support for servers that do not provide path (e.g. ownCloud Music)
            childEntry.path ??= string.Concat(folderPath.Substring(baseFolderName.Length + 1), childEntry.id);

            var tags = GetTags(childEntry, baseFolderName);
            if (tags != null)
                files.Add(tags);
        }
    }

    private static KeyValuePair<byte, string>[][] GetFolderFiles(string path)
    {
        if (!path.EndsWith(@"\"))
            path += @"\";

        var folderId = GetFolderId(path);
        if (folderId == null)
            return [];

        // Workaround for MusicBee calling this on root folder(s)
        var rootFolders = GetRootFolders(true, true, false);
        if (rootFolders.Any(x => x.Key.Equals(folderId)))
            return [];

        var baseFolderName = path.Substring(0, path.IndexOf(@"\", StringComparison.Ordinal));
        var files = new List<KeyValuePair<byte, string>[]>();
        GetFolderFiles(baseFolderName, path, folderId, files);

        return [.. files];
    }

    private static string GetFolderId(string url)
    {
        var charIndex = url.LastIndexOf(@"\", StringComparison.Ordinal);
        if (charIndex == -1)
            throw new ArgumentException(nameof(url));

        if (!FolderLookup.Any())
            GetRootFolders(false, false, false);

        if (FolderLookup.TryGetValue(url.Substring(0, charIndex), out var folderId))
            return folderId;

        var sectionStartIndex = url.IndexOf(@"\", StringComparison.Ordinal) + 1;
        charIndex = url.IndexOf(@"\", sectionStartIndex, StringComparison.Ordinal);

        if (charIndex == -1)
            throw new ArgumentException(nameof(url));

        while (charIndex != -1)
        {
            if (FolderLookup.TryGetValue(url.Substring(0, charIndex), out var subFolderId))
            {
                folderId = subFolderId;
            }
            else if (folderId != null)
            {
                var folderName = url.Substring(sectionStartIndex, charIndex - sectionStartIndex);
                var request = new RestRequest("getMusicDirectory");
                request.AddParameter("id", folderId);
                var result = SendRequest(request);
                if (result == null)
                    return string.Empty;

                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", CaptionServerError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                if (result.Item is not SubsonicAPI.Directory content || content.child == null)
                    return null;

                foreach (var childEntry in content.child)
                {
                    if (!childEntry.isDir || childEntry.title != folderName)
                        continue;

                    folderId = childEntry.id;
                    if (!FolderLookup.ContainsKey(url.Substring(0, charIndex)))
                        FolderLookup.Add(url.Substring(0, charIndex), folderId);
                    break;
                }
            }

            sectionStartIndex = charIndex + 1;
            charIndex = url.IndexOf(@"\", sectionStartIndex, StringComparison.Ordinal);
        }

        return folderId;
    }

    private static string GetTranslatedUrl(string url)
    {
        return url?.Replace(@"\", "/");
    }

    private static string GetFileId(string url)
    {
        var folderId = GetFolderId(url);
        if (string.IsNullOrWhiteSpace(folderId))
            return null;

        // Workaround for MusicBee calling this on root folder(s)
        var rootFolders = GetRootFolders(true, true, false);
        if (rootFolders.Any(x => x.Key.Equals(folderId)))
            return null;

        var request = new RestRequest("getMusicDirectory");
        request.AddParameter("id", folderId);
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

        var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));
        foreach (var childEntry in content.child)
        {
            // Support for servers that does not provide path (e.g. ownCloud Music)
            if (childEntry.path == null && filePath.EndsWith(childEntry.id))
            {
                return childEntry.id;
            }

            if (childEntry.path == filePath)
            {
                return childEntry.id;
            }
        }

        return null;
    }

    private static string GetResolvedUrl(string url)
    {
        if (!FolderLookup.Any())
            return string.Empty;

        if (_collectionNames.Length == 1)
            return _collectionNames[0] + url;

        var path = url.Substring(0, url.LastIndexOf(@"\", StringComparison.Ordinal));
        var matchingCollections = _collectionNames
            .Where(item => GetFolderId(item + path) != null)
            .ToList();

        if (matchingCollections.Count == 1)
            return matchingCollections[0] + url;

        return matchingCollections
            .Select(item => item + url)
            .FirstOrDefault(potentialMatch => GetFileId(potentialMatch) != null) ?? url;
    }

    private static string GetCoverArtId(string url)
    {
        var folderId = GetFolderId(url);
        if (string.IsNullOrWhiteSpace(folderId))
            return null;

        // Workaround for MusicBee calling this on root folder(s)
        if (GetRootFolders(true, true, false)
            .Any(x => x.Key.Equals(folderId)))
            return null;

        var request = new RestRequest("getMusicDirectory");
        request.AddParameter("id", folderId);
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

        var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));
        return content.child.FirstOrDefault(child => child.path == filePath)?.coverArt;
    }

    public static bool FileExists(string url)
    {
        return !string.IsNullOrEmpty(url) && GetFileId(url) != null;
    }

    public static KeyValuePair<byte, string>[] GetFile(string url)
    {
        var folderId = GetFolderId(url);
        if (string.IsNullOrWhiteSpace(folderId))
            return null;

        // Workaround for MusicBee calling this on root folder(s)
        if (GetRootFolders(true, true, false).Any(x => x.Key.Equals(folderId)))
            return null;

        var baseFolderName = url.Substring(0, url.IndexOf(@"\", StringComparison.Ordinal));

        var request = new RestRequest("getMusicDirectory");
        request.AddParameter("id", folderId);
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

        var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));
        var matchingChild = content.child.FirstOrDefault(child => child.path == filePath);

        return matchingChild != null ? GetTags(matchingChild, baseFolderName) : null;
    }

    private static KeyValuePair<byte, string>[] GetTags(Child child, string baseFolderName)
    {
        if (child.isVideo)
            return null;

        var path = child.path?.Replace("/", @"\") ?? string.Empty;
        path = baseFolderName == null ? GetResolvedUrl(path) : $"{baseFolderName}\\{path}";

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
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Artwork, string.IsNullOrEmpty(child.coverArt) ? "" : "Y"),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.DiscNo, child.discNumber.ToString()),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.RatingLove, child.starred != default ? "L" : ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Custom16, child.id ?? ""),
            new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Rating, child.userRating.ToString())
        ];
    }

    public static byte[] GetFileArtwork(string url)
    {
        _lastEx = null;
        var coverArtId = GetCoverArtId(url);
        if (coverArtId == null)
            return null;

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
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(starred))
            throw new ArgumentException("Id and starred parameters cannot be null or empty.");

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
        var fileId = GetFileId(url);
        if (fileId == null)
        {
            _lastEx = new FileNotFoundException();
            return null;
        }

        var salt = GenerateSalt();
        var token = Md5(_currentSettings.Password + salt);
        var transcodeAndBitRate = GetTranscodeAndBitRate();
        var uriLine = _currentSettings.Auth == SubsonicSettings.AuthMethod.HexPass
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
        /* If the Transcode is set, then there must be a bitrate that you would want to set.
         * ... and if the maxbitrate is already set at the server side, this would not
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

        if (_currentSettings.Auth == SubsonicSettings.AuthMethod.HexPass)
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
            MessageBox.Show($@"Error retrieving response from Subsonic server:

{response.ErrorException}",
                @"Subsonic Plugin Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        return response.Data;
    }

    private static byte[] DownloadData(RestRequest request)
    {
        var client = new RestClient($"{_serverName}rest/");
        request.AddParameter("u", _currentSettings.Username);

        if (_currentSettings.Auth == SubsonicSettings.AuthMethod.HexPass)
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
}