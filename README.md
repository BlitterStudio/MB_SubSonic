# MB_SubSonic
MusicBee SubSonic/LibreSonic plugin

Description
===========
With this plugin MusicBee acts as a Subsonic player.
In simple terms, that means that you can use MusicBee to stream your music from a Subsonic/LibreSonic server you have access to (for example, a home server you setup or a friend's server you have access to, etc). You will need to have a Subsonic server somewhere of course, for this to work. :)

Subsonic (www.subsonic.org) is a free streaming media server that you can install and have it manage your collection. You can access that collection using multiple "players", including a web front-end (which comes with Subsonic), smartphone players (e.g. Android, iPhone), and desktop ones. With this plugin, MusicBee also gets this functionality added.

Since version 2.9 the plugin also supports LibreSonic servers officially.

Based on the original work by Steven Mayall. The code was converted from VB.NET to C# with the aim to to futher develop, the plugin, since Steven did not wish to continue working on it.

Any bugs or improvement suggestions can be reported in the Issues section.

Requirements
============
- MusicBee (v2.x or 3.x) - get it from http://getmusicbee.com
- .NET Framework 4.5.2 - normally delivered through Windows Update automatically
- Subsonic / LibreSonic server (tested with v5.x and 6.x) - more information at http://www.subsonic.org

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

Please note that the first time it connects, it will scan through your collection and create a local cache. This operation might take a while (depending on the size of your collection and the network speed) during which you won't be able to see anything in the Subsonic node. Please be patient. :)

Usage
=============
In the tree menu on the left side there will appear a new subsonic menu item.
Simply click on it to access all the files.
