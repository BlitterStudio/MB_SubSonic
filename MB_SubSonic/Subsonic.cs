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
using Directory = MusicBeePlugin.SubsonicAPI.Directory;
using ResponseStatus = MusicBeePlugin.SubsonicAPI.ResponseStatus;

namespace MusicBeePlugin
{
    public static class Subsonic
    {
        private const int TagCount = 13;
        private const string ApiVersion = "1.13.0";
        private static SubsonicSettings _currentSettings;
        private static SubsonicSettings.ServerType _serverType = SubsonicSettings.ServerType.Subsonic;
        public static bool IsInitialized;
        public static string SettingsFilename;
        public static string CacheFilename;
        public static Interfaces.Plugin.MB_SendNotificationDelegate SendNotificationsHandler;
        public static Interfaces.Plugin.MB_CreateBackgroundTaskDelegate CreateBackgroundTask;
        public static Interfaces.Plugin.MB_SetBackgroundTaskMessageDelegate SetBackgroundTaskMessage;
        public static Interfaces.Plugin.MB_RefreshPanelsDelegate RefreshPanels;

        public static Interfaces.Plugin.Library_GetFileTagDelegate GetFileTag;

        //public static Interfaces.Plugin.Library_GetFileTagsDelegate GetFileTags;
        public static Interfaces.Plugin.Playlist_QueryFilesExDelegate QueryPlaylistFilesEx;
        private static string _serverName;
        private static Exception _lastEx;
        private static readonly object CacheFileLock = new object();
        private static readonly object CacheLock = new object();
        private static KeyValuePair<byte, string>[][] _cachedFiles;
        private static string[] _collectionNames;
        private static readonly Dictionary<string, ulong> LastModified = new Dictionary<string, ulong>();
        private static readonly object FolderLookupLock = new object();
        private static readonly Dictionary<string, string> FolderLookup = new Dictionary<string, string>();
        private static bool _cacheUpdating;

        public static bool Initialize()
        {
            _lastEx = null;

            _currentSettings = FileHelper.ReadSettingsFromFile(SettingsFilename);
            IsInitialized = PingServer(_currentSettings);

            if (_lastEx != null)
                IsInitialized = false;

            return IsInitialized;
        }

        public static SubsonicSettings GetCurrentSettings()
        {
            return _currentSettings ?? SettingsHelper.SetDefaultSettings();
        }

        public static bool PingServer(SubsonicSettings settings)
        {
            settings = SettingsHelper.SanitizeSettings(settings);

            SetBackgroundTaskMessage("Attempting to Ping the server...");
            _serverName = BuildServerUri(settings);

            try
            {
                var request = new RestRequest
                {
                    Resource = "ping.view"
                };
                var response = SendRequest(request);
                _serverType = GetServerTypeFromResponse(response);
                var isPingOk = IsPingOk(response);
                return isPingOk;
            }
            catch (Exception ex)
            {
                _lastEx = ex;
                return false;
            }
        }

        private static SubsonicSettings.ServerType GetServerTypeFromResponse(string response)
        {
            // Default server type is Subsonic, but check if we're connected to a LibreSonic server
            return response.Contains("libresonic")
                ? SubsonicSettings.ServerType.LibreSonic
                : SubsonicSettings.ServerType.Subsonic;
        }

        private static bool IsPingOk(string response)
        {
            switch (_serverType)
            {
                case SubsonicSettings.ServerType.Subsonic:
                {
                    SetBackgroundTaskMessage("Detected a Subsonic server");
                    var result = Response.Deserialize(response);
                    return result.status == ResponseStatus.ok;
                }
                case SubsonicSettings.ServerType.LibreSonic:
                {
                    SetBackgroundTaskMessage("Detected a LibreSonic server");
                    var result = LibreSonicAPI.Response.Deserialize(response);
                    return result.status == LibreSonicAPI.ResponseStatus.ok;
                }
                default:
                    return false;
            }
        }

        private static string BuildServerUri(SubsonicSettings settings)
        {
            return $"{settings.Protocol.ToFriendlyString()}://{settings.Host}:{settings.Port}{settings.BasePath}";
        }

        public static void Close()
        {
        }

