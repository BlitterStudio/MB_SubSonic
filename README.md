# MB_SubSonic
MusicBee SubSonic plugin
With this plugin MusicBee acts as a Subsonic player.

Requirements
============
- MusicBee (v2.x or 3.x beta) - get it from http://getmusicbee.org
- .NET Framework 4.5.2 - normally delivered through Windows Update automatically
- Subsonic server (tested with v5.x and 6.x beta) - more information at http://www.subsonic.org

Installation
============
Once a release version is ready, all you will need to do is download the binary (DLL) file and copy it in your MusicBee "plugins" folder (the default is "C:\Program Files (x86)\MusicBee\Plugins\"). After that simply (re)start MusicBee.

Configuration
=============
You will find the configuration section in MusicBee's "Preferences" window -> Plugins.
The necessary information you need to provide is "Hostname", "Port", "Username" and "Password".
You can optionally specify a "Path" as well, if your Subsonic server needs one. Otherwise the default "/" will be used.

Note: this readme will be updated and completed as the project evolves :)

Based on the original work by Steven Mayall. The code was converted from VB.NET to C# with the aim to to futher develop, the plugin, since Steven did not wish to continue working on it.

Once a stable and tested version is ready, it will be available in the Releases section.
From that point onwards, any bugs or improvement suggestions can be reported in the Issues section.
