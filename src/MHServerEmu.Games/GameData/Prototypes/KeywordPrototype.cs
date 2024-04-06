﻿using MHServerEmu.Core.Extensions;
using MHServerEmu.Games.Common;

namespace MHServerEmu.Games.GameData.Prototypes
{
    using KeywordsMask = BitList;

    public class KeywordPrototype : Prototype
    {
        public PrototypeId IsAKeyword { get; protected set; }

        private int _bitIndex = -1;
        private KeywordsMask _bitMask = new();

        public static KeywordsMask GetBitMaskForKeywordList(PrototypeId[] keywordsList)
        {
            KeywordsMask rusult = new();

            if (keywordsList.HasValue())
            {
                foreach (var kerwordRef in keywordsList)
                {
                    KeywordPrototype keywordProto = GameDatabase.GetPrototype<KeywordPrototype>(kerwordRef);
                    keywordProto?.GetBitMask(ref rusult);
                }
            }
            return rusult;
        }

        private void GetBitMask(ref KeywordsMask keywordMask)
        {
            if (_bitIndex == -1)
            {
                CacheBitMaskInfo();
                if (_bitMask.Any() == false) return;
                
            }
            keywordMask |= _bitMask;
        }

        private int GetBitIndex()
        {
            if (_bitIndex == -1)
            {
                CacheBitMaskInfo();
                if (_bitMask.Any() == false) return 0;

            }
            return _bitIndex;
        }

        private void CacheBitMaskInfo()
        {
            BlueprintId keywordBlueprintRef = GameDatabase.DataDirectory.KeywordBlueprint;
            if (keywordBlueprintRef != BlueprintId.Invalid)
            {
                _bitIndex = GameDatabase.DataDirectory.GetPrototypeEnumValue(DataRef, keywordBlueprintRef);
                if (_bitIndex >= 0)
                {
                    _bitMask.Clear();
                    _bitMask.Set(_bitIndex);

                    if (IsAKeyword != PrototypeId.Invalid)
                    {
                        KeywordPrototype isAKeywordProto = GameDatabase.GetPrototype<KeywordPrototype>(IsAKeyword);
                        isAKeywordProto?.GetBitMask(ref _bitMask);
                    }
                }
            }
        }

        public static bool TestKeywordBit(KeywordsMask keywordsMask, KeywordPrototype keywordProto)
        {
            return keywordsMask[keywordProto.GetBitIndex()];
        }
    }

    public class EntityKeywordPrototype : KeywordPrototype
    {
        public LocaleStringId DisplayName { get; protected set; }
    }

    public class MobKeywordPrototype : EntityKeywordPrototype
    {
    }

    public class AvatarKeywordPrototype : EntityKeywordPrototype
    {
    }

    public class MissionKeywordPrototype : KeywordPrototype
    {
    }

    public class PowerKeywordPrototype : KeywordPrototype
    {
        public LocaleStringId DisplayName { get; protected set; }
        public bool DisplayInPowerKeywordsList { get; protected set; }
    }

    public class RankKeywordPrototype : KeywordPrototype
    {
    }

    public class RegionKeywordPrototype : KeywordPrototype
    {
    }

    public class AffixCategoryPrototype : KeywordPrototype
    {
    }

    public class FulfillablePrototype : Prototype
    {
    }

}
