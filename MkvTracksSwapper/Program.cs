using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

namespace MkvTracksSwapper
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            ConfigureNlog(args.Contains("-v"));

            var logger = LogManager.GetCurrentClassLogger();

            var overwriteFile = args.Contains("-f");
            var (audio, subtitles) = GetWantedLanguages(args);
            if (audio == null && subtitles == null)
            {
                logger.Warn("No language specified, nothing to do.");
                return;
            }

            var logMessage = "Modifying following files, setting:";
            if (audio != null)
            {
                logMessage += $"{Environment.NewLine} - {audio} as default and first audio track";
            }
            if (subtitles != null)
            {
                logMessage += $"{Environment.NewLine} - {subtitles} as default and first subtitles track";
            }
            logger.Info(logMessage);

            if (overwriteFile)
            {
                logger.Info("Files will be overwritten");
            }

            var files = new List<FileInfo>();
            var filesNames = GetMkvFileNames(args);
            foreach (var fileName in filesNames)
            {
                var fileInfo = new FileInfo(fileName);
                files.Add(fileInfo);
                logger.Info(fileInfo.FullName);
            }
            logger.Info($"------------------------------------------{Environment.NewLine}");

            if (files.Count == 0)
            {
                logger.Warn("No valid file found, nothing to do.");
            }

            var taskList = new List<Task>(files.Count);
            using var cts = new CancellationTokenSource();
            foreach (var file in files)
            {
                var fileReader = new FileReader(file);
                var task = Task.Run(async () =>
                {
                    var mkvHandle = await fileReader.ProcessFile(cts.Token);
                    if (mkvHandle == null)
                    {
                        return false;
                    }

                    var swapper = new TracksProcessor(mkvHandle, audio, subtitles, overwriteFile);
                    var successful = await swapper.PutTracksFirst(cts.Token);
                    if (successful)
                    {
                        logger.Info($"Swapped tracks for file {file.FullName}");
                    }
                    else
                    {
                        logger.Warn($"Tracks not swapped for file {file.FullName}");
                    }

                    return successful;
                }, cts.Token);

                taskList.Add(task);
            }

            await Task.WhenAll(taskList);
            logger.Info("All file processed");
        }

        private static void ConfigureNlog(bool verbose)
        {
            var consoleTarget = new ColoredConsoleTarget("ConsoleLogging")
            {
                Layout = Layout.FromString("${message}")
            };

            var fileTarget = new FileTarget("FileLogging")
            {
                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveFileKind = FilePathKind.Relative,
                ConcurrentWrites = true,
                FileName = Layout.FromString("nlog-log.txt"),
                KeepFileOpen = true,
                Layout = Layout.FromString("${longdate}|${threadid}|${level:uppercase=true}|${logger}|${message}")
            };

            LogManager.Configuration = new LoggingConfiguration();
            LogManager.Configuration.AddTarget(consoleTarget);
            LogManager.Configuration.AddTarget(fileTarget);
            LogManager.Configuration.AddRule(verbose ? LogLevel.Trace : LogLevel.Info, LogLevel.Fatal, consoleTarget);
            LogManager.Configuration.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);
            LogManager.ReconfigExistingLoggers();
        }

        private static (string audioLanguage, string subtitlesLanguage) GetWantedLanguages(string[] args)
        {
            var indexOfAudioArg = Array.IndexOf(args, "-a");
            var indexOfSubtitlesArg = Array.IndexOf(args, "-s");

            return (indexOfAudioArg != -1 ? args[indexOfAudioArg + 1] : null,
                    indexOfSubtitlesArg != -1 ? args[indexOfSubtitlesArg + 1] : null);
        }

        private static List<string> GetMkvFileNames(string[] args)
        {
            var filesNames = args.Where(arg => File.Exists(arg) && Path.GetExtension(arg) == ".mkv").ToList();

            foreach (var directory in args.Where(Directory.Exists))
            {
                filesNames.AddRange(Directory.GetFiles(directory, "*.mkv", new EnumerationOptions { RecurseSubdirectories = true }));
            }

            return filesNames;
        }
    }
}