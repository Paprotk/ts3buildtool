# TS3BuildTool
A small C# utility for The Sims 3 modding that automatically imports your compiled script into a Sims 3 package file immediately after building in your IDE. 


## How it works
This tool removes the need to manually open a package editor after every compile. It automates the "Compile -> Inject -> Test" loop.

1.  **Build** your project in Rider or Visual Studio.
2.  **Locate**: The tool finds your new DLL and scans your `Mods` folder for the package.
3.  **Inject**: It swaps the `S3SA` resource automatically.


## Setup
1.  Download the latest release from the [Releases page](https://github.com/Paprotk/ts3buildtool/releases).
2.  Place the `ts3buildtool.exe` into a **Tools** folder within your project solution.
3.  In your Mod project, go to **Properties > Build Events > Post-build event** and paste:
    `"$(SolutionDir)Tools\ts3buildtool.exe" "YourModName_*" "$(ConfigurationName)"`


## Command Line Arguments
Arguments must be passed in this specific order:

| Argument | Description | Example |
| :--- | :--- | :--- |
| **Mod Name** | Name of your package. Supports `*` wildcards. | `"Arro_MCR_2.*"` |
| **Config** | The build folder to look in (Debug/Release). | `"$(ConfigurationName)"` |
| **Skip Folders** | *(Optional)* Folders to ignore in your Mods directory. | `"Backups,Old"` |


## Licenses
This project is licensed under the **GNU General Public License v3.0 (GPLv3)**.
