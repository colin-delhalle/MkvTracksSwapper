using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace MkvTracksSwapper
{
    public class TracksProcessor
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly MkvFileHandle handle;
        private readonly Settings settings;

        public TracksProcessor(MkvFileHandle mkvHandle, string audio, string subtitles, bool overrideFile = false)
        {
            handle = mkvHandle;
            settings = new Settings
            {
                AudioLanguage = audio,
                SubtitlesLanguage = subtitles,
                OverrideFile = overrideFile
            };
        }

        public async Task<bool> PutTracksFirst(CancellationToken ct = default)
        {
            if (!TracksAreLoaded())
            {
                return false;
            }

            string argLine;
            try
            {
                argLine = BuildMkvMergeArgumentLine();
                logger.Trace($"Argument line for mkvmerge for file {handle.FileInfo.FullName}: {argLine}");
            }
            catch (Exception e)
            {
                logger.Fatal(e);
                return false;
            }

            using var mkvMergeRunner = new ProcessRunner("mkvmerge");
            var successful = await mkvMergeRunner.RunWithArg(argLine, ct);

            if (successful)
            {
                if (settings.OverrideFile)
                {
                    await MoveFile();
                }
            }
            else
            {
                logger.Fatal($"mkvmerge exited with non zero code: {mkvMergeRunner.Error}");
            }

            return successful;
        }

        private string BuildMkvMergeArgumentLine()
        {
            // command line:
            // mkvmerge --output outputFileName [--default-track 3:yes] [--default-track 4:yes] --track-order 0:0,0:3,0:2,0:1,0:6,0:5,0:4... fileName

            var args = new StringBuilder();

            settings.OutputPath = settings.OverrideFile ? GetTempFile() ?? CreateOutputFileName() : CreateOutputFileName();
            args.Append($" --output \"{settings.OutputPath}\"");

            if (settings.AudioLanguage != null)
            {
                MarkTrackOfTypeAsDefault(TrackType.Audio,settings.AudioLanguage, args);
            }

            if (settings.SubtitlesLanguage != null)
            {
                MarkTrackOfTypeAsDefault(TrackType.Subtitles, settings.SubtitlesLanguage, args);
            }

            args.Append(" --track-order ");
            var orderedTracks = handle.Tracks.OrderBy(t => t.Type).ThenByDescending(t => t.IsDefault);
            var trackArgs = orderedTracks.Select(t => $"0:{t.TrackNumber - 1}"); // mkvmerge use index starting at 0, mkvinfo at 1, so -1
            args.Append($"{string.Join(',', trackArgs)}");

            args.Append($" \"{handle.FileInfo.FullName}\"");

            return args.ToString();
        }

        private void MarkTrackOfTypeAsDefault(TrackType trackType, string language, StringBuilder argsBuilder)
        {
            var tracksSubset = handle.Tracks.Where(t => t.Type == trackType).ToList();
            var trackThatShouldBeFirst = tracksSubset.FirstOrDefault(t => t.Language == language);

            if (trackThatShouldBeFirst != null)
            {
                trackThatShouldBeFirst.IsDefault = true;
                foreach (var tracks in tracksSubset.Where(t => t != trackThatShouldBeFirst))
                {
                    tracks.IsDefault = false;
                }

                argsBuilder.Append($" --default-track {trackThatShouldBeFirst.TrackNumber - 1}:yes "); // mkvmerge use index starting at 0, mkvinfo at 1, so -1
            }
        }

        private string GetTempFile()
        {
            var tmpFolderPath = Path.GetTempPath();
            try
            {
                var tempFile = Path.Combine(tmpFolderPath, Path.GetRandomFileName());

                while (File.Exists(tempFile))
                {
                    tempFile = Path.Combine(tmpFolderPath, Path.GetRandomFileName());
                }

                return tempFile;
            }
            catch (Exception e)
            {
                logger.Trace($"Error creating tmp file: {e.Message}");
            }

            return null;
        }

        private string CreateOutputFileName()
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

        private bool TracksAreLoaded()
        {
            if (handle.Tracks.Count == 0)
            {
                //_logger.LogError($"No track have been found, nothing to do for file {handle.FullName}");
                return false;
            }

            return true;
        }

        private async Task MoveFile()
        {
            await Task.Run(() =>
            {
                try
                {
                    File.Move(settings.OutputPath, handle.FileInfo.FullName, true);
                }
                catch (Exception e)
                {
                    logger.Fatal(e);
                }
            });
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