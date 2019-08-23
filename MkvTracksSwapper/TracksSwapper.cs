using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MkvTracksSwapper
{
    public class TracksSwapper
    {
        private readonly ILogger _logger;
        private readonly FileInfo _fileInfo;
        private List<Track> _tracksList;
        private bool _tracksLoaded;

        public TracksSwapper(FileInfo fileInfo, ILogger logger)
        {
            _logger = logger;
            _fileInfo = fileInfo;
            _tracksList = new List<Track>();
            _tracksLoaded = false;
        }

        public void ReadTracks()
        {
            ProcessStartInfo processStartInfo = BuildProcessStartInfo("mkvinfo");
            processStartInfo.Arguments = $"\"{_fileInfo.FullName}\"";

            try
            {
                using Process mkvInfoProcess = Process.Start(processStartInfo);
                mkvInfoProcess.WaitForExit();

                if (mkvInfoProcess.ExitCode != 0)
                {
                    _logger.LogError(mkvInfoProcess.StandardOutput.ReadToEnd());
                    _tracksLoaded = false;

                    return;
                }

                string line;
                Track currentTrack = null;
                // TODO: remove string parsing and do it with a deserializer
                while ((line = mkvInfoProcess.StandardOutput.ReadLine()) != null)
                {
                    if (line == "| + Track")
                    {
                        if (currentTrack != null)
                            _tracksList.Add(currentTrack);

                        currentTrack = new Track();
                    }
                    else if (line.StartsWith("|  + Track number:"))
                        currentTrack.TrackNumber = Int32.Parse(GetPropertyValue(line));
                    else if (line.StartsWith("|  + Track UID:"))
                        currentTrack.UID = GetPropertyValue(line);
                    else if (line.StartsWith("|  + Track type:"))
                        currentTrack.Type = Enum.Parse<TrackType>(GetPropertyValue(line), ignoreCase: true);
                    else if (line.StartsWith("|  + Language:"))
                        currentTrack.Language = GetPropertyValue(line);
                }

                if (currentTrack != null)
                    _tracksList.Add(currentTrack);

                _tracksLoaded = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                _tracksLoaded = false;
            }
        }

        public void SwapTracks(string audioTrack, string subtitlesTrack, bool overrideFile = false)
        {
            if (_tracksList.Count == 0)
            {
                _logger.LogError($"No track have been found, nothing to do for file {_fileInfo.FullName}");
                return;
            }

            if (!_tracksLoaded)
            {
                _logger.LogError($"Tracks haven't been loaded for file {_fileInfo.FullName}, cannot switch.");
                return;
            }

            var wantedAudioTrackNb = -1;
            var wantedsubTrackNb = -1;
            if (audioTrack != null)
                wantedAudioTrackNb = SwapTrackNumberForType(TrackType.Audio, audioTrack);
            if (subtitlesTrack != null)
                wantedsubTrackNb = SwapTrackNumberForType(TrackType.Subtitles, subtitlesTrack);

            ProcessStartInfo processStartInfo = BuildProcessStartInfo("mkvmerge");

            // command line:
            // mkvmerge --output outputFileName fileName --track-order 0:0,0:3,0:2,0:1,0:6,0:5,0:4...

            var argsBuilder = new StringBuilder();

            var outputName = overrideFile ? Path.GetTempFileName() : CreateOutputFileName();
            argsBuilder.Append($" --output \"{outputName}\" \"{_fileInfo.FullName}\"");

            argsBuilder.Append(" --track-order ");
            var trackArgs = new List<string>();
            foreach (var track in _tracksList)
                trackArgs.Add($"0:{track.TrackNumber - 1}"); // mkvmerge use index starting at 0, mkvinfo at 1 hence the - 1
            argsBuilder.Append($"{String.Join(',', trackArgs)}"); // build the value for parameter track-order: 0:0,0:3,0:2,0:1...

            if (wantedAudioTrackNb != -1)
                argsBuilder.Append($" --default-track {wantedAudioTrackNb} ");
            if (wantedsubTrackNb != -1)
                argsBuilder.Append($" --default-track {wantedsubTrackNb} ");

            processStartInfo.Arguments = argsBuilder.ToString();

            try
            {
                using Process mkvPropEditProcess = Process.Start(processStartInfo);
                mkvPropEditProcess.WaitForExit();

                if (mkvPropEditProcess.ExitCode != 0)
                {
                    _logger.LogError(mkvPropEditProcess.StandardOutput.ReadToEnd());

                    return;
                }

                _logger.LogInformation($"Successfully swapped tracks for file {_fileInfo.FullName}");

                if (overrideFile)
                {
                    File.Delete(_fileInfo.FullName);
                    File.Move(outputName, _fileInfo.FullName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        private string CreateOutputFileName()
        {
            var fullPathWithoutExtension = Path.ChangeExtension(_fileInfo.FullName, null);

            var newFileName = $"{fullPathWithoutExtension}_swapped{_fileInfo.Extension}";

            uint index = 1;
            while (File.Exists(newFileName))
            {
                newFileName = $"{fullPathWithoutExtension}_swapped_{index}{_fileInfo.Extension}";
                index++;
            }

            return newFileName;
        }

        private int SwapTrackNumberForType(TrackType type, string language)
        {
            var firstTrack = _tracksList.FirstOrDefault(t => t.Type == type);
            var wantedTrack = _tracksList.FirstOrDefault(t => t.Type == type && t.Language.StartsWith(language));

            if (firstTrack == null || wantedTrack == null || firstTrack == wantedTrack) // useless if first track is already of wanted language
                return -1;

            var temp = wantedTrack.TrackNumber;
            wantedTrack.TrackNumber = firstTrack.TrackNumber;
            firstTrack.TrackNumber = temp;

            return wantedTrack.TrackNumber;
        }

        private string GetPropertyValue(string line)
        {
            return line.Split(':')[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        private ProcessStartInfo BuildProcessStartInfo(string programName)
        {
            return new ProcessStartInfo()
            {
                CreateNoWindow = false,
                FileName = programName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Environment.CurrentDirectory
            };
        }
    }
}
