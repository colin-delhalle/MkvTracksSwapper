# MkvTracksSwapper

Small command line wrapper around [mkvtoolnix](https://mkvtoolnix.download) tools mkvinfo and mkvmerge to put audio and / or subtitles tracks as first in the mkv file (useful if your device use the first track as default).

You need to have mkvinfo and mkvmerge in your PATH.

Usage:
```
mkvtracksswapper (arguments) [file | folder]   
```

Arguments:
  - `-f`: overwrite source file, if not present the program creates a new file called *originalname_**swapped**.mkv*
  - `-a language`: audio track to set as first
  - `-s language`: subtitles track to set as first
  - `-v`: verbose switch

The `language` must follow the [mkv format for languages](https://www.matroska.org/technical/specs/index.html#languages).
All arguments are optional and can be placed anywhere in the line.
The program accepts file and / or folder name and will search for all mkv files in the folder(s) passed.

Exemples:
`mkvtracksswapper -a eng file.mkv` put english audio track first in `file.mkv`, creating a new file `file_swapped.mkv` in the same folder
`mkvtracksswapper -f -a eng -s fre folderWithMkvFiles` put english audio track and french subtitles first in all mkv files in folder, overwritting them