/*
SPDX-License-Identifier: GPL-3.0-or-later
Copyright (c) 2025 RussDev7
This file is part of https://github.com/RussDev7/MCToCMZSchemConverter - see LICENSE for details.
*/

using System.Collections.Generic;
using System.Text;
using System.IO;

namespace SchemConverter.CastleMinerZ
{
    #region CMZ Schematic Writer

    /// <summary>
    /// Writes CastleMinerZ WorldEdit schematic files using the custom CMZ WorldEdit binary format.
    /// </summary>
    /// <remarks>
    /// This writer outputs the CastleMinerZ WorldEdit schematic format used by the converter.
    ///
    /// Format written:
    /// - Header: "WES"
    /// - Version: 0x3
    /// - Copy anchor offset: X, Y, Z floats
    /// - Block count
    /// - Block records: X, Y, Z floats + CMZ block id
    /// - Crate sidecar count
    ///
    /// Minecraft inventory/container data is not converted here.
    /// Converted schematics currently write a crate sidecar count of 0.
    /// </remarks>
    public static class CmzSchematicWriter
    {
        #region Constants

        /// <summary>
        /// CastleMinerZ WorldEdit schematic file header.
        /// </summary>
        /// <remarks>
        /// The CMZ WorldEdit schematic loader expects the file to begin with the ASCII bytes for "WES".
        /// </remarks>
        private static readonly byte[] Header = Encoding.UTF8.GetBytes("WES");

        /// <summary>
        /// CastleMinerZ WorldEdit schematic format version written by this converter.
        /// </summary>
        private const byte Version = 0x3;

        #endregion

        #region Write

        /// <summary>
        /// Writes a CastleMinerZ WorldEdit schematic file to disk.
        /// </summary>
        /// <param name="outputPath">Path where the converted CMZ schematic should be written.</param>
        /// <param name="blocks">Collection of converted CMZ block records to write.</param>
        /// <param name="anchorX">Copy anchor offset on the X axis.</param>
        /// <param name="anchorY">Copy anchor offset on the Y axis.</param>
        /// <param name="anchorZ">Copy anchor offset on the Z axis.</param>
        /// <remarks>
        /// The default anchor values are zero.
        /// For converted Minecraft schematics, this means the paste anchor starts from the minimum corner.
        ///
        /// Note:
        /// This method writes block positions as floats because that is how the existing CMZ WorldEdit
        /// schematic format stores copied block positions.
        /// </remarks>
        public static void Write(
            string outputPath,
            IReadOnlyList<CmzBlockRecord> blocks,
            float anchorX = 0,
            float anchorY = 0,
            float anchorZ = 0)
        {
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            using (var stream = File.Create(outputPath))
            using (var writer = new BinaryWriter(stream))
            {

                writer.Write(Header);
                writer.Write(Version);

                // CopyAnchorOffset.
                // For converted schematics, zero means paste from the min corner.
                writer.Write(anchorX);
                writer.Write(anchorY);
                writer.Write(anchorZ);

                writer.Write(blocks.Count);

                foreach (CmzBlockRecord block in blocks)
                {
                    writer.Write((float)block.X);
                    writer.Write((float)block.Y);
                    writer.Write((float)block.Z);
                    writer.Write((int)block.BlockType);
                }

                // Crate sidecar count.
                // Minecraft chests/barrels/etc. are not converted with contents here.
                writer.Write(0);
            }
        }
        #endregion
    }
    #endregion

    #region CMZ Block Record

    /// <summary>
    /// Represents one block entry in a CastleMinerZ WorldEdit schematic.
    /// </summary>
    /// <remarks>
    /// A block record stores the local schematic position and the CastleMinerZ block type
    /// that should be written at that position.
    ///
    /// This is intentionally small and immutable so converted schematic block lists can be
    /// safely built and passed to <see cref="CmzSchematicWriter"/>.
    /// </remarks>
    public readonly struct CmzBlockRecord
    {
        #region Fields

        /// <summary>
        /// Local schematic X coordinate.
        /// </summary>
        public readonly int X;

        /// <summary>
        /// Local schematic Y coordinate.
        /// </summary>
        public readonly int Y;

        /// <summary>
        /// Local schematic Z coordinate.
        /// </summary>
        public readonly int Z;

        /// <summary>
        /// CastleMinerZ block type to place at this position.
        /// </summary>
        public readonly CmzBlockType BlockType;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a CastleMinerZ schematic block record.
        /// </summary>
        /// <param name="x">Local schematic X coordinate.</param>
        /// <param name="y">Local schematic Y coordinate.</param>
        /// <param name="z">Local schematic Z coordinate.</param>
        /// <param name="blockType">CastleMinerZ block type to place at this position.</param>
        public CmzBlockRecord(int x, int y, int z, CmzBlockType blockType)
        {
            X = x;
            Y = y;
            Z = z;
            BlockType = blockType;
        }
        #endregion
    }
    #endregion
}