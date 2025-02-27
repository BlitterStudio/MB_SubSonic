[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/X8X4FHDY4)

# MB_SubSonic

MusicBee SubSonic plugin

Description
===========

With this plugin MusicBee acts as a Subsonic player.
In simple terms, that means that you can use MusicBee to stream your music from a Subsonic server you have access to (for example, a home server you setup or a friend's server you have access to, etc). You will need to have a Subsonic server somewhere of course, for this to work.

Subsonic (<www.subsonic.org>) is a free streaming media server that you can install and have it manage your collection. You can access that collection using multiple "players", including a web front-end (which comes with Subsonic), smartphone players (e.g. Android, iPhone), and desktop ones. With this plugin, MusicBee also gets this functionality added.

Any bugs or improvement suggestions can be reported in the Issues section.

Requirements
============

- MusicBee (v2.x or 3.x) - get it from <https://getmusicbee.com>

- .NET Framework 4.8 - normally delivered through Windows Update automatically

- Subsonic compatible server - more information at <https://www.subsonic.org>

Installation
============

- Download the latest binary file from the Releases section.

- Extract the archive and copy the DLL file in your MusicBee "plugins" folder (the default is "C:\Program Files (x86)\MusicBee\Plugins\"). Alternatively, use MusicBee's "Add Plugin" button and browse to either the ZIP archive or the extracted .DLL file. MusicBee will perform the copy for you, in this case.

- (Re)start MusicBee.

- Go to Preferences -> Plugins to configure it to connect to your Subsonic server.

Configuration
=============

You will find the configuration section in MusicBee's "Preferences" window -> Plugins.
The minimum necessary information you will need to provide is "Hostname", "Port", "Username" and "Password" for your Subsonic compatible server.
You can optionally specify a "Path" as well, if your Subsonic server needs one. Otherwise the default "/" will be used.

Usage
=============

In the tree menu on the left side there will appear a new subsonic menu item.
Simply click on it to access all the files.

Depending on what Browse mode you chose in the plugin Settings, the contents will be either displayed by Tags (Artist\Album\Songs) or by Directories (replicating how your Subsonic server sees the files, e.g. Rock\ACDC\High Voltage).
