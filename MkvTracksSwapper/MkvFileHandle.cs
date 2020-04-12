using System.Collections.Generic;
using System.IO;

namespace MkvTracksSwapper
{
    public class MkvFileHandle
    {
        public FileInfo FileInfo { get; }
        public List<Track> Tracks { get; }

        public MkvFileHandle(FileInfo file, List<Track> tracks)
        {
            FileInfo = file;
            Tracks = tracks;
        }
    }
}