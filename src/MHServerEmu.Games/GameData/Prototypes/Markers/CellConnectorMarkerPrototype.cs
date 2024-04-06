﻿using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.VectorMath;

namespace MHServerEmu.Games.GameData.Prototypes.Markers
{
    public class CellConnectorMarkerPrototype : MarkerPrototype
    {
        public Vector3 Extents { get; }

        public CellConnectorMarkerPrototype(BinaryReader reader)
        {
            Extents = reader.ReadVector3();

            ReadMarker(reader);
        }
    }
}
