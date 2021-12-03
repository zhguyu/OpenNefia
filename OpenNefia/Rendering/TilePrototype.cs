﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;

namespace OpenNefia.Core.Rendering
{
    public enum TileKind
    {
        None = 0,
        Dryground = 1,
        Crop = 2,
        Water = 3,
        Snow = 4,
        MountainWater = 5,
        HardWall = 6,
        Sand = 7,
        SandHard = 8,
        Coast = 9,
        SandWater = 10
    }

    [Prototype("Tile")]
    public class TilePrototype : IPrototype, ITileDefinition, IAtlasRegionProvider
    {
        [DataField("id", required: true)]
        public string ID { get; } = default!;

        /// <inheritdoc />
        public ushort TileIndex { get; private set; } = 0;

        [DataField(required: true)]
        public TileSpecifier Image = null!;

        [DataField]
        public int? ElonaAtlas = null;

        [DataField]
        public bool IsSolid = false;

        [DataField]
        public bool IsOpaque = false;

        [DataField]
        public TileSpecifier? WallImage = null;

        [DataField]
        public TileKind Kind = TileKind.None;

        [DataField]
        public TileKind Kind2 = TileKind.None;

        /// <inheritdoc />
        public void AssignTileIndex(ushort id)
        {
            TileIndex = id;
        }

        public IEnumerable<AtlasRegion> GetAtlasRegions()
        {
            var hasOverhang = WallImage != null;

            yield return new(AtlasNames.Tile, $"{ID}:Tile", Image, hasOverhang);

            if (WallImage != null)
                yield return new(AtlasNames.Tile, $"{ID}:Tile_Wall", WallImage, hasOverhang);
        }
    }
}