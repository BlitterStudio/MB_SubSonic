# MB_SubSonic
MusicBee SubSonic plugin

Description
===========
With this plugin MusicBee acts as a Subsonic player.
Based on the original work by Steven Mayall. The code was converted from VB.NET to C# with the aim to to futher develop, the plugin, since Steven did not wish to continue working on it.

Any bugs or improvement suggestions can be reported in the Issues section.

Requirements
============
- MusicBee (v2.x or 3.x beta) - get it from http://getmusicbee.com
- .NET Framework 4.5.2 - normally delivered through Windows Update automatically
- Subsonic server (tested with v5.x and 6.x beta) - more information at http://www.subsonic.org

Installation
============
- Download the latest binary file from the Releases section.
- Extract the archive and copy the DLL file in your MusicBee "plugins" folder (the default is "C:\Program Files (x86)\MusicBee\Plugins\"). 
- (Re)start MusicBee.
- Go to Preferences -> Plugins to configure it to connect to your Subsonic server.

Configuration
=============
You will find the configuration section in MusicBee's "Preferences" window -> Plugins.
The necessary information you need to provide is "Hostname", "Port", "Username" and "Password".
You can optionally specify a "Path" as well, if your Subsonic server needs one. Otherwise the default "/" will be used.
