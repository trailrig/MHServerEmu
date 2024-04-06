﻿using MHServerEmu.Games.GameData.Calligraphy.Attributes;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.GameData.Prototypes
{
    #region Enums

    [AssetEnum((int)Invalid)]
    public enum DEPRECATEDDifficultyMode
    {
        Invalid = -1,
        Normal = 0,
        Heroic = 1,
        SuperHeroic = 2,
    }

    #endregion

    public class RegionDifficultySettingsPrototype : Prototype
    {
        public PrototypeId TuningTable { get; protected set; }
    }

    public class NumNearbyPlayersDmgByRankPrototype : Prototype
    {
        public Rank Rank { get; protected set; }
        public CurveId MobToPlayerCurve { get; protected set; }
        public CurveId PlayerToMobCurve { get; protected set; }
    }

    public class DifficultyIndexDamageByRankPrototype : Prototype
    {
        public Rank Rank { get; protected set; }
        public CurveId MobToPlayerCurve { get; protected set; }
        public CurveId PlayerToMobCurve { get; protected set; }
    }

    public class TuningDamageByRankPrototype : Prototype
    {
        public Rank Rank { get; protected set; }
        public float TuningMobToPlayer { get; protected set; }
        public float TuningPlayerToMob { get; protected set; }
    }

    public class NegStatusRankCurveEntryPrototype : Prototype
    {
        public Rank Rank { get; protected set; }
        public CurveId TenacityModifierCurve { get; protected set; }
    }

    public class NegStatusPropCurveEntryPrototype : Prototype
    {
        public PrototypeId NegStatusProp { get; protected set; }
        public NegStatusRankCurveEntryPrototype[] RankEntries { get; protected set; }
    }

    public class RankAffixTableByDifficultyEntryPrototype : Prototype
    {
        public PrototypeId DifficultyMin { get; protected set; }
        public PrototypeId DifficultyMax { get; protected set; }
        public RankAffixEntryPrototype[] RankAffixTable { get; protected set; }
    }

    public class TuningPrototype : Prototype
    {
        public LocaleStringId Name { get; protected set; }
        public float PlayerInflictedDamageTimerSec { get; protected set; }
        public float PlayerNearbyRange { get; protected set; }
        public NegStatusPropCurveEntryPrototype[] NegativeStatusCurves { get; protected set; }
        public CurveId LootFindByLevelDeltaCurve { get; protected set; }
        public CurveId SpecialItemFindByLevelDeltaCurve { get; protected set; }
        public CurveId LootFindByDifficultyIndexCurve { get; protected set; }
        public CurveId PlayerXPByDifficultyIndexCurve { get; protected set; }
        public PrototypeId DeathPenaltyCondition { get; protected set; }
        public CurveId PctXPFromParty { get; protected set; }
        public CurveId PctXPFromRaid { get; protected set; }
        public PrototypeId Tier { get; protected set; }
        public bool UseTierMinimapColor { get; protected set; }
        public RankAffixEntryPrototype[] RankAffixTable { get; protected set; }
        public float PctXPMultiplier { get; protected set; }
        public bool NumNearbyPlayersScalingEnabled { get; protected set; }
        public float TuningDamageMobToPlayer { get; protected set; }
        public float TuningDamageMobToPlayerDCL { get; protected set; }
        public float TuningDamagePlayerToMob { get; protected set; }
        public float TuningDamagePlayerToMobDCL { get; protected set; }
        public TuningDamageByRankPrototype[] TuningDamageByRank { get; protected set; }
        public TuningDamageByRankPrototype[] TuningDamageByRankDCL { get; protected set; }
        public RankAffixTableByDifficultyEntryPrototype[] RankAffixTableByDifficulty { get; protected set; }

        internal RankAffixEntryPrototype GetDifficultyRankEntry(PrototypeId difficultyTierRef, RankPrototype rankProto)
        {
            throw new NotImplementedException();
        }
    }

    public class DifficultyModePrototype : Prototype
    {
        public AssetId IconPath { get; protected set; }
        public LocaleStringId Name { get; protected set; }
        public DEPRECATEDDifficultyMode DifficultyModeEnum { get; protected set; }
        public int MigrationUnlocksAtLevel { get; protected set; }
        public PrototypeId UnlockNotification { get; protected set; }
        public PrototypeId TextStyle { get; protected set; }
    }

    public class DifficultyTierPrototype : Prototype
    {
        public int DEPTier { get; protected set; }
        public DifficultyTierAsset Tier { get; protected set; }
        public float BonusExperiencePct { get; protected set; }
        public float DamageMobToPlayerPct { get; protected set; }
        public float DamagePlayerToMobPct { get; protected set; }
        public float ItemFindCreditsPct { get; protected set; }
        public float ItemFindRarePct { get; protected set; }
        public float ItemFindSpecialPct { get; protected set; }
        public int UnlockLevel { get; protected set; }
        public AssetId UIColor { get; protected set; }
        public LocaleStringId UIDisplayName { get; protected set; }
        public int BonusItemFindBonusDifficultyMult { get; protected set; }
    }

    public class DifficultyGlobalsPrototype : Prototype
    {
        public CurveId MobConLevelCurve { get; protected set; }
        public RegionDifficultySettingsPrototype RegionSettingsDefault { get; protected set; }
        public RegionDifficultySettingsPrototype RegionSettingsDefaultPCZ { get; protected set; }
        public CurveId NumNearbyPlayersDmgDefaultMtoP { get; protected set; }
        public CurveId NumNearbyPlayersDmgDefaultPtoM { get; protected set; }
        public NumNearbyPlayersDmgByRankPrototype[] NumNearbyPlayersDmgByRank { get; protected set; }
        public NumNearbyPlayersDmgByRankPrototype[] NumNearbyPlayersDmgByRankPCZ { get; protected set; }
        public CurveId DifficultyIndexDamageDefaultMtoP { get; protected set; }
        public CurveId DifficultyIndexDamageDefaultPtoM { get; protected set; }
        public DifficultyIndexDamageByRankPrototype[] DifficultyIndexDamageByRank { get; protected set; }
        public float PvPDamageMultiplier { get; protected set; }
        public float PvPCritDamageMultiplier { get; protected set; }
        public CurveId PvPDamageScalarFromLevelCurve { get; protected set; }
        public CurveId TeamUpDamageScalarFromLevelCurve { get; protected set; }
        public EvalPrototype EvalDamageLevelDeltaMtoP { get; protected set; }
        public EvalPrototype EvalDamageLevelDeltaPtoM { get; protected set; }
    }
}
