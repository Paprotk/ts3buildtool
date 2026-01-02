# TS3BuildTool

## Setup
* Download the latest release from the [Releases page](https://github.com/Paprotk/ts3buildtool/releases).

* Place the executable into a Tools folder within your project solution.

* In your Mod project, go to Properties > Build Events > Post-build event.

* Add the following command: "$(SolutionDir)Tools\ts3buildtool.exe" "YourModName_*" "$(ConfigurationName)"

## Licenses
This project is licensed under the **GNU General Public License v3.0 (GPLv3)**.
