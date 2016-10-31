using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Subsonic.Domain;

namespace MusicBeePlugin
{
    public static class Subsonic
    {
        private const int TagCount = 10;
        private const string Passphrase = "PeekAndPoke";
        private static SubsonicService _service;
        public static string Host = "localhost";
        public static string Port = "80";
        public static string BasePath = "/";
        public static string Username = "admin";
        public static string Password = "";
        public static SubsonicSettings.ConnectionProtocol Protocol = SubsonicSettings.ConnectionProtocol.Http;
        public static SubsonicSettings.AuthMethod AuthMethod = SubsonicSettings.AuthMethod.Token;
        private static SubsonicSettings.ApiVersion _api = SubsonicSettings.ApiVersion.V13;
        public static bool Transcode;
        public static bool IsInitialized;
        public static string SettingsUrl;
        public static string CacheUrl;
        public static Plugin.MB_SendNotificationDelegate SendNotificationsHandler;
        private static string _serverName;
        private static Exception _lastEx;
        private static KeyValuePair<byte, string>[][] _cachedFiles;
        //private static Thread _retrieveThread;
        private static string[] _collectionNames;
        private static readonly Dictionary<string, ulong> LastModified = new Dictionary<string, ulong>();
        private static readonly Dictionary<string, string> FolderLookup = new Dictionary<string, string>();

        public static bool Initialize()
        {
            _lastEx = null;
            try
            {
                // if there's a settings file found, load it
                if (File.Exists(SettingsUrl))
                    using (var reader = new StreamReader(SettingsUrl))
                    {
                        var protocolText = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                        Protocol = protocolText.Equals("HTTP")
                            ? SubsonicSettings.ConnectionProtocol.Http
                            : SubsonicSettings.ConnectionProtocol.Https;
                        Host = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                        Port = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                        BasePath = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                        Username = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                        Password = AesEncryption.Decrypt(reader.ReadLine(), Passphrase);
                        Transcode = AesEncryption.Decrypt(reader.ReadLine(), Passphrase) == "Y";
                        AuthMethod = AesEncryption.Decrypt(reader.ReadLine(), Passphrase) == "HexPass"
                            ? SubsonicSettings.AuthMethod.HexPass
                            : SubsonicSettings.AuthMethod.Token;
                        // If HexPass is selected, we need to use an older API version. Otherwise we default to 1.13
                        _api = AuthMethod == SubsonicSettings.AuthMethod.HexPass
                            ? SubsonicSettings.ApiVersion.V11
                            : SubsonicSettings.ApiVersion.V13;
                    }

                _serverName = $"{Protocol.ToFriendlyString()}://{Host}:{Port}{BasePath}";

                _service = new SubsonicService(_serverName, Username, Password, AuthMethod, _api.ToFriendlyString(),
                    Transcode ? "mp3" : "raw");

                // test if the server responds to a Subsonic Ping request
                IsInitialized = PingServer();
            }
            catch (Exception ex)
            {
                _lastEx = ex;
                IsInitialized = false;
            }
            return IsInitialized;
        }

        private static bool PingServer()
        {
            try
            {
                //var request = new RestRequest
                //{
                //    Resource = "ping.view"
                //};
                //var result = _client.Execute<Response>(request);
                var result = Task.Run(() => _service.PingServer()).Result;
                var isPingOk = result.Status == ResponseStatus.ok;
                return isPingOk;
            }
            catch (Exception ex)
            {
                _lastEx = ex;
                return false;
            }
        }

        public static void Close()
        {
            //if ((_retrieveThread == null) || !_retrieveThread.IsAlive) return;
            //_retrieveThread.Abort();
            //_retrieveThread = null;
        }

