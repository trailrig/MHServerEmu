﻿using MHServerEmu.Core.Logging;
using MHServerEmu.Games.GameData.Calligraphy.Attributes;

namespace MHServerEmu.Games.GameData.Prototypes
{
    #region Enums

    [AssetEnum((int)Invalid)]
    public enum AvatarStat
    {
        Invalid = 0,
        Durability = 1,
        Energy = 2,
        Fighting = 3,
        Intelligence = 4,
        Speed = 5,
        Strength = 6,
    }

    [AssetEnum((int)Invalid)]
    public enum AvatarMode
    {
        Invalid = -1,
        Normal = 0,
        Hardcore = 1,
        Ladder = 2,
    }

    #endregion

    public class AvatarPrototype : AgentPrototype
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        public LocaleStringId BioText { get; protected set; }
        public AbilityAssignmentPrototype[] HiddenPassivePowers { get; protected set; }
        public AssetId PortraitPath { get; protected set; }
        public PrototypeId StartingLootTable { get; protected set; }
        public AssetId UnlockDialogImage { get; protected set; }
        public AssetId HUDTheme { get; protected set; }
        public AvatarPrimaryStatPrototype[] PrimaryStats { get; protected set; }
        public PowerProgressionTablePrototype[] PowerProgressionTables { get; protected set; }
        public ItemAssignmentPrototype StartingCostume { get; protected set; }
        public PrototypeId ResurrectOtherEntityPower { get; protected set; }
        public AvatarEquipInventoryAssignmentPrototype[] EquipmentInventories { get; protected set; }
        public PrototypeId PartyBonusPower { get; protected set; }
        public LocaleStringId UnlockDialogText { get; protected set; }
        public PrototypeId SecondaryResourceBehavior { get; protected set; }
        public PrototypeId[] LoadingScreens { get; protected set; }
        public int PowerProgressionVersion { get; protected set; }
        public PrototypeId OnLevelUpEval { get; protected set; }
        public EvalPrototype OnPartySizeChange { get; protected set; }
        public PrototypeId StatsPower { get; protected set; }
        public AssetId SocialIconPath { get; protected set; }
        public AssetId CharacterSelectIconPath { get; protected set; }
        public PrototypeId[] StatProgressionTable { get; protected set; }
        public TransformModeEntryPrototype[] TransformModes { get; protected set; }
        public AvatarSynergyEntryPrototype[] SynergyTable { get; protected set; }
        public PrototypeId[] SuperteamMemberships { get; protected set; }
        public PrototypeId[] CharacterSelectPowers { get; protected set; }
        public PrototypeId[] PrimaryResourceBehaviors { get; protected set; }     // VectorPrototypeRefPtr PrimaryResourceManaBehaviorPrototype
        public PrototypeId[] StealablePowersAllowed { get; protected set; }       // VectorPrototypeRefPtr StealablePowerInfoPrototype
        public bool ShowInRosterIfLocked { get; protected set; }
        public LocaleStringId CharacterVideoUrl { get; protected set; }
        public AssetId CharacterSelectIconPortraitSmall { get; protected set; }
        public AssetId CharacterSelectIconPortraitFull { get; protected set; }
        public LocaleStringId PrimaryResourceBehaviorNames { get; protected set; }
        public bool IsStarterAvatar { get; protected set; }
        public int CharacterSelectDisplayOrder { get; protected set; }
        public PrototypeId CostumeCore { get; protected set; }
        public TalentGroupPrototype[] TalentGroups { get; protected set; }
        public PrototypeId TravelPower { get; protected set; }
        public AbilityAutoAssignmentSlotPrototype[] AbilityAutoAssignmentSlot { get; protected set; }
        public PrototypeId[] LoadingScreensConsole { get; protected set; }
        public ItemAssignmentPrototype StartingCostumePS4 { get; protected set; }
        public ItemAssignmentPrototype StartingCostumeXboxOne { get; protected set; }

        public override bool ApprovedForUse()
        {
            if (base.ApprovedForUse() == false) return false;

            // Avatars also need their starting costume to be approved to be considered approved themselves.
            // This is done in a separate AvatarPrototype.CostumeApprovedForUse() method rather than
            // CostumePrototype.ApprovedForUse() because the latter calls AvatarPrototype.ApprovedForUse().

            // Add settings for PS4 and Xbox One here if we end up supporting console clients
            PrototypeId startingCostumeId = GetStartingCostumeForPlatform(Platforms.PC);
            return CostumeApprovedForUse(startingCostumeId);
        }

        /// <summary>
        /// Returns the <see cref="PrototypeId"/> of the starting costume for the specified platform.
        /// </summary>
        public PrototypeId GetStartingCostumeForPlatform(Platforms platform)
        {
            if (platform == Platforms.PS4 && StartingCostumePS4 != null)
                return StartingCostumePS4.Item;
            else if (platform == Platforms.XboxOne && StartingCostumeXboxOne != null)
                return StartingCostumeXboxOne.Item;

            if (StartingCostume == null)
                return Logger.WarnReturn(PrototypeId.Invalid, $"GetStartingCostumeForPlatform(): failed to get starting costume for {platform}");

            return StartingCostume.Item;
        }

