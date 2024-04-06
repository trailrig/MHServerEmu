﻿using System.Text;
using Google.ProtocolBuffers;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Generators;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.Powers;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Core.Collisions;
using MHServerEmu.Core.VectorMath;

namespace MHServerEmu.Games.Entities
{
    public class WorldEntity : Entity
    {
        public List<EntityTrackingContextMap> TrackingContextMap { get; set; }
        public List<Condition> ConditionCollection { get; set; }
        public List<PowerCollectionRecord> PowerCollection { get; set; }
        public int UnkEvent { get; set; }
        public RegionLocation Location { get; private set; } = new(); // TODO init;
        public Cell Cell { get => Location.Cell; }
        public EntityRegionSpatialPartitionLocation SpatialPartitionLocation { get; }
        public Aabb RegionBounds { get; set; }
        public Bounds Bounds { get; set; } = new();
        public Region Region { get => Location.Region; }
        public WorldEntityPrototype WorldEntityPrototype { get => EntityPrototype as WorldEntityPrototype; }
        public RegionLocation LastLocation { get; private set; }
        public bool TrackAfterDiscovery { get; private set; }
        public string PrototypeName => GameDatabase.GetFormattedPrototypeName(BaseData.PrototypeId);

        public WorldEntity(EntityBaseData baseData, ByteString archiveData) : base(baseData, archiveData) { SpatialPartitionLocation = new(this); }

        public WorldEntity(EntityBaseData baseData) : base(baseData) { SpatialPartitionLocation = new(this); }

        public WorldEntity(EntityBaseData baseData, AOINetworkPolicyValues replicationPolicy, ReplicatedPropertyCollection properties) : base(baseData)
        {
            ReplicationPolicy = replicationPolicy;
            Properties = properties;
            TrackingContextMap = new();
            ConditionCollection = new();
            PowerCollection = new();
            UnkEvent = 0;
            SpatialPartitionLocation = new(this);
        }

        protected override void Decode(CodedInputStream stream)
        {
            base.Decode(stream);

            TrackingContextMap = new();
            int trackingContextMapCount = (int)stream.ReadRawVarint64();
            for (int i = 0; i < trackingContextMapCount; i++)
                TrackingContextMap.Add(new(stream));

            ConditionCollection = new();
            int conditionCollectionCount = (int)stream.ReadRawVarint64();
            for (int i = 0; i < conditionCollectionCount; i++)
                ConditionCollection.Add(new(stream));

            // Gazillion::PowerCollection::SerializeRecordCount
            if (ReplicationPolicy.HasFlag(AOINetworkPolicyValues.AOIChannelProximity))
            {
                PowerCollection = new();
                int powerCollectionCount = (int)stream.ReadRawVarint32();
                if (powerCollectionCount > 0)
                {
                    // Records after the first one may require the previous record to get values from
                    PowerCollection.Add(new(stream, null));
                    for (int i = 1; i < powerCollectionCount; i++)
                        PowerCollection.Add(new(stream, PowerCollection[i - 1]));
                }
            }
            else
            {
                PowerCollection = new();
            }

            UnkEvent = stream.ReadRawInt32();
        }

        public override void Encode(CodedOutputStream stream)
        {
            base.Encode(stream);

            stream.WriteRawVarint64((ulong)TrackingContextMap.Count);
            foreach (EntityTrackingContextMap entry in TrackingContextMap) entry.Encode(stream);

            stream.WriteRawVarint64((ulong)ConditionCollection.Count);
            foreach (Condition condition in ConditionCollection) condition.Encode(stream);

            if (ReplicationPolicy.HasFlag(AOINetworkPolicyValues.AOIChannelProximity))
            {
                stream.WriteRawVarint32((uint)PowerCollection.Count);
                for (int i = 0; i < PowerCollection.Count; i++) PowerCollection[i].Encode(stream);
            }

            stream.WriteRawInt32(UnkEvent);
        }

        protected override void BuildString(StringBuilder sb)
        {
            base.BuildString(sb);

            for (int i = 0; i < TrackingContextMap.Count; i++)
                sb.AppendLine($"TrackingContextMap{i}: {TrackingContextMap[i]}");

            for (int i = 0; i < ConditionCollection.Count; i++)
                sb.AppendLine($"ConditionCollection{i}: {ConditionCollection[i]}");

            for (int i = 0; i < PowerCollection.Count; i++)
                sb.AppendLine($"PowerCollection{i}: {PowerCollection[i]}");

            sb.AppendLine($"UnkEvent: {UnkEvent}");
        }

        internal Entity GetRootOwner()
        {
            throw new NotImplementedException();
        }

