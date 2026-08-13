# TS3BuildTool
A small C# utility for The Sims 3 modding that automatically imports your compiled script and resources into a Sims 3 `.package` file immediately after building in your IDE.

## How it works

1. **Build**: Build your project in your IDE.
2. **Locate**: The tool looks for an existing package in your `Mods` folder.
3. **Recreate**: 
   * If a package is found, the tool targets that specific path to overwrite it.
   * If no package exists, it defines a new path in your `Mods` folder (defaults to a `Packages` subfolder unless `-defaultPath` is specified).
   * The tool then deletes the old file and creates a fresh package at that location.
4. **Add**: It reads your `nameMap.xml` and imports all listed resources from your `resources` folder and its subfolders.
5. **Inject**: It takes your freshly compiled DLL and injects it into the `S3SA` resource automatically.
6. **Localize**: It supports automatic injection of localized string tables (`.stbl`) by parsing your resource directory and mapping them to correct language codes based on their filenames.

## Setup
1. Download the latest release from the [Releases page](https://github.com/Paprotk/ts3buildtool/releases).
2. Extract the **Tools** folder directly into your project's solution directory.
3. Extract the **Resources** folder from the ZIP into your **Project directory** (next to your `.csproj` file).
   * This folder contains `nameMap.xml` and `nameMap.xsd`. 
   * Keeping them here allows some IDEs to provide autocomplete and documentation popups automatically.
   
    *See Project Structure Example section below*
4. In your Mod project, go to **Properties > Build Events > Post-build event** and paste:
   
   `"$(SolutionDir)Tools\ts3buildtool.exe" -modName="YourModName" -dllPath="$(TargetPath)" -projectDir="$(ProjectDir)"`
   
5. Change **modName** to your preferred filename; this name is used as the name for the created package file.

## Project Structure Example
```markdown
.
├── YourSolution.sln
├── Tools/
│   └── ts3buildtool.exe    <-- The tool
└── YourProject/
    ├── YourProject.csproj
    ├── resources/          
    │   ├── nameMap.xml
    │   ├── nameMap.xsd
    │   ├── XML/            
    │   │   └── Tuning.xml
    │   ├── UI/
    │   │   └── Layout.layout
    │   ├── Strings/
    │   │   ├── MyMod_ENG_US.stbl
    │   │   └── MyMod_FRE_FR.stbl
    │   └── Icon.png
    └── Scripts/
        └── MyModScript.cs
```

### `nameMap.xml` Example:
```xml
<resources>
    <!-- S3SA (Script) to be added -->
    <resource name="YourNamespace.YourClassName" type="0x073FAA07" />
    
    <!-- XML containing a [Tunable] field -->
    <resource name="YourNamespace.YourClassName.YourStaticConstructor" type="0x0333406C" />
    
    <!-- Standard Resources -->
    <resource name="Tuning" type="0x0333406C" />
    <resource name="Layout" type="0x025C95B6" />
    <resource name="Icon" type="0x2F7D0004" />

    <!-- STBL Wildcard for Localization -->
    <resource name="*.stbl" type="0x220557DA" />
</resources>
```

## Localization (STBL) Support
The tool can automatically pack multiple string tables for your mod and generate correct Instance IDs based on the locale language code and your mod's name hash.
1. Save your `.stbl` files ending with a valid Sims 3 locale code (e.g., `_ENG_US`, `_SPA_ES`, `_POL_PL`).
2. Add `<resource name="*.stbl" type="0x220557DA" />` to your `nameMap.xml`.
3. The tool will find all matching files in your `resources` folder and inject them properly aligned with your mod's base hash.

## Command Line Arguments
The tool uses named parameters, so the order of arguments does not matter.

| Parameter | Description | Example |
| :--- | :--- | :--- |
| **modName** | The filename of your package (without `.package`). | `-modName="Arro_MCR"` |
| **dllPath** | Path to the compiled assembly. Use the IDE macro. | `-dllPath="$(TargetPath)"` |
| **projectDir** | *(Optional)* Explicit path to the project directory containing your `resources` folder. If omitted, the tool attempts to auto-discover it. | `-projectDir="$(ProjectDir)"` |
| **defaultPath** | *(Optional)* Sub-folder in the `Mods` directory to create the package if not found. | `-defaultPath="Packages/MyMod"` |
| **skip** | *(Optional)* Folders to ignore when searching the `Mods` directory (comma separated). | `-skip="Backups,Old"` |

## Credits & Legal
* This tool uses the **s3pi** library (Copyright © 2009 Peter L Jones).
* Distributed under the GPLv3 license as a derivative work.
