# MCToCMZSchemConverter

A C# console tool for converting Minecraft WorldEdit `.schem` files into CastleMiner Z WorldEdit schematics using a customizable JSON block map.

This tool lets you map Minecraft blocks to CastleMiner Z blocks, such as:

```text
minecraft:acacia_log      -> Log
minecraft:twisting_vines  -> Leaves
minecraft:stone           -> Rock
minecraft:air             -> Empty
```

It is designed for use with CastleMiner Z WorldEdit-style schematics used by CastleForge tooling.

---

## Features

- Converts Minecraft WorldEdit/Sponge `.schem` files into CastleMiner Z WorldEdit schematics.
- Reads Minecraft schematic NBT data using `fNbt`.
- Supports Minecraft palette-based schematic data.
- Converts Minecraft block names and block states through a JSON block map.
- Supports user-friendly block map keys like:
  - `minecraft:acacia_log`
  - `acacia_log`
  - `Acacia Log`
  - `minecraft:torch[facing=east]`
- Writes CastleMiner Z WorldEdit schematic files using the custom `WES` binary format.
- Generates an `.unmapped.txt` report for Minecraft blocks that were not found in the block map.
- Optional `--save-air` mode for writing `Empty` blocks into the output schematic.

---

## Requirements

- Windows
- .NET Framework 4.8.1
- `fNbt.dll`
- `System.Text.Json.dll` and its required dependency DLLs

The converter is currently built as a C# console application.

---

## Project Structure

```text
MCToCMZSchemConverter/
│
├── block-map.json
├── MCToCMZSchemConverter.csproj
├── Program.cs
│
├── CastleMinerZ/
│   ├── CmzBlockType.cs
│   └── CmzSchematicWriter.cs
│
├── Mapping/
│   └── BlockMap.cs
│
├── Minecraft/
│   └── MinecraftSchematicReader.cs
│
└── ReferenceAssemblies/
    ├── fNbt.dll
    ├── System.Memory.dll
    └── System.Text.Json.dll
```

---

## Usage

```bat
MCToCMZSchemConverter.exe <input.schem> <output.schem> <block-map.json>
```

Example:

```bat
MCToCMZSchemConverter.exe house.schem house_cmz.schem block-map.json
```

This reads:

```text
house.schem
```

and writes:

```text
house_cmz.schem
```

using the mappings from:

```text
block-map.json
```

---

## Save Air Mode

By default, blocks mapped to `Empty` are skipped.

This prevents converted air blocks from overwriting existing CastleMiner Z terrain when pasted.

To force `Empty` blocks to be written into the schematic, use:

```bat
MCToCMZSchemConverter.exe house.schem house_cmz.schem block-map.json --save-air
```

Use this when you want the converted schematic to erase blocks during paste.

---

## Preserve Origin Mode

By default, converted schematics paste from the minimum corner of the schematic.

```bat
MCToCMZSchemConverter.exe house.schem house_cmz.schem block-map.json
```

To preserve the Minecraft/Sponge schematic paste offset, use:

```bat
MCToCMZSchemConverter.exe house.schem house_cmz.schem block-map.json --preserve-origin
```

This reads the Minecraft/Sponge `Offset` tag and converts it into the CastleMiner Z WorldEdit `CopyAnchorOffset`.

This makes the converted schematic paste closer to how it would paste in Minecraft WorldEdit relative to the point where it was copied.

You can combine it with `--save-air`:

```bat
MCToCMZSchemConverter.exe house.schem house_cmz.schem block-map.json --save-air --preserve-origin
```

### Notes

Minecraft/Sponge and CastleMiner Z store this offset in opposite directions.

Minecraft/Sponge:

```text
first block = paster position + Offset
```

CastleMiner Z WorldEdit:

```text
first block = player position - CopyAnchorOffset
```

So the converter writes the negative of the Minecraft/Sponge offset into the CMZ schematic. house.schem house_cmz.schem block-map.json

---

## Drag-and-Drop Batch File

Dragging a `.schem` directly onto the `.exe` does not work by default because the converter requires three arguments:

```text
<input.schem> <output.schem> <block-map.json>
```

Use a batch file for drag-and-drop support.

Create a file named:

```text
ConvertToCMZ.bat
```

next to the `.exe`:

```bat
@echo off
setlocal

if "%~1"=="" (
    echo Drag a Minecraft .schem file onto this batch file.
    pause
    exit /b 1
)

set "INPUT=%~1"
set "EXE=%~dp0MCToCMZSchemConverter.exe"
set "MAP=%~dp0block-map.json"

REM Output folder next to this batch/exe.
set "OUTPUT_DIR=%~dp0output"

REM Converted file name.
set "OUTPUT=%OUTPUT_DIR%\%~n1_cmz.schem"

REM Create output folder if it does not exist.
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

"%EXE%" "%INPUT%" "%OUTPUT%" "%MAP%"

echo.
echo Converted schematic saved to:
echo "%OUTPUT%"
echo.
pause
```

