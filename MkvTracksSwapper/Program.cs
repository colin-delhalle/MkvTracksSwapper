using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MkvTracksSwapper.Logging;

namespace MkvTracksSwapper
{
    internal class Program
    {
        private static async Task Main(string[] args)
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
            {
                logMessage += $" {audio} as first audio track";
            }
            if (subtitles != null)
            {
                logMessage += $" {subtitles} as first subtitles track";
            }
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
            {
                logger.LogError("No valid file found.");
            }

            var swapper = new TracksProcessor(audio, subtitles, args.Contains("-f"));

            var taskList = new List<Task>(files.Count);
            foreach (var file in files)
            {
                var fileReader = new FileReader(file);
                var task = Task.Run(async () =>
                {
                    await fileReader
                    .ProcessFile()
                    .ContinueWith(async readTask =>
                    {
                        if (readTask.IsCompletedSuccessfully && readTask.Result != null)
                        {
                            Console.WriteLine("AFTER READ RESULT OK");

                            var success= await swapper.PutTracksFirst(readTask.Result);
                            Console.WriteLine("SWAPPING SUCCESS: " + success);
                        }
                    });
                });

                taskList.Add(task);
                break;
            }

            await Task.WhenAll(taskList);
        }

        private static ServiceProvider ConfigureDI(bool verbose)
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
                filesNames.AddRange(Directory.GetFiles(directory, "*.mkv", new EnumerationOptions { RecurseSubdirectories = true }));

            return filesNames;
        }
    }
}