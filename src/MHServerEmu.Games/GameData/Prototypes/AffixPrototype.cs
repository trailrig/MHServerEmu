﻿using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.System.Random;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.Calligraphy.Attributes;

namespace MHServerEmu.Games.GameData.Prototypes
{
    #region Enums

    [AssetEnum((int)Fail)]
    public enum DuplicateHandlingBehavior
    {
        Fail,
        Ignore,
        Overwrite,
        Append,
    }

    [AssetEnum]
    public enum OmegaPageType
    {
        Psionics = 12,
        CosmicEntities = 1,
        ArcaneAttunement = 2,
        InterstellarExploration = 3,
        SpecialWeapons = 4,
        ExtraDimensionalTravel = 5,
        MolecularAdjustment = 13,
        RadioactiveOrigins = 7,
        TemporalManipulation = 8,
        Nanotechnology = 9,
        SupernaturalInvestigation = 10,
        HumanAugmentation = 11,
        NeuralEnhancement = 0,
        Xenobiology = 6,
    }

    [AssetEnum((int)None)]
    public enum InfinityGem
    {
        Soul = 3,
        Time = 5,
        Space = 4,
        Mind = 0,
        Reality = 2,
        Power = 1,
        None = 7,
    }

    [AssetEnum((int)Popcorn)]
    public enum Rank
    {
        Popcorn,
        Champion,
        Elite,
        MiniBoss,
        Boss,
        Player,
        GroupBoss,
        TeamUp,
    }

    [AssetEnum((int)None)]
    public enum LootDropEventType
    {
        None = 0,
        OnInteractedWith = 3,
        OnHealthBelowPct = 2,
        OnHealthBelowPctHit = 1,
        OnKilled = 4,
        OnKilledChampion = 5,
        OnKilledElite = 6,
        OnKilledMiniBoss = 7,
        OnHit = 8,
        OnDamagedForPctHealth = 9,
    }

    [AssetEnum((int)Default)]
    public enum HealthBarType
    {
        Default = 0,
        EliteMinion = 1,
        MiniBoss = 2,
        Boss = 3,
        None = 4,
    }

    [AssetEnum((int)Default)]
    public enum OverheadInfoDisplayType
    {
        Default = 0,
        Always = 1,
        Never = 2,
    }

    #endregion

    public class AffixPrototype : Prototype
    {
        public AffixPosition Position { get; protected set; }
        public PrototypePropertyCollection Properties { get; protected set; }
        public LocaleStringId DisplayNameText { get; protected set; }
        public int Weight { get; protected set; }
        public PrototypeId[] TypeFilters { get; protected set; }
        public PropertyPickInRangeEntryPrototype[] PropertyEntries { get; protected set; }
        public AssetId[] Keywords { get; protected set; }
        public DropRestrictionPrototype[] DropRestrictions { get; protected set; }
        public DuplicateHandlingBehavior DuplicateHandlingBehavior { get; protected set; }
    }

    public class AffixPowerModifierPrototype : AffixPrototype
    {
        public bool IsForSinglePowerOnly { get; protected set; }
        public EvalPrototype PowerBoostMax { get; protected set; }
        public EvalPrototype PowerGrantRankMax { get; protected set; }
        public PrototypeId PowerKeywordFilter { get; protected set; }
        public EvalPrototype PowerUnlockLevelMax { get; protected set; }
        public EvalPrototype PowerUnlockLevelMin { get; protected set; }
        public EvalPrototype PowerBoostMin { get; protected set; }
        public EvalPrototype PowerGrantRankMin { get; protected set; }
        public PrototypeId PowerProgTableTabRef { get; protected set; }
    }

    public class AffixRegionModifierPrototype : AffixPrototype
    {
        public PrototypeId AffixTable { get; protected set; }
    }

