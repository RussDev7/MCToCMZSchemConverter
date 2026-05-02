/*
SPDX-License-Identifier: GPL-3.0-or-later
Copyright (c) 2025 RussDev7
This file is part of https://github.com/RussDev7/MCToCMZSchemConverter - see LICENSE for details.
*/

using System.Collections.Generic;
using System;
using fNbt;

namespace SchemConverter.Minecraft
{
    #region Minecraft Schematic Reader

    /// <summary>
    /// Reads Minecraft WorldEdit/Sponge schematic files and converts their NBT data
    /// into a simplified <see cref="MinecraftSchematic"/> object.
    /// </summary>
    /// <remarks>
    /// Supports both older Sponge/WorldEdit schematic layouts and newer v3-style layouts.
    ///
    /// Older layouts usually store:
    /// - Palette
    /// - BlockData
    ///
    /// Newer v3 layouts usually store:
    /// - Blocks/Palette
    /// - Blocks/Data
    ///
    /// This reader does not convert blocks to CastleMinerZ blocks directly.
    /// It only reads the Minecraft dimensions, palette, and palette id stream.
    /// </remarks>
    public static class MinecraftSchematicReader
    {
        #region Read

        /// <summary>
        /// Loads a Minecraft WorldEdit/Sponge schematic from disk.
        /// </summary>
        /// <param name="path">Path to the Minecraft <c>.schem</c> file.</param>
        /// <returns>
        /// A <see cref="MinecraftSchematic"/> containing the schematic dimensions,
        /// palette entries, version, and decoded block palette ids.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the schematic contains invalid or incomplete varint block data.
        /// </exception>
        public static MinecraftSchematic Read(string path)
        {
            var file = new NbtFile();
            file.LoadFromFile(path, NbtCompression.AutoDetect, null);

            NbtCompound root = file.RootTag;

            // Sponge v3 usually has root["Schematic"].
            // Sponge v1/v2 usually use the root compound directly.
            NbtCompound schematic = root.Get<NbtCompound>("Schematic") ?? root;

            short width = schematic.Get<NbtShort>("Width").Value;
            short height = schematic.Get<NbtShort>("Height").Value;
            short length = schematic.Get<NbtShort>("Length").Value;

            int version = schematic.Get<NbtInt>("Version")?.Value ?? 1;

            // Sponge schematics may store a paste offset.
            // This is the relative offset from the paster to the schematic's minimum corner.
            // CMZ uses the opposite sign for CopyAnchorOffset, so Program.cs converts it later.
            int offsetX = 0;
            int offsetY = 0;
            int offsetZ = 0;

            NbtIntArray offsetTag = schematic.Get<NbtIntArray>("Offset");
            if (offsetTag != null && offsetTag.Value != null && offsetTag.Value.Length >= 3)
            {
                offsetX = offsetTag.Value[0];
                offsetY = offsetTag.Value[1];
                offsetZ = offsetTag.Value[2];
            }

            NbtCompound paletteTag;
            byte[] blockData;

            // Sponge/WorldEdit v3 stores block data under the "Blocks" compound.
            // Older versions store "Palette" and "BlockData" directly on the schematic compound.
            if (version >= 3 && schematic.Contains("Blocks"))
            {
                NbtCompound blocks = schematic.Get<NbtCompound>("Blocks");
                paletteTag = blocks.Get<NbtCompound>("Palette");
                blockData = blocks.Get<NbtByteArray>("Data").Value;
            }
            else
            {
                paletteTag = schematic.Get<NbtCompound>("Palette");
                blockData = schematic.Get<NbtByteArray>("BlockData").Value;
            }

            var palette = new Dictionary<int, string>();

            foreach (NbtTag tag in paletteTag)
            {
                if (!(tag is NbtInt paletteEntry))
                    continue;

                // tag.Name is something like:
                // minecraft:acacia_log[axis=y]
                palette[paletteEntry.Value] = tag.Name;
            }

            var paletteIds = DecodeVarInts(blockData);

            return new MinecraftSchematic(
                width,
                height,
                length,
                version,
                offsetX,
                offsetY,
                offsetZ,
                palette,
                paletteIds);
        }
        #endregion

        #region VarInt Decoding

        /// <summary>
        /// Decodes Minecraft/Sponge schematic block data from a packed varint byte array.
        /// </summary>
        /// <param name="data">The raw block data byte array from the schematic.</param>
        /// <returns>
        /// A list of palette ids, where each id can be looked up in the schematic palette.
        /// </returns>
        /// <remarks>
        /// Minecraft schematic block data does not directly store block names.
        /// It stores palette ids as varints.
        ///
        /// Example:
        /// - Palette id 5 may point to "minecraft:acacia_log[axis=y]"
        /// - The decoded block data list stores many occurrences of 5
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the varint data ends unexpectedly or contains an invalid varint.
        /// </exception>
        private static List<int> DecodeVarInts(byte[] data)
        {
            var result = new List<int>();
            int offset = 0;

            while (offset < data.Length)
            {
                int value = 0;
                int shift = 0;

                while (true)
                {
                    if (offset >= data.Length)
                        throw new InvalidOperationException("Unexpected end of varint block data.");

                    byte b = data[offset++];

                    value |= (b & 0x7F) << shift;

                    if ((b & 0x80) == 0)
                        break;

                    shift += 7;

                    if (shift > 35)
                        throw new InvalidOperationException("Invalid varint in block data.");
                }

                result.Add(value);
            }

            return result;
        }
        #endregion
    }
    #endregion

