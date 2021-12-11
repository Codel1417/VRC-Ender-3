# VRChat Ender 3 Udon 3D Printer

This is a functional 3d printer in VRChat which prints real [Marlin](https://marlinfw.org/docs/gcode/G000-G001.html) gcode files. Print progress is synced over the network.

## Requirements

* [UdonSharp](https://github.com/MerlinVR/UdonSharp) by Merlin

## How to use

1) Download the newest Release from [here](https://github.com/Codel1417/VRC-Ender-3/releases)
   * Please do not use the zip file, but the UnityPackage.
2) Import the project into your Unity Project.
3) Place the prefab in your world.

* The Next Few instructions are for adding your own GCode files.

4) Change the file extension of your '.gcode file to '.txt'.
    * You may need to Check 'Show File Extensions' in windows explorer.
5) Duplicate a GameObject under ``Ender_3_VRC/GCode Files`` and set its name to your File Name.
6) Set the ``File`` variable to your GCode TextAsset '.txt'.

Compatible GCode can be generated in any Marlin compatible slicer. Ultimaker Cura with the Ender 3 profile works great.

## What is GCode?

GCode is a text file that contains commands for a 3D printer to execute. these commands are simple. Go here, Set Temperature, Put plastic between two points.

#### Example GCode
```
M190 S65 ; Set Bed Temperature to 65C
M109 S225 ; Set Hotend Temperature to 225C
G1 X0.1 Y20 Z0.3 F5000.0 ; Move to start position
G1 X0.1 Y200.0 Z0.3 F1500.0 E15 ; Draw the first line
G1 X0.4 Y200.0 Z0.3 F5000.0 ; Move to side a little
G1 X0.4 Y20 Z0.3 F1500.0 E30 ; Draw the second line
```

### Supported GCode commands

* G0/G1 Linear Move
* G28 Auto Home
* G90 Absolute Positioning
* M25 Pause
* M73 Set Print Progress
* M82 Relative Positioning
* M104 Set Hotend Temperature
* M106 Set Fan Speed
* M107 Fan Off
* M109 Wait For Hotend Temperature
* M117 LCD Print
* M118 Serial Print
* M140 Set Bed Temperature
* M190 Wait For Bed Temperature

## Notes

* Print Bed size is 235X 235Y 250Z.
* Minimum extrusion temperature is 160C (Cold Extrusion Protection).
* Incapable of filament pressure simulation.
* Supports are unnecessary as gravity is a relic of the past.
* Please do not remove any credits given, Just append to the credits page.

## Credits

* Filament Shader by [Lyuma](https://github.com/lyuma)
* Lighting Template by [Xiexe](https://github.com/Xiexe)
* UdonSharp by [Merlin](https://github.com/MerlinVR/UdonSharp)  
