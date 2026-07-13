# CustomInspectors
[![Thunderstore Badge](https://modding.resonite.net/assets/available-on-thunderstore.svg)](https://thunderstore.io/c/resonite/)

A [Resonite](https://resonite.com/) mod that allows you to use custom inspector panels by saving them and favoriting them in your inventory.<br>
Make sure to wait for it to finish syncing before favoriting or it won't work!<br>
The `SceneInspector` component MUST be on the top level slot of the inspector, but most should be doing that anyway.

## Installation (Manual)
1. Install [BepisLoader](https://github.com/ResoniteModding/BepisLoader) for Resonite.
2. Download the latest release ZIP file (e.g., `art0007i-CustomInspectors-1.0.0.zip`) from the [Releases](https://github.com/art0007i/CustomInspectors/releases) page.
3. Extract the ZIP and copy the `plugins` folder to your BepInEx folder in your Resonite installation directory:
   - **Default location:** `C:\Program Files (x86)\Steam\steamapps\common\Resonite\BepInEx\`
4. Start the game. If you want to verify that the mod is working you can check your BepInEx logs.

## Building

If using a mod manager (such as gale or r2), it's recommended to symlink `Resonite/BepInEx` folder to your mod manager profile's BepInEx folder.

In my case it was
```bash
cd $HOME/.steam/root/steamapps/common/Resonite
ln -s $HOME/.local/share/com.kesomannen.gale/resonite/profiles/Default/BepInEx BepInEx
```

This way it should pick up the dependancy SpecialItemsLib.dll automatically