    #region Minecraft Schematic Data Model

    /// <summary>
    /// Represents the decoded Minecraft schematic data needed for conversion.
    /// </summary>
    /// <remarks>
    /// This object keeps the Minecraft data in Minecraft form.
    /// It does not know anything about CastleMinerZ block ids.
    ///
    /// The converter should use:
    /// - <see cref="Palette"/> to resolve palette ids into Minecraft block names/states.
    /// - <see cref="PaletteIds"/> to know which palette id exists at each block index.
    /// - <see cref="DecodeIndex"/> to convert a flat block index into X/Y/Z coordinates.
    /// </remarks>
    public sealed class MinecraftSchematic
    {
        #region Properties

        /// <summary>
        /// Width of the schematic on the X axis.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Height of the schematic on the Y axis.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Length of the schematic on the Z axis.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Sponge/WorldEdit schematic version.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Minecraft/Sponge paste offset on the X axis.
        /// </summary>
        /// <remarks>
        /// Sponge stores this as the relative offset from the paster to the schematic's minimum corner.
        /// CMZ stores the opposite concept as CopyAnchorOffset.
        /// </remarks>
        public int OffsetX { get; }

        /// <summary>
        /// Minecraft/Sponge paste offset on the Y axis.
        /// </summary>
        /// <remarks>
        /// Sponge stores this as the relative offset from the paster to the schematic's minimum corner.
        /// CMZ stores the opposite concept as CopyAnchorOffset.
        /// </remarks>
        public int OffsetY { get; }

        /// <summary>
        /// Minecraft/Sponge paste offset on the Z axis.
        /// </summary>
        /// <remarks>
        /// Sponge stores this as the relative offset from the paster to the schematic's minimum corner.
        /// CMZ stores the opposite concept as CopyAnchorOffset.
        /// </remarks>
        public int OffsetZ { get; }

        /// <summary>
        /// Maps palette ids to Minecraft block names or block states.
        /// </summary>
        /// <remarks>
        /// Example:
        /// <code>
        /// 5 -> minecraft:acacia_log[axis=y]
        /// </code>
        /// </remarks>
        public Dictionary<int, string> Palette { get; }

        /// <summary>
        /// Flat list of palette ids for every block position in the schematic.
        /// </summary>
        /// <remarks>
        /// Each entry points back to <see cref="Palette"/>.
        /// The index can be converted to X/Y/Z with <see cref="DecodeIndex"/>.
        /// </remarks>
        public List<int> PaletteIds { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a decoded Minecraft schematic data object.
        /// </summary>
        /// <param name="width">Width of the schematic on the X axis.</param>
        /// <param name="height">Height of the schematic on the Y axis.</param>
        /// <param name="length">Length of the schematic on the Z axis.</param>
        /// <param name="version">Sponge/WorldEdit schematic version.</param>
        /// <param name="offsetX">Minecraft/Sponge paste offset on the X axis.</param>
        /// <param name="offsetY">Minecraft/Sponge paste offset on the Y axis.</param>
        /// <param name="offsetZ">Minecraft/Sponge paste offset on the Z axis.</param>
        /// <param name="palette">Palette id to Minecraft block name/state map.</param>
        /// <param name="paletteIds">Flat block palette id list.</param>
        public MinecraftSchematic(
            int width,
            int height,
            int length,
            int version,
            int offsetX,
            int offsetY,
            int offsetZ,
            Dictionary<int, string> palette,
            List<int> paletteIds)
        {
            Width = width;
            Height = height;
            Length = length;
            Version = version;
            OffsetX = offsetX;
            OffsetY = offsetY;
            OffsetZ = offsetZ;
            Palette = palette;
            PaletteIds = paletteIds;
        }
        #endregion

        #region Coordinate Helpers

        /// <summary>
        /// Converts a flat Sponge/WorldEdit block index into local schematic X/Y/Z coordinates.
        /// </summary>
        /// <param name="width">Schematic width on the X axis.</param>
        /// <param name="length">Schematic length on the Z axis.</param>
        /// <param name="index">Flat block index from the decoded palette id list.</param>
        /// <param name="x">Decoded local X coordinate.</param>
        /// <param name="y">Decoded local Y coordinate.</param>
        /// <param name="z">Decoded local Z coordinate.</param>
        /// <remarks>
        /// WorldEdit/Sponge stores block data in this order:
        /// <code>
        /// index = (y * width * length) + (z * width) + x
        /// </code>
        /// </remarks>
        public static void DecodeIndex(int width, int length, int index, out int x, out int y, out int z)
        {
            // WorldEdit Sponge order:
            // index = (y * width * length) + (z * width) + x
            y = index / (width * length);
            int remainder = index - (y * width * length);
            z = remainder / width;
            x = remainder - z * width;
        }
        #endregion
    }
    #endregion
}