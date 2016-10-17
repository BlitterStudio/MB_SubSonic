using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MusicBeePlugin.Domain;
using RestSharp;

namespace MusicBeePlugin
{
    public static class Subsonic
    {
        private const int TagCount = 10;
        private const string Passphrase = "PeekAndPoke";
        public static string Host = "localhost";
        public static string Port = "80";
        public static string BasePath = "/";
        public static string Username = "admin";
        public static string Password = "";
        public static SubsonicSettings.ConnectionProtocol Protocol = SubsonicSettings.ConnectionProtocol.Http;
        public static SubsonicSettings.AuthMethod AuthMethod = SubsonicSettings.AuthMethod.Token;
        public static SubsonicSettings.ApiVersion Api = SubsonicSettings.ApiVersion.V113;
        public static bool Transcode;
        public static bool IsInitialized;
        public static string SettingsUrl;
        public static string CacheUrl;
        public static Plugin.MB_SendNotificationDelegate SendNotificationsHandler;
        private static string _serverName;
        private static Exception _lastEx;
        private static KeyValuePair<byte, string>[][] _cachedFiles;
        private static Thread _retrieveThread;
        private static string[] _collectionNames;
        private static readonly Dictionary<string, ulong> LastModified = new Dictionary<string, ulong>();
        private static readonly Dictionary<string, string> FolderLookup = new Dictionary<string, string>();