    public class AffixRegionRestrictedPrototype : AffixPrototype
    {
        public PrototypeId RequiredRegion { get; protected set; }
        public PrototypeId[] RequiredRegionKeywords { get; protected set; }
    }

    public class AffixTeamUpPrototype : AffixPrototype
    {
        public bool IsAppliedToOwnerAvatar { get; protected set; }
    }

    public class AffixRunewordPrototype : AffixPrototype
    {
        public PrototypeId Runeword { get; protected set; }
    }

    public class RunewordDefinitionEntryPrototype : Prototype
    {
        public PrototypeId Rune { get; protected set; }
    }

    public class RunewordDefinitionPrototype : Prototype
    {
        public RunewordDefinitionEntryPrototype[] Runes { get; protected set; }
    }

    public class AffixEntryPrototype : Prototype
    {
        public PrototypeId Affix { get; protected set; }
        public PrototypeId Power { get; protected set; }
        public PrototypeId Avatar { get; protected set; }
    }

    public class LeveledAffixEntryPrototype : AffixEntryPrototype
    {
        public int LevelRequired { get; protected set; }
        public LocaleStringId LockedDescriptionText { get; protected set; }
    }

    public class AffixDisplaySlotPrototype : Prototype
    {
        public AssetId[] AffixKeywords { get; protected set; }
        public LocaleStringId DisplayText { get; protected set; }
    }

    public class ModPrototype : Prototype
    {
        public LocaleStringId TooltipTitle { get; protected set; }
        public AssetId UIIcon { get; protected set; }
        public LocaleStringId TooltipDescription { get; protected set; }
        public PrototypePropertyCollection Properties { get; protected set; }     // Property list, should this be a property collection?
        public PrototypeId[] PassivePowers { get; protected set; }
        public PrototypeId Type { get; protected set; }
        public int RanksMax { get; protected set; }
        public CurveId RankCostCurve { get; protected set; }
        public PrototypeId TooltipTemplateCurrentRank { get; protected set; }
        public EvalPrototype[] EvalOnCreate { get; protected set; }
        public PrototypeId TooltipTemplateNextRank { get; protected set; }
        public PropertySetEntryPrototype[] PropertiesForTooltips { get; protected set; }
        public AssetId UIIconHiRes { get; protected set; }
    }

    public class ModTypePrototype : Prototype
    {
        public PrototypeId AggregateProperty { get; protected set; }
        public PrototypeId TempProperty { get; protected set; }
        public PrototypeId BaseProperty { get; protected set; }
        public PrototypeId CurrencyIndexProperty { get; protected set; }
        public CurveId CurrencyCurve { get; protected set; }
        public bool UseCurrencyIndexAsValue { get; protected set; }
    }

    public class ModGlobalsPrototype : Prototype
    {
        public PrototypeId RankModType { get; protected set; }
        public PrototypeId SkillModType { get; protected set; }
        public PrototypeId EnemyBoostModType { get; protected set; }
        public PrototypeId PvPUpgradeModType { get; protected set; }
        public PrototypeId TalentModType { get; protected set; }
        public PrototypeId OmegaBonusModType { get; protected set; }
        public PrototypeId OmegaHowToTooltipTemplate { get; protected set; }
        public PrototypeId InfinityHowToTooltipTemplate { get; protected set; }
    }

    public class SkillPrototype : ModPrototype
    {
        public CurveId DamageBonusByRank { get; protected set; }
    }

    public class TalentSetPrototype : ModPrototype
    {
        public LocaleStringId UITitle { get; protected set; }
        public PrototypeId[] Talents { get; protected set; }
    }

    public class TalentPrototype : ModPrototype
    {
    }

    public class OmegaBonusPrototype : ModPrototype
    {
        public PrototypeId[] Prerequisites { get; protected set; }
        public int UIHexIndex { get; protected set; }
    }

