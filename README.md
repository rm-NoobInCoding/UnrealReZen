# UnrealReZen
![GitHub Stars](https://img.shields.io/github/stars/rm-NoobInCoding/UnrealUnZen) ![GitHub Forks](https://img.shields.io/github/forks/rm-NoobInCoding/UnrealUnZen) [![build and test](https://github.com/rm-NoobInCoding/UnrealUnZen/actions/workflows/dotnet-desktop.yml/badge.svg)](https://github.com/rm-NoobInCoding/UnrealUnZen/actions/workflows/dotnet-desktop.yml) [![Github All Releases](https://img.shields.io/github/downloads/rm-NoobInCoding/UnrealUnZen/total.svg)]()

A tool for creating and packing Unreal Engine .Utoc and .Ucas files.

## How it works
First, it is better to know how ZenLoader files work
ZenLoader consists of two parts
- .utoc, which stands for Unreal table of contents, contains the information of the assets, such as ID, offset, size, etc.
- .ucas, which contains the content of Assets in compressed or raw form

The important point of this structure is that you cannot add a new Asset to the game because each Asset contains a unique ID. And due to this unique ID, the tool must first read the game archives and then create the new archive.

The tool checks the game archives using the [CUE4Parse](https://github.com/FabianFG/CUE4Parse) library and after receiving the required information of the Assets, it creates a patch based on your edited files.
## Usage

```console
> UnrealReZen.exe --help
UnrealReZen 1.0.0
Copyright (C) 2024 UnrealReZen
USAGE:
Making a patch for a ue5 game:
  UnrealReZen.exe --content-path C:/Games/MyGame/ExportedFiles --compression-format Zlib --engine-version GAME_UE5_1
  --game-dir C:/Games/MyGame --output-path C:/Games/MyGame/TestPatch_P.utoc

  -g, --game-dir          Required. Path to the game directory (for loading UCAS and UTOC files).

  -c, --content-path      Required. Path of the content that the you want to pack.

  -e, --engine-version    Required. Unreal Engine version (e.g., GAME_UE4_0).

  -o, --output-path       Required. Path (including file name) for the packed utoc file.

  -a, --aes-key           AES key of the game (only if its encrypted)

  --compression-format    (Default: Zlib) Compression format (None, Zlib, Oodle, LZ4).

  --mount-point           (Default: ../../../) Mount point of packed archive

  --help                  Display this help screen.

  --version               Display version information.
```
Some important notes :
- You DON'T have to pack the WHOLE ARCHIVE that you wanna patch! just put the assets that you wanna patch
- This DOESN'T support extracting assets from ZenLoader archives. use [FModel](https://github.com/4sval/FModel).
- This tool supports multi archive patching. check example section.
- For games that have archive signature (.sig file for each utoc) this tool doesn't work until you bypass the sig loader.
- Utoc structure can be different in games (unlikely) and your patch may not be loaded by the game and I don't have enough time to support all the games in the world.



## Examples
Lets mod a game. For example i wanna mod two assets in "The Casting of Frank Stone".
- `pakchunk3-Windows.utoc/SMG037UE5/Content/Animations/Cinematics/curiosity_howmuch.uasset`
- `pakchunk7-Windows.utoc/SMG037UE5/Content/Animations/Cinematics/warning_reset_CHRISEND_CHILD_BODY.uasset`

I will export the assets with FModel, edit them and put them in a folder that I named MyPatchedContent (with same path root for each asset).
- `MyPatchedContent/SMG037UE5/Content/Animations/Cinematics/curiosity_howmuch.uasset`
- `MyPatchedContent/SMG037UE5/Content/Animations/Cinematics/warning_reset_CHRISEND_CHILD_BODY.uasset`

Now Because the game is UE 5.1 and its not encrypted I run the tool with this args :

```
UnrealReZen.exe --content-path C:/TheCastingofFrankStone/MyPatchedContent --compression-format Zlib --engine-version GAME_UE5_1 --game-dir C:/TheCastingofFrankStone/SMG037UE5/Content/Paks  --output-path C:/TheCastingofFrankStone/SMG037UE5/Content/Paks/MyPatch_P.utoc
```
This will make three files (MyPatch_P.utoc / MyPatch_P.ucas / MyPatch_P.pak) in the "C:/TheCastingofFrankStone/SMG037UE5/Content/Paks/" folder. Now I can run the game!

*To find out the version of Unreal Engine used in a game, just check the version of the bootstap executable file of the game (executable file that is in the root of the game folder)
## Contributing

We welcome contributions from the modding community to enhance UnealReZen's functionality and support for various Unreal Engine games. Feel free to fork the repository, make improvements, and submit pull requests.



## License

[GNU General Public License v3.0](https://choosealicense.com/licenses/gpl-3.0/)

