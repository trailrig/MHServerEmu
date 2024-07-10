﻿using System.Text;
using Google.ProtocolBuffers;
using MHServerEmu.Core.Extensions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Serialization;
using MHServerEmu.Games.Common;
using MHServerEmu.Games.Entities;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.Properties;
using MHServerEmu.Games.Regions;

namespace MHServerEmu.Games.MetaGames
{
    public class MetaGame : Entity
    {
        public static readonly Logger Logger = LogManager.CreateLogger();

        protected ReplicatedVariable<string> _name = new(0, string.Empty);

        // new
        public MetaGame(Game game) : base(game) { }

        public override bool Initialize(EntitySettings settings)
        {
            base.Initialize(settings);

            _name = new(0, "");
            Region region = Game.RegionManager.GetRegion(settings.RegionId);
            region?.RegisterMetaGame(this);

            return true;
        }

        public override bool Serialize(Archive archive)
        {
            bool success = base.Serialize(archive);
            // if (archive.IsTransient)
            success &= Serializer.Transfer(archive, ref _name);
            return success;
        }

        public override void Destroy()
        {
            // TODO clear Teams;
            base.Destroy();
        }

        protected override void BuildString(StringBuilder sb)
        {
            base.BuildString(sb);

            sb.AppendLine($"{nameof(_name)}: {_name}");
        }

        // TODO event registry States
        public void RegistyStates()
        {
            Region region = Game.RegionManager.GetRegion(RegionId);           
            if (region == null) return;
            var popManager = region.PopulationManager;
            if (Prototype is not MetaGamePrototype metaGameProto) return;
            if (metaGameProto.GameModes.HasValue())
            {
                var gameMode = metaGameProto.GameModes.First().As<MetaGameModePrototype>();
                if (gameMode == null) return;

                if (gameMode.ApplyStates.HasValue())
                    foreach(var state in gameMode.ApplyStates)
                        popManager.MetaStateRegisty(state);

                if (region.PrototypeId == RegionPrototypeId.HoloSimARegion1to60) // Hardcode for Holo-Sim
                {
                    MetaGameStateModePrototype stateMode = gameMode as MetaGameStateModePrototype;
                    int wave = Game.Random.Next(0, stateMode.States.Length);
                    popManager.MetaStateRegisty(stateMode.States[wave]);
                } 
                else if (region.PrototypeId == RegionPrototypeId.LimboRegionL60) // Hardcode for Limbo
                {
                    MetaGameStateModePrototype stateMode = gameMode as MetaGameStateModePrototype;
                    popManager.MetaStateRegisty(stateMode.States[0]);
                }
                else if (region.PrototypeId == RegionPrototypeId.CH0402UpperEastRegion) // Hack for Moloids
                    popManager.MetaStateRegisty((PrototypeId)7730041682554854878); // CH04UpperMoloids
                else if (region.PrototypeId == RegionPrototypeId.SurturRaidRegionGreen) // Hardcode for Surtur
                {   
                    var stateRef = (PrototypeId)5463286934959496963; // SurturMissionProgressionStateFiveMan
                    var missionProgression = stateRef.As<MetaStateMissionProgressionPrototype>();
                    foreach(var state in missionProgression.StatesProgression)
                        popManager.MetaStateRegisty(state);
                }
            }
        }
    }
}