Now you can drag a Minecraft `.schem` file onto `ConvertToCMZ.bat`.

Converted files will be saved to:

```text
output/
```

Example:

```text
input:
house.schem

output:
output/house_cmz.schem
```

---

## Tools

The `Tools/` folder contains helper scripts for maintaining and updating `block-map.json`.

These tools are not required for normal schematic conversion, but they make it easier to generate Minecraft block id lists and quickly fill large block maps with reasonable CastleMiner Z defaults.

```text
Tools/
├── AutoFillBlockMap.ps1
└── DumpMinecraftBlockIds.bat
````

---

### DumpMinecraftBlockIds.bat

`DumpMinecraftBlockIds.bat` generates a plain text list of Minecraft block ids from Minecraft's extracted `blockstates` folder.

Minecraft blockstates are stored inside the Minecraft Java Edition client jar at:

```text
assets/minecraft/blockstates
```

Each `.json` file in that folder represents one Minecraft block id.

Example:

```text
acacia_log.json      -> minecraft:acacia_log
twisting_vines.json  -> minecraft:twisting_vines
stone.json           -> minecraft:stone
oak_stairs.json      -> minecraft:oak_stairs
```

#### Usage

1. Open or extract the Minecraft Java Edition jar you want to support.

Typical jar location:

```text
%APPDATA%\.minecraft\versions\<version>\<version>.jar
```

2. Extract this folder from the jar:

```text
assets\minecraft\blockstates
```

3. Copy `DumpMinecraftBlockIds.bat` into the extracted `blockstates` folder.

4. Run the batch file.

The script will create:

```text
minecraft-block-ids.txt
```

containing entries like:

```text
minecraft:acacia_log
minecraft:twisting_vines
minecraft:stone
minecraft:oak_stairs
```

#### Notes

Use the newest Minecraft jar when updating the main `block-map.json`.

Use an older jar only if you specifically want to support or compare against an older Minecraft version.

This tool only dumps base block ids. It does not generate every possible block state combination, such as:

```text
minecraft:oak_stairs[facing=north,half=bottom,shape=straight,waterlogged=false]
```

---

### AutoFillBlockMap.ps1

`AutoFillBlockMap.ps1` automatically fills blank or existing Minecraft mapping entries with guessed CastleMiner Z block types.

It is useful after generating or expanding `block-map.json` with a large Minecraft block list.

Example input:

```json
"minecraft:oak_log": "",
"minecraft:oak_leaves": "",
"minecraft:stone": "",
"minecraft:glass": "",
"minecraft:water": ""
```

Example output:

```json
"minecraft:oak_log": "Log",
"minecraft:oak_leaves": "Leaves",
"minecraft:stone": "Rock",
"minecraft:glass": "GlassMystery",
"minecraft:water": "Empty"
```

#### Usage

From the folder containing `block-map.json`, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AutoFillBlockMap.ps1
```

By default, this reads:

```text
block-map.json
```

and writes:

```text
block-map.autofilled.json
```

