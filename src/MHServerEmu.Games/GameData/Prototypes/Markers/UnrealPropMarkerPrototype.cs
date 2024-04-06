﻿using MHServerEmu.Core.Extensions;

namespace MHServerEmu.Games.GameData.Prototypes.Markers
{
    public class UnrealPropMarkerPrototype : MarkerPrototype
    {
        public string UnrealClassName { get; }
        public string UnrealQualifiedName { get; }
        public string UnrealArchetypeName { get; }

        public UnrealPropMarkerPrototype(BinaryReader reader)
        {
            UnrealClassName = reader.ReadFixedString32();
            UnrealQualifiedName = reader.ReadFixedString32();
            UnrealArchetypeName = reader.ReadFixedString32();

            ReadMarker(reader);
        }
    }
}