        public override void Destroy()
        {
            if (Game == null) return;

            ExitWorld();
            if (IsDestroyed() == false)
            {
                // CancelExitWorldEvent();
                // CancelKillEvent();
                // CancelDestroyEvent();
                base.Destroy();
            }
        }

        public virtual void EnterWorld(Cell cell, Vector3 position, Orientation orientation)
        {
            var proto = WorldEntityPrototype;
            Game = cell.Game; // TODO: Init Game to constructor
            if (proto.ObjectiveInfo != null)
                TrackAfterDiscovery = proto.ObjectiveInfo.TrackAfterDiscovery;
            if (proto is HotspotPrototype) _flags |= EntityFlags.IsHotspot;

            Location.Region = cell.GetRegion();
            Location.Cell = cell; // Set directly
            Location.SetPosition(position);
            Location.SetOrientation(orientation);
            // TODO ChangeRegionPosition
            if (proto.Bounds != null)
                Bounds.InitializeFromPrototype(proto.Bounds);
            Bounds.Center = position;
            UpdateRegionBounds(); // Add to Quadtree
        }

        public bool ShouldUseSpatialPartitioning() => Bounds.Geometry != GeometryType.None;

        public void UpdateRegionBounds()
        {
            RegionBounds = Bounds.ToAabb();
            if (ShouldUseSpatialPartitioning())
                Region.UpdateEntityInSpatialPartition(this);
        }

        public bool IsInWorld() => Location.IsValid();

        public void ExitWorld()
        {
            // TODO send packets for delete entities from world
            var entityManager = Game.EntityManager;
            ClearWorldLocation();
            entityManager.DestroyEntity(this);
        }

        public void ClearWorldLocation()
        {
            if(Location.IsValid()) LastLocation = Location;
            if (Region != null && SpatialPartitionLocation.IsValid()) Region.RemoveEntityFromSpatialPartition(this);
            Location = null;
        }

        internal void EmergencyRegionCleanup(Region region)
        {
            throw new NotImplementedException();
        }

        private bool AppendStartPower(PrototypeId startPowerRef)
        {
            if (startPowerRef == PrototypeId.Invalid) return false;
            //Console.WriteLine($"[{Id}]{GameDatabase.GetPrototypeName(startPowerRef)}");
            Condition condition = new()
            {
                SerializationFlags = ConditionSerializationFlags.NoCreatorId
                | ConditionSerializationFlags.NoUltimateCreatorId
                | ConditionSerializationFlags.NoConditionPrototypeId
                | ConditionSerializationFlags.HasIndex
                | ConditionSerializationFlags.HasAssetDataRef,
                Id = 1,
                CreatorPowerPrototypeId = startPowerRef
            };             
            ConditionCollection.Add(condition);
            PowerCollectionRecord powerCollection = new()
            {
                Flags = PowerCollectionRecordFlags.PowerRefCountIsOne
                | PowerCollectionRecordFlags.PowerRankIsZero
                | PowerCollectionRecordFlags.CombatLevelIsSameAsCharacterLevel
                | PowerCollectionRecordFlags.ItemLevelIsOne
                | PowerCollectionRecordFlags.ItemVariationIsOne,
                PowerPrototypeId = startPowerRef,
                PowerRefCount = 1
            };
            PowerCollection.Add(powerCollection);
            return true;
        }

        public bool AppendOnStartActions(PrototypeId targetRef)
        {
            if (GameDatabase.InteractionManager.GetStartAction(BaseData.PrototypeId, targetRef, out MissionActionEntityPerformPowerPrototype action))
                return AppendStartPower(action.PowerPrototype);
            return false;
        }

        public string PowerCollectionToString()
        {
            StringBuilder sb = new();
            sb.AppendLine($"Powers:");
            foreach(var power in PowerCollection) sb.AppendLine($" {GameDatabase.GetFormattedPrototypeName(power.PowerPrototypeId)}");
            return sb.ToString();
        }

        public bool AppendSelectorActions(EntitySelectorActionPrototype[] entitySelectorActions)
        {
            foreach(var action in entitySelectorActions)
            {
                if (action.EventTypes.HasValue() && action.EventTypes.First() == EntitySelectorActionEventType.OnSimulated)
                {
                    if (action.AIOverrides.HasValue()) 
                    {
                        int index = Game.Random.Next(0, action.AIOverrides.Length);
                        var actionAIOverrideRef = action.AIOverrides[index];
                        if (actionAIOverrideRef == PrototypeId.Invalid) return false;
                        var actionAIOverride = actionAIOverrideRef.As<EntityActionAIOverridePrototype>();
                        if (actionAIOverride != null) return AppendStartPower(actionAIOverride.Power);
                    }
                    break;
                }
            }
            return false;
        }
    }
}
