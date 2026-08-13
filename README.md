# GGC2LanModule
Adds LAN multiplayer support to Guns, Gore and Cannoli 2.

- Star this repository to stay updated on new releases.
- This project is still in development, so bugs may occur - feel free to open an [issue](https://github.com/ggc2-lan-dev/ggc2-lanmodule/issues/new/choose) if you run into one.

> **Disclaimer:** This is an unofficial fan-made mod for Guns, Gore and Cannoli 2, not affiliated with or endorsed by Rogueside.
> All game assets and trademarks belong to their respective owners.

## Installing the mod
### 1. Downloading files
1. Go to the [releases](https://github.com/ggc2-lan-dev/ggc2-lanmodule/releases) page, select the latest version, and download `GGC2LanModule.dll`.
2. Go to this [link](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) and download the `BepInEx` archive matching your system (default is `BepInEx_win_x64_5.4.23.5.zip` for **Windows**).

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
1. If you want to modify the code, you can download the repository as a `.zip` or clone it using the `git clone` command.
2. In the root folder of the mod project, open the `ggc2-lanmodule/GGC2LanModule/` folder using **Visual Studio**.
3. In the opened project, navigate to the file `ggc2-lanmodule/GGC2LanModule/GGC2LanModule.csproj`. In the code, find the lines `<PropertyGroup><GamePath>...</GamePath></PropertyGroup>` and specify the path to your game's root folder, then save the file.
4. Now you can edit the file `ggc2-lanmodule/GGC2LanModule/ggc2.lanmodule.cs` and compile it. The compiled file will be output to `ggc2-lanmodule/GGC2LanModule/bin/Debug/GGC2LanModule.dll`.
5. You can copy the compiled file and paste it into the game's root folder at `Guns, Gore and Cannoli 2\BepInEx\plugins\`.