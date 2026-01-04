# TS3BuildTool
A small C# utility for The Sims 3 modding that automatically imports your compiled script and resources into a Sims 3 .package file immediately after building in your IDE. 

## How it works
This tool removes the need to manually open S3PE after every compile to import .dll into the S3SA resource and also simplifies resource adding so you don't need to use S3PE anymore.

1. **Build**: Build your project in your IDE.
2. **Locate**: The tool looks for an existing package in your `Mods` folder.
3. **Recreate**: 
   * If a package is found, the tool targets that specific path to overwrite it.
   * If no package exists, it defines a new path in your `Mods` folder (defaults to a `Packages` subfolder).
   * The tool then deletes the old file and creates a fresh package at that location.
4. **Add**: It reads your `nameMap.xml` and imports all listed resources from your `Resources` folder.
5. **Inject**: It takes your freshly compiled DLL and injects it into the `S3SA` resource automatically.

## Setup
1. Download the latest release from the [Releases page](https://github.com/Paprotk/ts3buildtool/releases).
2. Extract the **Tools** folder directly into your project's solution directory.
3. Extract the **Resources** folder from the ZIP into your **Project directory** (next to your `.csproj` file).
   * This folder contains `nameMap.xml` and `nameMap.xsd`. 
   * Keeping them here allows some IDEs to provide autocomplete and documentation popups automatically.
4. In your Mod project, go to **Properties > Build Events > Post-build event** and paste:
   
   `"$(SolutionDir)Tools\ts3buildtool.exe" -modName="YourModName" -dllPath="$(TargetPath)"`
   
   See Command Line Arguments section below for all possible parameters
   
5. Change **modName** to your preferred filename; this name is used as the name for the created package file.


## Command Line Arguments
The tool uses named parameters, so the order of arguments does not matter.

| Parameter | Description | Example |
| :--- | :--- | :--- |
| **-modName** | The filename of your package (without .package). | `-modName="Arro_MCR"` |
| **-dllPath** | Path to the compiled assembly. Use the IDE macro. | `-dllPath="$(TargetPath)"` |
| **-defaultPath** | *(Optional)* Sub-folder in Mods to create package if not found. | `-defaultPath="Packages/MyMod"` |
| **-skip** | *(Optional)* Folders to ignore when searching the Mods directory. | `-skip="Backups,Old"` |

## Credits & Legal
* This tool uses the **s3pi** library (Copyright © 2009 Peter L Jones).
* Distributed under the GPLv3 license as a derivative work.