        public static bool SaveSettings(SubsonicSettings settings)
        {
            settings = SettingsHelper.SanitizeSettings(settings);
            var savedResult = FileHelper.SaveSettingsToFile(settings, SettingsFilename);
            if (!savedResult)
                return false;

            _currentSettings = settings;
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

        public static bool FolderExists(string path)
        {
            var exists = string.IsNullOrEmpty(path) ||
                         path.Equals(@"\") ||
                         GetFolderId(path) != null;
            return exists;
        }

        public static string[] GetFolders(string path)
        {
            SetBackgroundTaskMessage("Running GetFolders...");
            _lastEx = null;
            string[] folders;
            if (!IsInitialized)
            {
                folders = new string[] { };
            }
            else if (string.IsNullOrEmpty(path))
            {
                var rootFolders = GetRootFolders(true, true, false);
                if (rootFolders != null)
                    folders = rootFolders.Select(folder => folder.Value).ToArray();
                else
                    return new string[] { };
            }
            else if (path.IndexOf(@"\", StringComparison.Ordinal)
                .Equals(path.LastIndexOf(@"\", StringComparison.Ordinal)))
            {
                var list = new List<string>();
                var folderId = GetFolderId(path);
                if (folderId != null)
                {
                    var alwaysFalse = false;
                    list.AddRange(
                        GetRootFolders(folderId, path.Substring(0, path.Length - 1), false, false, ref alwaysFalse)
                            .Select(folder => folder.Key));
                }

                folders = list.ToArray();
            }
            else
            {
                if (!path.EndsWith(@"\"))
                    path += @"\";
                var folderId = GetFolderId(path);
                if (string.IsNullOrEmpty(folderId))
                {
                    folders = new string[] { };
                }
                else
                {
                    var request = new RestRequest
                    {
                        Resource = "getMusicDirectory.view"
                    };
                    request.AddParameter("id", folderId);
                    var response = SendRequest(request);

                    var list = new List<string>();
                    if (_serverType == SubsonicSettings.ServerType.Subsonic)
                    {
                        var result = Response.Deserialize(response);
                        if (result.Item is Error error)
                        {
                            MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return null;
                        }

                        var content = result.Item as Directory;
                        if (content?.child != null)
                        {
                            var total = content.child.Count;
                            for (var index = 0; index < content.child.Count; index++)
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
                        }
                    }
                    else
                    {
                        var result = LibreSonicAPI.Response.Deserialize(response);
                        if (result.Item is LibreSonicAPI.Error error)
                        {
                            MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return null;
                        }

                        var content = result.Item as LibreSonicAPI.Directory;
                        if (content?.child != null)
                        {
                            var total = content.child.Count;
                            for (var index = 0; index < content.child.Count; index++)
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
                        }
                    }

                    folders = list.ToArray();
                }
            }

            SetBackgroundTaskMessage("Done processing GetFolders");
            return folders;
        }

        public static KeyValuePair<byte, string>[][] GetFiles(string path)
        {
            SetBackgroundTaskMessage("Running GetFiles...");
            var threadStarted = false;
            _lastEx = null;
            KeyValuePair<byte, string>[][] files;
            if (!IsInitialized)
            {
                files = new KeyValuePair<byte, string>[][] { };
            }
            else
            {
                var cacheLoaded = _cachedFiles != null;
                if (!cacheLoaded && !File.Exists(CacheFilename))
                    files = null;
                else
                    files = GetCachedFiles();

                if (!_cacheUpdating && (string.IsNullOrEmpty(path) || !cacheLoaded))
                {
                    threadStarted = true;
                    CreateBackgroundTask(ExecuteGetFolderFiles, Form.ActiveForm);
                }

                if (!string.IsNullOrEmpty(path))
                {
                    if (!cacheLoaded || _cacheUpdating || files == null)
                        return GetFolderFiles(path);
                    files = GetPathFilteredFiles(files, path);
                }
            }

            if (threadStarted) return files;
            try
            {
                SendNotificationsHandler.Invoke(Interfaces.Plugin.CallbackType.FilesRetrievedNoChange);
            }
            catch (Exception ex)
            {
                _lastEx = ex;
            }

            SetBackgroundTaskMessage("Done running GetFiles");
            return files;
        }

        private static KeyValuePair<byte, string>[][] GetCachedFiles()
        {
            SetBackgroundTaskMessage("Running GetCachedFiles...");
            if (_cachedFiles != null) return _cachedFiles;
            KeyValuePair<byte, string>[][] files = null;
            lock (CacheFileLock)
            {
                using (
                    var stream = new FileStream(CacheFilename, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                        FileOptions.SequentialScan))
                using (var reader = new BinaryReader(stream))
                {
                    try
                    {
                        var version = reader.ReadInt32();
                        var count = reader.ReadInt32();
                        files = new KeyValuePair<byte, string>[count][];
                        for (var index = 0; index < count; index++)
                        {
                            var tags = new KeyValuePair<byte, string>[TagCount + 1];
                            for (var tagIndex = 0; tagIndex <= TagCount; tagIndex++)
                            {
                                var tagType = reader.ReadByte();
                                tags[tagIndex] = new KeyValuePair<byte, string>(tagType, reader.ReadString());
                            }

                            files[index] = tags;
                        }

                        if (version.Equals(2))
                        {
                            count = reader.ReadInt32();
                            for (var index = 1; index <= count; index++)
                            {
                                var collectionName = reader.ReadString();
                                if (!LastModified.ContainsKey(collectionName))
                                    LastModified.Add(collectionName, reader.ReadUInt64());
                            }
                        }

                        reader.Close();
                    }
                    catch (EndOfStreamException)
                    {
                        MessageBox.Show(@"The Cache file seems to be empty!");
                    }
                }
            }

            lock (CacheLock)
            {
                if (_cachedFiles == null && files != null)
                    _cachedFiles = files;
            }

            SetBackgroundTaskMessage("Done running GetCachedFiles");
            return _cachedFiles;
        }

        private static KeyValuePair<byte, string>[][] GetPathFilteredFiles(KeyValuePair<byte, string>[][] files,
            string path)
        {
            if (!path.EndsWith(@"\"))
                path += @"\";
            files = files.Where(t => t[0].Value.StartsWith(path)).ToArray();
            Array.Sort(files, new FileSorter());
            return files;
        }

        private static void ExecuteGetFolderFiles()
        {
            _cacheUpdating = true;
            SetBackgroundTaskMessage("Running GetFolderFiles...");
            try
            {
                var files = new KeyValuePair<byte, string>[][] { };
                var list = new List<KeyValuePair<byte, string>[]>();
                var folders = GetRootFolders(false, true, true);
                bool anyChanges;
                if (folders == null)
                {
                    anyChanges = false;
                }
                else
                {
                    foreach (var folder in folders)
                        GetFolderFiles(folder.Value, folder.Key, list);
                    files = list.ToArray();
                    KeyValuePair<byte, string>[][] oldCachedFiles;
                    lock (CacheLock)
                    {
                        oldCachedFiles = _cachedFiles;
                        _cachedFiles = files;
                    }

                    anyChanges = oldCachedFiles == null || _cachedFiles.Length != oldCachedFiles.Length;
                    if (!anyChanges)
                        for (var index = 0; index < _cachedFiles.Length; index++)
                        {
                            var tags1 = _cachedFiles[index];
                            var tags2 = oldCachedFiles[index];
                            for (var tagIndex = 0; tagIndex < TagCount; tagIndex++)
                            {
                                if (string.Compare(tags1[tagIndex].Value, tags2[tagIndex].Value,
                                        StringComparison.Ordinal) == 0) continue;
                                anyChanges = true;
                                break;
                            }
                        }
                }

                if (!anyChanges)
                {
                    try
                    {
                        SendNotificationsHandler.Invoke(Interfaces.Plugin.CallbackType.FilesRetrievedNoChange);
                    }
                    catch (Exception ex)
                    {
                        _lastEx = ex;
                    }
                }
                else
                {
                    try
                    {
                        SendNotificationsHandler.Invoke(Interfaces.Plugin.CallbackType.FilesRetrievedChanged);
                    }
                    catch (Exception ex)
                    {
                        _lastEx = ex;
                    }

                    try
                    {
                        lock (CacheFileLock)
                        {
                            using (
                                var stream = new FileStream(CacheFilename, FileMode.Create, FileAccess.Write,
                                    FileShare.None))
                            using (var writer = new BinaryWriter(stream))
                            {
                                writer.Write(2); // version
                                writer.Write(files.Length);
                                foreach (var tags in files)
                                    for (var tagIndex = 0; tagIndex <= TagCount; tagIndex++)
                                    {
                                        var tag = tags[tagIndex];
                                        writer.Write(tag.Key);
                                        writer.Write(tag.Value);
                                    }

                                writer.Write(LastModified.Count);
                                foreach (var item in LastModified)
                                {
                                    writer.Write(item.Key);
                                    writer.Write(item.Value);
                                }

                                writer.Close();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _lastEx = ex;
                    }
                }
            }
            catch (Exception ex)
            {
                _lastEx = ex;
                try
                {
                    SendNotificationsHandler.Invoke(Interfaces.Plugin.CallbackType.FilesRetrievedFail);
                }
                catch
                {
                    _lastEx = ex;
                }
            }
            finally
            {
                _cacheUpdating = false;
            }

            SetBackgroundTaskMessage("Done running GetFolderFiles");
            RefreshPanels();
        }

        private static List<KeyValuePair<string, string>> GetRootFolders(bool collectionOnly, bool refresh,
            bool dirtyOnly)
        {
            SetBackgroundTaskMessage("Running GetMusicFolders");
            var folders = new List<KeyValuePair<string, string>>();
            lock (FolderLookupLock)
            {
                if (!refresh && !FolderLookup.Count.Equals(0)) return folders;
                folders = new List<KeyValuePair<string, string>>();
                var collection = new List<KeyValuePair<string, string>>();

                var request = new RestRequest
                {
                    Resource = "getMusicFolders.view"
                };
                var response = SendRequest(request);
                if (_serverType == SubsonicSettings.ServerType.Subsonic)
                {
                    var result = Response.Deserialize(response);
                    if (result.Item is Error error)
                    {
                        MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return null;
                    }

                    var content = (MusicFolders) result.Item;

                    if (content.musicFolder != null)
                    {
                        var total = content.musicFolder.Count;
                        for (var index = 0; index < content.musicFolder.Count; index++)
                        {
                            SetBackgroundTaskMessage($"Processing {index} of {total} music folders");
                            var folder = content.musicFolder[index];
                            var folderId = folder.id.ToString();
                            var folderName = folder.name;

                            if (folderName != null && FolderLookup.ContainsKey(folderName))
                                FolderLookup[folderName] = folderId;
                            else if (folderName != null) FolderLookup.Add(folderName, folderId);

                            collection.Add(new KeyValuePair<string, string>(folderId, folderName));
                        }
                    }
                }
                else
                {
                    var result = LibreSonicAPI.Response.Deserialize(response);
                    if (result.Item is LibreSonicAPI.Error error)
                    {
                        MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return null;
                    }

                    var content = (LibreSonicAPI.MusicFolders) result.Item;

                    if (content.musicFolder != null)
                    {
                        var total = content.musicFolder.Count;
                        for (var index = 0; index < content.musicFolder.Count; index++)
                        {
                            SetBackgroundTaskMessage($"Processing {index} of {total} music folders");
                            var folder = content.musicFolder[index];
                            var folderId = folder.id.ToString();
                            var folderName = folder.name;

                            if (folderName != null && FolderLookup.ContainsKey(folderName))
                                FolderLookup[folderName] = folderId;
                            else if (folderName != null) FolderLookup.Add(folderName, folderId);

                            collection.Add(new KeyValuePair<string, string>(folderId, folderName));
                        }
                    }
                }

                _collectionNames = new string[collection.Count];
                for (var index = 0; index < collection.Count; index++)
                    _collectionNames[index] = collection[index].Value + @"\";

                var isDirty = false;

                foreach (var collectionItem in collection)
                    folders.AddRange(GetRootFolders(collectionItem.Key, collectionItem.Value, true,
                        refresh && dirtyOnly,
                        ref isDirty));

                if (collectionOnly)
                    return collection;

                if (dirtyOnly && !isDirty)
                    return null;
            }

            SetBackgroundTaskMessage("Done running GetMusicFolders");
            return folders;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetRootFolders(string collectionId,
            string collectionName,
            bool indices, bool updateIsDirty, ref bool isDirty)
        {
            SetBackgroundTaskMessage("Running GetIndexes...");
            var folders = new List<KeyValuePair<string, string>>();

            var request = new RestRequest
            {
                Resource = "getIndexes.view"
            };
            request.AddParameter("musicFolderId", collectionId);
            var response = SendRequest(request);

            if (_serverType == SubsonicSettings.ServerType.Subsonic)
            {
                var result = Response.Deserialize(response);
                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as Indexes;
                if (updateIsDirty && content?.lastModified != null)
                {
                    var serverLastModified = (ulong) content.lastModified;
                    lock (CacheFileLock)
                    {
                        if (!LastModified.TryGetValue(collectionName, out var clientLastModified))
                        {
                            isDirty = true;
                            LastModified.Add(collectionName, serverLastModified);
                        }
                        else if (serverLastModified > clientLastModified)
                        {
                            isDirty = true;
                            LastModified[collectionName] = serverLastModified;
                        }
                    }
                }

                if (content?.index != null)
                    foreach (var indexChild in content.index)
                    foreach (var artistChild in indexChild.artist)
                    {
                        var folderId = artistChild.id;
                        var folderName = $"{collectionName}\\{artistChild.name}";
                        if (FolderLookup.ContainsKey(folderName))
                            FolderLookup[folderName] = folderId;
                        else
                            FolderLookup.Add(folderName, folderId);
                        folders.Add(new KeyValuePair<string, string>(indices ? folderId : folderName, collectionName));
                    }
            }
            else
            {
                var result = LibreSonicAPI.Response.Deserialize(response);
                if (result.Item is LibreSonicAPI.Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as LibreSonicAPI.Indexes;
                if (updateIsDirty && content?.lastModified != null)
                {
                    var serverLastModified = (ulong) content.lastModified;
                    lock (CacheFileLock)
                    {
                        if (!LastModified.TryGetValue(collectionName, out var clientLastModified))
                        {
                            isDirty = true;
                            LastModified.Add(collectionName, serverLastModified);
                        }
                        else if (serverLastModified > clientLastModified)
                        {
                            isDirty = true;
                            LastModified[collectionName] = serverLastModified;
                        }
                    }
                }

                if (content?.index != null)
                    foreach (var indexChild in content.index)
                    foreach (var artistChild in indexChild.artist)
                    {
                        var folderId = artistChild.id;
                        var folderName = $"{collectionName}\\{artistChild.name}";
                        if (FolderLookup.ContainsKey(folderName))
                            FolderLookup[folderName] = folderId;
                        else
                            FolderLookup.Add(folderName, folderId);
                        folders.Add(new KeyValuePair<string, string>(indices ? folderId : folderName, collectionName));
                    }
            }

            SetBackgroundTaskMessage("Done Running GetIndexes");
            return folders;
        }

        private static void GetFolderFiles(string baseFolderName, string folderId,
            ICollection<KeyValuePair<byte, string>[]> files)
        {
            //SetBackgroundTaskMessage("Running GetMusicDirectory...");
            var request = new RestRequest
            {
                Resource = "getMusicDirectory.view"
            };
            request.AddParameter("id", folderId);
            var response = SendRequest(request);

            if (_serverType == SubsonicSettings.ServerType.Subsonic)
            {
                var result = Response.Deserialize(response.Replace("\0", string.Empty));
                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var content = result.Item as Directory;

                if (content?.child != null)
                {
                    var total = content.child.Count;
                    for (var index = 0; index < total; index++)
                    {
                        SetBackgroundTaskMessage($"Processing MusicDirectory {index} of {total}...");
                        var childEntry = content.child[index];
                        if (childEntry.isDir && _currentSettings.PreCacheAll)
                        {
                            GetFolderFiles(baseFolderName, childEntry.id, files);
                        }
                        else
                        {
                            var tags = GetTags(childEntry, baseFolderName);
                            if (tags != null)
                                files.Add(tags);
                        }
                    }
                }
            }
            else
            {
                var result = LibreSonicAPI.Response.Deserialize(response.Replace("\0", string.Empty));
                if (result.Item is LibreSonicAPI.Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var content = result.Item as LibreSonicAPI.Directory;

                if (content?.child != null)
                {
                    var total = content.child.Count;
                    for (var index = 0; index < total; index++)
                    {
                        SetBackgroundTaskMessage($"Processing MusicDirectory {index} of {total}...");
                        var childEntry = content.child[index];
                        if (childEntry.isDir && _currentSettings.PreCacheAll)
                        {
                            GetFolderFiles(baseFolderName, childEntry.id, files);
                        }
                        else
                        {
                            var tags = GetTags(childEntry, baseFolderName);
                            if (tags != null)
                                files.Add(tags);
                        }
                    }
                }
            }

            SetBackgroundTaskMessage("Done Running GetMusicDirectory");
        }

        private static KeyValuePair<byte, string>[][] GetFolderFiles(string path)
        {
            if (!path.EndsWith(@"\"))
                path += @"\";
            var folderId = GetFolderId(path);
            var files = new List<KeyValuePair<byte, string>[]>();

            if (folderId == null)
                return new KeyValuePair<byte, string>[][] { };

            GetFolderFiles(path.Substring(0, path.IndexOf(@"\", StringComparison.Ordinal)), folderId, files);
            return files.ToArray();
        }

        private static string GetFolderId(string url)
        {
            var charIndex = url.LastIndexOf(@"\", StringComparison.Ordinal);
            if (charIndex.Equals(-1))
                throw new ArgumentException();

            if (FolderLookup.Count.Equals(0))
                GetRootFolders(false, false, false);

            if (FolderLookup.TryGetValue(url.Substring(0, charIndex), out var folderId)) return folderId;

            var sectionStartIndex = url.IndexOf(@"\", StringComparison.Ordinal) + 1;
            charIndex = url.IndexOf(@"\", sectionStartIndex, StringComparison.Ordinal);

            if (charIndex.Equals(-1))
                throw new ArgumentException();

            while (charIndex != -1)
            {
                if (FolderLookup.TryGetValue(url.Substring(0, charIndex), out var subFolderId))
                {
                    folderId = subFolderId;
                }
                else if (folderId != null)
                {
                    var folderName = url.Substring(sectionStartIndex, charIndex - sectionStartIndex);
                    var request = new RestRequest
                    {
                        Resource = "getMusicDirectory.view"
                    };
                    request.AddParameter("id", folderId);
                    var response = SendRequest(request);

                    if (_serverType == SubsonicSettings.ServerType.Subsonic)
                    {
                        var result = Response.Deserialize(response.Replace("\0", string.Empty));
                        if (result.Item is Error error)
                        {
                            MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return null;
                        }

                        var content = result.Item as Directory;

                        if (content?.child != null)
                            foreach (var childEntry in content.child)
                                if (childEntry.isDir && childEntry.title == folderName)
                                {
                                    folderId = childEntry.id;
                                    if (!FolderLookup.ContainsKey(url.Substring(0, charIndex)))
                                        FolderLookup.Add(url.Substring(0, charIndex), folderId);
                                    break;
                                }
                    }
                    else
                    {
                        var result = LibreSonicAPI.Response.Deserialize(response.Replace("\0", string.Empty));
                        if (result.Item is LibreSonicAPI.Error error)
                        {
                            MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return null;
                        }

                        var content = result.Item as LibreSonicAPI.Directory;

                        if (content?.child != null)
                            foreach (var childEntry in content.child)
                                if (childEntry.isDir && childEntry.title == folderName)
                                {
                                    folderId = childEntry.id;
                                    if (!FolderLookup.ContainsKey(url.Substring(0, charIndex)))
                                        FolderLookup.Add(url.Substring(0, charIndex), folderId);
                                    break;
                                }
                    }
                }

                sectionStartIndex = charIndex + 1;
                charIndex = url.IndexOf(@"\", sectionStartIndex, StringComparison.Ordinal);
            }

            return folderId;
        }

        private static string GetTranslatedUrl(string url)
        {
            return url.Replace(@"\", @"/");
        }

        private static string GetFileId(string url)
        {
            var folderId = GetFolderId(url);
            if (folderId == null) return null;

            var request = new RestRequest
            {
                Resource = "getMusicDirectory.view"
            };
            request.AddParameter("id", folderId);
            var response = SendRequest(request);

            if (_serverType == SubsonicSettings.ServerType.Subsonic)
            {
                var result = Response.Deserialize(response.Replace("\0", string.Empty));
                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as Directory;
                var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

                if (content?.child == null) return null;

                foreach (var childEntry in content.child)
                    if (childEntry.path == filePath)
                        return childEntry.id;
            }
            else
            {
                var result = LibreSonicAPI.Response.Deserialize(response.Replace("\0", string.Empty));
                if (result.Item is LibreSonicAPI.Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as LibreSonicAPI.Directory;
                var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

                if (content?.child == null) return null;

                foreach (var childEntry in content.child)
                    if (childEntry.path == filePath)
                        return childEntry.id;
            }

            return null;
        }

        private static string GetResolvedUrl(string url)
        {
            if (FolderLookup.Count.Equals(0))
                GetRootFolders(false, false, false);
            if (_collectionNames.Length.Equals(1))
                return _collectionNames[0] + url;
            var path = url.Substring(0, url.LastIndexOf(@"\", StringComparison.Ordinal));
            string lastMatch = null;
            var count = 0;
            foreach (var item in _collectionNames.Where(item => GetFolderId(item + path) != null))
            {
                count += 1;
                lastMatch = item + url;
            }

            if (count.Equals(1))
                return lastMatch;
            foreach (var item in _collectionNames.Where(item => GetFolderId(item + path) != null))
            {
                lastMatch = item + url;
                if (GetFileId(lastMatch) != null)
                    return lastMatch;
            }

            return url;
        }

        private static string GetCoverArtId(string url)
        {
            var folderId = GetFolderId(url);
            if (folderId == null) return null;

            var request = new RestRequest
            {
                Resource = "getMusicDirectory.view"
            };
            request.AddParameter("id", folderId);
            var response = SendRequest(request);

            if (_serverType == SubsonicSettings.ServerType.Subsonic)
            {
                var result = Response.Deserialize(response);
                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as Directory;
                var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

                if (content?.child == null) return null;

                foreach (var childEntry in content.child)
                    if (childEntry.path == filePath)
                        return childEntry.coverArt;
            }
            else
            {
                var result = LibreSonicAPI.Response.Deserialize(response);
                if (result.Item is LibreSonicAPI.Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as LibreSonicAPI.Directory;
                var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

                if (content?.child == null) return null;

                foreach (var childEntry in content.child)
                    if (childEntry.path == filePath)
                        return childEntry.coverArt;
            }

            return null;
        }

        public static bool FileExists(string url)
        {
            return GetFileId(url) != null;
        }

        public static KeyValuePair<byte, string>[] GetFile(string url)
        {
            var folderId = GetFolderId(url);
            if (folderId == null) return null;
            var baseFolderName = url.Substring(0, url.IndexOf(@"\", StringComparison.Ordinal));

            var request = new RestRequest
            {
                Resource = "getMusicDirectory.view"
            };
            request.AddParameter("id", folderId);
            var response = SendRequest(request);

            if (_serverType == SubsonicSettings.ServerType.Subsonic)
            {
                var result = Response.Deserialize(response.Replace("\0", string.Empty));
                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as Directory;
                var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

                if (content?.child == null) return null;

                foreach (var childEntry in content.child)
                    if (childEntry.path == filePath)
                        return GetTags(childEntry, baseFolderName);
            }
            else
            {
                var result = LibreSonicAPI.Response.Deserialize(response.Replace("\0", string.Empty));
                if (result.Item is LibreSonicAPI.Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as LibreSonicAPI.Directory;
                var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

                if (content?.child == null) return null;

                foreach (var childEntry in content.child)
                    if (childEntry.path == filePath)
                        return GetTags(childEntry, baseFolderName);
            }

            return null;
        }

        private static KeyValuePair<byte, string>[] GetTags(Child child, string baseFolderName)
        {
            if (child.isVideo)
                return null;

            var tags = new KeyValuePair<byte, string>[TagCount + 1];
            var path = string.Empty;
            var attribute = child.path;
            if (attribute != null)
                path = attribute.Replace(@"/", @"\");
            path = baseFolderName == null ? GetResolvedUrl(path) : $"{baseFolderName}\\{path}";
            tags[0] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Url, path);
            tags[1] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Artist, child.artist ?? "");
            tags[2] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.TrackTitle,
                child.title ?? "");
            tags[3] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Album, child.album ?? "");
            tags[4] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Year, child.year.ToString());
            tags[5] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.TrackNo,
                child.track.ToString());
            tags[6] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Genre, child.genre ?? "");
            tags[7] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Duration,
                (child.duration * 1000).ToString());
            tags[8] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Bitrate,
                child.bitRate.ToString());
            tags[9] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Size,
                child.size.ToString());
            tags[10] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Artwork,
                string.IsNullOrEmpty(child.coverArt) ? "" : "Y");
            tags[11] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.DiscNo,
                child.discNumber.ToString());
            tags[12] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.RatingLove,
                child.starred != default ? "L" : "");
            tags[13] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Custom16, child.id ?? "");

            return tags;
        }

        private static KeyValuePair<byte, string>[] GetTags(LibreSonicAPI.Child child, string baseFolderName)
        {
            if (child.isVideo)
                return null;

            var tags = new KeyValuePair<byte, string>[TagCount + 1];
            var path = string.Empty;
            var attribute = child.path;
            if (attribute != null)
                path = attribute.Replace(@"/", @"\");
            path = baseFolderName == null ? GetResolvedUrl(path) : $"{baseFolderName}\\{path}";
            tags[0] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Url, path);
            tags[1] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Artist, child.artist ?? "");
            tags[2] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.TrackTitle,
                child.title ?? "");
            tags[3] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Album, child.album ?? "");
            tags[4] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Year, child.year.ToString());
            tags[5] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.TrackNo,
                child.track.ToString());
            tags[6] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Genre, child.genre ?? "");
            tags[7] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Duration,
                (child.duration * 1000).ToString());
            tags[8] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Bitrate,
                child.bitRate.ToString());
            tags[9] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.FilePropertyType.Size,
                child.size.ToString());
            tags[10] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Artwork,
                string.IsNullOrEmpty(child.coverArt) ? "" : "Y");
            tags[11] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.DiscNo,
                child.discNumber.ToString());
            tags[12] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.RatingLove,
                child.starred != default ? "L" : "");
            tags[13] = new KeyValuePair<byte, string>((byte) Interfaces.Plugin.MetaDataType.Custom16, child.id ?? "");

            return tags;
        }

        public static byte[] GetFileArtwork(string url)
        {
            _lastEx = null;
            byte[] bytes = null;
            try
            {
                var id = GetCoverArtId(url);
                if (id != null)
                {
                    var request = new RestRequest
                    {
                        Resource = "getCoverArt.view"
                    };
                    request.AddParameter("id", id);
                    bytes = DownloadData(request);
                }
            }
            catch (Exception ex)
            {
                _lastEx = ex;
            }

            return bytes;
        }

        public static KeyValuePair<string, string>[] GetPlaylists()
        {
            _lastEx = null;
            var playlists = new List<KeyValuePair<string, string>>();

            var request = new RestRequest
            {
                Resource = "getPlaylists.view"
            };
            var response = SendRequest(request);

            if (_serverType == SubsonicSettings.ServerType.Subsonic)
            {
                var result = Response.Deserialize(response);
                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as Playlists;
                if (content?.playlist == null) return playlists.ToArray();

                foreach (var playlistEntry in content.playlist)
                    playlists.Add(new KeyValuePair<string, string>(playlistEntry.id, playlistEntry.name));
            }
            else
            {
                var result = LibreSonicAPI.Response.Deserialize(response);
                if (result.Item is LibreSonicAPI.Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as LibreSonicAPI.Playlists;
                if (content?.playlist == null) return playlists.ToArray();

                foreach (var playlistEntry in content.playlist)
                    playlists.Add(new KeyValuePair<string, string>(playlistEntry.id, playlistEntry.name));
            }

            return playlists.ToArray();
        }

        public static KeyValuePair<byte, string>[][] GetPlaylistFiles(string id)
        {
            _lastEx = null;
            var files = new List<KeyValuePair<byte, string>[]>();
            var request = new RestRequest
            {
                Resource = "getPlaylist.view"
            };
            request.AddParameter("id", id);
            var response = SendRequest(request);

            if (_serverType == SubsonicSettings.ServerType.Subsonic)
            {
                var result = Response.Deserialize(response.Replace("\0", string.Empty));
                if (result.Item is Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as PlaylistWithSongs;
                if (content?.entry == null) return files.ToArray();

                foreach (var playlistEntry in content.entry)
                {
                    var tags = GetTags(playlistEntry, null);
                    if (tags != null)
                        files.Add(tags);
                }
            }
            else
            {
                var result = LibreSonicAPI.Response.Deserialize(response.Replace("\0", string.Empty));
                if (result.Item is LibreSonicAPI.Error error)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var content = result.Item as LibreSonicAPI.PlaylistWithSongs;
                if (content?.entry == null) return files.ToArray();

                foreach (var playlistEntry in content.entry)
                {
                    var tags = GetTags(playlistEntry, null);
                    if (tags != null)
                        files.Add(tags);
                }
            }

            return files.ToArray();
        }

        public static void UpdateRating(string id, string rating)
        {
            if (string.IsNullOrEmpty(rating)) return;
            int.TryParse(rating, out var result);

            var request = new RestRequest
            {
                Resource = "setRating"
            };
            request.AddParameter("id", id);
            request.AddParameter("rating", result);
            SendRequest(request);
        }

        public static void UpdateRatingLove(string id, string starred)
        {
            var request = new RestRequest
            {
                Resource = starred.Equals("L") ? "star" : "unstar"
            };

            request.AddParameter("id", id);
            SendRequest(request);
        }

        public static void CreatePlaylist(string name, List<int> songIds)
        {
            //TODO
            var request = new RestRequest
            {
                Resource = "createPlaylist"
            };
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
            var request = new RestRequest
            {
                Resource = "deletePlaylist"
            };
            request.AddParameter("id", playlistId);
            SendRequest(request);
        }

        public static Stream GetStream(string url)
        {
            _lastEx = null;
            var id = GetFileId(url);
            if (id == null)
            {
                _lastEx = new FileNotFoundException();
            }
            else
            {
                var salt = NewSalt();
                var token = Md5(_currentSettings.Password + salt);
                var transcodeAndBitRate = GetTranscodeAndBitRate();
                string uriLine;
                if (_currentSettings.Auth == SubsonicSettings.AuthMethod.HexPass)
                {
                    var ba = Encoding.Default.GetBytes(_currentSettings.Password);
                    var hexString = BitConverter.ToString(ba);
                    hexString = hexString.Replace("-", "");
                    var hexPass = $"enc:{hexString}";
                    uriLine =
                        $"{_serverName}rest/stream.view?u={_currentSettings.Username}&p={hexPass}&v={ApiVersion}&c=MusicBee&id={id}&{transcodeAndBitRate}";
                }
                else
                {
                    uriLine =
                        $"{_serverName}rest/stream.view?u={_currentSettings.Username}&t={token}&s={salt}&v={ApiVersion}&c=MusicBee&id={id}&{transcodeAndBitRate}";
                }

                var uri = new Uri(uriLine);
                var stream = new ConnectStream(uri);
                if (stream.ContentType.StartsWith("text/xml"))
                    using (stream)
                    {
                        _lastEx = new InvalidDataException();
                    }
                else
                    return stream;
            }

            return null;
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

        private static string SendRequest(RestRequest request)
        {
            var client = new RestClient {BaseUrl = new Uri(_serverName + "rest/")};
            request.AddParameter("u", _currentSettings.Username);
            if (_currentSettings.Auth == SubsonicSettings.AuthMethod.HexPass)
            {
                var ba = Encoding.Default.GetBytes(_currentSettings.Password);
                var hexString = BitConverter.ToString(ba);
                hexString = hexString.Replace("-", "");
                var hexPass = $"enc:{hexString}";
                request.AddParameter("p", hexPass);
            }
            else
            {
                var salt = NewSalt();
                var token = Md5(_currentSettings.Password + salt);
                request.AddParameter("t", token);
                request.AddParameter("s", salt);
            }

            request.AddParameter("v", ApiVersion);
            request.AddParameter("c", "MusicBee");

            var response = client.Execute(request);

            if (response.ErrorException == null) return response.Content;
            const string message = "Error retrieving response from Subsonic server:";
            MessageBox.Show($@"{message}

{response.ErrorException}", @"Subsonic Plugin Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            return response.Content;
        }

        private static byte[] DownloadData(RestRequest request)
        {
            var client = new RestClient {BaseUrl = new Uri(_serverName + "rest/")};
            request.AddParameter("u", _currentSettings.Username);

            if (_currentSettings.Auth == SubsonicSettings.AuthMethod.HexPass)
            {
                var ba = Encoding.Default.GetBytes(_currentSettings.Password);
                var hexString = BitConverter.ToString(ba);
                hexString = hexString.Replace("-", "");
                var hexPass = $"enc:{hexString}";
                request.AddParameter("p", hexPass);
            }
            else
            {
                var salt = NewSalt();
                var token = Md5(_currentSettings.Password + salt);
                request.AddParameter("t", token);
                request.AddParameter("s", salt);
            }

            request.AddParameter("v", ApiVersion);
            request.AddParameter("c", "MusicBee");
            var response = client.Execute(request);

            if (!response.ContentType.StartsWith("text/xml")) return response.RawBytes;

            if (_serverType == SubsonicSettings.ServerType.Subsonic)
            {
                var result = Response.Deserialize(response.Content.Replace("\0", string.Empty));
                if (!(result.Item is Error error)) return response.RawBytes;

                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            else
            {
                var result = LibreSonicAPI.Response.Deserialize(response.Content.Replace("\0", string.Empty));
                if (!(result.Item is LibreSonicAPI.Error error)) return response.RawBytes;

                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from LibreSonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private static string NewSalt()
        {
            // Define min and max salt sizes.
            const int minSaltSize = 6;
            const int maxSaltSize = 12;

            // Generate a random number for the size of the salt.
            var random = new Random();
            var saltSize = random.Next(minSaltSize, maxSaltSize);
            // Allocate a byte array, which will hold the salt.
            var saltBytes = new byte[saltSize];
            // Initialize a random number generator.
            var rng = new RNGCryptoServiceProvider();
            // Fill the salt with cryptographically strong byte values.
            rng.GetNonZeroBytes(saltBytes);
            return BitConverter.ToString(saltBytes).Replace("-", string.Empty).ToLower();
        }

        private static string Md5(string saltedPassword)
        {
            //Create a byte array from source data.
            var tmpSource = Encoding.ASCII.GetBytes(saltedPassword);
            var result = new MD5CryptoServiceProvider().ComputeHash(tmpSource);
            return BitConverter.ToString(result).Replace("-", string.Empty).ToLower();
        }

        public static bool IsSettingChanged(SubsonicSettings settings)
        {
            var result = SettingsHelper.IsSettingChanged(settings, _currentSettings);
            return result;
        }

        private sealed class FileSorter : Comparer<KeyValuePair<byte, string>[]>
        {
            public override int Compare(KeyValuePair<byte, string>[] x, KeyValuePair<byte, string>[] y)
            {
                if (x != null && y != null)
                    return string.Compare(x[0].Value, y[0].Value, StringComparison.OrdinalIgnoreCase);
                return 0;
            }
        }
    }
}