using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicBeePlugin.Service
{
    public interface IMusicService
    {
        void Ping();
        bool IsLicenseValid();
        List<MusicFolder> GetMusicFolders(bool refresh);
        void StartRescan();
        Indexes GetIndexes(string musicFolderId, bool refresh);
        
    }
}
