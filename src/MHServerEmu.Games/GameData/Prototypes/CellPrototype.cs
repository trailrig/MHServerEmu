﻿using MHServerEmu.Core.Collisions;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.GameData.Prototypes.Markers;
using MHServerEmu.Games.GameData.Resources;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.GameData.Prototypes
{
    public class CellPrototype : Prototype, IBinaryResource
    {
        public Aabb BoundingBox { get; protected set; }
        public Cell.Type Type { get; protected set; }
        public Cell.Walls Walls { get; protected set; }
        public Cell.Filler FillerEdges { get; protected set; }
        public Cell.Type RoadConnections { get; protected set; }
        public string ClientMap { get; protected set; }
        public MarkerSetPrototype InitializeSet { get; protected set; }
        public MarkerSetPrototype MarkerSet { get; protected set; }
        public NaviPatchSourcePrototype NaviPatchSource { get; protected set; }
        public bool IsOffsetInMapFile { get; protected set; }
        public HeightMapPrototype HeightMap { get; protected set; }
        public PrototypeGuid[] HotspotPrototypes { get; protected set; }

        public void Deserialize(BinaryReader reader)
        {
            Vector3 max = reader.ReadVector3();
            Vector3 min = reader.ReadVector3();
            BoundingBox = new(min, max);
            Type = (Cell.Type)reader.ReadUInt32();
            Walls = (Cell.Walls)reader.ReadUInt32();
            FillerEdges = (Cell.Filler)reader.ReadUInt32();
            RoadConnections = (Cell.Type)reader.ReadUInt32();
            ClientMap = reader.ReadFixedString32();
            InitializeSet = new(reader);
            MarkerSet = new(reader);
            NaviPatchSource = new(reader);
            IsOffsetInMapFile = reader.ReadByte()>0;
            HeightMap = new(reader);

            HotspotPrototypes = new PrototypeGuid[reader.ReadUInt32()];
            for (int i = 0; i < HotspotPrototypes.Length; i++)
                HotspotPrototypes[i] = (PrototypeGuid)reader.ReadUInt64();
        }
    }

    public class HeightMapPrototype : Prototype
    {
        public Vector2 HeightMapSize { get; }
        public short[] HeightMapData { get; }
        public byte[] HotspotData { get; }

        public HeightMapPrototype(BinaryReader reader)
        {
            HeightMapSize = new(reader.ReadUInt32(), reader.ReadUInt32());

            HeightMapData = new short[reader.ReadUInt32()];
            for (int i = 0; i < HeightMapData.Length; i++)
                HeightMapData[i] = reader.ReadInt16();

            HotspotData = new byte[reader.ReadUInt32()];
            for (int i = 0; i < HotspotData.Length; i++)
                HotspotData[i] = reader.ReadByte();
        }
    }
}
