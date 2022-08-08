This repository contains my mods for CVR (Chillout VR). Join the [CVR Modding Group discord](https://discord.gg/gbvQpNhB) for support and more mods!

## Installation

Requirements:
- [MelonLoader](https://github.com/LavaGang/MelonLoader#how-to-use-the-installer)

Then download the .dll mods you want [from here in the release section](https://github.com/dakyneko/DakyModsCVR/releases) which you must place into your `Mods` folder of your game directory (check Steam installation directory).

**Warning**: Mods are provided as is and there is no guarantee of working.

## NoIntro

Always skip Chillout intro when game starts, roughly take 10 seconds.

## Lag Free Screenshots
This mod significantly improves screenshot taking performance for handheld camera in VR and F12 key in desktop mode. Benefits are especially noticeable with higher resolution screenshots (4K/8K).
Additional features:
 * You can set your screenshots to be saved as JPEG files instead of PNG to save on file size.  
 * Automatically rotate screenshots so that proper side faces up (like on your real phone!)
 * Add metadata about world and players to screenshot files (disabled by default; both JPEG and PNG are supported, though PNG metadata is not displayed by Windows - you'll have to use a use different photo viewer software)

Note: This is a [knah mod Lag Free Screenshot](https://github.com/knah/VRCMods) ported from VRC and improved in the version here. Credit to knah for the great work!

![screenshot](dakymods1.jpg?raw=true "Camera Instants and Remote")

## Camera★Remote

Allows to control the camera like a drone, it will fly under your control remotely. To enable this, there is a button "Remote" under the Camera QuickMenu page, a cube will spawn, can grab it and move it to move the camera.

## Camera★Instants

Spawn little vignettes of photo you take in-game. Mimicking the old good instant camera which gave you the photo in a few seconds. Also mimicking Neos VR.


## Building
To build yourself, copy all required .dll libraries listed in `Directory.Build.props` into ManagedLibs/ folder. Basically all from `<cvr dir>/ChilloutVR_Data/Managed` and also Melonloader.dll and 0Harmony.dll in directory above it. Then use Visual Studio 2019 or your IDE of choice to build.

Once the dll copied you will need to run `nstrip_public_dll.sh` which will convert some .dll for easier developpment (make some code public, used by this project). Also you need to add a new environnment variable `CVRPATH` to your Windows which points to your game directory.

## License
With the following exceptions, all mods here are provided under the terms of [GNU GPLv3 license](LICENSE)


# WIP

Mods below were written for VRC and should be adapted to Chillout soon.

## PickupLib

TODO: to port to CVR soon

For developpers. Library that helps spawn and control VRC Pickup.

## Dakytils

TODO: to port to CVR soon

For developpers. Library with lots of useful utilities.
