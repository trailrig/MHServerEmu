﻿using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.System.Random;
using MHServerEmu.Games.Generators;
using MHServerEmu.Games.GameData.Calligraphy.Attributes;
using MHServerEmu.Core.VectorMath;

namespace MHServerEmu.Games.GameData.Prototypes
{

    public class SuperCellEntryPrototype : Prototype
    {
        public sbyte X { get; protected set; }
        public sbyte Y { get; protected set; }
        public AssetId Cell { get; protected set; }
        public AssetId[] Alts { get; protected set; }

        [DoNotCopy]
        public Point2 Offset { get => new(X, Y); }

        public PrototypeId PickCell(GRandom random, List<PrototypeId> list)
        {
            if (Alts.IsNullOrEmpty())
            {
                return GameDatabase.GetDataRefByAsset(Cell);
            }
            else
            {
                Picker<PrototypeId> picker = new(random);

                if (Cell != 0)
                {
                    PrototypeId cellRef = GameDatabase.GetDataRefByAsset(Cell);
                    if (cellRef != 0) picker.Add(cellRef);
                }

                foreach (AssetId alt in Alts)
                {
                    PrototypeId altRef = GameDatabase.GetDataRefByAsset(alt);
                    if (altRef != 0)
                    {
                        bool isUnique = true;
                        foreach (PrototypeId item in list)
                        {
                            if (altRef == item)
                            {
                                isUnique = false;
                                break;
                            }
                        }

                        if (isUnique) picker.Add(altRef);
                    }
                }

                PrototypeId pickCell = 0;
                if (!picker.Empty())
                {
                    picker.Pick(out pickCell);
                }

                return pickCell;

            }
        }

    }

    public class SuperCellPrototype : Prototype
    {
        public SuperCellEntryPrototype[] Entries { get; protected set; }

        [DoNotCopy]
        public Point2 Max { get; private set; }

        public override void PostProcess()
        {
            base.PostProcess();

            Max = new(-1, -1);

            if (Entries.HasValue())
            {
                foreach (SuperCellEntryPrototype superCellEntry in Entries)
                {
                    if (superCellEntry != null)
                    {
                        Max.X = Math.Max(Max.X, superCellEntry.X);
                        Max.Y = Math.Max(Max.Y, superCellEntry.Y);
                    }
                }
            }
        }

        public bool ContainsCell(PrototypeId cellRef)
        {
            if (Entries.HasValue())
            {
                foreach (var entryProto in Entries)
                {
                    if (entryProto != null && GameDatabase.GetDataRefByAsset(entryProto.Cell) == cellRef)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
