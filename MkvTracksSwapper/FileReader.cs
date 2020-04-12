using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MkvTracksSwapper
{
    public class FileReader
    {
        private readonly FileInfo fileInfo;
        private readonly List<Track> tracks;
        private Track current;

        public FileReader(FileInfo file)
        {
            fileInfo = file;
            tracks = new List<Track>();
            current = null;
        }

        public async Task<MkvFileHandle> ProcessFile()
        {
            var mkvInfoRunner = new ProcessRunner("mkvinfo", true, HandleMkvInfoOutput);
            try
            {
                Console.WriteLine("Starting reading of file: " + fileInfo.FullName);

                var successful = await mkvInfoRunner.RunWithArg($"\"{fileInfo.FullName}\"");

                if (!successful)
                {
                    Console.WriteLine($"Error occured while running mkvinfo: {mkvInfoRunner.Error}");
                    return null;
                }

                if (current != null)
                {
                    tracks.Add(current);
                }

                return new MkvFileHandle(fileInfo, tracks);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occured while running mkvinfo: {e.Message}");
                return null;
            }
            finally
            {
                mkvInfoRunner.Dispose();
            }
        }

        private void HandleMkvInfoOutput(object sender, DataReceivedEventArgs e)
        {
            string line = e.Data;

            if (line == null)
            {
                Console.WriteLine("END OF OUTPUT");
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
                current.TrackNumber = int.Parse(GetPropertyValue(line));
            }
            else if (line.StartsWith("|  + Track UID:"))
            {
                current.UID = GetPropertyValue(line);
            }
            else if (line.StartsWith("|  + Track type:"))
            {
                current.Type = Enum.Parse<TrackType>(GetPropertyValue(line), true);
            }
            else if (line.StartsWith("|  + Language:"))
            {
                current.Language = GetPropertyValue(line);
            }
        }

        private string GetPropertyValue(string line)
        {
            return line.Split(':')[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }
    }
}