﻿using MHServerEmu.Core.Extensions;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Missions;

namespace MHServerEmu.Games.Dialog
{
    public struct EntityObjectiveInfo
    {
        public PrototypeId MissionRef;
        public MissionStateFlags MissionState;
        public MissionObjectiveStateFlags ObjectiveState;
        public sbyte ObjectiveIndex;
        public BaseMissionOption MissionOption;
        public PlayerHUDEnum Flags;
    }

    public class BaseMissionOption : InteractionOption
    {
        public MissionPrototype MissionProto { get; private set; }
        public MissionStateFlags MissionState { get; private set; }
        public sbyte ObjectiveIndex { get; private set; }
        public MissionObjectiveStateFlags ObjectiveState { get; private set; }
        public SortedSet<PrototypeId> InterestRegions { get; private set; }
        public SortedSet<PrototypeId> InterestAreas { get; private set; }
        public SortedSet<PrototypeId> InterestCells { get; private set; }

        public BaseMissionOption()
        {
            MissionState = 0;
            InterestRegions = new();
            InterestAreas = new();
            InterestCells = new();
            ObjectiveIndex = -1;
            ObjectiveState = 0;
            Priority = 10;
        }

        public virtual void InitializeForMission(MissionPrototype missionProto, MissionStateFlags state, sbyte objectiveIndex, MissionObjectiveStateFlags objectiveState, MissionOptionTypeFlags optionType)
        {
            MissionProto = missionProto;
            MissionState = state;
            ObjectiveIndex = objectiveIndex;
            ObjectiveState = objectiveState;
            OptionType = optionType;

            EntityFilterWrapper.FilterContextMissionRef = missionProto.DataRef;
        }

        public Mission GetMission(Player player)
        {
            MissionManager missionManger = MissionManager.FindMissionManagerForMission(player, player.GetRegion(), MissionProto.DataRef);
            return missionManger?.FindMissionByDataRef(MissionProto.DataRef);
        }

        internal MissionObjective GetObjective(Mission mission)
        {
            throw new NotImplementedException();
        }

        internal bool HasObjective()
        {
            throw new NotImplementedException();
        }

        internal bool IsActiveForMissionAndEntity(Mission mission, WorldEntity interactee)
        {
            throw new NotImplementedException();
        }

        internal void SetInteractDataObjectiveFlags(Player interactingPlayer, ref InteractData outInteractData, Mission mission, BaseMissionOption completeOption)
        {
            throw new NotImplementedException();
        }

        internal bool ObjectiveFlagsAllowed()
        {
            throw new NotImplementedException();
        }
    }

    public class MissionHintOption : BaseMissionOption
    {
        public MissionObjectiveHintPrototype Proto { get; set; }  

