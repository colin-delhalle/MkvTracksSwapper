using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MkvTracksSwapper
{
    public class TracksProcessor
    {
        private readonly Settings settings;

        public TracksProcessor(string audio, string subtitles, bool overrideFile = false)
        {
            settings = new Settings
            {
                AudioLanguage = audio,
                SubtitlesLanguage = subtitles,
                OverrideFile = overrideFile
            };
        }

        public async Task<bool> PutTracksFirst(MkvFileHandle handle)
        {
            if (!TracksAreLoaded(handle))
            {
                return false;
            }

            var mkvMergeRunner = new ProcessRunner("mkvmerge", false);

            try
            {
                var argLine = BuildMkvMergeArgumentLine(handle);
                Console.WriteLine("ARG LINE: " + argLine);
                var successful = await mkvMergeRunner.RunWithArg(argLine);

                if (successful)
                {
                    if (settings.OverrideFile)
                    {
                        await Task.Run(() =>
                        {
                            File.Delete(handle.FileInfo.FullName);
                            File.Move(settings.OutputPath, handle.FileInfo.FullName);
                        });
                    }
                }
                else
                {
                    Console.WriteLine("mkvmerge failed: " + mkvMergeRunner.Error);
                }

                return successful;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error occured while running mkvmerge: {e.Message}");
                return false;
            }
            finally
            {
                mkvMergeRunner.Dispose();
            }
        }

        private string BuildMkvMergeArgumentLine(MkvFileHandle handle)
        {
            int? wantedAudioTrackNb = null;
            int? wantedSubTrackNb = null;
            if (settings.AudioLanguage != null)
            {
                wantedAudioTrackNb = SwapTrackNumberForType(TrackType.Audio, settings.AudioLanguage, handle);
            }

            if (settings.SubtitlesLanguage != null)
            {
                wantedSubTrackNb = SwapTrackNumberForType(TrackType.Subtitles, settings.SubtitlesLanguage, handle);
            }

            // command line:
            // mkvmerge --output outputFileName fileName --track-order 0:0,0:3,0:2,0:1,0:6,0:5,0:4...

            var argsBuilder = new StringBuilder();

            settings.OutputPath = settings.OverrideFile ? Path.GetTempFileName() : CreateOutputFileName(handle);
            argsBuilder.Append($" --output \"{settings.OutputPath}\" \"{handle.FileInfo.FullName}\"");

            argsBuilder.Append(" --track-order ");
            var trackArgs = new List<string>();
            foreach (var track in handle.Tracks)
            {
                trackArgs.Add($"0:{track.TrackNumber - 1}"); // mkvmerge use index starting at 0, mkvinfo at 1 hence the - 1
            }

            argsBuilder.Append($"{string.Join(',', trackArgs)}"); // build the value for parameter track-order: 0:0,0:3,0:2,0:1...

            if (wantedAudioTrackNb.HasValue)
            {
                argsBuilder.Append($" --default-track {wantedAudioTrackNb.Value - 1}:1 ");
            }

            if (wantedSubTrackNb.HasValue)
            {
                argsBuilder.Append($" --default-track {wantedSubTrackNb.Value - 1}:1 ");
            }

            return argsBuilder.ToString();
        }

        private string CreateOutputFileName(MkvFileHandle handle)
        {
            var fullPathWithoutExtension = Path.ChangeExtension(handle.FileInfo.FullName, null);

            var newFileName = $"{fullPathWithoutExtension}_swapped{handle.FileInfo.Extension}";

            uint index = 1;
            while (File.Exists(newFileName))
            {
                newFileName = $"{fullPathWithoutExtension}_swapped_{index}{handle.FileInfo.Extension}";
                index++;
            }

            return newFileName;
        }

        private bool TracksAreLoaded(MkvFileHandle handle)
        {
            if (handle.Tracks.Count == 0)
            {
                Console.WriteLine("NO TRACKS");
                //_logger.LogError($"No track have been found, nothing to do for file {handle.FullName}");
                return false;
            }

            return true;
        }

        private int? SwapTrackNumberForType(TrackType type, string language, MkvFileHandle handle)
        {
            var firstTrack = handle.Tracks.FirstOrDefault(t => t.Type == type);
            var wantedTrack = handle.Tracks.FirstOrDefault(t => t.Type == type && t.Language.StartsWith(language));

            if (firstTrack == null || wantedTrack == null || firstTrack == wantedTrack) // useless if first track is already of wanted language
            {
                return null;
            }

            var temp = wantedTrack.TrackNumber;
            wantedTrack.TrackNumber = firstTrack.TrackNumber;
            firstTrack.TrackNumber = temp;

            return firstTrack.TrackNumber;
        }

        private class Settings
        {
            public string AudioLanguage { get; set; }
            public string SubtitlesLanguage { get; set; }
            public bool OverrideFile { get; set; }
            public string OutputPath { get; set; }
        }
    }
}