    public class OmegaBonusSetPrototype : ModPrototype
    {
        public LocaleStringId UITitle { get; protected set; }
        public PrototypeId[] OmegaBonuses { get; protected set; }
        public OmegaPageType UIPageType { get; protected set; }
        public bool Unlocked { get; protected set; }
        public AssetId UIColor { get; protected set; }
        public AssetId UIBackgroundImage { get; protected set; }
    }

    public class InfinityGemBonusPrototype : ModPrototype
    {
        public PrototypeId[] Prerequisites { get; protected set; }
    }

    public class InfinityGemSetPrototype : ModPrototype
    {
        public LocaleStringId UITitle { get; protected set; }
        public PrototypeId[] Bonuses { get; protected set; }    // VectorPrototypeRefPtr InfinityGemBonusPrototype
        public InfinityGem Gem { get; protected set; }
        public bool Unlocked { get; protected set; }
        public AssetId UIColor { get; protected set; }
        public AssetId UIBackgroundImage { get; protected set; }
        public LocaleStringId UIDescription { get; protected set; }
        public new AssetId UIIcon { get; protected set; }
        public AssetId UIIconRadialNormal { get; protected set; }
        public AssetId UIIconRadialSelected { get; protected set; }
    }

    public class RankPrototype : ModPrototype
    {
        public Rank Rank { get; protected set; }
        public HealthBarType HealthBarType { get; protected set; }
        public LootRollModifierPrototype[] LootModifiers { get; protected set; }
        public LootDropEventType LootTableParam { get; protected set; }
        public OverheadInfoDisplayType OverheadInfoDisplayType { get; protected set; }
        public PrototypeId[] Keywords { get; protected set; }
        public int BonusItemFindPoints { get; protected set; }

        public bool IsRankBoss()
        {
            return Rank == Rank.Boss || Rank == Rank.GroupBoss;
        }
    }

    public class EnemyBoostSetPrototype : Prototype
    {
        public PrototypeId[] Modifiers { get; protected set; }

        public bool Contains(PrototypeId affixRef)
        {
            return Modifiers.HasValue() ? Modifiers.Contains(affixRef) : false;
        }
    }

    public class EnemyBoostPrototype : ModPrototype
    {
        public PrototypeId ActivePower { get; protected set; }
        public bool ShowVisualFX { get; protected set; }
        public bool DisableForControlledAgents { get; protected set; }
        public bool CountsAsAffixSlot { get; protected set; }
    }

    public class AffixTableEntryPrototype : Prototype
    {
        public PrototypeId AffixTable { get; protected set; }
        public int ChancePct { get; protected set; }
        [DoNotCopy]
        public EnemyBoostSetPrototype AffixTablePrototype { get => AffixTable.As<EnemyBoostSetPrototype>(); }

        internal PrototypeId RollAffix(GRandom random, HashSet<PrototypeId> affixes, HashSet<PrototypeId> exclude)
        {
            throw new NotImplementedException();
        }
    }

    public class RankAffixEntryPrototype : Prototype
    {
        public AffixTableEntryPrototype[] Affixes { get; protected set; }
        public PrototypeId Rank { get; protected set; }
        public int Weight { get; protected set; }

        public AffixTableEntryPrototype GetAffixSlot(int slot)
        {
            if (Affixes.HasValue() && slot >=0 && slot < Affixes.Length)
                return Affixes[slot];
            return null; // empty prototype
        }

        public int GetMaxAffixes()
        {
            return (Affixes.HasValue() ? Affixes.Length : 0);
        }
    }

    public class RarityPrototype : Prototype
    {
        public PrototypeId DowngradeTo { get; protected set; }
        public PrototypeId TextStyle { get; protected set; }
        public CurveId Weight { get; protected set; }
        public LocaleStringId DisplayNameText { get; protected set; }
        public int BroadcastToPartyLevelMax { get; protected set; }
        public AffixEntryPrototype[] AffixesBuiltIn { get; protected set; }
        public int ItemLevelBonus { get; protected set; }
    }
}