        /// <summary>
        /// Retrieves <see cref="PowerProgressionEntryPrototype"/> instances for powers that would be unlocked at the specified level or level range.
        /// </summary>
        public IEnumerable<PowerProgressionEntryPrototype> GetPowersUnlockedAtLevel(int level = -1, bool retrieveForLevelRange = false, int startingLevel = -1)
        {
            if (PowerProgressionTables == null) yield break;

            foreach (PowerProgressionTablePrototype table in PowerProgressionTables)
            {
                if (table.PowerProgressionEntries == null) continue;

                foreach (PowerProgressionEntryPrototype entry in table.PowerProgressionEntries)
                {
                    if (entry.PowerAssignment == null) continue;
                    if (entry.PowerAssignment.Ability == PrototypeId.Invalid) continue;

                    bool match = true;

                    // If the specified level is set to -1 it means we need to include all levels.
                    match &= level < 0 || entry.Level <= level;

                    // retrieveForLevelRange means to retrieve all abilities that would be unlocked
                    // if you got from startingLevel to level. Otherwise retrieve just the abilities
                    // for the specified level.
                    match &= retrieveForLevelRange && entry.Level > startingLevel || entry.Level == level;

                    if (match) yield return entry;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="AbilityAutoAssignmentSlotPrototype"/> for the specified power <see cref="PrototypeId"/> if there is one.
        /// Otherwise, returns <see langword="null"/>.
        /// </summary>
        public AbilityAutoAssignmentSlotPrototype GetPowerInAbilityAutoAssignmentSlot(PrototypeId powerProtoId)
        {
            if (AbilityAutoAssignmentSlot == null) return null;

            foreach (var abilityAutoAssignmentSlot in AbilityAutoAssignmentSlot)
            {
                if (abilityAutoAssignmentSlot.Ability == powerProtoId)
                    return abilityAutoAssignmentSlot;
            }

            return null;
        }

        /// <summary>
        /// Returns <see langword="true"/> if the provided costume is approved for use.
        /// </summary>
        private bool CostumeApprovedForUse(PrototypeId costumeId)
        {
            // See AvatarPrototype.ApprovedForUse() for why this method exists.
            var costume = GameDatabase.GetPrototype<CostumePrototype>(costumeId);
            return costume != null && GameDatabase.DesignStateOk(costume.DesignState);
        }
    }

    public class ItemAssignmentPrototype : Prototype
    {
        public PrototypeId Item { get; protected set; }
        public PrototypeId Rarity { get; protected set; }
    }

    public class AvatarPrimaryStatPrototype : Prototype
    {
        public AvatarStat Stat { get; protected set; }
        public LocaleStringId Tooltip { get; protected set; }
    }

    public class IngredientLookupEntryPrototype : Prototype
    {
        public long LookupSlot { get; protected set; }
        public PrototypeId Ingredient { get; protected set; }
    }

    public class AvatarSynergyEntryPrototype : Prototype
    {
        public int Level { get; protected set; }
        public LocaleStringId TooltipTextForIcon { get; protected set; }
        public PrototypeId UIData { get; protected set; }
    }

    public class AvatarSynergyEvalEntryPrototype : AvatarSynergyEntryPrototype
    {
        public EvalPrototype SynergyEval { get; protected set; }
    }

    public class VanityTitlePrototype : Prototype
    {
        public LocaleStringId Text { get; protected set; }
    }

    public class PowerSpecPrototype : Prototype
    {
        public int Index { get; protected set; }
    }

    public class TalentEntryPrototype : Prototype
    {
        public PrototypeId Talent { get; protected set; }
        public int UnlockLevel { get; protected set; }
    }

    public class TalentGroupPrototype : Prototype
    {
        public TalentEntryPrototype[] Talents { get; protected set; }
        public float UIPositionPctX { get; protected set; }
        public float UIPositionPctY { get; protected set; }
    }

    public class AvatarModePrototype : Prototype
    {
        public AvatarMode AvatarModeEnum { get; protected set; }
        public ConvenienceLabel Inventory { get; protected set; }
    }

    public class StatProgressionEntryPrototype : Prototype
    {
        public int Level { get; protected set; }
        public int DurabilityValue { get; protected set; }
        public int EnergyProjectionValue { get; protected set; }
        public int FightingSkillsValue { get; protected set; }
        public int IntelligenceValue { get; protected set; }
        public int SpeedValue { get; protected set; }
        public int StrengthValue { get; protected set; }
    }

    public class PowerProgressionEntryPrototype : ProgressionEntryPrototype
    {
        public int Level { get; protected set; }
        public AbilityAssignmentPrototype PowerAssignment { get; protected set; }
        public CurveId MaxRankForPowerAtCharacterLevel { get; protected set; }
        public PrototypeId[] Prerequisites { get; protected set; }
        public float UIPositionPctX { get; protected set; }
        public float UIPositionPctY { get; protected set; }
        public int UIFanSortNumber { get; protected set; }
        public int UIFanTier { get; protected set; }
        public PrototypeId[] Antirequisites { get; protected set; }
        public bool IsTrait { get; protected set; }
    }

    public class PowerProgressionTablePrototype : Prototype
    {
        public LocaleStringId DisplayName { get; protected set; }
        public PowerProgressionEntryPrototype[] PowerProgressionEntries { get; protected set; }
    }

    public class PowerProgTableTabRefPrototype : Prototype
    {
        public int PowerProgTableTabIndex { get; protected set; }
    }
}
