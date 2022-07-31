#!/bin/sh
here="$(readlink -f $(dirname "$0"))"
cd "$CVRPATH"/ChilloutVR_Data/Managed;
for f in Assembly-CSharp.dll Assembly-CSharp-firstpass.dll UnityEngine.CoreModule.dll; do
    # Nstrip is from https://github.com/BepInEx/NStrip
    # Put it into your windows user dir under bin/
    NStrip.exe -p -n "$f" "$here"/ManagedLibs/"$f";
done
