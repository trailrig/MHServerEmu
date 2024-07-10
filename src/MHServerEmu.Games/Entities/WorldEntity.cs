﻿using System.Text;
using Gazillion;
using MHServerEmu.Core.Collisions;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Serialization;
using MHServerEmu.Core.System.Time;
using MHServerEmu.Core.VectorMath;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Dialog;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.Entities.Locomotion;
using MHServerEmu.Games.Entities.Physics;
using MHServerEmu.Games.Entities.PowerCollections;
using MHServerEmu.Games.Events;
using MHServerEmu.Games.Events.LegacyImplementations;
using MHServerEmu.Games.Events.Templates;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Generators;
using MHServerEmu.Games.Generators.Population;
using MHServerEmu.Games.Navi;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.Powers;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.Entities
{
    public enum PowerMovementPreventionFlags
    {
        Forced = 0,
        NonForced = 1,
        Sync = 2,
    }

    [Flags]
    public enum KillFlags
    {
        None,
    }

    [Flags]
    public enum ChangePositionFlags
    {
        None                = 0,
        Update              = 1 << 0,
        DoNotSendToOwner    = 1 << 1,
        DoNotSendToServer   = 1 << 2,
        DoNotSendToClients  = 1 << 3,
        Orientation         = 1 << 4,
        Force               = 1 << 5,
        Teleport            = 1 << 6,
        HighFlying          = 1 << 7,
        PhysicsResolve      = 1 << 8,
        SkipAOI             = 1 << 9,
        EnterWorld          = 1 << 10,
    }

    public enum ChangePositionResult
    {
        InvalidPosition,
        PositionChanged,
        NotChanged,
    }

    public class WorldEntity : Entity
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private EventPointer<TEMP_SendActivatePowerMessageEvent> _sendActivatePowerMessageEvent = new();
        private EventPointer<ScheduledExitWorldEvent> _exitWorldEvent = new();

        private AlliancePrototype _allianceProto;
        private Transform3 _transform = Transform3.Identity();

        protected EntityTrackingContextMap _trackingContextMap;
        protected ConditionCollection _conditionCollection;
        protected PowerCollection _powerCollection;
        protected int _unkEvent;

        public EntityTrackingContextMap TrackingContextMap { get => _trackingContextMap; }
        public ConditionCollection ConditionCollection { get => _conditionCollection; }
        public PowerCollection PowerCollection { get => _powerCollection; }
        public AlliancePrototype Alliance { get => GetAlliance(); }
        public RegionLocation RegionLocation { get; private set; } = new();
        public Cell Cell { get => RegionLocation.Cell; }
        public Area Area { get => RegionLocation.Area; }
        public RegionLocationSafe ExitWorldRegionLocation { get; private set; } = new();
        public EntityRegionSpatialPartitionLocation SpatialPartitionLocation { get; }
        public Aabb RegionBounds { get; set; }
        public Bounds Bounds { get; set; } = new();
        public Region Region { get => RegionLocation.Region; }
        public NaviMesh NaviMesh { get => RegionLocation.NaviMesh; }
        public Orientation Orientation { get => RegionLocation.Orientation; }
        public WorldEntityPrototype WorldEntityPrototype { get => Prototype as WorldEntityPrototype; }
        public bool TrackAfterDiscovery { get; private set; }
        public bool ShouldSnapToFloorOnSpawn { get; private set; }
        public EntityActionComponent EntityActionComponent { get; protected set; }
        public SpawnSpec SpawnSpec { get; private set; }
        public SpawnGroup SpawnGroup { get => SpawnSpec?.Group; }
        public Locomotor Locomotor { get; protected set; }
        public virtual Bounds EntityCollideBounds { get => Bounds; set { } }
        public virtual bool IsTeamUpAgent { get => false; }
        public bool IsInWorld { get => RegionLocation.IsValid(); }
        public bool IsAliveInWorld { get => IsInWorld && IsDead == false; }
        public bool IsVendor { get => Properties[PropertyEnum.VendorType] != PrototypeId.Invalid; }
        public EntityPhysics Physics { get; private set; }
        public bool HasNavigationInfluence { get; private set; }
        public NavigationInfluence NaviInfluence { get; private set; }
        public virtual bool IsMovementAuthoritative { get => true; }
        public virtual bool CanBeRepulsed { get => Locomotor != null && Locomotor.IsMoving && !IsExecutingPower; }
        public virtual bool CanRepulseOthers { get => true; }
        public PrototypeId ActivePowerRef { get; protected set; }
        public Power ActivePower { get => GetActivePower(); }
        public bool IsExecutingPower { get => ActivePowerRef != PrototypeId.Invalid; }

        public Vector3 Forward { get => GetTransform().Col0; }
        public Vector3 GetUp { get => GetTransform().Col2; }
        public float MovementSpeedRate { get => Properties[PropertyEnum.MovementSpeedRate]; } // PropertyTemp[PropertyEnum.MovementSpeedRate]
        public float MovementSpeedOverride { get => Properties[PropertyEnum.MovementSpeedOverride]; } // PropertyTemp[PropertyEnum.MovementSpeedOverride]
        public float BonusMovementSpeed => Locomotor?.GetBonusMovementSpeed(false) ?? 0.0f;
        public NaviPoint NavigationInfluencePoint { get => NaviInfluence.Point; }
        public bool DefaultRuntimeVisibility { get => WorldEntityPrototype != null && WorldEntityPrototype.VisibleByDefault; }
        public virtual int Throwability { get => 0; }
        public virtual int InteractRange { get => GameDatabase.GlobalsPrototype?.InteractRange ?? 0; }
        public int InteractFallbackRange { get => GameDatabase.GlobalsPrototype?.InteractFallbackRange ?? 0; }
        public bool IsWeaponMissing { get => Properties[PropertyEnum.WeaponMissing]; }
        public bool IsGlobalEventVendor { get; internal set; }
        public bool IsHighFlying { get => Locomotor?.IsHighFlying ?? false; }
        public bool IsDestructible { get => HasKeyword(GameDatabase.KeywordGlobalsPrototype.DestructibleKeyword); }
        public bool IsDestroyProtectedEntity { get => IsControlledEntity || IsTeamUpAgent || this is Avatar; }  // Persistent entities cannot be easily destroyed

        public WorldEntity(Game game) : base(game)
        {
            SpatialPartitionLocation = new(this);
            Physics = new();
            HasNavigationInfluence = false;
            NaviInfluence = new();
        }

        public override bool Initialize(EntitySettings settings)
        {
            if (base.Initialize(settings) == false) return Logger.WarnReturn(false, "Initialize(): base.Initialize(settings) == false");

            var proto = WorldEntityPrototype;

            if (settings.IgnoreNavi)
                _flags |= EntityFlags.IgnoreNavi;

            ShouldSnapToFloorOnSpawn = settings.OptionFlags.HasFlag(EntitySettingsOptionFlags.HasOverrideSnapToFloor)
                ? settings.OptionFlags.HasFlag(EntitySettingsOptionFlags.OverrideSnapToFloorValue)
                : proto.SnapToFloorOnSpawn;

            OnAllianceChanged(Properties[PropertyEnum.AllianceOverride]);
            RegionLocation.Initialize(this);
            SpawnSpec = settings.SpawnSpec;

            // Old
            Properties[PropertyEnum.VariationSeed] = Game.Random.Next(1, 10000);

            // HACK: Override base health to make things more reasonable with the current damage implementation
            /*
            float healthBaseOverride = EntityHelper.GetHealthForWorldEntity(this);
            if (healthBaseOverride > 0f)
                Properties[PropertyEnum.Health] = Properties[PropertyEnum.HealthMaxOther];
            */

            Properties[PropertyEnum.CharacterLevel] = 60;
            Properties[PropertyEnum.CombatLevel] = 60;
            Properties[PropertyEnum.Health] = Properties[PropertyEnum.HealthMaxOther];

            if (proto.Bounds != null)
                Bounds.InitializeFromPrototype(proto.Bounds);

            Physics.Initialize(this);

            _trackingContextMap = new();
            _conditionCollection = new(this);
            _powerCollection = new(this);
            _unkEvent = 0;

            return true;
        }

        public override bool Serialize(Archive archive)
        {
            bool success = base.Serialize(archive);

            if (archive.IsTransient)
                success &= Serializer.Transfer(archive, ref _trackingContextMap);

            success &= Serializer.Transfer(archive, ref _conditionCollection);

            uint numRecords = 0;
            success &= PowerCollection.SerializeRecordCount(archive, _powerCollection, ref numRecords);
            if (numRecords > 0)
            {
                if (archive.IsPacking)
                {
                    success &= PowerCollection.SerializeTo(archive, _powerCollection, numRecords);
                }
                else
                {
                    if (_powerCollection == null) _powerCollection = new(this);
                    success &= PowerCollection.SerializeFrom(archive, _powerCollection, numRecords);
                }
            }

            if (archive.IsReplication)
                success &= Serializer.Transfer(archive, ref _unkEvent);

            return success;
        }

        public virtual void OnKilled(WorldEntity killer, KillFlags killFlags, WorldEntity directKiller)
        {
            // HACK: LOOT
            if (this is Agent agent && agent is not Missile)
                Game.LootGenerator.DropRandomLoot(agent);

            // HACK: Schedule respawn using SpawnSpec
            if (SpawnSpec != null)
            {
                Logger.Debug($"Respawn scheduled for {this}");
                EventPointer<TEMP_SpawnEntityEvent> eventPointer = new();
                Game.GameEventScheduler.ScheduleEvent(eventPointer, Game.CustomGameOptions.WorldEntityRespawnTime);
                eventPointer.Get().Initialize(SpawnSpec);
            }

            // HACK?: Set death state
            _flags |= EntityFlags.IsDead;
            Properties[PropertyEnum.IsDead] = true;
            Properties[PropertyEnum.NoEntityCollide] = true;

            // Send kill message to clients
            var killMessage = NetMessageEntityKill.CreateBuilder()
                .SetIdEntity(Id)
                .SetIdKillerEntity(killer != null ? killer.Id : InvalidId)
                .SetKillFlags((uint)killFlags)
                .Build();

            Game.NetworkManager.SendMessageToInterested(killMessage, this, AOINetworkPolicyValues.AOIChannelProximity);

            // Schedule destruction
            int removeFromWorldTimerMS = WorldEntityPrototype.RemoveFromWorldTimerMS;
            if (removeFromWorldTimerMS < 0)     // -1 means entities are not destroyed (e.g. avatars)
                return;

            TimeSpan removeFromWorldTimer = TimeSpan.FromMilliseconds(removeFromWorldTimerMS);

            // Team-ups continue existing in player's inventory even after they are defeated because their unlocks are tied to their entities
            if (IsTeamUpAgent)
            {
                if (removeFromWorldTimer == TimeSpan.Zero)
                    ExitWorld();
                else
                    ScheduleExitWorldEvent(removeFromWorldTimer);

                return;
            }

            // Other entities are destroyed 
            if (removeFromWorldTimer == TimeSpan.Zero)
                Destroy();
            else
                ScheduleDestroyEvent(removeFromWorldTimer);
        }

        public void Kill(WorldEntity killer = null, KillFlags killFlags = KillFlags.None, WorldEntity directKiller = null)
        {
            // CancelKillEvent();

            // TODO Implement

            Properties[PropertyEnum.Health] = 0;
            OnKilled(killer, killFlags, directKiller);   
        }

        public override void Destroy()
        {
            if (Game == null) return;

            ExitWorld();
            if (IsDestroyed == false)
            {
                // CancelExitWorldEvent();
                // CancelKillEvent();
                CancelDestroyEvent();
                base.Destroy();
            }
        }

        public override bool ScheduleDestroyEvent(TimeSpan delay)
        {
            if (IsDestroyProtectedEntity)
                return Logger.WarnReturn(false, $"ScheduleDestroyEvent(): Trying to schedule destruction of a destroy-protected entity {this}");

            return base.ScheduleDestroyEvent(delay);
        }

        #region World and Positioning

        public virtual bool EnterWorld(Region region, Vector3 position, Orientation orientation, EntitySettings settings = null)
        {
            var proto = WorldEntityPrototype;
            if (proto.ObjectiveInfo != null)
                TrackAfterDiscovery = proto.ObjectiveInfo.TrackAfterDiscovery;

            SetStatus(EntityStatus.EnteringWorld, true);

            RegionLocation.Region = region;

            Physics.AcquireCollisionId();

            if (ChangeRegionPosition(position, orientation, ChangePositionFlags.DoNotSendToClients | ChangePositionFlags.SkipAOI) == ChangePositionResult.PositionChanged)
                OnEnteredWorld(settings);
            else
                ClearWorldLocation();

            SetStatus(EntityStatus.EnteringWorld, false);

            return IsInWorld;
        }

        public void ExitWorld()
        {
            if (IsInWorld == false) return;

            bool exitStatus = !TestStatus(EntityStatus.ExitingWorld);
            SetStatus(EntityStatus.ExitingWorld, true);
            Physics.ReleaseCollisionId();
            // TODO IsAttachedToEntity()
            Physics.DetachAllChildren();
            DisableNavigationInfluence();

            var entityManager = Game.EntityManager;
            if (entityManager == null) return;
            entityManager.PhysicsManager?.OnExitedWorld(Physics);
            OnExitedWorld();
            var oldLocation = ClearWorldLocation();
            SendLocationChangeEvents(oldLocation, RegionLocation, ChangePositionFlags.None);
            ModifyCollectionMembership(EntityCollection.Simulated, false);
            ModifyCollectionMembership(EntityCollection.Locomotion, false);

            if (exitStatus)
                SetStatus(EntityStatus.ExitingWorld, false);
        }

        public SimulateResult UpdateSimulationState()
        {
            // Never simulate when not in the world
            if (IsInWorld == false)
                return SetSimulated(false);

            // Simulate if the prototype is flagged as always simulated
            if (WorldEntityPrototype?.AlwaysSimulated == true)
                return SetSimulated(true);

            // Simulate is there are any player interested in this world entity
            return SetSimulated(InterestReferences.IsAnyPlayerInterested(AOINetworkPolicyValues.AOIChannelProximity) ||
                                InterestReferences.IsAnyPlayerInterested(AOINetworkPolicyValues.AOIChannelClientIndependent));
        }

        public virtual bool CanRotate()
        {
            return true;
        }

        public virtual bool CanMove()
        {
            return Locomotor != null && Locomotor.GetCurrentSpeed() > 0.0f;
        }

        public virtual ChangePositionResult ChangeRegionPosition(Vector3? position, Orientation? orientation, ChangePositionFlags flags = ChangePositionFlags.None)
        {
            bool positionChanged = false;
            bool orientationChanged = false;

            RegionLocation preChangeLocation = new(RegionLocation);
            Region region = Game.RegionManager.GetRegion(preChangeLocation.RegionId);
            if (region == null) return ChangePositionResult.NotChanged;

            if (position.HasValue && (flags.HasFlag(ChangePositionFlags.Update) || preChangeLocation.Position != position))
            {
                var result = RegionLocation.SetPosition(position.Value);

                if (result != RegionLocation.SetPositionResult.Success)     // onSetPositionFailure()
                    return Logger.WarnReturn(ChangePositionResult.NotChanged, string.Format(
                        "ChangeRegionPosition(): Failed to set entity new position (Moved out of world)\n\tEntity: {0}\n\tResult: {1}\n\tPrev Loc: {2}\n\tNew Pos: {3}",
                        this, result, RegionLocation, position));

                if (Bounds.Geometry != GeometryType.None)
                    Bounds.Center = position.Value;

                if (flags.HasFlag(ChangePositionFlags.PhysicsResolve) == false)
                    RegisterForPendingPhysicsResolve();

                positionChanged = true;
                // Old
                Properties[PropertyEnum.MapPosition] = position.Value;
            }

            if (orientation.HasValue && (flags.HasFlag(ChangePositionFlags.Update) || preChangeLocation.Orientation != orientation))
            {
                RegionLocation.Orientation = orientation.Value;

                if (Bounds.Geometry != GeometryType.None)
                    Bounds.Orientation = orientation.Value;
                if (Physics.HasAttachedEntities())
                    RegisterForPendingPhysicsResolve();
                orientationChanged = true;
                // Old
                Properties[PropertyEnum.MapOrientation] = orientation.Value.GetYawNormalized();
            }

            if (Locomotor != null && flags.HasFlag(ChangePositionFlags.PhysicsResolve) == false)
            {
                if (positionChanged)
                    Locomotor.ClearSyncState();
                else if (orientationChanged)
                    Locomotor.ClearOrientationSyncState();
            }

            if (positionChanged == false && orientationChanged == false)
                return ChangePositionResult.NotChanged;

            UpdateRegionBounds(); // Add to Quadtree
            SendLocationChangeEvents(preChangeLocation, RegionLocation, flags);
            SetStatus(EntityStatus.ToTransform, true);
            if (RegionLocation.IsValid())
                ExitWorldRegionLocation.Set(RegionLocation);

            if (flags.HasFlag(ChangePositionFlags.DoNotSendToClients) == false)
            {
                bool excludeOwner = flags.HasFlag(ChangePositionFlags.DoNotSendToOwner);

                var networkManager = Game.NetworkManager;
                var interestedClients = networkManager.GetInterestedClients(this, AOINetworkPolicyValues.AOIChannelProximity, excludeOwner);
                if (interestedClients.Any())
                {
                    var entityPositionMessageBuilder = NetMessageEntityPosition.CreateBuilder()
                        .SetIdEntity(Id)
                        .SetFlags((uint)flags);

                    if (position.HasValue) entityPositionMessageBuilder.SetPosition(position.Value.ToNetStructPoint3());
                    if (orientation.HasValue) entityPositionMessageBuilder.SetOrientation(orientation.Value.ToNetStructPoint3());

                    networkManager.SendMessageToMultiple(interestedClients, entityPositionMessageBuilder.Build());
                }
            }

            if (Cell != null && flags.HasFlag(ChangePositionFlags.SkipAOI) == false)
            {
                // TODO: Notify if distance is far enough, similar to AOI updates
                UpdateInterestPolicies(true);
            }

            return ChangePositionResult.PositionChanged;
        }

        public RegionLocation ClearWorldLocation()
        {
            if (RegionLocation.IsValid()) ExitWorldRegionLocation.Set(RegionLocation);
            if (Region != null && SpatialPartitionLocation.IsValid()) Region.RemoveEntityFromSpatialPartition(this);
            RegionLocation oldLocation = new(RegionLocation);
            RegionLocation.Set(RegionLocation.Invalid);
            return oldLocation;
        }

        public Vector3 FloorToCenter(Vector3 position)
        {
            Vector3 resultPosition = position;
            if (Bounds.Geometry != GeometryType.None)
                resultPosition.Z += Bounds.HalfHeight;
            // TODO Locomotor.GetCurrentFlyingHeight
            return resultPosition;
        }

        public bool ShouldUseSpatialPartitioning() => Bounds.Geometry != GeometryType.None;

        public EntityRegionSPContext GetEntityRegionSPContext()
        {
            EntityRegionSPContextFlags flags = EntityRegionSPContextFlags.ActivePartition;
            ulong playerRestrictedGuid = 0;

            WorldEntityPrototype entityProto = WorldEntityPrototype;
            if (entityProto == null) return new(flags);

            if (entityProto.CanCollideWithPowerUserItems)
            {
                Avatar avatar = GetMostResponsiblePowerUser<Avatar>();
                if (avatar != null)
                    playerRestrictedGuid = avatar.OwnerPlayerDbId;
            }

            if (!(IsNeverAffectedByPowers || (IsHotspot && !IsCollidableHotspot && !IsReflectingHotspot)))
                flags |= EntityRegionSPContextFlags.StaticPartition;

            return new(flags, playerRestrictedGuid);
        }

        public void UpdateRegionBounds()
        {
            RegionBounds = Bounds.ToAabb();
            if (ShouldUseSpatialPartitioning())
                Region.UpdateEntityInSpatialPartition(this);
        }

        public float GetDistanceTo(WorldEntity other, bool calcRadius)
        {
            if (other == null) return 0f;
            float distance = Vector3.Distance2D(RegionLocation.Position, other.RegionLocation.Position);
            if (calcRadius)
                distance -= Bounds.Radius + other.Bounds.Radius;
            return Math.Max(0.0f, distance);
        }

        public bool OrientToward(Vector3 point, bool ignorePitch = false, ChangePositionFlags changeFlags = ChangePositionFlags.None)
        {
            return OrientToward(point, RegionLocation.Position, ignorePitch, changeFlags);
        }

        private bool OrientToward(Vector3 point, Vector3 origin, bool ignorePitch = false, ChangePositionFlags changeFlags = ChangePositionFlags.None)
        {
            if (IsInWorld == false) Logger.Debug($"Trying to orient entity that is not in the world {this}.  point={point}, ignorePitch={ignorePitch}, cpFlags={changeFlags}");
            Vector3 delta = point - origin;
            if (ignorePitch) delta.Z = 0.0f;
            if (Vector3.LengthSqr(delta) >= MathHelper.PositionSqTolerance)
                return ChangeRegionPosition(null, Orientation.FromDeltaVector(delta), changeFlags) == ChangePositionResult.PositionChanged;
            return false;
        }

        public void ScheduleExitWorldEvent(TimeSpan time)
        {
            if (_exitWorldEvent.IsValid)
            {
                if (_exitWorldEvent.Get().FireTime > Game.CurrentTime + time)
                    Game.GameEventScheduler.RescheduleEvent(_exitWorldEvent, time);
            }
            else
                ScheduleEntityEvent(_exitWorldEvent, time);
        }

        public Vector3 GetVectorFrom(WorldEntity other)
        {
            if (other == null) return Vector3.Zero;
            return RegionLocation.GetVectorFrom(other.RegionLocation);
        }

        private Transform3 GetTransform()
        {
            if (TestStatus(EntityStatus.ToTransform))
            {
                _transform = Transform3.BuildTransform(RegionLocation.Position, RegionLocation.Orientation);
                SetStatus(EntityStatus.ToTransform, false);
            }
            return _transform;
        }

        private void SendLocationChangeEvents(RegionLocation oldLocation, RegionLocation newLocation, ChangePositionFlags flags)
        {
            if (flags.HasFlag(ChangePositionFlags.EnterWorld))
                OnRegionChanged(null, newLocation.Region);
            else
                OnRegionChanged(oldLocation.Region, newLocation.Region);

            if (oldLocation.Area != newLocation.Area)
                OnAreaChanged(oldLocation, newLocation);

            if (oldLocation.Cell != newLocation.Cell)
                OnCellChanged(oldLocation, newLocation, flags);
        }

        protected class ScheduledExitWorldEvent : CallMethodEvent<Entity>
        {
            protected override CallbackDelegate GetCallback() => (t) => (t as WorldEntity)?.ExitWorld();
        }

        #endregion

        #region Physics

        public bool CanBeBlockedBy(WorldEntity other)
        {
            if (other == null || CanCollideWith(other) == false || Bounds.CanBeBlockedBy(other.Bounds) == false) return false;

            if (NoCollide || other.NoCollide)
            {
                bool noEntityCollideException = (HasNoCollideException && Properties[PropertyEnum.NoEntityCollideException] == other.Id) ||
                   (other.HasNoCollideException && other.Properties[PropertyEnum.NoEntityCollideException] == Id);
                return noEntityCollideException;
            }

            var worldEntityProto = WorldEntityPrototype;
            var boundsProto = worldEntityProto?.Bounds;
            var otherWorldEntityProto = other.WorldEntityPrototype;
            var otherBoundsProto = otherWorldEntityProto?.Bounds;

            if ((boundsProto != null && boundsProto.BlockOnlyMyself)
                || (otherBoundsProto != null && otherBoundsProto.BlockOnlyMyself))
                return PrototypeDataRef == other.PrototypeDataRef;

            if ((otherBoundsProto != null && otherBoundsProto.IgnoreBlockingWithAvatars && this is Avatar) ||
                (boundsProto != null && boundsProto.IgnoreBlockingWithAvatars && other is Avatar)) return false;

            bool locomotionNoCollide = Locomotor != null && (Locomotor.HasLocomotionNoEntityCollide || IsInKnockback);
            bool otherLocomotionNoCollide = other.Locomotor != null && (other.Locomotor.HasLocomotionNoEntityCollide || other.IsInKnockback);

            if (locomotionNoCollide || otherLocomotionNoCollide || IsIntangible || other.IsIntangible)
            {
                bool locomotorMovementPower = false;
                if (otherBoundsProto != null)
                {
                    switch (otherBoundsProto.BlocksMovementPowers)
                    {
                        case BoundsMovementPowerBlockType.All:
                            locomotorMovementPower = (Locomotor != null
                                && (Locomotor.IsMovementPower || Locomotor.IsHighFlying))
                                || IsInKnockback || IsIntangible;
                            break;
                        case BoundsMovementPowerBlockType.Ground:
                            locomotorMovementPower = (Locomotor != null
                                && (Locomotor.IsMovementPower && Locomotor.CurrentMoveHeight == 0)
                                && !Locomotor.IgnoresWorldCollision && !IsIntangible)
                                || IsInKnockback;
                            break;
                        case BoundsMovementPowerBlockType.None:
                        default:
                            break;
                    }
                }
                if (locomotorMovementPower == false) return false;
            }

            if (CanBePlayerOwned() && other.CanBePlayerOwned())
            {
                if (GetAlliance() == other.GetAlliance())
                {
                    if (HasPowerUserOverride == false || other.HasPowerUserOverride == false) return false;
                    uint powerId = Properties[PropertyEnum.PowerUserOverrideID];
                    uint otherPowerId = other.Properties[PropertyEnum.PowerUserOverrideID];
                    if (powerId != otherPowerId) return false;
                }
                if (other.IsInKnockdown || other.IsInKnockup) return false;
            }
            return true;
        }

        public virtual bool CanCollideWith(WorldEntity other)
        {
            if (other == null) return false;

            if (TestStatus(EntityStatus.Destroyed) || !IsInWorld
                || other.TestStatus(EntityStatus.Destroyed) || !other.IsInWorld) return false;

            if (Bounds.CollisionType == BoundsCollisionType.None
                || other.Bounds.CollisionType == BoundsCollisionType.None) return false;

            if ((other.Bounds.Geometry == GeometryType.Triangle || other.Bounds.Geometry == GeometryType.Wedge)
                && (Bounds.Geometry == GeometryType.Triangle || Bounds.Geometry == GeometryType.Wedge)) return false;

            var entityProto = WorldEntityPrototype;
            if (entityProto == null) return false;

            if (IsCloneParent()) return false;

            var boundsProto = entityProto.Bounds;
            if (boundsProto != null && boundsProto.IgnoreCollisionWithAllies && IsFriendlyTo(other)) return false;

            if (IsDormant || other.IsDormant) return false;

            return true;
        }

        public void RegisterForPendingPhysicsResolve()
        {
            PhysicsManager physMan = Game?.EntityManager?.PhysicsManager;
            physMan?.RegisterEntityForPendingPhysicsResolve(this);
        }

        #endregion

        #region Navi and Senses

        public void EnableNavigationInfluence()
        {
            if (IsInWorld == false || TestStatus(EntityStatus.ExitingWorld)) return;

            if (HasNavigationInfluence == false)
            {
                var region = Region;
                if (region == null) return;
                if (region.NaviMesh.AddInfluence(RegionLocation.Position, Bounds.Radius, NaviInfluence) == false)
                    Logger.Warn($"Failed to add navi influence for ENTITY={this} MISSION={GameDatabase.GetFormattedPrototypeName(MissionPrototype)}");
                HasNavigationInfluence = true;
            }
        }

        public void DisableNavigationInfluence()
        {
            if (HasNavigationInfluence)
            {
                Region region = Region;
                if (region == null) return;
                if (region.NaviMesh.RemoveInfluence(NaviInfluence) == false)
                    Logger.Warn($"Failed to remove navi influence for ENTITY={this} MISSION={GameDatabase.GetFormattedPrototypeName(MissionPrototype)}");
                HasNavigationInfluence = false;
            }
        }

        public bool CanInfluenceNavigationMesh()
        {
            if (IsInWorld == false || TestStatus(EntityStatus.ExitingWorld) || NoCollide || IsIntangible || IsCloneParent())
                return false;

            var prototype = WorldEntityPrototype;
            if (prototype != null && prototype.Bounds != null)
                return prototype.Bounds.CollisionType == BoundsCollisionType.Blocking && prototype.AffectNavigation;

            return false;
        }

        public void UpdateNavigationInfluence()
        {
            if (HasNavigationInfluence == false) return;

            Region region = Region;
            if (region == null) return;
            var regionPosition = RegionLocation.Position;
            if (NaviInfluence.Point != null)
            {
                if (NaviInfluence.Point.Pos.X != regionPosition.X ||
                    NaviInfluence.Point.Pos.Y != regionPosition.Y)
                    if (region.NaviMesh.UpdateInfluence(NaviInfluence, regionPosition, Bounds.Radius) == false)
                        Logger.Warn($"Failed to update navi influence for ENTITY={ToString()} MISSION={GameDatabase.GetFormattedPrototypeName(MissionPrototype)}");
            }
            else
                if (region.NaviMesh.AddInfluence(regionPosition, Bounds.Radius, NaviInfluence) == false)
                Logger.Warn($"Failed to add navi influence for ENTITY={ToString()} MISSION={GameDatabase.GetFormattedPrototypeName(MissionPrototype)}");
        }

        public PathFlags GetPathFlags()
        {
            if (Locomotor != null) return Locomotor.PathFlags;
            if (WorldEntityPrototype == null) return PathFlags.None;
            return Locomotor.GetPathFlags(WorldEntityPrototype.NaviMethod);
        }

        public NaviPathResult CheckCanPathTo(Vector3 toPosition)
        {
            return CheckCanPathTo(toPosition, GetPathFlags());
        }

        public NaviPathResult CheckCanPathTo(Vector3 toPosition, PathFlags pathFlags)
        {
            var region = Region;
            if (IsInWorld == false || region == null)
            {
                Logger.Warn($"Entity not InWorld when trying to check for a path! Entity: {ToString()}");
                return NaviPathResult.Failed;
            }

            bool hasNaviInfluence = false;
            if (HasNavigationInfluence)
            {
                DisableNavigationInfluence();
                hasNaviInfluence = HasNavigationInfluence;
            }

            var result = NaviPath.CheckCanPathTo(region.NaviMesh, RegionLocation.Position, toPosition, Bounds.Radius, pathFlags);
            if (hasNaviInfluence) EnableNavigationInfluence();

            return result;
        }

        public virtual bool CheckLandingSpot(Power power)
        {
            // TODO: Overrides in Agent and Avatar
            return true;
        }

        public bool LineOfSightTo(WorldEntity other, float radius = 0.0f, float padding = 0.0f, float height = 0.0f)
        {
            if (other == null) return false;
            if (this == other) return true;
            if (other.IsInWorld == false) return false;
            Region region = Region;
            if (region == null) return false;

            Vector3 startPosition = GetEyesPosition();
            return region.LineOfSightTo(startPosition, this, other.RegionLocation.Position, other.Id, radius, padding, height);
        }

        public bool LineOfSightTo(Vector3 targetPosition, float radius = 0.0f, float padding = 0.0f, float height = 0.0f, PathFlags pathFlags = PathFlags.Sight)
        {
            Region region = Region;
            if (region == null) return false;
            Vector3 startPosition = GetEyesPosition();
            return region.LineOfSightTo(startPosition, this, targetPosition, InvalidId, radius, padding, height, pathFlags);
        }

        private Vector3 GetEyesPosition()
        {
            Vector3 retPos = RegionLocation.Position;
            Bounds bounds = Bounds;
            retPos.Z += bounds.EyeHeight;
            return retPos;
        }

        #endregion

        #region Powers

        public Power GetPower(PrototypeId powerProtoRef) => _powerCollection?.GetPower(powerProtoRef);
        public Power GetThrowablePower() => _powerCollection?.ThrowablePower;
        public Power GetThrowableCancelPower() => _powerCollection?.ThrowableCancelPower;
        public virtual bool IsMelee() => false;

        public bool HasPowerInPowerCollection(PrototypeId powerProtoRef)
        {
            if (_powerCollection == null) return Logger.WarnReturn(false, "HasPowerInPowerCollection(): PowerCollection == null");
            return _powerCollection.ContainsPower(powerProtoRef);
        }

        public Power AssignPower(PrototypeId powerProtoRef, PowerIndexProperties indexProps, bool sendPowerAssignmentToClients = true, PrototypeId triggeringPowerRef = PrototypeId.Invalid)
        {
            if (_powerCollection == null) return Logger.WarnReturn<Power>(null, "AssignPower(): _powerCollection == null");
            Power assignedPower = _powerCollection.AssignPower(powerProtoRef, indexProps, triggeringPowerRef, sendPowerAssignmentToClients);
            if (assignedPower == null) return Logger.WarnReturn(assignedPower, "AssignPower(): assignedPower == null");
            return assignedPower;
        }

        public bool UnassignPower(PrototypeId powerProtoRef, bool sendPowerUnassignToClients = true)
        {
            if (HasPowerInPowerCollection(powerProtoRef) == false) return false;    // This includes the null check for PowerCollection

            if (_powerCollection.UnassignPower(powerProtoRef, sendPowerUnassignToClients) == false)
                return Logger.WarnReturn(false, "UnassignPower(): Failed to unassign power");

            return true;
        }

        public virtual PowerUseResult ActivatePower(PrototypeId powerRef, ref PowerActivationSettings settings)
        {
            Power power = GetPower(powerRef);
            if (power == null)
            {
                Logger.Warn($"ActivatePower(): Requested activation of power {GameDatabase.GetPrototypeName(powerRef)} but that power not found on {this}");
                return PowerUseResult.AbilityMissing;
            }
            return ActivatePower(power, ref settings);
        }

        public void EndAllPowers(bool v)
        {
            // TODO
        }

        public T GetMostResponsiblePowerUser<T>(bool skipPet = false) where T : WorldEntity
        {
            if (Game == null)
                return Logger.WarnReturn<T>(null, "GetMostResponsiblePowerUser(): Entity has no associated game. \nEntity: " + ToString());

            WorldEntity currentWorldEntity = this;
            T result = null;
            while (currentWorldEntity != null)
            {
                if (skipPet && currentWorldEntity.IsSummonedPet())
                    return null;

                if (currentWorldEntity is T possibleResult)
                    result = possibleResult;

                if (currentWorldEntity.HasPowerUserOverride == false)
                    break;

                ulong powerUserOverrideId = currentWorldEntity.Properties[PropertyEnum.PowerUserOverrideID];
                currentWorldEntity = Game.EntityManager.GetEntity<WorldEntity>(powerUserOverrideId);

                if (currentWorldEntity == this)
                    return Logger.WarnReturn<T>(null, "GetMostResponsiblePowerUser(): Circular reference in PowerUserOverrideID chain!");
            }

            return result;
        }

        public bool ActivePowerPreventsMovement(PowerMovementPreventionFlags movementPreventionFlag)
        {
            if (IsExecutingPower == false) return false;

            var activePower = ActivePower;
            if (activePower == null) return false;

            if (activePower.IsPartOfAMovementPower())
                return movementPreventionFlag == PowerMovementPreventionFlags.NonForced;

            if (movementPreventionFlag == PowerMovementPreventionFlags.NonForced && activePower.PreventsNewMovementWhileActive())
            {
                if (activePower.IsChannelingPower() == false) return true;
                else if (activePower.IsNonCancellableChannelPower()) return true;
            }

            if (movementPreventionFlag == PowerMovementPreventionFlags.Sync)
                if (activePower.IsChannelingPower() == false || activePower.IsCancelledOnMove())
                    return true;

            if (activePower.TriggersComboPowerOnEvent(PowerEventType.OnPowerEnd))
                return true;

            return false;
        }

        public bool ActivePowerDisablesOrientation()
        {
            if (IsExecutingPower == false) return false;
            var activePower = ActivePower;
            if (activePower == null)
            {
                Logger.Warn($"WorldEntity has ActivePowerRef set, but is missing the power in its power collection! Power: [{GameDatabase.GetPrototypeName(ActivePowerRef)}] WorldEntity: [{ToString()}]");
                return false;
            }
            return activePower.DisableOrientationWhileActive();
        }

        public bool ActivePowerOrientsToTarget()
        {
            if (IsExecutingPower == false) return false;

            var activePower = ActivePower;
            if (activePower == null) return false;

            return activePower.ShouldOrientToTarget();
        }

        public void OrientForPower(Power power, Vector3 targetPosition, Vector3 userPosition)
        {
            if (power.ShouldOrientToTarget() == false)
                return;

            if (power.GetTargetingShape() == TargetingShapeType.Self)
                return;

            if (Properties[PropertyEnum.LookAtMousePosition])
                return;

            OrientToward(targetPosition, userPosition, true, ChangePositionFlags.DoNotSendToServer | ChangePositionFlags.DoNotSendToClients);
        }

        public virtual PowerUseResult CanTriggerPower(PowerPrototype powerProto, Power power, PowerActivationSettingsFlags flags)
        {
            if (power == null && powerProto.Properties == null) return PowerUseResult.GenericError;
            if (power != null && power.Prototype != powerProto) return PowerUseResult.GenericError;

            var powerProperties = power != null ? power.Properties : powerProto.Properties;

            var region = Region;
            if (region == null) return PowerUseResult.GenericError;
            if (Power.CanBeUsedInRegion(powerProto, powerProperties, region) == false)
                return PowerUseResult.RegionRestricted;

            if (Power.IsMovementPower(powerProto)
                && (IsSystemImmobilized || (IsImmobilized && powerProperties[PropertyEnum.NegStatusUsable] == false)))
                return PowerUseResult.RestrictiveCondition;

            if (powerProperties[PropertyEnum.PowerUsesReturningWeapon] && IsWeaponMissing)
                return PowerUseResult.WeaponMissing;

            var targetingShape = Power.GetTargetingShape(powerProto);
            if (targetingShape == TargetingShapeType.Self)
            {
                if (Power.IsValidTarget(powerProto, this, Alliance, this) == false)
                    return PowerUseResult.BadTarget;
            }
            else if (targetingShape == TargetingShapeType.TeamUp)
            {
                if (this is not Avatar avatar)
                    return PowerUseResult.GenericError;
                var teamUpAgent = avatar.CurrentTeamUpAgent;
                if (teamUpAgent == null)
                    return PowerUseResult.TargetIsMissing;
                if (Power.IsValidTarget(powerProto, this, Alliance, teamUpAgent) == false)
                    return PowerUseResult.BadTarget;
            }

            if (powerProto.IsHighFlyingPower)
            {
                var naviMesh = region.NaviMesh;
                var pathFlags = GetPathFlags();
                pathFlags |= PathFlags.Fly;
                pathFlags &= ~PathFlags.Walk;
                if (naviMesh.Contains(RegionLocation.Position, Bounds.Radius, new DefaultContainsPathFlagsCheck(pathFlags)) == false)
                    return PowerUseResult.RegionRestricted;
            }

            return PowerUseResult.Success;
        }

        public virtual bool CanPowerTeleportToPosition(Vector3 position)
        {
            if (Region == null) return false;

            return Region.NaviMesh.Contains(position, Bounds.GetRadius(), new DefaultContainsPathFlagsCheck(GetPathFlags()));
        }

        public int GetPowerChargesAvailable(PrototypeId powerProtoRef)
        {
            return Properties[PropertyEnum.PowerChargesAvailable, powerProtoRef];
        }

        public int GetPowerChargesMax(PrototypeId powerProtoRef)
        {
            return Properties[PropertyEnum.PowerChargesMax, powerProtoRef];
        }

        public TimeSpan GetAbilityCooldownStartTime(PowerPrototype powerProto)
        {
            return Properties[PropertyEnum.PowerCooldownStartTime, powerProto.DataRef];
        }

        public virtual TimeSpan GetAbilityCooldownTimeElapsed(PowerPrototype powerProto)
        {
            // Overriden in Avatar
            return Game.CurrentTime - GetAbilityCooldownStartTime(powerProto);
        }

        public virtual TimeSpan GetAbilityCooldownTimeRemaining(PowerPrototype powerProto)
        {
            // Overriden in Agent
            TimeSpan cooldownDurationForLastActivation = GetAbilityCooldownDurationUsedForLastActivation(powerProto);
            TimeSpan cooldownTimeElapsed = Clock.Max(GetAbilityCooldownTimeElapsed(powerProto), TimeSpan.Zero);
            return Clock.Max(cooldownDurationForLastActivation - cooldownTimeElapsed, TimeSpan.Zero);
        }

        public TimeSpan GetAbilityCooldownDuration(PowerPrototype powerProto)
        {
            Power power = GetPower(powerProto.DataRef);
            
            if (power != null)
                return power.GetCooldownDuration();

            if (powerProto.Properties == null) return Logger.WarnReturn(TimeSpan.Zero, "GetAbilityCooldownDuration(): powerProto.Properties == null");
            return Power.GetCooldownDuration(powerProto, this, powerProto.Properties);
        }

        public TimeSpan GetAbilityCooldownDurationUsedForLastActivation(PowerPrototype powerProto)
        {
            TimeSpan powerCooldownDuration = TimeSpan.Zero;

            if (Power.IsCooldownOnPlayer(powerProto))
            {
                Player powerOwnerPlayer = GetOwnerOfType<Player>();
                if (powerOwnerPlayer != null)
                    powerCooldownDuration = powerOwnerPlayer.Properties[PropertyEnum.PowerCooldownDuration, powerProto.DataRef];
                else
                    Logger.Warn("GetAbilityCooldownDurationUsedForLastActivation(): powerOwnerPlayer == null");
            }
            else
            {
                powerCooldownDuration = Properties[PropertyEnum.PowerCooldownDuration, powerProto.DataRef];
            }

            return powerCooldownDuration;
        }

        public bool IsPowerOnCooldown(PowerPrototype powerProto)
        {
            return GetAbilityCooldownTimeRemaining(powerProto) > TimeSpan.Zero;
        }

        public virtual TimeSpan GetPowerInterruptCooldown(PowerPrototype powerProto)
        {
            // Overriden in Agent and Avatar
            return TimeSpan.Zero;
        }

        public bool IsTargetable(WorldEntity entity)
        {
            if (IsTargetableInternal() == false) return false;
            if (entity == null) return false;

            var player = GetOwnerOfType<Player>();
            if (player != null && player.IsTargetable(entity.Alliance) == false) return false;

            return true;
        }

        public bool IsAffectedByPowers()
        {
            if (IsAffectedByPowersInternal() == false)
                return false;

            if (Alliance == null)
                return false;

            return true;
        }

        public virtual void ActivatePostPowerAction(Power power, EndPowerFlags flags)
        {
            // NOTE: Overriden in avatar
        }

        public virtual void UpdateRecurringPowerApplication(PowerApplication powerApplication, PrototypeId powerProtoRef)
        {
            // NOTE: Overriden in avatar
        }

        public virtual bool ShouldContinueRecurringPower(Power power, ref EndPowerFlags flags)
        {
            // NOTE: Overriden in avatar
            return true;
        }

        public bool ApplyPowerResults(PowerResults powerResults)
        {
            // Send power results to clients
            NetMessagePowerResult powerResultMessage = ArchiveMessageBuilder.BuildPowerResultMessage(powerResults);
            Game.NetworkManager.SendMessageToInterested(powerResultMessage, this, AOINetworkPolicyValues.AOIChannelProximity);

            // Do not apply damage to avatars and team-ups... yet
            // Do this after sending the message so that the client can play hit effects anyway
            if (this is Avatar || (this is Agent agent && agent.IsTeamUpAgent))
                return true;

            // Apply the results to this entity
            // NOTE: A lot more things should happen here, but for now we just apply damage and check death
            int health = Properties[PropertyEnum.Health];

            float totalDamage = powerResults.Properties[PropertyEnum.Damage, (int)DamageType.Physical];
            totalDamage += powerResults.Properties[PropertyEnum.Damage, (int)DamageType.Energy];
            totalDamage += powerResults.Properties[PropertyEnum.Damage, (int)DamageType.Mental];

            health = (int)Math.Max(health - totalDamage, 0);

            // Kill
            WorldEntity powerUser = Game.EntityManager.GetEntity<WorldEntity>(powerResults.PowerOwnerId);

            if (health <= 0)
            {
                Kill(powerUser, KillFlags.None, null);
            }
            else
            {
                Properties[PropertyEnum.Health] = health;
                /*
                if (totalDamage > 0f && this is Agent aiAgent) aiAgent.AITestOn();
                // HACK: Rotate towards the power user
                if (totalDamage > 0f && powerUser is Avatar && Locomotor != null)
                {
                    ChangeRegionPosition(null, new(Vector3.AngleYaw(RegionLocation.Position, powerUser.RegionLocation.Position), 0f, 0f));
                }*/
            }

            return true;
        }

        public bool TEMP_ScheduleSendActivatePowerMessage(PrototypeId powerProtoRef, TimeSpan timeOffset)
        {
            if (_sendActivatePowerMessageEvent.IsValid) return false;
            ScheduleEntityEvent(_sendActivatePowerMessageEvent, timeOffset, powerProtoRef);
            return true;
        }

        public bool TEMP_SendActivatePowerMessage(PrototypeId powerProtoRef)
        {
            if (IsInWorld == false) return false;

            Logger.Trace($"Activating {GameDatabase.GetPrototypeName(powerProtoRef)} for {this}");

            OLD_ActivatePowerArchive activatePower = new()
            {
                Flags = ActivatePowerMessageFlags.TargetIsUser | ActivatePowerMessageFlags.HasTargetPosition |
                ActivatePowerMessageFlags.TargetPositionIsUserPosition | ActivatePowerMessageFlags.HasFXRandomSeed |
                ActivatePowerMessageFlags.HasPowerRandomSeed,

                PowerPrototypeRef = powerProtoRef,
                UserEntityId = Id,
                TargetPosition = RegionLocation.Position,
                FXRandomSeed = (uint)Game.Random.Next(),
                PowerRandomSeed = (uint)Game.Random.Next()
            };

            var activatePowerMessage = NetMessageActivatePower.CreateBuilder().SetArchiveData(activatePower.ToByteString()).Build();
            Game.NetworkManager.SendMessageToInterested(activatePowerMessage, this, AOINetworkPolicyValues.AOIChannelProximity);

            return true;
        }

        public string PowerCollectionToString()
        {
            StringBuilder sb = new();
            sb.AppendLine($"Powers:");
            foreach (var kvp in _powerCollection)
                sb.AppendLine($" {GameDatabase.GetFormattedPrototypeName(kvp.Value.PowerPrototypeRef)}");
            return sb.ToString();
        }

        protected virtual PowerUseResult ActivatePower(Power power, ref PowerActivationSettings settings)
        {
            return power.Activate(ref settings);
        }

        private Power GetActivePower()
        {
            if (ActivePowerRef != PrototypeId.Invalid)
                return PowerCollection?.GetPower(ActivePowerRef);
            return null;
        }

        private bool IsTargetableInternal()
        {
            if (IsAffectedByPowersInternal() == false) return false;
            if (IsUntargetable) return false;
            if (Alliance == null) return false;
            return true;
        }

        private bool IsAffectedByPowersInternal()
        {
            if (IsNeverAffectedByPowers
                || IsInWorld == false || IsSimulated == false
                || IsDormant || IsUnaffectable || IsHotspot) return false;
            return true;
        }

        protected class TEMP_SendActivatePowerMessageEvent : CallMethodEventParam1<Entity, PrototypeId>
        {
            protected override CallbackDelegate GetCallback() => (t, p1) => ((WorldEntity)t).TEMP_SendActivatePowerMessage(p1);
        }

        #endregion

        #region Stats

        public RankPrototype GetRankPrototype()
        {
            var rankRef = Properties[PropertyEnum.Rank];
            if (rankRef != PrototypeId.Invalid)
            {
                var rankProto = GameDatabase.GetPrototype<RankPrototype>(rankRef);
                if (rankProto == null) return null;
                return rankProto;
            }
            else
            {
                var worldEntityProto = WorldEntityPrototype;
                if (worldEntityProto == null) return null;
                return GameDatabase.GetPrototype<RankPrototype>(worldEntityProto.Rank);
            }
        }

        public float GetDefenseRating(DamageType damageType)
        {
            throw new NotImplementedException();
        }

        public float GetDamageReductionPct(float defenseRating, WorldEntity worldEntity, PowerPrototype powerProto)
        {
            throw new NotImplementedException();
        }

        public float GetDamageRating(DamageType damageType)
        {
            CombatGlobalsPrototype combatGlobals = GameDatabase.CombatGlobalsPrototype;
            if (combatGlobals == null) return Logger.WarnReturn(0f, "GetDamageRating(): combatGlobal == null");

            float damageRating = Properties[PropertyEnum.DamageRating];
            damageRating += Properties[PropertyEnum.DamageRatingBonusHardcore] * combatGlobals.GetHardcoreAttenuationFactor(Properties);
            damageRating += Properties[PropertyEnum.DamageRatingBonusMvmtSpeed] * MathF.Max(0f, BonusMovementSpeed);

            if (damageType != DamageType.Any)
                damageRating += Properties[PropertyEnum.DamageRatingBonusByType, (int)damageType];

            return damageRating;
        }

        public float GetCastSpeedPct(PowerPrototype powerProto)
        {
            float castSpeedPct = Properties[PropertyEnum.CastSpeedIncrPct] - Properties[PropertyEnum.CastSpeedDecrPct];
            float castSpeedMult = Properties[PropertyEnum.CastSpeedMult];

            if (powerProto != null)
            {
                // Apply power-specific multiplier
                castSpeedMult += Properties[PropertyEnum.CastSpeedMultPower, powerProto.DataRef];

                // Apply keyword bonuses
                foreach (var kvp in Properties.IteratePropertyRange(PropertyEnum.CastSpeedIncrPctKwd))
                {
                    Property.FromParam(kvp.Key, 0, out PrototypeId keywordRef);
                    if (powerProto.HasKeyword(keywordRef.As<KeywordPrototype>()))
                        castSpeedPct += kvp.Value;
                }

                foreach (var kvp in Properties.IteratePropertyRange(PropertyEnum.CastSpeedMultKwd))
                {
                    Property.FromParam(kvp.Key, 0, out PrototypeId keywordRef);
                    if (powerProto.HasKeyword(keywordRef.As<KeywordPrototype>()))
                        castSpeedMult += kvp.Value;
                }

                // Apply tab bonuses for avatars
                if (Prototype is AvatarPrototype avatarProto)
                {
                    var powerProgTableRef = avatarProto.GetPowerProgressionTableTabRefForPower(powerProto.DataRef);
                    if (powerProgTableRef != PrototypeId.Invalid)
                        castSpeedPct += Properties[PropertyEnum.CastSpeedIncrPctTab, powerProgTableRef, avatarProto.DataRef];
                }
            }

            // Cast speed is capped at 50000%
            castSpeedPct = MathF.Min(castSpeedPct, 500f);

            // Diminishing returns?
            if (castSpeedPct > 0f)
            {
                float pow = MathF.Pow(2.718f, -3f * castSpeedPct);
                castSpeedPct = MathF.Min(castSpeedPct, 0.4f - (0.4f * pow));
            }

            // Apply multiplier
            castSpeedPct = (1f + castSpeedPct) * (1f + castSpeedMult);

            // Cast speed can't go below 50%
            castSpeedPct = MathF.Max(castSpeedPct, 0.5f);

            return castSpeedPct;
        }

        #endregion

        #region Alliances

        public bool IsFriendlyTo(WorldEntity other, AlliancePrototype allianceProto = null)
        {
            if (other == null) return false;
            return IsFriendlyTo(other.Alliance, allianceProto);
        }

        public bool IsFriendlyTo(AlliancePrototype otherAllianceProto, AlliancePrototype allianceOverrideProto = null)
        {
            if (otherAllianceProto == null) return false;
            AlliancePrototype thisAllianceProto = allianceOverrideProto ?? Alliance;
            if (thisAllianceProto == null) return false;
            return thisAllianceProto.IsFriendlyTo(otherAllianceProto) && !thisAllianceProto.IsHostileTo(otherAllianceProto);
        }

        public bool IsHostileTo(AlliancePrototype otherAllianceProto, AlliancePrototype allianceOverrideProto = null)
        {
            if (otherAllianceProto == null) return false;
            AlliancePrototype thisAllianceProto = allianceOverrideProto ?? Alliance;
            if (thisAllianceProto == null) return false;
            return thisAllianceProto.IsHostileTo(otherAllianceProto);
        }

        public bool IsHostileTo(WorldEntity other, AlliancePrototype allianceOverride = null)
        {
            if (other == null) return false;

            if (this is not Avatar && IsMissionCrossEncounterHostilityOk == false)
            {
                bool isPlayer = false;
                if (HasPowerUserOverride)
                {
                    var userId = Properties[PropertyEnum.PowerUserOverrideID];
                    if (userId != InvalidId)
                    {
                        var user = Game.EntityManager.GetEntity<Entity>(userId);
                        if (user?.GetOwnerOfType<Player>() != null)
                            isPlayer = true;
                    }
                }

                if (isPlayer == false
                    && IgnoreMissionOwnerForTargeting == false
                    && HasMissionPrototype && other.HasMissionPrototype
                    && (PrototypeId)Properties[PropertyEnum.MissionPrototype] != (PrototypeId)other.Properties[PropertyEnum.MissionPrototype])
                    return false;
            }

            return IsHostileTo(other.Alliance, allianceOverride);
        }

        private void OnAllianceChanged(PrototypeId allianceRef)
        {
            if (allianceRef != PrototypeId.Invalid)
            {
                var allianceProto = GameDatabase.GetPrototype<AlliancePrototype>(allianceRef);
                if (allianceProto != null)
                    _allianceProto = allianceProto;
            }
            else
            {
                var worldEntityProto = WorldEntityPrototype;
                if (worldEntityProto != null)
                    _allianceProto = GameDatabase.GetPrototype<AlliancePrototype>(worldEntityProto.Alliance);
            }
        }

        private AlliancePrototype GetAlliance()
        {
            if (_allianceProto == null) return null;

            PrototypeId allianceRef = _allianceProto.DataRef;
            if (IsControlledEntity && _allianceProto.WhileControlled != PrototypeId.Invalid)
                allianceRef = _allianceProto.WhileControlled;
            if (IsConfused && _allianceProto.WhileConfused != PrototypeId.Invalid)
                allianceRef = _allianceProto.WhileConfused;

            return GameDatabase.GetPrototype<AlliancePrototype>(allianceRef);
        }

        #endregion

        #region Interaction

        public virtual bool InInteractRange(WorldEntity interactee, InteractionMethod interaction, bool interactFallbackRange = false)
        {
            if (IsSingleInteraction(interaction) == false && interaction.HasFlag(InteractionMethod.Throw)) return false;

            if (IsInWorld == false || interactee.IsInWorld == false) return false;

            float checkRange;
            float interactRange = InteractRange;

            if (interaction == InteractionMethod.Throw)
                if (Prototype is AgentPrototype agentProto) interactRange = agentProto.InteractRangeThrow;

            var worldEntityProto = WorldEntityPrototype;
            if (worldEntityProto == null) return false;

            var interacteeWorldEntityProto = interactee.WorldEntityPrototype;
            if (interacteeWorldEntityProto == null) return false;

            if (interacteeWorldEntityProto.InteractIgnoreBoundsForDistance == false)
                checkRange = Bounds.Radius + interactee.Bounds.Radius + interactRange + worldEntityProto.InteractRangeBonus + interacteeWorldEntityProto.InteractRangeBonus;
            else
                checkRange = interactRange;

            if (checkRange <= 0f) return false;

            if (interactFallbackRange)
                checkRange += InteractFallbackRange;

            float checkRangeSq = checkRange * checkRange;
            float rangeSq = Vector3.DistanceSquared(interactee.RegionLocation.Position, RegionLocation.Position);

            return rangeSq <= checkRangeSq;
        }

        public static bool IsSingleInteraction(InteractionMethod interaction)
        {
            return interaction != InteractionMethod.None; // IO::BitfieldHasSingleBitSet
        }

        public virtual InteractionResult AttemptInteractionBy(EntityDesc interactorDesc, InteractionFlags flags, InteractionMethod method)
        {
            var interactor = interactorDesc.GetEntity<Agent>(Game);
            if (interactor == null || interactor.IsInWorld == false) return InteractionResult.Failure;
            InteractData data = null;
            InteractionMethod iteractionStatus = InteractionManager.CallGetInteractionStatus(new EntityDesc(this), interactor, InteractionOptimizationFlags.None, flags, ref data);
            iteractionStatus &= method;

            switch (iteractionStatus)
            {
                case InteractionMethod.None:
                    return InteractionResult.Failure;

                // case InteractionMethod.Attack: // client only
                //    if (interactor.StartDefaultAttack(flags.HaveFlag(InteractionFlags.StopMove) == false))
                //        return InteractionResult.Success;
                //    else
                //        return InteractionResult.AttackFail; 

                case InteractionMethod.Throw: // server

                    if (interactor.InInteractRange(this, InteractionMethod.Throw) == false)
                        return InteractionResult.OutOfRange;
                    if (interactor.IsExecutingPower)
                        return InteractionResult.ExecutingPower;
                    if (interactor.StartThrowing(Id))
                        return InteractionResult.Success;

                    break;

                    // case InteractionMethod.Converse: // client only
                    // case InteractionMethod.Use:
                    //    return PostAttemptInteractionBy(interactor, iteractionStatus) 
            }

            return InteractionResult.Failure;
        }

        public bool IsThrowableBy(WorldEntity thrower)
        {
            if (IsDead || Properties[PropertyEnum.ThrowablePower] == PrototypeId.Invalid) return false;
            if (thrower != null)
                if (thrower.Throwability < Properties[PropertyEnum.Throwability]) return false;
            return true;
        }

        #endregion

        #region Event Handlers

        public override void OnChangePlayerAOI(Player player, InterestTrackOperation operation, AOINetworkPolicyValues newInterestPolicies, AOINetworkPolicyValues previousInterestPolicies, AOINetworkPolicyValues archiveInterestPolicies = AOINetworkPolicyValues.AOIChannelNone)
        {
            base.OnChangePlayerAOI(player, operation, newInterestPolicies, previousInterestPolicies, archiveInterestPolicies);
            UpdateSimulationState();
        }

        public virtual void OnEnteredWorld(EntitySettings settings)
        {
            if (CanInfluenceNavigationMesh())
                EnableNavigationInfluence();

            PowerCollection?.OnOwnerEnteredWorld();

            UpdateInterestPolicies(true, settings);

            UpdateSimulationState();
        }

        public virtual void OnExitedWorld()
        {
            PowerCollection?.OnOwnerExitedWorld();
            UpdateInterestPolicies(false);

            UpdateSimulationState();
        }

        public override void OnDeallocate()
        {
            base.OnDeallocate();
            PowerCollection?.OnOwnerDeallocate();
        }

        public virtual void OnDramaticEntranceEnd() { }

        public override void OnPropertyChange(PropertyId id, PropertyValue newValue, PropertyValue oldValue, SetPropertyFlags flags)
        {
            base.OnPropertyChange(id, newValue, oldValue, flags);
            if (flags.HasFlag(SetPropertyFlags.Refresh)) return;

            switch (id.Enum)
            {
                case PropertyEnum.CastSpeedDecrPct:
                case PropertyEnum.CastSpeedIncrPct:
                case PropertyEnum.CastSpeedMult:
                    PowerCollection?.OnOwnerCastSpeedChange(PrototypeId.Invalid);
                    break;

                case PropertyEnum.CastSpeedIncrPctKwd:
                case PropertyEnum.CastSpeedMultKwd:
                    if (PowerCollection != null)
                    {
                        Property.FromParam(id, 0, out PrototypeId powerKeywordRef);

                        if (powerKeywordRef == PrototypeId.Invalid)
                        {
                            Logger.Warn("OnPropertyChange(): powerKeywordRef == PrototypeId.Invalid");
                            break;
                        }

                        PowerCollection.OnOwnerCastSpeedChange(powerKeywordRef);
                    }

                    break;

                case PropertyEnum.CastSpeedIncrPctTab:
                    if (PowerCollection != null)
                    {
                        Property.FromParam(id, 0, out PrototypeId powerTabRef);

                        if (powerTabRef == PrototypeId.Invalid)
                        {
                            Logger.Warn("OnPropertyChange(): powerTabRef == PrototypeId.Invalid");
                            break;
                        }

                        PowerCollection.OnOwnerCastSpeedChange(powerTabRef);
                    }

                    break;

                case PropertyEnum.CastSpeedMultPower:
                    Property.FromParam(id, 0, out PrototypeId powerProtoRef);

                    if (powerProtoRef == PrototypeId.Invalid)
                    {
                        Logger.Warn("OnPropertyChange(): powerProtoRef == PrototypeId.Invalid");
                        break;
                    }

                    GetPower(powerProtoRef)?.OnOwnerCastSpeedChange();

                    break;

                case PropertyEnum.HealthMax:
                    Properties[PropertyEnum.HealthMaxOther] = newValue;
                    break;

                case PropertyEnum.NoEntityCollide:
                    _flags |= EntityFlags.NoCollide;
                    // EnableNavigationInfluence DisableNavigationInfluence
                    break;
            }
        }

        public virtual void OnCellChanged(RegionLocation oldLocation, RegionLocation newLocation, ChangePositionFlags flags)
        {
            Cell oldCell = oldLocation.Cell;
            Cell newCell = newLocation.Cell;

            if (newCell != null)
                Properties[PropertyEnum.MapCellId] = newCell.Id;

            // TODO other events
        }

        public virtual void OnAreaChanged(RegionLocation oldLocation, RegionLocation newLocation)
        {
            Area oldArea = oldLocation.Area;
            Area newArea = newLocation.Area;
            if (newArea != null)
            {
                Properties[PropertyEnum.MapAreaId] = newArea.Id;
                Properties[PropertyEnum.ContextAreaRef] = newArea.PrototypeDataRef;
            }

            // TODO other events
        }

        public virtual void OnRegionChanged(Region oldRegion, Region newRegion)
        {
            if (newRegion != null)
                Properties[PropertyEnum.MapRegionId] = newRegion.Id;

            // TODO other events
        }

        public virtual void OnLocomotionStateChanged(LocomotionState oldLocomotionState, LocomotionState newLocomotionState)
        {
            if (IsInWorld == false) return;

            // Check if locomotion state requires updating
            LocomotionState.CompareLocomotionStatesForSync(oldLocomotionState, newLocomotionState,
                out bool syncRequired, out bool pathNodeSyncRequired, newLocomotionState.FollowEntityId != InvalidId);

            if (syncRequired == false && pathNodeSyncRequired == false) return;

            // Send locomotion update to interested clients
            // NOTE: Avatars are locomoted on their local client independently, so they are excluded from locomotion updates.
            var networkManager = Game.NetworkManager;
            var interestedClients = networkManager.GetInterestedClients(this, AOINetworkPolicyValues.AOIChannelProximity, IsMovementAuthoritative == false);
            if (interestedClients.Any() == false) return;
            NetMessageLocomotionStateUpdate locomotionStateUpdateMessage = ArchiveMessageBuilder.BuildLocomotionStateUpdateMessage(
                this, oldLocomotionState, newLocomotionState, pathNodeSyncRequired);
            networkManager.SendMessageToMultiple(interestedClients, locomotionStateUpdateMessage);
        }

        public virtual void OnPreGeneratePath(Vector3 start, Vector3 end, List<WorldEntity> entities) { }

        public override void OnPostAOIAddOrRemove(Player player, InterestTrackOperation operation,
            AOINetworkPolicyValues newInterestPolicies, AOINetworkPolicyValues previousInterestPolicies)
        {
            base.OnPostAOIAddOrRemove(player, operation, newInterestPolicies, previousInterestPolicies);

            // Send our entire power collection when we gain proximity (enter game world)
            if (previousInterestPolicies != AOINetworkPolicyValues.AOIChannelNone
                && previousInterestPolicies.HasFlag(AOINetworkPolicyValues.AOIChannelProximity) == false
                && newInterestPolicies.HasFlag(AOINetworkPolicyValues.AOIChannelProximity))
            {
                PowerCollection?.SendEntireCollection(player);
            }
        }

        public virtual bool OnPowerAssigned(Power power) { return true; }
        public virtual bool OnPowerUnassigned(Power power) { return true; }
        public virtual void OnPowerEnded(Power power, EndPowerFlags flags) { }

        public virtual void OnOverlapBegin(WorldEntity whom, Vector3 whoPos, Vector3 whomPos) { }
        public virtual void OnOverlapEnd(WorldEntity whom) { }
        public virtual void OnCollide(WorldEntity whom, Vector3 whoPos) { }
        public virtual void OnSkillshotReflected(Missile missile) { }

        #endregion

        public virtual AssetId GetEntityWorldAsset()
        {
            // NOTE: Overriden in Agent, Avatar, and Missile
            return GetOriginalWorldAsset();
        }

        public AssetId GetOriginalWorldAsset()
        {
            return GetOriginalWorldAsset(WorldEntityPrototype);
        }

        public static AssetId GetOriginalWorldAsset(WorldEntityPrototype prototype)
        {
            if (prototype == null) return Logger.WarnReturn(AssetId.Invalid, $"GetOriginalWorldAsset(): prototype == null");
            return prototype.UnrealClass;
        }

        public virtual bool IsSummonedPet()
        {
            return false;
        }

        public bool IsCloneParent()
        {
            return WorldEntityPrototype.ClonePerPlayer && Properties[PropertyEnum.RestrictedToPlayerGuid] == 0;
        }

        public override SimulateResult SetSimulated(bool simulated)
        {
            var result = base.SetSimulated(simulated);
            if (result != SimulateResult.None && Locomotor != null)
                ModifyCollectionMembership(EntityCollection.Locomotion, IsSimulated);
            if (result == SimulateResult.Set)
            {
                // TODO EnemyBoost Rank
            }
            return result;
        }

        public void EmergencyRegionCleanup(Region region)
        {
            throw new NotImplementedException();
        }

        public void RegisterActions(List<EntitySelectorActionPrototype> actions)
        {
            if (actions == null) return;
            EntityActionComponent ??= new(this);
            EntityActionComponent.Register(actions);
        }

        public virtual void AppendStartAction(PrototypeId actionsTarget) { }

        public ScriptRoleKeyEnum GetScriptRoleKey()
        {
            if (SpawnSpec != null)
                return SpawnSpec.RoleKey;
            else
                return (ScriptRoleKeyEnum)(uint)Properties[PropertyEnum.ScriptRoleKey];
        }

        public bool HasKeyword(PrototypeId keyword)
        {
            return HasKeyword(GameDatabase.GetPrototype<KeywordPrototype>(keyword));
        }

        public bool HasKeyword(KeywordPrototype keywordProto)
        {
            return keywordProto != null && WorldEntityPrototype.HasKeyword(keywordProto);
        }

        public bool HasConditionWithKeyword(PrototypeId keywordRef)
        {
            var keywordProto = GameDatabase.GetPrototype<KeywordPrototype>(keywordRef);
            if (keywordProto == null) return false;
            if (keywordProto is not PowerKeywordPrototype) return false;
            return HasConditionWithKeyword(GameDatabase.DataDirectory.GetPrototypeEnumValue(keywordRef, GameDatabase.DataDirectory.KeywordBlueprint));
        }

        private bool HasConditionWithKeyword(int keyword)
        {
            var conditionCollection = ConditionCollection;
            if (conditionCollection != null)
            {
                KeywordsMask keywordsMask = conditionCollection.ConditionKeywordsMask;
                if (keywordsMask == null) return false;     // REMOVEME: Temp fix for condition collections not having keyword masks
                return keywordsMask[keyword];
            }
            return false;
        }

        public bool HasConditionWithAnyKeyword(IEnumerable<PrototypeId> keywordProtoRefs)
        {
            foreach (PrototypeId keywordProtoRef in keywordProtoRefs)
            {
                if (HasConditionWithKeyword(keywordProtoRef))
                    return true;
            }

            return false;
        }

        public void AccumulateKeywordProperties(PropertyEnum propertyEnum, PropertyCollection properties, ref float value)
        {
            foreach (var kvp in properties.IteratePropertyRange(propertyEnum))
            {
                Property.FromParam(kvp.Key, 0, out PrototypeId keywordProtoRef);
                var keywordPrototype = keywordProtoRef.As<KeywordPrototype>();

                if (HasKeyword(keywordPrototype) || HasConditionWithKeyword(keywordProtoRef))
                    value += kvp.Value;
            }
        }

        public bool CanEntityActionTrigger(EntitySelectorActionEventType eventType)
        {
            Logger.Debug($"CanEntityActionTrigger {eventType}");
            return false;
            // throw new NotImplementedException();
        }

        public void TriggerEntityActionEvent(EntitySelectorActionEventType actionType)
        {
            Logger.Debug($"TriggerEntityActionEvent {actionType}");
            // throw new NotImplementedException();
        }

        protected override void BuildString(StringBuilder sb)
        {
            base.BuildString(sb);

            foreach (var kvp in _trackingContextMap)
                sb.AppendLine($"{nameof(_trackingContextMap)}[{GameDatabase.GetPrototypeName(kvp.Key)}]: {kvp.Value}");

            foreach (var kvp in _conditionCollection)
                sb.AppendLine($"{nameof(_conditionCollection)}[{kvp.Key}]: {kvp.Value}");

            if (_powerCollection.PowerCount > 0)
            {
                sb.AppendLine($"{nameof(_powerCollection)}:");
                foreach (var kvp in _powerCollection)
                    sb.AppendLine(kvp.Value.ToString());
                sb.AppendLine();
            }

            sb.AppendLine($"{nameof(_unkEvent)}: 0x{_unkEvent:X}");
        }

        public static bool CheckWithinAngle(in Vector3 targetPosition, in Vector3 targetForward, in Vector3 position, float angle)
        {
            if (angle > 0)
            {
                Vector3 distance = Vector3.SafeNormalize(position - targetPosition);
                float targetForwardDot = Vector3.Dot(targetForward, distance);
                float checkAngle = MathHelper.ToDegrees(MathF.Acos(targetForwardDot)) * 2;
                if (checkAngle < angle)
                    return true;
            }
            return false;
        }
    }
}
