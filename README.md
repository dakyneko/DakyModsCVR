This repository contains my mods for CVR (Chillout VR). Join the [CVR Modding Group discord](https://discord.gg/gbvQpNhB) for support and more mods!

# Installation

Requirements:
- [MelonLoader](https://github.com/LavaGang/MelonLoader#how-to-use-the-installer)

Then download the .dll mods you want [from here in the release section](https://github.com/dakyneko/DakyModsCVR/releases) which you must place into your `Mods` folder of your game directory (check Steam installation directory).

**Warning**: Mods are provided as is and there is no guarantee of working.

## Building
To build yourself:

 - (1) Install NStrip.exe from https://github.com/BepInEx/NStrip into this directory (or into your PATH). This tools converts all assembly symbols to public ones. Make life easy!
 - (2) Create a new Windows environnment variable `%CVRPATH%` which should point to your game path (folder where `ChilloutVR.exe` resides). In Windows, look for Settings > Advanced system settings > Advanced > Environment Variables, add a new one there, it should point to something like `C:\Program Files (x86)\Steam\steamapps\common\ChilloutVR` or similar.
 - (3) Run `copy_and_nstrip_dll.bat` this will copy the game + MelonLoader .dll into this project and run NStrip.exe to make them public (easier developpers).

Use Visual Studio 2019 or your IDE of choice to build. Enjoy. Feel free to join the the Discord server for support (in case of errors or questions). Thanks.

# Mods

## NoIntro

Always skip Chillout intro when game starts, roughly take 10 seconds.

## Lag Free Screenshots
No lag when you take screenshots; Pick the resolution up to 16K and the format: JPG, PNG or WebP (and the compression quality); Portrait pictures are automatically rotated like your smartphone does. Optionally you can embed metadata EXIF/ITxT like world info and player list.

Note: This is a [knah mod Lag Free Screenshot](https://github.com/knah/VRCMods) ported from VRC and improved in the version here. Credit to knah for the great work!

### WebP supports

Optionnally you can install some .dll dependency to have directly screenshots saved as .webp. Copy the 3 libwebp\*.dll files found at the root of this project into your game directory (next to ChilloutVR.exe).

For developpers: libwebp and libwebmux are built from the original sources. The libwebpwrapper is my patched version of the C# libwebp wrapper, source here <https://github.com/dakyneko/WebP-wrapper>.

![screenshot](dakymods1.jpg?raw=true "Camera Instants and Remote")

## Camera★Remote

Allows to control the camera like a drone, it will fly under your control remotely. To enable this, there is a button "Remote" under the Camera QuickMenu page, a cube will spawn, can grab it and move it to move the camera.

## Camera★Instants

Spawn little vignettes of photo you take in-game. Mimicking the old good instant camera which gave you the photo in a few seconds. Also mimicking Neos VR.


# License
With the following exceptions, all mods here are provided under the terms of [GNU GPLv3 license](LICENSE)
