/*
SPDX-License-Identifier: GPL-3.0-or-later
Copyright (c) 2025 RussDev7
This file is part of https://github.com/RussDev7/MCToCMZSchemConverter - see LICENSE for details.
*/

using SchemConverter.CastleMinerZ;
using System.Collections.Generic;
using SchemConverter.Minecraft;
using SchemConverter.Mapping;
using System.IO;
using System;

namespace SchemConverter
{
    #region Program Entry Point

    /// <summary>
    /// Console entry point for converting Minecraft WorldEdit/Sponge schematics
    /// into CastleMinerZ WorldEdit schematics.
    /// </summary>
    /// <remarks>
    /// Conversion flow:
    /// - Read command-line arguments.
    /// - Load the Minecraft schematic.
    /// - Load the JSON block map.
    /// - Convert Minecraft palette ids into CMZ block records.
    /// - Write the converted CMZ schematic.
    /// - Report unmapped Minecraft blocks, if any.
    ///
    /// Usage:
    /// <code>
    /// SchemConverter input.schem output.schem block-map.json
    /// SchemConverter input.schem output.schem block-map.json --save-air
    /// SchemConverter input.schem output.schem block-map.json --save-air --preserve-origin
    /// </code>
    /// </remarks>
    internal static class Program
    {
        #region Main

        /// <summary>
        /// Runs the schematic converter.
        /// </summary>
        /// <param name="args">
        /// Command-line arguments:
        /// <list type="bullet">
        /// <item><description><c>args[0]</c>: Input Minecraft <c>.schem</c> path.</description></item>
        /// <item><description><c>args[1]</c>: Output CastleMinerZ <c>.schem</c> path.</description></item>
        /// <item><description><c>args[2]</c>: JSON block map path.</description></item>
        /// <item><description><c>--save-air</c>: Optional switch for writing Empty blocks into the output schematic.</description></item>
        /// <item><description><c>--preserve-origin</c>: Optional switch for converting the Minecraft/Sponge paste offset into the CMZ copy anchor.</description></item>
        /// </list>
        /// </param>
        /// <returns>
        /// Exit code:
        /// - 0 = success.
        /// - 1 = invalid usage.
        /// - 2 = conversion error.
        /// </returns>
        /// <remarks>
        /// By default, blocks mapped to <see cref="CmzBlockType.Empty"/> are skipped.
        /// This prevents air from overwriting existing CMZ terrain during paste.
        ///
        /// When <c>--save-air</c> is supplied, Empty blocks are written into the CMZ schematic.
        /// This allows the converted schematic to erase blocks when pasted, assuming the CMZ
        /// WorldEdit paste logic treats Empty as air.
        /// </remarks>
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length < 3)
                {
                    PrintUsage();
                    return 1;
                }

                string inputPath = args[0];
                string outputPath = args[1];
                string blockMapPath = args[2];

                bool saveAir = HasSwitch(args, "--save-air");
                bool preserveOrigin = HasSwitch(args, "--preserve-origin");

                if (!File.Exists(inputPath))
                    throw new FileNotFoundException("Input schematic was not found.", inputPath);

                if (!File.Exists(blockMapPath))
                    throw new FileNotFoundException("Block map was not found.", blockMapPath);

                BlockMap blockMap = BlockMap.Load(blockMapPath);
                MinecraftSchematic mc = MinecraftSchematicReader.Read(inputPath);

                var outputBlocks = new List<CmzBlockRecord>();

                for (int i = 0; i < mc.PaletteIds.Count; i++)
                {
                    int paletteId = mc.PaletteIds[i];

                    if (!mc.Palette.TryGetValue(paletteId, out string mcBlockState))
                    {
                        Console.WriteLine($"WARNING: Missing palette id {paletteId}; using Empty.");
                        mcBlockState = "minecraft:air";
                    }

                    CmzBlockType cmzBlock = blockMap.Resolve(mcBlockState);

                    if (!saveAir && cmzBlock == CmzBlockType.Empty)
                        continue;

                    MinecraftSchematic.DecodeIndex(
                        mc.Width,
                        mc.Length,
                        i,
                        out int x,
                        out int y,
                        out int z);

                    outputBlocks.Add(new CmzBlockRecord(x, y, z, cmzBlock));
                }

                float anchorX = 0;
                float anchorY = 0;
                float anchorZ = 0;

                if (preserveOrigin)
                {
                    // X/Z use the inverse Sponge offset so the horizontal Minecraft copy point
                    // becomes the CMZ paste anchor.
                    //
                    // Y is different for CMZ because /paste uses the player's body location,
                    // which is one block above the block the player is standing on.
                    //
                    // Using Minecraft Offset.Y here can place the schematic too high or too low,
                    // especially when the Minecraft copy origin was above/below the selected build.
                    //
                    // Anchor Y = 1 means:
                    //   basePosition.Y = playerLoc.Y - 1
                    // so the schematic's local Y=0 layer lands on the block under the player.
                    anchorX = -mc.OffsetX;
                    anchorY = 1;
                    anchorZ = -mc.OffsetZ;
                }

                CmzSchematicWriter.Write(
                    outputPath,
                    outputBlocks,
                    anchorX,
                    anchorY,
                    anchorZ);

                Console.WriteLine("Conversion complete.");
                Console.WriteLine($"Input:  {inputPath}");
                Console.WriteLine($"Output: {outputPath}");
                Console.WriteLine($"Size:   {mc.Width} x {mc.Height} x {mc.Length}");
                Console.WriteLine($"Blocks written: {outputBlocks.Count}");
                Console.WriteLine($"Preserve origin: {preserveOrigin}");

                if (preserveOrigin)
                {
                    Console.WriteLine($"Minecraft Offset: {mc.OffsetX}, {mc.OffsetY}, {mc.OffsetZ}");
                    Console.WriteLine($"CMZ CopyAnchorOffset: {anchorX}, {anchorY}, {anchorZ}");
                }

                if (blockMap.UnmappedBlocks.Count > 0)
                {
                    string unmappedPath = Path.ChangeExtension(outputPath, ".unmapped.txt");
                    File.WriteAllLines(unmappedPath, blockMap.UnmappedBlocks);

                    Console.WriteLine();
                    Console.WriteLine($"WARNING: {blockMap.UnmappedBlocks.Count} Minecraft blocks were not mapped.");
                    Console.WriteLine($"Missing map list written to: {unmappedPath}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR:");
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }
        #endregion

        #region Argument Helpers

        /// <summary>
        /// Checks whether a command-line switch was supplied.
        /// </summary>
        /// <param name="args">Command-line argument list.</param>
        /// <param name="name">Switch name to search for, such as <c>--save-air</c>.</param>
        /// <returns>
        /// <c>true</c> if the switch exists in <paramref name="args"/>; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Switch matching is case-insensitive.
        /// </remarks>
        private static bool HasSwitch(string[] args, string name)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        #endregion

        #region Usage Output

        /// <summary>
        /// Prints command-line usage help to the console.
        /// </summary>
        /// <remarks>
        /// This is shown when the converter is started without the required arguments.
        /// </remarks>
        private static void PrintUsage()
        {
            Console.WriteLine("Minecraft WorldEdit .schem -> CastleMinerZ WorldEdit .schem converter");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  SchemConverter <input.schem> <output.schem> <block-map.json> [--save-air] [--preserve-origin]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  SchemConverter house.schem house_cmz.schem block-map.json");
            Console.WriteLine("  SchemConverter house.schem house_cmz.schem block-map.json --save-air --preserve-origin");
        }
        #endregion
    }
    #endregion
}