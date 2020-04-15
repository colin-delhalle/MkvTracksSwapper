using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace MkvTracksSwapper
{
    public class FileReader
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly FileInfo fileInfo;
        private readonly List<Track> tracks;
        private Track current;

        public FileReader(FileInfo file)
        {
            fileInfo = file;
            tracks = new List<Track>();
            current = null;
        }

        public async Task<MkvFileHandle> ProcessFile(CancellationToken ct = default)
        {
            using var mkvInfoRunner = new ProcessRunner("mkvinfo", TimeSpan.FromSeconds(15), HandleMkvInfoOutput);

            var successful = await mkvInfoRunner.RunWithArg($"\"{fileInfo.FullName}\"", ct);

            if (!successful)
            {
                logger.Fatal($"mkvinfo exited with non zero code: {mkvInfoRunner.Error}");
                return null;
            }

            return new MkvFileHandle(fileInfo, tracks);
        }

        private void HandleMkvInfoOutput(object sender, DataReceivedEventArgs e)
        {
            string line = e.Data;

            if (line == null) // received EOF on stream, need to add last track processed to the tracks list
            {
                if (current != null)
                {
                    tracks.Add(current);
                }

                return;
            }

            // TODO: remove string parsing and do it with a deserializer
            if (line == "| + Track")
            {
                if (current != null)
                {
                    tracks.Add(current);
                }

                current = new Track();
            }
            else if (line.StartsWith("|  + Track number:"))
            {
                current.TrackNumber = int.TryParse(GetPropertyValue(line), out int trackNumber) ? trackNumber : int.MinValue;
            }
            else if (line.StartsWith("|  + Track UID:"))
            {
                current.UID = GetPropertyValue(line);
            }
            else if (line.StartsWith("|  + Track type:"))
            {
                current.Type = Enum.TryParse(GetPropertyValue(line), true, out TrackType trackType) ? trackType : TrackType.Unknown;
            }
            else if (line.StartsWith("|  + Language:"))
            {
                current.Language = GetPropertyValue(line);
            }
            else if (line.StartsWith("|  + Default track flag:"))
            {
                current.IsDefault = GetPropertyValue(line) == "1";
            }
        }

        private string GetPropertyValue(string line)
        {
            try
            {
                return line.Split(':')[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            }
            catch (Exception e)
            {
                logger.Fatal(e);
                return string.Empty;
            }
        }
    }
}