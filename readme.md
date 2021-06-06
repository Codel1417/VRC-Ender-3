# VRChat Ender 3 Udon 3D Printer

This is a functional 3d printer in VRChat which prints real [Marlin](https://marlinfw.org/docs/gcode/G000-G001.html) gcode files. Print progress is synced over the network.

## Requirements

* VRChat SDK 3 World
* [UdonSharp](https://github.com/MerlinVR/UdonSharp) by Merlin

## How to use

1) Import the project into your Unity Project
2) Place the prefab in your world
    * Do not rescale the prefab
    * Do not rotate the prefab on the 'X' or 'Z' axis
    * Do not commit tax fraud
3) Change the file extention of your '.gcode file to '.txt'
    * You may need to Check 'Show File Extentions' in windows explorer.
4) In the Ender 3 VRC gameobject assign the 'Gcode Text File' variable to your custom gcode '.txt' file.


Compatable Gcode can be generated in any Marlin compatible slicer. Cura with the Ender 3 profile works great.

## What is GCode?

Gcode is a text file that contains commands for a 3D printer to execute. these commands are simple. Go here, Set Temperature, Put plastic between two points.

#### Example Gcode
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
* M73 Set Print Progress
* M104 Set Hotend Temperature
* M106 Set Fan Speed
* M107 Fan Off
* M109 Wait For Hotend Temperature
* M117 LCD Print
* M118 Serial Print
* M140 Set Bed Temperature
* M190 Wait For Bed Temperature

## Notes

* Relative Positioning is not supported
* Print Bed size is 235X 235Y 250Z
* Minimum extrusion temperature is 160C (Cold Extrusion Protection)
* Incaple of filament preasure simulation
* Supports are unnecessary as gravity is a relic of the past

## Credits

* Filament Shader by [Lyuma](https://github.com/lyuma), [Xiexe](https://github.com/Xiexe)
* UdonSharp by [Merlin](https://github.com/MerlinVR/UdonSharp)  