You can also pass custom paths:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\AutoFillBlockMap.ps1 -InputPath ".\block-map.json" -OutputPath ".\block-map.autofilled.json"
```

#### Important

Review the generated file before replacing your main `block-map.json`.

The auto-fill script uses broad name-matching rules. It is designed to create a fast first pass, not a perfect hand-authored map.

For example:

```text
minecraft:oak_stairs       -> Wood
minecraft:deepslate        -> Rock
minecraft:diamond_ore      -> DiamondOre
minecraft:redstone_wire    -> Empty
minecraft:white_bed        -> Empty
```

Some Minecraft blocks do not have a clean CastleMiner Z equivalent, so they may need manual adjustment.

Recommended workflow:

1. Back up `block-map.json`.
2. Run `AutoFillBlockMap.ps1`.
3. Open `block-map.autofilled.json`.
4. Review important mappings.
5. Rename it to `block-map.json` when satisfied.

---

## Block Map

The `block-map.json` file controls how Minecraft blocks are converted into CastleMiner Z blocks.

Example:

```json
{
  // Default CMZ block used when a Minecraft block is not listed below.
  // "Empty" means unmapped blocks become air.
  "DefaultBlock": "Empty",

  // Minecraft block -> CastleMinerZ block mappings.
  "Mappings": {
    "minecraft:air": "Empty",
    "minecraft:cave_air": "Empty",
    "minecraft:void_air": "Empty",

    "minecraft:acacia_log": "Log",
    "Acacia Log": "Log",

    "minecraft:oak_log": "Log",
    "minecraft:spruce_log": "Log",
    "minecraft:birch_log": "Log",
    "minecraft:jungle_log": "Log",
    "minecraft:dark_oak_log": "Log",
    "minecraft:mangrove_log": "Log",
    "minecraft:cherry_log": "Log",

    "minecraft:oak_leaves": "Leaves",
    "minecraft:spruce_leaves": "Leaves",
    "minecraft:birch_leaves": "Leaves",
    "minecraft:jungle_leaves": "Leaves",
    "minecraft:acacia_leaves": "Leaves",
    "minecraft:dark_oak_leaves": "Leaves",
    "minecraft:mangrove_leaves": "Leaves",
    "minecraft:cherry_leaves": "Leaves",

    "minecraft:twisting_vines": "Leaves",
    "Twisting Vines": "Leaves",

    "minecraft:stone": "Rock",
    "minecraft:cobblestone": "Rock",
    "minecraft:dirt": "Dirt",
    "minecraft:grass_block": "Grass",
    "minecraft:sand": "Sand",
    "minecraft:snow_block": "Snow",
    "minecraft:ice": "Ice",

    "minecraft:glass": "GlassBasic",
    "minecraft:tnt": "TNT",
    "minecraft:torch": "Torch",
    "minecraft:chest": "Crate"
  }
}
```

---

## Block Map Notes

The converter normalizes Minecraft block names before lookup.

These can all resolve to the same mapping:

```text
minecraft:acacia_log
acacia_log
Acacia Log
```

Minecraft block states can also be mapped directly:

```json
{
  "Mappings": {
    "minecraft:torch[facing=east]": "TorchPOSX",
    "minecraft:torch[facing=west]": "TorchNEGX",
    "minecraft:torch[facing=north]": "TorchNEGZ",
    "minecraft:torch[facing=south]": "TorchPOSZ",
    "minecraft:torch": "Torch"
  }
}
```

This allows broad mappings and more specific state-based mappings.

---

## Mapping Blocks to Air

To remove or ignore a Minecraft block, map it to `Empty`:

```json
{
  "Mappings": {
    "minecraft:water": "Empty",
    "minecraft:lava": "Empty",
    "minecraft:grass": "Empty",
    "minecraft:tall_grass": "Empty"
  }
}
```

By default, `Empty` blocks are skipped.

To write `Empty` blocks into the CMZ schematic, run the converter with:

```bat
--save-air
```

---

## Unmapped Block Report

When the converter finds Minecraft blocks that are not listed in `block-map.json`, it writes a report next to the output schematic.

Example:

```text
house_cmz.unmapped.txt
```

This file helps you quickly find missing Minecraft block mappings.

Example contents:

```text
minecraft:polished_andesite
minecraft:oak_stairs[facing=north,half=bottom,shape=straight,waterlogged=false]
minecraft:lantern[hanging=false,waterlogged=false]
```

You can then add those blocks to `block-map.json`.

---

## CastleMiner Z Output Format

The converter writes the CastleMiner Z WorldEdit schematic format:

```text
Header:            WES
Version:           0x3
CopyAnchorOffset:  X, Y, Z floats
BlockCount:        int
BlockRecords:      float X, float Y, float Z, int block id
CrateCount:        int
```

For converted Minecraft schematics, crate sidecar data is currently written as:

```text
0
```

Minecraft container contents are not converted.

---

## Current Limitations

This converter currently handles block shape/material conversion only.

The following Minecraft data is not converted:

- Chest inventory contents
- Barrel contents
- Sign text
- Command block data
- Entities
- Item frames
- Paintings
- Banner patterns
- Redstone behavior
- Waterlogging behavior
- Stairs/fence/wall connection shapes
- Block rotation unless specifically mapped through block states

For example:

```json
{
  "Mappings": {
    "minecraft:oak_stairs": "Wood"
  }
}
```

will convert oak stairs into normal CMZ wood blocks.

---

## Recommended Workflow

1. Export or download a Minecraft WorldEdit `.schem`.
2. Place it somewhere easy to access.
3. Edit `block-map.json` to control how Minecraft blocks convert.
4. Run the converter.
5. Check the generated `.unmapped.txt` file.
6. Add missing mappings.
7. Run the converter again.
8. Import or paste the converted schematic in CastleMiner Z WorldEdit.

---

## Example

```bat
MCToCMZSchemConverter.exe castle.schem castle_cmz.schem block-map.json
```

Output:

```text
Conversion complete.
Input:  castle.schem
Output: castle_cmz.schem
Size:   64 x 48 x 64
Blocks written: 12483
```

If missing mappings are found:

```text
WARNING: 12 Minecraft blocks were not mapped.
Missing map list written to: castle_cmz.unmapped.txt
```

---

## Build Notes

The project targets:

```text
.NET Framework 4.8.1
```

Recommended build settings:

```text
Platform: x86
Configuration: Debug or Release
```

The following files should be copied next to the final `.exe`:

```text
block-map.json
fNbt.dll
System.Text.Json.dll
System.Memory.dll
System.Buffers.dll
System.Runtime.CompilerServices.Unsafe.dll
System.Text.Encodings.Web.dll
System.Threading.Tasks.Extensions.dll
Microsoft.Bcl.AsyncInterfaces.dll
System.IO.Pipelines.dll
```

---

## License

This project is licensed under the GPL-3.0-or-later license.

See `LICENSE` for details.

---

## Credits

Created by RussDev7 for CastleMiner Z / CastleForge tooling.

This project uses `fNbt` for reading Minecraft NBT schematic data.