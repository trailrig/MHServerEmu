﻿using System.Diagnostics;
using MHServerEmu.Core.Logging;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.Prototypes;

namespace MHServerEmu.Games.Properties
{
    public class PropertyInfoTable
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private readonly Dictionary<PropertyEnum, PropertyInfo> _propertyInfoDict = new();
        private readonly Dictionary<PrototypeId, PropertyEnum> _prototypeIdToPropertyEnumDict = new();

        public static readonly (string, Type)[] AssetEnumBindings = new(string, Type)[]     // s_PropertyParamEnumLookups
        {
            ("ProcTriggerType",                 typeof(ProcTriggerType)),
            ("DamageType",                      typeof(DamageType)),
            ("TargetRestrictionType",           typeof(TargetRestrictionType)),
            ("PowerEventType",                  typeof(PowerEventType)),
            ("LootDropEventType",               typeof(LootDropEventType)),
            ("LootDropActionType",              typeof(LootActionType)),
            ("PowerConditionType",              typeof(PowerConditionType)),
            ("ItemEffectUnrealClass",           null),
            ("HotspotNegateByAllianceType",     typeof(HotspotNegateByAllianceType)),
            ("DEPRECATEDDifficultyMode",        typeof(DEPRECATEDDifficultyMode)),
            ("EntityGameEventEnum",             typeof(EntityGameEventEnum)),
            ("EntitySelectorActionEventType",   typeof(EntitySelectorActionEventType)),
            ("Weekday",                         typeof(Weekday)),
            ("AffixPositionType",               typeof(AffixPosition)),
            ("ManaType",                        typeof(ManaType)),
            ("Ranks",                           typeof(Rank))
        };

        public void Initialize()
        {
            var stopwatch = Stopwatch.StartNew();

            var dataDirectory = GameDatabase.DataDirectory;

            var propertyBlueprintId = dataDirectory.PropertyBlueprint;
            var propertyInfoBlueprintId = dataDirectory.PropertyInfoBlueprint;
            var propertyInfoDefaultPrototypeId = dataDirectory.GetBlueprintDefaultPrototype(propertyInfoBlueprintId);

            // Create property infos
            foreach (PrototypeId propertyInfoPrototypeRef in dataDirectory.IteratePrototypesInHierarchy(propertyInfoBlueprintId))
            {
                if (propertyInfoPrototypeRef == propertyInfoDefaultPrototypeId) continue;

                string prototypeName = GameDatabase.GetPrototypeName(propertyInfoPrototypeRef);
                string propertyName = Path.GetFileNameWithoutExtension(prototypeName);

                // Note: in the client there are enums that are not pre-defined in the property enum. The game handles this
                // by adding them to the property info table here, but we just have them in the enum.
                // See PropertyEnum.cs for more details.
                var propertyEnum = Enum.Parse<PropertyEnum>(propertyName);

                // Add data ref -> property enum lookup
                _prototypeIdToPropertyEnumDict.Add(propertyInfoPrototypeRef, propertyEnum);

                // Create property info instance
                PropertyInfo propertyInfo = new(propertyEnum, propertyName, propertyInfoPrototypeRef);
                _propertyInfoDict.Add(propertyEnum, propertyInfo);
            }

            // Match property infos with mixin prototypes where possible
            foreach (var blueprint in GameDatabase.DataDirectory.IterateBlueprints())
            {
                // Skip irrelevant blueprints
                if (blueprint.Id == propertyBlueprintId) continue;
                if (blueprint.RuntimeBindingClassType != typeof(PropertyPrototype)) continue;

                // Get property name from blueprint file path
                string propertyBlueprintName = GameDatabase.GetBlueprintName(blueprint.Id);
                string propertyName = Path.GetFileNameWithoutExtension(propertyBlueprintName);

                // Try to find a matching property info for this property mixin
                bool infoFound = false;
                foreach (var propertyInfo in _propertyInfoDict.Values)
                {
                    // Property mixin blueprints are inconsistently named: most have the Prop suffix, but some do not
                    if (propertyInfo.PropertyName == propertyName || propertyInfo.PropertyInfoName == propertyName)
                    {
                        blueprint.SetPropertyPrototypeDataRef(propertyInfo.PropertyInfoPrototypeRef);
                        propertyInfo.PropertyMixinBlueprintRef = blueprint.Id;
                        infoFound = true;
                        break;
                    }
                }

                // All mixins should have a matching info. If this goes off, something went wrong
                if (infoFound == false)
                    Logger.Warn($"Failed to find matching property info for property mixin {propertyName}");
            }

            // Preload infos
            foreach (var propertyInfo in _propertyInfoDict.Values)
                LoadPropertyInfo(propertyInfo);                

            // Preload property default prototypes
            foreach (var propertyPrototypeId in GameDatabase.DataDirectory.IteratePrototypesInHierarchy(typeof(PropertyPrototype)))
                GameDatabase.GetPrototype<Prototype>(propertyPrototypeId);

            // todo: eval dependencies

            // Finish initialization
            if (Verify())
                Logger.Info($"Initialized info for {_propertyInfoDict.Count} properties in {stopwatch.ElapsedMilliseconds} ms");
            else
                Logger.Error("Failed to initialize PropertyInfoTable");
        }

        public PropertyInfo LookupPropertyInfo(PropertyEnum property)
        {
            if (property == PropertyEnum.Invalid)
                Logger.WarnReturn<PropertyInfo>(null, "Attempted to lookup property info for invalid enum");

            return _propertyInfoDict[property];
        }

        public PropertyEnum GetPropertyEnumFromPrototype(PrototypeId propertyDataRef)
        {
            if (_prototypeIdToPropertyEnumDict.TryGetValue(propertyDataRef, out var propertyEnum) == false)
                return PropertyEnum.Invalid;

            return propertyEnum;
        }

        public bool Verify() => _propertyInfoDict.Count > 0;

        private bool LoadPropertyInfo(PropertyInfo propertyInfo)
        {
            if (propertyInfo.IsFullyLoaded) return true;

            // Load mixin property prototype if there is one
            if (propertyInfo.PropertyMixinBlueprintRef != BlueprintId.Invalid)
            {
                Blueprint blueprint = GameDatabase.GetBlueprint(propertyInfo.PropertyMixinBlueprintRef);
                GameDatabase.GetPrototype<PropertyPrototype>(blueprint.DefaultPrototypeId);
            }

            // Load the property info prototype and assign it to the property info instance
            if (propertyInfo.PropertyInfoPrototypeRef != PrototypeId.Invalid)
            {
                var propertyInfoPrototype = GameDatabase.GetPrototype<PropertyInfoPrototype>(propertyInfo.PropertyInfoPrototypeRef);
                propertyInfo.SetPropertyInfoPrototype(propertyInfoPrototype);
            }

            propertyInfo.IsFullyLoaded = true;
            return true;    // propertyInfo.VerifyPropertyInfo()
        }
    }
}