        public static bool SetHost(SubsonicSettings settings)
        {
            _lastEx = null;

            settings.Host = settings.Host.Trim();
            if (settings.Host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                settings.Host = settings.Host.Substring(7);
            else if (settings.Host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                settings.Host = settings.Host.Substring(8);
            settings.Port = settings.Port.Trim();
            settings.BasePath = settings.BasePath.Trim();
            if (!settings.BasePath.EndsWith(@"/"))
                settings.BasePath += @"/";
            var isChanged = !settings.Host.Equals(Host) ||
                            !settings.Port.Equals(Port) ||
                            !settings.BasePath.Equals(BasePath) ||
                            !settings.Username.Equals(Username) ||
                            !settings.Password.Equals(Password) ||
                            !settings.Protocol.Equals(Protocol) ||
                            !settings.Transcode.Equals(Transcode) ||
                            !settings.Auth.Equals(AuthMethod);
            if (isChanged)
            {
                bool isPingOk;
                var previousProtocol = Protocol;
                var previousHost = Host;
                var previousPort = Port;
                var previousBasePath = BasePath;
                var previousUsername = Username;
                var previousPassword = Password;
                var previousTranscode = Transcode;

                try
                {
                    Protocol = settings.Protocol;
                    Host = settings.Host;
                    Port = settings.Port;
                    BasePath = settings.BasePath;
                    Username = settings.Username;
                    Password = settings.Password;
                    Transcode = settings.Transcode;
                    AuthMethod = settings.Auth;

                    isPingOk = PingServer();
                }
                catch (Exception)
                {
                    isPingOk = false;
                }
                if (!isPingOk)
                {
                    var dialog = MessageBox.Show(
                        @"The Subsonic server did not respond as expected, do you want to save these settings anyway?",
                        @"Could not get OK from server",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2);

                    if (dialog == DialogResult.Yes)
                        isPingOk = true;
                }

                if (!isPingOk)
                {
                    Protocol = previousProtocol;
                    Host = previousHost;
                    Port = previousPort;
                    BasePath = previousBasePath;
                    Username = previousUsername;
                    Password = previousPassword;
                    Transcode = previousTranscode;
                    return false;
                }

                IsInitialized = true;
            }
            if (!isChanged)
                return true;
            using (var writer = new StreamWriter(SettingsUrl))
            {
                writer.WriteLine(
                    AesEncryption.Encrypt(
                        settings.Protocol == SubsonicSettings.ConnectionProtocol.Http ? "HTTP" : "HTTPS", Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Host, Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Port, Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.BasePath, Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Username, Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Password, Passphrase));
                writer.WriteLine(settings.Transcode
                    ? AesEncryption.Encrypt("Y", Passphrase)
                    : AesEncryption.Encrypt("N", Passphrase));
                writer.WriteLine(
                    AesEncryption.Encrypt(settings.Auth == SubsonicSettings.AuthMethod.HexPass ? "HexPass" : "Token",
                        Passphrase));
            }
            Transcode = settings.Transcode;
            try
            {
                SendNotificationsHandler.Invoke(Plugin.CallbackType.SettingsUpdated);
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
        }

        public static bool FolderExists(string path)
        {
            var exists = string.IsNullOrEmpty(path) ||
                         path.Equals(@"\") ||
                         (GetFolderId(path) != null);
            return exists;
        }

        public static string[] GetFolders(string path)
        {
            _lastEx = null;
            string[] folders;
            if (!IsInitialized)
            {
                folders = new string[] {};
            }
            else if (string.IsNullOrEmpty(path))
            {
                folders = GetRootFolders(true, true, false).Select(folder => folder.Value).ToArray();
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
                    folders = new string[] {};
                }
                else
                {
                    //var request = new RestRequest
                    //{
                    //    Resource = "getMusicDirectory.view"
                    //};
                    //request.AddParameter("id", folderId);

                    //var result = _client.Execute<Directory>(request);
                    var id = folderId;
                    var result = Task.Run(() => _service.GetMusicDirectory(id)).Result;

                    var list = new List<string>();
                    if (result?.Child != null)
                        foreach (var dirChild in result.Child)
                        {
                            folderId = dirChild.Id;
                            var folderName = path + dirChild.Title;
                            list.Add(folderName);
                            if (!FolderLookup.ContainsKey(folderName))
                                FolderLookup.Add(folderName, folderId);
                        }
                    folders = list.ToArray();
                }
            }
            return folders;
        }

        public static KeyValuePair<byte, string>[][] GetFiles(string path)
        {
            //var threadStarted = false;
            _lastEx = null;
            KeyValuePair<byte, string>[][] files;
            if (!IsInitialized)
            {
                files = new KeyValuePair<byte, string>[][] {};
            }
            else
            {
                var cacheLoaded = _cachedFiles != null;
                if (!cacheLoaded && !File.Exists(CacheUrl))
                    files = null;
                else
                    files = GetCachedFiles();
                //var cacheUpdating = _retrieveThread != null;
                //if (!cacheUpdating && (string.IsNullOrEmpty(path) || !cacheLoaded))
                //{
                //    threadStarted = true;
                //    _retrieveThread = new Thread(ExecuteGetFolderFiles) {IsBackground = true};
                //    _retrieveThread.Start();
                //}

                if (!string.IsNullOrEmpty(path))
                {
                    //if (!cacheLoaded || cacheUpdating || (files == null))
                    if (!cacheLoaded || (files == null))
                        return GetFolderFiles(path);
                    files = GetPathFilteredFiles(files, path);
                }
            }
            //if (threadStarted) return files;
            try
            {
                SendNotificationsHandler.Invoke(Plugin.CallbackType.FilesRetrievedNoChange);
            }
            catch (Exception ex)
            {
                _lastEx = ex;
            }
            return files;
        }

        private static KeyValuePair<byte, string>[][] GetCachedFiles()
        {
            if (_cachedFiles != null) return _cachedFiles;
            KeyValuePair<byte, string>[][] files = null;
            //lock (CacheFileLock)
            //{
            using (
                var stream = new FileStream(CacheUrl, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                    FileOptions.SequentialScan))
            {
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
            //}
            //lock (CacheLock)
            //{
            if ((_cachedFiles == null) && (files != null))
                _cachedFiles = files;
            //}
            return _cachedFiles;
        }

        private static KeyValuePair<byte, string>[][] GetPathFilteredFiles(KeyValuePair<byte, string>[][] files,
            string path)
        {
            if (!path.EndsWith(@"\"))
                path += @"\";
            files = files.AsParallel().Where(t => t[0].Value.StartsWith(path)).ToArray();
            Array.Sort(files, new FileSorter());
            return files;
        }

        private static void ExecuteGetFolderFiles()
        {
            try
            {
                var files = new KeyValuePair<byte, string>[][] {};
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
                    //lock (CacheLock)
                    //{
                    var oldCachedFiles = _cachedFiles;
                    _cachedFiles = files;
                    //}
                    anyChanges = (oldCachedFiles == null) || (_cachedFiles.Length != oldCachedFiles.Length);
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
                        SendNotificationsHandler.Invoke(Plugin.CallbackType.FilesRetrievedNoChange);
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
                        SendNotificationsHandler.Invoke(Plugin.CallbackType.FilesRetrievedChanged);
                    }
                    catch (Exception ex)
                    {
                        _lastEx = ex;
                    }
                    try
                    {
                        //lock (CacheFileLock)
                        //{
                        using (
                            var stream = new FileStream(CacheUrl, FileMode.Create, FileAccess.Write,
                                FileShare.None))
                        {
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
                        //}
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
                    SendNotificationsHandler.Invoke(Plugin.CallbackType.FilesRetrievedFail);
                }
                catch
                {
                    _lastEx = ex;
                }
            }
            finally
            {
                //_retrieveThread = null;
            }
        }

        private static List<KeyValuePair<string, string>> GetRootFolders(bool collectionOnly, bool refresh,
            bool dirtyOnly)
        {
            var folders = new List<KeyValuePair<string, string>>();
            //lock (FolderLookupLock)
            //{
            if (!refresh && !FolderLookup.Count.Equals(0)) return folders;
            folders = new List<KeyValuePair<string, string>>();
            var collection = new List<KeyValuePair<string, string>>();

            //var request = new RestRequest
            //{
            //    Resource = "getMusicFolders.view"
            //};

            //var result = _client.Execute<MusicFolders>(request);
            var result = Task.Run(() => _service.GetMusicFolders()).Result;

            if (result?.MusicFolder != null)
                foreach (var folder in result.MusicFolder)
                {
                    var folderId = folder.Id.ToString();
                    var folderName = folder.Name;
                    if ((folderName != null) && FolderLookup.ContainsKey(folderName))
                    {
                        FolderLookup[folderName] = folderId;
                    }
                    else
                    {
                        if (folderName != null) FolderLookup.Add(folderName, folderId);
                    }
                    collection.Add(new KeyValuePair<string, string>(folderId, folderName));
                }

            _collectionNames = new string[collection.Count];
            for (var index = 0; index < collection.Count; index++)
                _collectionNames[index] = collection[index].Value + @"\";
            var isDirty = false;
            foreach (var collectionItem in collection)
                folders.AddRange(GetRootFolders(collectionItem.Key, collectionItem.Value, true, refresh && dirtyOnly,
                    ref isDirty));
            if (collectionOnly)
                return collection;
            if (dirtyOnly && !isDirty)
                return null;
            //}
            return folders;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetRootFolders(string collectionId,
            string collectionName,
            bool indices, bool updateIsDirty, ref bool isDirty)
        {
            var folders = new List<KeyValuePair<string, string>>();

            //var request = new RestRequest
            //{
            //    Resource = "getIndexes.view"
            //};
            //request.AddParameter("musicFolderId", collectionId);
            //var result = _client.Execute<Indexes>(request);
            var result = Task.Run(() => _service.GetIndexes(collectionId)).Result;

            if (updateIsDirty && (result?.LastModified != null))
            {
                var serverLastModified = (ulong) result.LastModified;
                //lock (CacheFileLock)
                //{
                ulong clientLastModified;
                if (!LastModified.TryGetValue(collectionName, out clientLastModified))
                {
                    isDirty = true;
                    LastModified.Add(collectionName, serverLastModified);
                }
                else if (serverLastModified > clientLastModified)
                {
                    isDirty = true;
                    LastModified[collectionName] = serverLastModified;
                }
                //}                
            }

            if (result?.Index != null)
                foreach (var indexChild in result.Index)
                    foreach (var artistChild in indexChild.Artist)
                    {
                        var folderId = artistChild.Id;
                        var folderName = $"{collectionName}\\{artistChild.Name}";
                        if (FolderLookup.ContainsKey(folderName))
                            FolderLookup[folderName] = folderId;
                        else
                            FolderLookup.Add(folderName, folderId);
                        folders.Add(new KeyValuePair<string, string>(indices ? folderId : folderName, collectionName));
                    }

            return folders;
        }

        private static void GetFolderFiles(string baseFolderName, string folderId,
            ICollection<KeyValuePair<byte, string>[]> files)
        {
            //var request = new RestRequest
            //{
            //    Resource = "getMusicDirectory.view"
            //};
            //request.AddParameter("id", folderId);
            //var result = _client.Execute<Directory>(request);
            //            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var result = Task.Run(() => _service.GetMusicDirectory(folderId)).Result;

            if (result?.Child != null)
                foreach (var childEntry in result.Child)
                    if (childEntry.IsDir)
                    {
                        GetFolderFiles(baseFolderName, childEntry.Id, files);
                    }
                    else
                    {
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
            var files = new List<KeyValuePair<byte, string>[]>();
            if (folderId == null)
                return new KeyValuePair<byte, string>[][] {};
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
            string folderId;
            if (FolderLookup.TryGetValue(url.Substring(0, charIndex), out folderId)) return folderId;
            var sectionStartIndex = url.IndexOf(@"\", StringComparison.Ordinal) + 1;
            charIndex = url.IndexOf(@"\", sectionStartIndex, StringComparison.Ordinal);
            if (charIndex.Equals(-1))
                throw new ArgumentException();
            while (charIndex != -1)
            {
                string subFolderId;
                if (FolderLookup.TryGetValue(url.Substring(0, charIndex), out subFolderId))
                {
                    folderId = subFolderId;
                }
                else if (folderId != null)
                {
                    var folderName = url.Substring(sectionStartIndex, charIndex - sectionStartIndex);
                    //var request = new RestRequest
                    //{
                    //    Resource = "getMusicDirectory.view"
                    //};
                    //request.AddParameter("id", folderId);
                    //var result = _client.Execute<Directory>(request);
                    //                    var result = Response.Deserialize(response.Replace("\0", string.Empty));
                    var id = folderId;
                    var result = Task.Run(() => _service.GetMusicDirectory(id)).Result;

                    if (result?.Child != null)
                        foreach (var childEntry in result.Child)
                            if (childEntry.IsDir && (childEntry.Title == folderName))
                            {
                                folderId = childEntry.Id;
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
            return url.Replace(@"\", @"/");
        }

        private static string GetFileId(string url)
        {
            var folderId = GetFolderId(url);
            if (folderId == null) return null;

            //var request = new RestRequest
            //{
            //    Resource = "getMusicDirectory.view"
            //};
            //request.AddParameter("id", folderId);
            //var result = _client.Execute<Directory>(request);
            //            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var result = Task.Run(() => _service.GetMusicDirectory(folderId)).Result;

            var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

            if (result?.Child != null)
                foreach (var childEntry in result.Child)
                    if (childEntry.Path == filePath)
                        return childEntry.Id;
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

            //var request = new RestRequest
            //{
            //    Resource = "getMusicDirectory.view"
            //};
            //request.AddParameter("id", folderId);
            //var result = _client.Execute<Directory>(request);
            //            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var result = Task.Run(() => _service.GetMusicDirectory(folderId)).Result;

            var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));
            if (result?.Child != null)
                foreach (var childEntry in result.Child)
                    if (childEntry.Path == filePath)
                        return childEntry.CoverArt;
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

            //var request = new RestRequest
            //{
            //    Resource = "getMusicDirectory.view"
            //};
            //request.AddParameter("id", folderId);
            //var result = _client.Execute<Directory>(request);
            //            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var result = Task.Run(() => _service.GetMusicDirectory(folderId)).Result;

            var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

            if (result?.Child != null)
                foreach (var childEntry in result.Child)
                    if (childEntry.Path == filePath)
                        return GetTags(childEntry, childEntry.Path);
            return null;
        }

        private static KeyValuePair<byte, string>[] GetTags(Child child, string baseFolderName)
        {
            if (child.IsVideo)
                return null;

            var tags = new KeyValuePair<byte, string>[TagCount + 1];
            var path = string.Empty;
            var attribute = child.Path;
            if (attribute != null)
                path = attribute.Replace(@"/", @"\");
            path = baseFolderName == null ? GetResolvedUrl(path) : $"{baseFolderName}\\{path}";
            tags[0] = new KeyValuePair<byte, string>((byte) Plugin.FilePropertyType.Url, path);
            tags[1] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Artist, child.Artist);
            tags[2] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.TrackTitle, child.Title);
            tags[3] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Album, child.Album);
            tags[4] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Year, child.Year.ToString());
            tags[5] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.TrackNo, child.Track.ToString());
            tags[6] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Genre, child.Genre);
            tags[7] = new KeyValuePair<byte, string>((byte) Plugin.FilePropertyType.Duration,
                (child.Duration*1000).ToString());
            tags[8] = new KeyValuePair<byte, string>((byte) Plugin.FilePropertyType.Bitrate, child.BitRate.ToString());
            tags[9] = new KeyValuePair<byte, string>((byte) Plugin.FilePropertyType.Size, child.Size.ToString());
            tags[10] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Artwork,
                string.IsNullOrEmpty(child.CoverArt) ? "" : "Y");

            for (var tagIndex = 1; tagIndex < TagCount; tagIndex++)
                if (tags[tagIndex].Value == null)
                    tags[tagIndex] = new KeyValuePair<byte, string>(tags[tagIndex].Key, "");
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
                    bytes = Task.Run(() => _service.GetCoverArt(id)).Result;
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
            //var request = new RestRequest
            //{
            //    Resource = "getPlaylists.view",
            //    //RootElement = "playlists"
            //};
            //var result = _client.Execute<Playlists>(request);
            var result = Task.Run(() => _service.GetPlaylists()).Result;
            if (result?.Playlist != null)
                foreach (var playlistEntry in result.Playlist)
                    playlists.Add(new KeyValuePair<string, string>(playlistEntry.Id, playlistEntry.Name));

            return playlists.ToArray();
        }

        public static KeyValuePair<byte, string>[][] GetPlaylistFiles(string id)
        {
            _lastEx = null;
            //var request = new RestRequest
            //{
            //    Resource = "getPlaylist.view"
            //};
            //request.AddParameter("id", id);
            //var result = _client.Execute<PlaylistWithSongs>(request);
            //            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var result = Task.Run(() => _service.GetPlaylist(id)).Result;

            var files = new List<KeyValuePair<byte, string>[]>();
            if (result?.Entry != null)
                foreach (var playlistEntry in result.Entry)
                {
                    var tags = GetTags(playlistEntry, null);
                    if (tags != null)
                        files.Add(tags);
                }
            return files.ToArray();
        }

        public static Stream GetStream(string url)
        {
            _lastEx = null;
            var id = GetFileId(url);
            if (id == null)
                _lastEx = new FileNotFoundException();
            else
                return Task.Run(() => _service.GetStream(id)).Result;
            return null;
        }

        public static Exception GetError()
        {
            return _lastEx;
        }

        private sealed class FileSorter : Comparer<KeyValuePair<byte, string>[]>
        {
            public override int Compare(KeyValuePair<byte, string>[] x, KeyValuePair<byte, string>[] y)
            {
                if ((x != null) && (y != null))
                    return string.Compare(x[0].Value, y[0].Value, StringComparison.OrdinalIgnoreCase);
                return 0;
            }
        }
    }
}