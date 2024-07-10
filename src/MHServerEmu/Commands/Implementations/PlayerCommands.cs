﻿using MHServerEmu.Commands.Attributes;
using MHServerEmu.Core.Config;
using MHServerEmu.DatabaseAccess.Models;
using MHServerEmu.Frontend;
using MHServerEmu.Games;
using MHServerEmu.Games.Entities.Avatars;
using MHServerEmu.Games.GameData;
using MHServerEmu.Games.GameData.Calligraphy;
using MHServerEmu.Games.GameData.Prototypes;
using MHServerEmu.Games.Network;
using MHServerEmu.Games.Properties;
using MHServerEmu.Grouping;

namespace MHServerEmu.Commands.Implementations
{
    [CommandGroup("player", "Changes player data for this account.", AccountUserLevel.User)]
    public class PlayerCommands : CommandGroup
    {
        [Command("costume", "Changes costume for the current avatar.\nUsage: player costume [name|reset|default]", AccountUserLevel.User)]
        public string Costume(string[] @params, FrontendClient client)
        {
            if (client == null) return "You can only invoke this command from the game.";
            if (@params.Length == 0) return "Invalid arguments. Type 'help player costume' to get help.";

            PrototypeId costumeId;

            switch (@params[0].ToLower())
            {
                case "reset":
                    costumeId = PrototypeId.Invalid;
                    break;

                case "default": // This undoes visual updates for most heroes
                    costumeId = (PrototypeId)HardcodedBlueprints.Costume;
                    break;

                default:
                    var matches = GameDatabase.SearchPrototypes(@params[0], DataFileSearchFlags.SortMatchesByName | DataFileSearchFlags.CaseInsensitive, HardcodedBlueprints.Costume);

                    if (matches.Any() == false)
                        return $"Failed to find any costumes containing {@params[0]}.";

                    if (matches.Count() > 1)
                    {
                        ChatHelper.SendMetagameMessage(client, $"Found multiple matches for {@params[0]}:");
                        ChatHelper.SendMetagameMessages(client, matches.Select(match => GameDatabase.GetPrototypeName(match)), false);
                        return string.Empty;
                    }

                    costumeId = matches.First();
                    break;
            }

            CommandHelper.TryGetPlayerConnection(client, out PlayerConnection playerConnection, out Game game);
            var player = playerConnection.Player;
            var avatar = player.CurrentAvatar;

            // Update player and avatar properties
            avatar.Properties[PropertyEnum.CostumeCurrent] = costumeId;
            player.Properties[PropertyEnum.AvatarLibraryCostume, 0, avatar.PrototypeDataRef] = costumeId;

            if (costumeId == PrototypeId.Invalid)
                return "Resetting costume.";

            return $"Changing costume to {GameDatabase.GetPrototypeName(costumeId)}.";
        }

        [Command("omegapoints", "Maxes out Omega points.\nUsage: player omegapoints", AccountUserLevel.User)]
        public string OmegaPoints(string[] @params, FrontendClient client)
        {
            if (client == null) return "You can only invoke this command from the game.";

            var config = ConfigManager.Instance.GetConfig<GameOptionsConfig>();
            if (config.InfinitySystemEnabled) return "Set InfinitySystemEnabled to false in Config.ini to enable the Omega system.";

            int value = GameDatabase.AdvancementGlobalsPrototype.OmegaPointsCap;

            CommandHelper.TryGetPlayerConnection(client, out PlayerConnection playerConnection);
            playerConnection.Player.Properties[PropertyEnum.OmegaPoints] = value;

            return $"Setting Omega points to {value}.";
        }

        [Command("infinitypoints", "Maxes out Infinity points.\nUsage: player infinitypoints", AccountUserLevel.User)]
        public string InfinityPoints(string[] @params, FrontendClient client)
        {
            if (client == null) return "You can only invoke this command from the game.";

            var config = ConfigManager.Instance.GetConfig<GameOptionsConfig>();
            if (config.InfinitySystemEnabled == false) return "Set InfinitySystemEnabled to true in Config.ini to enable the Infinity system.";

            long value = GameDatabase.AdvancementGlobalsPrototype.InfinityPointsCapPerGem;
            CommandHelper.TryGetPlayerConnection(client, out PlayerConnection playerConnection);

            foreach (InfinityGem gem in Enum.GetValues<InfinityGem>())
            {
                if (gem == InfinityGem.None) continue;
                playerConnection.Player.Properties[PropertyEnum.InfinityPoints, (int)gem] = value;
            }
            
            return $"Setting all Infinity points to {value}.";
        }
    }
}
