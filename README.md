# GGC2LanModule
Adds LAN multiplayer support to Guns, Gore and Cannoli 2.

- Star this repository to stay updated on new releases.
- This project has stable releases, but if you notice a bug or would like to see a new feature, please open an [issue](https://github.com/ggc2-lan-dev/ggc2-lanmodule/issues/new/choose) and tell us about it.

> [!IMPORTANT]
> This is an unofficial fan-made module/mod for Guns, Gore and Cannoli 2, not affiliated with or endorsed by Rogueside.
> All game assets and trademarks belong to their respective owners.

## Installing the mod
### 1. Downloading files
1. Go to the [releases](https://github.com/ggc2-lan-dev/ggc2-lanmodule/releases) page, select the latest version, and download `GGC2LanModule.dll`.
2. Go to this [link](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.6) and download the `BepInEx` archive matching your system (default is `BepInEx_win_x64_5.4.23.6.zip` for **Windows**).

### 2. Installing on your computer
1. Go to the game's root folder `Guns, Gore and Cannoli 2\` and extract the contents of the `BepInEx` archive directly into it.
2. Launch the game once, wait for the main menu to load, then close the game (this initializes `BepInEx`).
3. Open the `Guns, Gore and Cannoli 2\BepInEx\plugins\` folder and place `GGC2LanModule.dll` there.

## How to connect
1. To play together over LAN, all players need to be on the same local Wi-Fi network. If a player is not physically with you, use [Radmin VPN](https://www.radmin-vpn.com) or similar tools and make sure everyone is connected within the app.
2. Launch the game and wait for the main menu to appear.
3. Click the `LAN` button. The first player should click `HOST LAN SERVER`, and the others should click `FIND LAN SERVER`.
4. You'll then see the familiar online lobby window. Enjoy playing with friends!

## Mod updates
1. Updates to the `GGC2LanModule.dll` file will be released on the [releases](https://github.com/ggc2-lan-dev/ggc2-lanmodule/releases) page. You just need to select the latest version, download it, and replace your current file with the updated one in the `Guns, Gore and Cannoli 2\BepInEx\plugins\` folder, and that's it.

## For developers
This section is for those who want to edit the code.

### 1. Preparation
1. You can download this repository as a `.zip` file or clone it using the `git clone` command:
   ```
   git clone https://github.com/ggc2-lan-dev/ggc2-lanmodule.git
   ```
2. Make sure you have completed the `BepInEx` installation step.
3. The default path to the game is `C:\Program Files (x86)\Steam\steamapps\common\Guns, Gore and Cannoli 2\`. If yours is different, open the repository folder, go to the `GGC2LanModule/` folder, copy the file `GGC2LanModule.csproj.user.example` and paste it with the new name `GGC2LanModule.csproj.user`. Open the copied file and change the path in the `<GamePath></GamePath>` parameter to the correct one (the game's root folder), otherwise the build will fail with an error. This file stays only on your computer and is never committed, because it is listed in `.gitignore`.

### 2. Building
#### 1. dotnet
This method works with any code editor.

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) (version 8 or newer).
2. In the root folder of the repository, run:
   ```
   dotnet build GGC2LanModule/GGC2LanModule.csproj -c Release
   ```
   The compiled file will be located at `GGC2LanModule/bin/Release/GGC2LanModule.dll` (without `-c Release` it will be in `bin/Debug`).
3. During the build, a small `NuGet` package (the .NET Framework 4.7.2 reference assemblies) will be downloaded, so you need an internet connection for the first build.

#### 2. Visual Studio
1. Open the `GGC2LanModule.slnx` file in Visual Studio with the **.NET desktop development** workload installed.
2. Select the `Release` configuration, then click **Build** > **Build Solution**. The compiled file will be located at `GGC2LanModule/bin/Release/GGC2LanModule.dll`.

### 3. Testing and releasing
1. Copy the compiled `GGC2LanModule.dll` file and paste it into the game's folder `Guns, Gore and Cannoli 2\BepInEx\plugins\`.
2. To change the module version, edit the `Version` constant in the file `GGC2LanModule/Properties/AssemblyInfo.cs`. This is where you change the version of the code itself.
3. The main code is located in `GGC2LanModule/ggc2.lanmodule.cs`.