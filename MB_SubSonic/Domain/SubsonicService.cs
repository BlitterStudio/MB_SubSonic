using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MusicBeePlugin.API11;
using RestSharp.Portable;
using RestSharp.Portable.WebRequest;
using Directory = MusicBeePlugin.API11.Directory;

namespace MusicBeePlugin.Domain
{
    public sealed class SubsonicService : IDisposable
    {
        private static readonly ISerializer RestSerializer = new ConservativeJsonSerializer();
        private readonly string _apiVersion;
        private readonly SubsonicSettings.AuthMethod _authMethod;
        private readonly string _hexpass;

        private readonly IRestClient _restClient;
        private readonly string _salt;
        private readonly string _token;
        private readonly string _trancode;
        private readonly string _username;

        public SubsonicService(string server, string username, string password,
            SubsonicSettings.AuthMethod authMethod = SubsonicSettings.AuthMethod.Token, string apiVersion = "1.13.0",
            string transcode = "raw")
        {
            var baseUrl = server + "rest/";
            _username = username;
            _authMethod = authMethod;

            // depending on the authentication method selected, we use:
            // 1) Hex-encoded password
            if (authMethod == SubsonicSettings.AuthMethod.HexPass)
            {
                var ba = Encoding.Default.GetBytes(password);
                var hexString = BitConverter.ToString(ba);
                hexString = hexString.Replace("-", "");
                _hexpass = $"enc:{hexString}";
            }
            // 2) Token based authentication
            else
            {
                _salt = NewSalt();
                _token = Md5(password + _salt);
            }
            _apiVersion = apiVersion;
            _trancode = transcode;

            _restClient = new RestClient {BaseUrl = new Uri(baseUrl)};
        }

        public async Task<Response> PingServer()
        {
            var request = CreateRequest("ping.view");
            return (await _restClient.Execute<Response>(request)).Data;
        }

        public async Task<Directory> GetMusicDirectory(string id)
        {
            var request = CreateRequest("getMusicDirectory.view");
            request.AddParameter("id", id);
            return (await _restClient.Execute<Directory>(request)).Data;
        }

        public async Task<MusicFolders> GetMusicFolders()
        {
            var request = CreateRequest("getMusicFolders.view");
            return (await _restClient.Execute<MusicFolders>(request)).Data;
        }

        public async Task<Indexes> GetIndexes(string musicFolderId)
        {
            var request = CreateRequest("getIndexes.view");
            request.AddParameter("musicFolderId", musicFolderId);
            return (await _restClient.Execute<Indexes>(request)).Data;
        }

        public async Task<Playlists> GetPlaylists()
        {
            var request = CreateRequest("getPlaylists.view");
            return (await _restClient.Execute<Playlists>(request)).Data;
        }

        public async Task<PlaylistWithSongs> GetPlaylist(string id)
        {
            var request = CreateRequest("getPlaylist.view");
            request.AddParameter("id", id);
            return (await _restClient.Execute<PlaylistWithSongs>(request)).Data;
        }

        public async Task<byte[]> GetCoverArt(string id)
        {
            var request = CreateRequest("getCoverArt.view");
            request.AddParameter("id", id);
            return (await _restClient.Execute(request)).RawBytes;
        }

        public async Task<Stream> GetStream(string id)
        {
            var request = CreateRequest("stream.view");
            request.AddParameter("id", id);
            request.AddParameter("format", _trancode);
            return new MemoryStream((await _restClient.Execute(request)).RawBytes);
        }

        private IRestRequest CreateRequest(string resource)
        {
            var request = new RestRequest(resource)
            {
                Serializer = RestSerializer
            };
            request.AddParameter("u", _username);
            if (_authMethod == SubsonicSettings.AuthMethod.HexPass)
            {
                request.AddParameter("p", _hexpass);
            }
            else
            {
                request.AddParameter("t", _token);
                request.AddParameter("s", _salt);
            }
            request.AddParameter("v", _apiVersion);
            request.AddParameter("c", "MusicBee");
            request.AddParameter("f", "json");
            return request;
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

        #region IDisposable Support

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                    _restClient.Dispose();
                _disposedValue = true;
            }
        }

        /// <summary>
        ///     Dispose all resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}