        public static bool Initialize()
        {
            _lastEx = null;
            try
            {
                if (File.Exists(SettingsUrl))
                {
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
                        Api = AuthMethod == SubsonicSettings.AuthMethod.HexPass
                            ? SubsonicSettings.ApiVersion.V111
                            : SubsonicSettings.ApiVersion.V113;
                    }
                }
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
            _serverName = $"{Protocol.ToFriendlyString()}://{Host}:{Port}{BasePath}";
            try
            {
                var request = new RestRequest
                {
                    Resource = "ping.view"
                };
                var response = SendRequest(request);
                var result = Response.Deserialize(response);
                bool isPingOk = result.status == ResponseStatus.ok;
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
            if (_retrieveThread == null || !_retrieveThread.IsAlive) return;
            _retrieveThread.Abort();
            _retrieveThread = null;
        }

        public static bool SetHost(SubsonicSettings settings)
        {
            _lastEx = null;

            settings.Host = settings.Host.Trim();
            if (settings.Host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                settings.Host = settings.Host.Substring(7);
            }
            else if (settings.Host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                settings.Host = settings.Host.Substring(8);
            }
            settings.Port = settings.Port.Trim();
            settings.BasePath = settings.BasePath.Trim();
            if (!settings.BasePath.EndsWith(@"/"))
            {
                settings.BasePath += @"/";
            }
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
                    {
                        isPingOk = true;
                    }
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
            {
                return true;
            }
            using (var writer = new StreamWriter(SettingsUrl))
            {
                writer.WriteLine(AesEncryption.Encrypt(settings.Protocol == SubsonicSettings.ConnectionProtocol.Http? "HTTP" : "HTTPS", Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Host, Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Port, Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.BasePath, Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Username, Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Password, Passphrase));
                writer.WriteLine(settings.Transcode
                    ? AesEncryption.Encrypt("Y", Passphrase)
                    : AesEncryption.Encrypt("N", Passphrase));
                writer.WriteLine(AesEncryption.Encrypt(settings.Auth == SubsonicSettings.AuthMethod.HexPass ? "HexPass" : "Token", Passphrase));
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
                         GetFolderId(path) != null;
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
                {
                    path += @"\";
                }
                var folderId = GetFolderId(path);
                if (string.IsNullOrEmpty(folderId))
                {
                    folders = new string[] {};
                }
                else
                {
                    var request = new RestRequest
                    {
                        Resource = "getMusicDirectory.view",
                    };
                    request.AddParameter("id", folderId);
                    var response = SendRequest(request);
                    var result = Response.Deserialize(response);
                    var error = result.Item as Error;
                    if (error != null)
                    {
                        MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return null;
                    }
                    var content = result.Item as Directory;

                    var list = new List<string>();
                    if (content?.child != null)
                    foreach (var dirChild in content.child)
                    {
                        folderId = dirChild.id;
                        var folderName = path + dirChild.title;
                        list.Add(folderName);
                        if (!FolderLookup.ContainsKey(folderName))
                        {
                            FolderLookup.Add(folderName, folderId);
                        }
                    }
                    folders = list.ToArray();
                }
            }
            return folders;
        }

        public static KeyValuePair<byte, string>[][] GetFiles(string path)
        {
            var threadStarted = false;
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
                {
                    files = null;
                }
                else
                {
                    files = GetCachedFiles();
                }
                var cacheUpdating = _retrieveThread != null;
                if (!cacheUpdating && (string.IsNullOrEmpty(path) || !cacheLoaded))
                {
                    threadStarted = true;
                    _retrieveThread = new Thread(ExecuteGetFolderFiles) {IsBackground = true};
                    _retrieveThread.Start();
                }
                if (!string.IsNullOrEmpty(path))
                {
                    if (!cacheLoaded || cacheUpdating || files == null)
                    {
                        return GetFolderFiles(path);
                    }
                    files = GetPathFilteredFiles(files, path);
                }
            }
            if (threadStarted) return files;
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
                                {
                                    LastModified.Add(collectionName, reader.ReadUInt64());
                                }
                            }
                        }
                        reader.Close();
                    }
                    catch (EndOfStreamException)
                    {
                        MessageBox.Show(@"The Cache file seems to be empty!");
                    }
                }
            //}
            //lock (CacheLock)
            //{
                if (_cachedFiles == null && files != null)
                {
                    _cachedFiles = files;
                }
            //}
            return _cachedFiles;
        }

        private static KeyValuePair<byte, string>[][] GetPathFilteredFiles(KeyValuePair<byte, string>[][] files,
            string path)
        {
            if (!path.EndsWith(@"\"))
            {
                path += @"\";
            }
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
                    {
                        GetFolderFiles(folder.Value, folder.Key, list);
                    }
                    files = list.ToArray();
                    //lock (CacheLock)
                    //{
                        var oldCachedFiles = _cachedFiles;
                        _cachedFiles = files;
                    //}
                    anyChanges = oldCachedFiles == null || _cachedFiles.Length != oldCachedFiles.Length;
                    if (!anyChanges)
                    {
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
                            using (var writer = new BinaryWriter(stream))
                            {
                                writer.Write(2); // version
                                writer.Write(files.Length);
                                foreach (var tags in files)
                                {
                                    for (var tagIndex = 0; tagIndex <= TagCount; tagIndex++)
                                    {
                                        var tag = tags[tagIndex];
                                        writer.Write(tag.Key);
                                        writer.Write(tag.Value);
                                    }
                                }
                                writer.Write(LastModified.Count);
                                foreach (var item in LastModified)
                                {
                                    writer.Write(item.Key);
                                    writer.Write(item.Value);
                                }
                                writer.Close();
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
                _retrieveThread = null;
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

                var request = new RestRequest
                {
                    Resource = "getMusicFolders.view",
                };
                var response = SendRequest(request);
                var result = Response.Deserialize(response);
                var error = result.Item as Error;
                if (error != null)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
                var content = (MusicFolders)result.Item;

                if (content.musicFolder != null)
                foreach (var folder in content.musicFolder)
                {
                    var folderId = folder.id.ToString();
                    var folderName = folder.name;
                    if (folderName != null && FolderLookup.ContainsKey(folderName))
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
                {
                    _collectionNames[index] = collection[index].Value + @"\";
                }
                var isDirty = false;
                foreach (var collectionItem in collection)
                {
                    folders.AddRange(GetRootFolders(collectionItem.Key, collectionItem.Value, true, refresh && dirtyOnly, ref isDirty));
                }
                if (collectionOnly)
                {
                    return collection;
                }
                if (dirtyOnly && !isDirty)
                {
                    return null;
                }
            //}
            return folders;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetRootFolders(string collectionId,
            string collectionName,
            bool indices, bool updateIsDirty, ref bool isDirty)
        {
            var folders = new List<KeyValuePair<string, string>>();

            var request = new RestRequest
            {
                Resource = "getIndexes.view",
            };
            request.AddParameter("musicFolderId", collectionId);
            var response = SendRequest(request);
            var result = Response.Deserialize(response);
            var error = result.Item as Error;
            if (error != null)
            {
                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            var content = result.Item as Indexes;

            if (updateIsDirty && content?.lastModified != null)
            {
                var serverLastModified = (ulong)content.lastModified;
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

            if (content?.index != null)
            foreach (var indexChild in content.index)
            {
                foreach (var artistChild in indexChild.artist)
                {
                    var folderId = artistChild.id;
                    var folderName = $"{collectionName}\\{artistChild.name}";
                    if (FolderLookup.ContainsKey(folderName))
                    {
                        FolderLookup[folderName] = folderId;
                    }
                    else
                    {
                        FolderLookup.Add(folderName, folderId);
                    }
                    folders.Add(new KeyValuePair<string, string>(indices ? folderId : folderName, collectionName));
                }
            }

            return folders;
        }

        private static void GetFolderFiles(string baseFolderName, string folderId,
            ICollection<KeyValuePair<byte, string>[]> files)
        {
            var request = new RestRequest
            {
                Resource = "getMusicDirectory.view",
            };
            request.AddParameter("id", folderId);
            var response = SendRequest(request);
            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var error = result.Item as Error;
            if (error != null)
            {
                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var content = result.Item as Directory;

            if (content?.child != null)
            foreach (var childEntry in content.child)
            {
                if (childEntry.isDir)
                {
                    GetFolderFiles(baseFolderName, childEntry.id, files);
                }
                else
                {
                    var tags = GetTags(childEntry, baseFolderName);
                    if (tags != null)
                    {
                        files.Add(tags);
                    }
                }
            }
        }

        private static KeyValuePair<byte, string>[][] GetFolderFiles(string path)
        {
            if (!path.EndsWith(@"\"))
            {
                path += @"\";
            }
            var folderId = GetFolderId(path);
            var files = new List<KeyValuePair<byte, string>[]>();
            if (folderId == null)
            {
                return new KeyValuePair<byte, string>[][] {};
            }
            GetFolderFiles(path.Substring(0, path.IndexOf(@"\", StringComparison.Ordinal)), folderId, files);
            return files.ToArray();
        }

        private static string GetFolderId(string url)
        {
            var charIndex = url.LastIndexOf(@"\", StringComparison.Ordinal);
            if (charIndex.Equals(-1))
            {
                throw new ArgumentException();
            }
            if (FolderLookup.Count.Equals(0))
            {
                GetRootFolders(false, false, false);
            }
            string folderId;
            if (FolderLookup.TryGetValue(url.Substring(0, charIndex), out folderId)) return folderId;
            var sectionStartIndex = url.IndexOf(@"\", StringComparison.Ordinal) + 1;
            charIndex = url.IndexOf(@"\", sectionStartIndex, StringComparison.Ordinal);
            if (charIndex.Equals(-1))
            {
                throw new ArgumentException();
            }
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
                    var request = new RestRequest
                    {
                        Resource = "getMusicDirectory.view",
                    };
                    request.AddParameter("id", folderId);
                    var response = SendRequest(request);
                    var result = Response.Deserialize(response.Replace("\0", string.Empty));
                    var error = result.Item as Error;
                    if (error != null)
                    {
                        MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return null;
                    }
                    var content = result.Item as Directory;

                    if (content?.child != null)
                    foreach (var childEntry in content.child)
                    {
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
                Resource = "getMusicDirectory.view",
            };
            request.AddParameter("id", folderId);
            var response = SendRequest(request);
            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var error = result.Item as Error;
            if (error != null)
            {
                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            var content = result.Item as Directory;

            var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

            if (content?.child != null)
            foreach (var childEntry in content.child)
            {
                if (childEntry.path == filePath)
                {
                    return childEntry.id;
                }
            }
            return null;
        }

        private static string GetResolvedUrl(string url)
        {
            if (FolderLookup.Count.Equals(0))
            {
                GetRootFolders(false, false, false);
            }
            if (_collectionNames.Length.Equals(1))
            {
                return _collectionNames[0] + url;
            }
            var path = url.Substring(0, url.LastIndexOf(@"\", StringComparison.Ordinal));
            string lastMatch = null;
            var count = 0;
            foreach (var item in _collectionNames.Where(item => GetFolderId(item + path) != null))
            {
                count += 1;
                lastMatch = item + url;
            }
            if (count.Equals(1))
            {
                return lastMatch;
            }
            foreach (var item in _collectionNames.Where(item => GetFolderId(item + path) != null))
            {
                lastMatch = item + url;
                if (GetFileId(lastMatch) != null)
                {
                    return lastMatch;
                }
            }
            return url;
        }

        private static string GetCoverArtId(string url)
        {
            var folderId = GetFolderId(url);
            if (folderId == null) return null;

            var request = new RestRequest
            {
                Resource = "getMusicDirectory.view",
            };
            request.AddParameter("id", folderId);
            var response = SendRequest(request);
            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var error = result.Item as Error;
            if (error != null)
            {
                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            var content = result.Item as Directory;

            var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));
            if (content?.child != null)
            foreach (var childEntry in content.child)
            {
                if (childEntry.path == filePath)
                {
                    return childEntry.coverArt;
                }
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

            var request = new RestRequest
            {
                Resource = "getMusicDirectory.view",
            };
            request.AddParameter("id", folderId);
            var response = SendRequest(request);
            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var error = result.Item as Error;
            if (error != null)
            {
                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            var content = result.Item as Directory;
            var filePath = GetTranslatedUrl(url.Substring(url.IndexOf(@"\", StringComparison.Ordinal) + 1));

            if (content?.child != null)
            foreach (var childEntry in content.child)
            {
                if (childEntry.path == filePath)
                {
                    return GetTags(childEntry, childEntry.path);
                }
            }
            return null;
        }

        private static KeyValuePair<byte, string>[] GetTags(Child child, string baseFolderName)
        {
            if (child.isVideo)
            {
                return null;
            }

            var tags = new KeyValuePair<byte, string>[TagCount + 1];
            var path = string.Empty;
            var attribute = child.path;
            if (attribute != null)
            {
                path = attribute.Replace(@"/", @"\");
            }
            path = baseFolderName == null ? GetResolvedUrl(path) : $"{baseFolderName}\\{path}";
            tags[0] = new KeyValuePair<byte, string>((byte) Plugin.FilePropertyType.Url, path);
            tags[1] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Artist, child.artist);
            tags[2] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.TrackTitle, child.title);
            tags[3] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Album, child.album);
            tags[4] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Year, child.year.ToString());
            tags[5] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.TrackNo, child.track.ToString());
            tags[6] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Genre, child.genre);
            tags[7] = new KeyValuePair<byte, string>((byte) Plugin.FilePropertyType.Duration, (child.duration * 1000).ToString());
            tags[8] = new KeyValuePair<byte, string>((byte) Plugin.FilePropertyType.Bitrate, child.bitRate.ToString());
            tags[9] = new KeyValuePair<byte, string>((byte) Plugin.FilePropertyType.Size, child.size.ToString());
            tags[10] = new KeyValuePair<byte, string>((byte) Plugin.MetaDataType.Artwork, string.IsNullOrEmpty(child.coverArt) ? "" : "Y");

            for (var tagIndex = 1; tagIndex < TagCount; tagIndex++)
            {
                if (tags[tagIndex].Value == null)
                {
                    tags[tagIndex] = new KeyValuePair<byte, string>(tags[tagIndex].Key, "");
                }
            }
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
                        Resource = "getCoverArt.view",
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
                Resource = "getPlaylists.view",
            };
            var response = SendRequest(request);
            var result = Response.Deserialize(response);
            var error = result.Item as Error;
            if (error != null)
            {
                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            var content = result.Item as Playlists;

            if (content?.playlist != null)
            foreach (var playlistEntry in content.playlist)
            {
                playlists.Add(new KeyValuePair<string, string>(playlistEntry.id, playlistEntry.name));
            }

            return playlists.ToArray();
        }

        public static KeyValuePair<byte, string>[][] GetPlaylistFiles(string id)
        {
            _lastEx = null;
            var request = new RestRequest
            {
                Resource = "getPlaylist.view",
            };
            request.AddParameter("id", id);
            var response = SendRequest(request);
            var result = Response.Deserialize(response.Replace("\0", string.Empty));
            var error = result.Item as Error;
            if (error != null)
            {
                MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            var content = result.Item as PlaylistWithSongs;

            var files = new List<KeyValuePair<byte, string>[]>();
            if (content?.entry != null)
            foreach (var playlistEntry in content.entry)
            {
                var tags = GetTags(playlistEntry, null);
                if (tags != null)
                {
                    files.Add(tags);
                }
            }
            return files.ToArray();
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
                var token = Md5(Password + salt);
                var encoding = Transcode ? "mp3" : "raw";
                string uriLine;
                if (AuthMethod == SubsonicSettings.AuthMethod.HexPass)
                {
                    var ba = Encoding.Default.GetBytes(Password);
                    var hexString = BitConverter.ToString(ba);
                    hexString = hexString.Replace("-", "");
                    var hexPass = $"enc:{hexString}";
                    uriLine =
                        $"{_serverName}rest/stream.view?u={Username}&p={hexPass}&v={Api.ToFriendlyString()}&c=MusicBee&id={id}&format={encoding}";
                }
                else
                {
                    uriLine =
                        $"{_serverName}rest/stream.view?u={Username}&t={token}&s={salt}&v={Api.ToFriendlyString()}&c=MusicBee&id={id}&format={encoding}";
                }
                var uri = new Uri(uriLine);

                var stream = new ConnectStream(uri);
                if (stream.ContentType.StartsWith("text/xml"))
                {
                    using (stream)
                        _lastEx = new InvalidDataException();
                }
                else
                {
                    return stream;
                }
            }
            return null;
        }

        public static Exception GetError()
        {
            return _lastEx;
        }

        private static string SendRequest(IRestRequest request)
        {
            var client = new RestClient { BaseUrl = new Uri(_serverName + "rest/") };
            request.AddParameter("u", Username);
            if (AuthMethod == SubsonicSettings.AuthMethod.HexPass)
            {
                var ba = Encoding.Default.GetBytes(Password);
                var hexString = BitConverter.ToString(ba);
                hexString = hexString.Replace("-", "");
                var hexPass = $"enc:{hexString}";
                request.AddParameter("p", hexPass);
            }
            else
            {
                var salt = NewSalt();
                var token = Md5(Password + salt);
                request.AddParameter("t", token);
                request.AddParameter("s", salt);
                
            }
            request.AddParameter("v", Api.ToFriendlyString());
            request.AddParameter("c", "MusicBee");

            var response = client.Execute(request);

            if (response.ErrorException != null)
            {
                const string message = "Error retrieving response from Subsonic server:";
                MessageBox.Show($@"{message}

{response.ErrorException}", @"Subsonic Plugin Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return response.Content;
        }

        private static byte[] DownloadData(IRestRequest request)
        {
            var client = new RestClient { BaseUrl = new Uri(_serverName + "rest/") };
            request.AddParameter("u", Username);

            if (AuthMethod == SubsonicSettings.AuthMethod.HexPass)
            {
                var ba = Encoding.Default.GetBytes(Password);
                var hexString = BitConverter.ToString(ba);
                hexString = hexString.Replace("-", "");
                var hexPass = $"enc:{hexString}";
                request.AddParameter("p", hexPass);
            }
            else
            {
                var salt = NewSalt();
                var token = Md5(Password + salt);
                request.AddParameter("t", token);
                request.AddParameter("s", salt);

            }
            request.AddParameter("v", Api.ToFriendlyString());
            request.AddParameter("c", "MusicBee");
            var response = client.Execute(request);

            if (response.ContentType.StartsWith("text/xml"))
            {
                var result = Response.Deserialize(response.Content.Replace("\0", string.Empty));
                var error = result.Item as Error;
                if (error != null)
                {
                    MessageBox.Show($@"An error has occurred:
{error.message}", @"Error reported from Subsonic Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }
            }
            return response.RawBytes;
        }

        private static string NewSalt()
        {
            // Define min and max salt sizes.
            var minSaltSize = 6;
            var maxSaltSize = 12;

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


        private sealed class FileSorter : Comparer<KeyValuePair<byte, string>[]>
        {
            public override int Compare(KeyValuePair<byte, string>[] x, KeyValuePair<byte, string>[] y)
            {
                if (x != null && y != null)
                {
                    return string.Compare(x[0].Value, y[0].Value, StringComparison.OrdinalIgnoreCase);
                }
                return 0;
            }
        }
    }
}