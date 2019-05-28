using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MkvTracksSwapper
{
    class Program
    {
        static void Main(string[] args)
        {
            var serviceProvider = ConfigureDI(args.Contains("-v"));

            var logger = serviceProvider.GetRequiredService<ILoggerProvider>().CreateLogger("MkvTracksSwapper");

            var (audio, subtitles) = GetWantedLanguages(args);
            if (audio == null && subtitles == null)
            {
                logger.LogInformation("No language specified, nothing to do.");
                return;
            }

            var logMessage = "Modifying following files, setting";
            if (audio != null)
                logMessage += $" {audio} as first audio track";
            if (subtitles != null)
                logMessage += $" {subtitles} as first subtitles track";
            logger.LogInformation(logMessage);

            var files = new List<FileInfo>();
            var filesNames = GetMkvFileNames(args);
            foreach (var fileName in filesNames)
            {
                var fileInfo = new FileInfo(fileName);
                files.Add(fileInfo);
                logger.LogInformation(fileInfo.FullName);
            }
            logger.LogInformation($"------------------------------------------{ Environment.NewLine}");

            if (files.Count == 0)
                logger.LogError("No valid file found.");

            foreach (var file in files)
            {
                var trackSwitcher = new TracksSwapper(file, logger);
                trackSwitcher.ReadTracks();
                trackSwitcher.SwapTracks(audio, subtitles, args.Contains("-f"));
            }
        }

        static ServiceProvider ConfigureDI(bool verbose)
        {
            var serviceProvider = new ServiceCollection()
                        .AddTransient<ILogger, ParametrableLogger>()
                        .AddLogging(loggingBuilder =>
                        {
                            loggingBuilder.AddProvider(new ParametrableLoggerProvider(verbose ? LogLevel.Information : LogLevel.Error));
                        })
                        .BuildServiceProvider();

            return serviceProvider;
        }

        static (string audioLanguage, string subtitlesLanguage) GetWantedLanguages(string[] args)
        {
            var indexOfAudioArg = Array.IndexOf(args, "-a");
            var indexOfSubtitlesArg = Array.IndexOf(args, "-s");

            return (args?[indexOfAudioArg + 1], args?[indexOfSubtitlesArg + 1]);
        }

        static List<string> GetMkvFileNames(string[] args)
        {
            var filesNames = new List<string>();
            filesNames = args.Where(arg => File.Exists(arg) && Path.HasExtension("mkv")).ToList();

            foreach (var directory in args.Where(arg => Directory.Exists(arg)))
                filesNames.AddRange(Directory.GetFiles(directory, "*.mkv", new EnumerationOptions() { RecurseSubdirectories = true }));

            return filesNames;
        }
    }
}
