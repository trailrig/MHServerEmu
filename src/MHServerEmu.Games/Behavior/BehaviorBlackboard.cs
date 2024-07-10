﻿using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.Entities.Locomotion;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Generators.Population;
using MHServerEmu.Games.Properties;

namespace MHServerEmu.Games.Behavior
{
    public class BehaviorBlackboard
    {
        private Agent _owner; 
        public PropertyCollection PropertyCollection { get; private set; }
        public Vector3 SpawnPoint { get; set; }
        public Vector3 SpawnOffset { get; set; }
        public Vector3 UsePowerTargetPos { get; set; }
        public Queue<CustomPowerQueueEntry> CustomPowerQueue { get; set; }
        public Vector3 LastFlankTargetEntityPos { get; set; }
        public Vector3 LastFlockPosition { get; set; }
        public Vector3 MoveToCurrentPathNodePos { get; set; }
        public Dictionary<ulong, long> DamageMap { get; set; }       

        public BehaviorBlackboard(Agent owner)
        {
            _owner = owner;
            PropertyCollection = new ();
            SpawnPoint = Vector3.Zero;
            SpawnOffset = Vector3.Zero;
            UsePowerTargetPos = Vector3.Zero;
        }

        public void Initialize(BehaviorProfilePrototype profile, SpawnSpec spec, PropertyCollection collection)
        {
            SpawnPoint = _owner.RegionLocation.Position;
            if (spec != null)
                SpawnOffset = spec.Transform.Translation;
       
            PropertyCollection[PropertyEnum.AIAggroDropRange] = profile.AggroDropDistance;
            PropertyCollection[PropertyEnum.AIAggroDropByLOSChance] = profile.AggroDropChanceLOS / 100.0f;
            PropertyCollection[PropertyEnum.AIAggroRangeHostile] = profile.AggroRangeHostile;
            PropertyCollection[PropertyEnum.AIAggroRangeAlly] = profile.AggroRangeAlly;

            if (profile.AlwaysAggroed)
            {
                PropertyCollection[PropertyEnum.AIAlwaysAggroed] = true;
                PropertyCollection[PropertyEnum.AIAggroState] = true;
                PropertyCollection[PropertyEnum.AIAggroTime] = (long)_owner.Game.CurrentTime.TotalMilliseconds;
            }

            if (profile.Properties != null)
                PropertyCollection.FlattenCopyFrom(profile.Properties, false);
            if (collection != null)
                PropertyCollection.FlattenCopyFrom(collection, false);
        }

        public void AddCustomPower(PrototypeId powerRef, Vector3 targetPos, ulong targetId)
        {
            CustomPowerQueue ??= new Queue<CustomPowerQueueEntry>();
            CustomPowerQueue.Enqueue(new (powerRef, targetPos, targetId ));
        }

        private GeneratedPath _cachedGenPath;
        public GeneratedPath CachedPath
        {
            get
            {
                _cachedGenPath ??= new ();
                return _cachedGenPath;
            }
        }

        private Dictionary<Type, ProceduralProfileRuntimeData> _proceduralProfileData = new ();

        public T GetProceduralProfileRuntimeData<T>() where T : ProceduralProfileRuntimeData
        {
            _proceduralProfileData.TryGetValue(typeof(T), out var runtimeData);
            return runtimeData as T;
        }

        public void SetProceduralProfileRuntimeData<T>(T runtimeData) where T : ProceduralProfileRuntimeData
        {
            _proceduralProfileData[typeof(T)] = runtimeData;
        }
    }

    public class ProceduralProfileRuntimeData { }

    public struct CustomPowerQueueEntry
    {
        public PrototypeId PowerRef;
        public Vector3 TargetPos;
        public ulong TargetId;

        public CustomPowerQueueEntry(PrototypeId powerRef, Vector3 targetPos, ulong targetId)
        {
            PowerRef = powerRef;
            TargetPos = targetPos;
            TargetId = targetId;
        }
    }
}