        public MissionHintOption()
        {
            OptimizationFlags |= InteractionOptimizationFlags.Hint;
            EntityTrackingFlags |= EntityTrackingFlag.HUD;
        }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            if (Proto != null && Proto.TargetEntity != null && Proto.TargetEntity.Evaluate(entity, new(MissionProto.DataRef))) 
            {
                map.Insert(MissionProto.DataRef, EntityTrackingFlag.HUD);
                return EntityTrackingFlag.HUD;                 
            }
            return EntityTrackingFlag.None;
        }
    }

    public class BaseMissionConditionOption : BaseMissionOption
    {
        public MissionConditionPrototype Proto { get; set; }

        public BaseMissionConditionOption()
        {
            EntityTrackingFlags |= EntityTrackingFlag.MissionCondition;
        }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            if (Proto != null && Proto.NoTrackingOptimization)
                return EntityTrackingFlag.None;

            if (EntityFilterWrapper.EvaluateEntity(entity))
            {
                map.Insert(MissionProto.DataRef, EntityTrackingFlags);
                return EntityTrackingFlags;
            }
            return EntityTrackingFlag.None;
        }

    }

    public class MissionConditionMissionCompleteOption : BaseMissionConditionOption
    {
        private readonly SortedSet<PrototypeId> _missionRefs = new();
        public SortedSet<PrototypeId> CompleteMissionRefs { get => _missionRefs; }

        public override void InitializeForMission(MissionPrototype missionProto, MissionStateFlags state, sbyte objectiveIndex, MissionObjectiveStateFlags objectiveState, MissionOptionTypeFlags optionType)
        {
            base.InitializeForMission(missionProto, state, objectiveIndex, objectiveState, optionType);
            if (Proto is not MissionConditionMissionCompletePrototype missionComplete) return;

            PrototypeId missionCompleteRef = missionComplete.MissionPrototype;
            if (missionCompleteRef != PrototypeId.Invalid)
                _missionRefs.Add(missionCompleteRef);

            if (missionComplete.MissionKeyword != PrototypeId.Invalid)
            {
                foreach (var missionRef in GameDatabase.DataDirectory.IteratePrototypesInHierarchy<MissionPrototype>(PrototypeIterateFlags.NoAbstractApprovedOnly))
                {
                    MissionPrototype mission = missionRef.As<MissionPrototype>();
                    if (mission != null && mission.Keywords.HasValue() && mission.Keywords.Contains(missionComplete.MissionKeyword))
                        _missionRefs.Add(missionRef);
                }
            }
        }

        public SortedSet<PrototypeId> GetCompleteMissionRefs()
        {
            return _missionRefs;
        }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            EntityTrackingFlag trackingFlag = EntityTrackingFlag.None;

            if (checkList.Contains(this))
                return trackingFlag;
            else
                checkList.Add(this);

            var manager = GameDatabase.InteractionManager;
            var completeMissions = GetCompleteMissionRefs();
            foreach (var completeMissionRef in completeMissions)
            {
                var missionData = manager.GetMissionData(completeMissionRef);
                if (missionData != null)
                    foreach (var option in missionData.Options)
                        trackingFlag |= option.InterestedInEntity(map, entity, checkList);
            }

            if (trackingFlag != EntityTrackingFlag.None)
                map.Insert(MissionProto.DataRef, trackingFlag);

            return trackingFlag;
        }
    }

    public class MissionConditionRegionOption : BaseMissionConditionOption
    {
        public MissionConditionRegionOption()
        {
            EntityTrackingFlags |= EntityTrackingFlag.TransitionRegion;
        }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            if (entity is Transition transition && InterestRegions.Any())
            {
                List<Destination> destinations = transition.DestinationList;
                foreach (var destination in destinations)
                    if (destination.RegionRef != PrototypeId.Invalid && InterestRegions.Contains(destination.RegionRef))
                    {
                        map.Insert(MissionProto.DataRef, EntityTrackingFlags);
                        return EntityTrackingFlags;
                    }
            }
            return EntityTrackingFlag.None;
        }
    }

    public class MissionConditionHotspotOption : BaseMissionConditionOption
    {
        public MissionConditionHotspotOption()
        {
            OptimizationFlags |= InteractionOptimizationFlags.ConditionHotspot;
            EntityTrackingFlags |= EntityTrackingFlag.Hotspot;
        }
    }

    public class MissionConditionEntityInteractOption : BaseMissionConditionOption
    {

    }

    public class MissionVisibilityOption : BaseMissionOption
    {
        public EntityVisibilitySpecPrototype Proto { get; set; }

        public MissionVisibilityOption()
        {
            OptimizationFlags |= InteractionOptimizationFlags.Visibility;
        }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            if (Proto != null && Proto.EntityFilter != null && Proto.EntityFilter.Evaluate(entity, new(MissionProto.DataRef)))
            {
                map.Insert(MissionProto.DataRef, EntityTrackingFlag.Appearance);
                return EntityTrackingFlag.Appearance;
            }
            return EntityTrackingFlag.None;
        }
    }

    public class MissionDialogOption : BaseMissionOption
    {
        public MissionDialogTextPrototype Proto { get; set; }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            if (Proto != null && Proto.EntityFilter != null && Proto.EntityFilter.Evaluate(entity, new(MissionProto.DataRef)))
            {
                map.Insert(MissionProto.DataRef, EntityTrackingFlag.MissionDialog);
                return EntityTrackingFlag.MissionDialog;
            }
            return EntityTrackingFlag.None;
        }
    }

    public class MissionConnectionTargetEnableOption : BaseMissionOption
    {
        public ConnectionTargetEnableSpecPrototype Proto { get; set; }

        public MissionConnectionTargetEnableOption()
        {
            OptimizationFlags |= InteractionOptimizationFlags.ConnectionTargetEnable;
        }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            if (Proto != null && Proto.ConnectionTarget != PrototypeId.Invalid)
            {
                if (entity is Transition transition)
                {
                    var targetRef = Proto.ConnectionTarget;
                    List<Destination> destinations = transition.DestinationList;
                    foreach (var destination in destinations)
                        if (destination.TargetRef == targetRef)
                        {
                            map.Insert(targetRef, EntityTrackingFlag.Appearance);
                            return EntityTrackingFlag.Appearance;
                        }
                }
            }
            return EntityTrackingFlag.None;
        }
    }

    public class MissionAppearanceOption : BaseMissionOption
    {
        public EntityAppearanceSpecPrototype Proto { get; set; }

        public MissionAppearanceOption()
        {
            OptimizationFlags |= InteractionOptimizationFlags.Appearance;
        }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            if (Proto != null && Proto.EntityFilter != null && Proto.EntityFilter.Evaluate(entity, new(MissionProto.DataRef)))
            {
                map.Insert(MissionProto.DataRef, EntityTrackingFlag.Appearance);
                return EntityTrackingFlag.Appearance;
            }
            return EntityTrackingFlag.None;
        }
    }

    public class MissionActionEntityTargetOption: BaseMissionOption
    {
        public MissionActionEntityTargetPrototype Proto { get; set; }

        public MissionActionEntityTargetOption()
        {
            OptimizationFlags |= InteractionOptimizationFlags.ActionEntityTarget;
        }

        public override EntityTrackingFlag InterestedInEntity(EntityTrackingContextMap map, WorldEntity entity, SortedSet<InteractionOption> checkList)
        {
            if (Proto != null && Proto.EntityFilter != null && Proto.EntityFilter.Evaluate(entity, new (MissionProto.DataRef)))
            {
                map.Insert(MissionProto.DataRef, EntityTrackingFlag.MissionAction);
                return EntityTrackingFlag.MissionAction;
            }
            return EntityTrackingFlag.None;
        }
    }
}
