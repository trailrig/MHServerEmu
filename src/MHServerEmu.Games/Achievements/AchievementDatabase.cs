﻿using System.Diagnostics;
using System.Text.Json;
using Google.ProtocolBuffers;
using Gazillion;
using MHServerEmu.Core.Helpers;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.Serialization;
using MHServerEmu.Games.Locales;
using MHServerEmu.Core.System.Time;

namespace MHServerEmu.Games.Achievements
{
    /// <summary>
    /// A singleton that contains achievement infomation.
    /// </summary>
    public class AchievementDatabase
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        private static readonly string AchievementsDirectory = Path.Combine(FileHelper.DataDirectory, "Game", "Achievements");

        private readonly Dictionary<uint, AchievementInfo> _achievementInfoMap = new();
        private byte[] _localizedAchievementStringBuffer = Array.Empty<byte>();
        private NetMessageAchievementDatabaseDump _cachedDump = NetMessageAchievementDatabaseDump.DefaultInstance;

        public static AchievementDatabase Instance { get; } = new();

        public TimeSpan AchievementNewThresholdUS { get; private set; }     // Unix timestamp in seconds

        private AchievementDatabase() { }

        /// <summary>
        /// Initializes the <see cref="AchievementDatabase"/> instance.
        /// </summary>
        public bool Initialize()    // AchievementDatabase::ReceiveDumpMsg()
        {
            Clear();    // Clean up whatever data there is

            var stopwatch = Stopwatch.StartNew();

            // Load achievement info map
            string achievementInfoMapPath = Path.Combine(AchievementsDirectory, "AchievementInfoMap.json");
            if (File.Exists(achievementInfoMapPath) == false)
                return Logger.WarnReturn(false, $"Initialize(): Achievement info map not found at {achievementInfoMapPath}");

            string achievementInfoMapJson = File.ReadAllText(achievementInfoMapPath);
            
            try
            {
                JsonSerializerOptions options = new();
                options.Converters.Add(new TimeSpanJsonConverter());
                var infos = JsonSerializer.Deserialize<IEnumerable<AchievementInfo>>(achievementInfoMapJson, options);

                foreach (AchievementInfo info in infos)
                    _achievementInfoMap.Add(info.Id, info);
            }
            catch (Exception e)
            {
                return Logger.WarnReturn(false, $"Initialize(): Achievement info map deserialization failed - {e.Message}");
            }

            // Load string buffer
            string stringBufferPath = Path.Combine(AchievementsDirectory, "eng.achievements.string");
            if (File.Exists(stringBufferPath) == false)
                return Logger.WarnReturn(false, $"Initialize(): String buffer not found at {stringBufferPath}");

            _localizedAchievementStringBuffer = File.ReadAllBytes(stringBufferPath);

            // Load new achievement threshold
            string thresholdPath = Path.Combine(AchievementsDirectory, "AchievementNewThresholdUS.txt");
            if (File.Exists(thresholdPath) == false)
            {
                // Default to now if file not found
                Logger.Warn($"Initialize(): New achievement threshold not found at {thresholdPath}");
                AchievementNewThresholdUS = Clock.UnixTime;
            }
            else
            {
                string thresholdString = File.ReadAllText(thresholdPath);
                if (long.TryParse(thresholdString, out long threshold) == false)
                {
                    // Default to now if failed to parse
                    Logger.Warn($"Initialize(): Failed to parse new achievement threshold");
                    AchievementNewThresholdUS = Clock.UnixTime;
                }
                else
                {
                    AchievementNewThresholdUS = TimeSpan.FromSeconds(threshold);
                }
            }

            // Post-process
            ImportAchievementStringsToCurrentLocale();
            HookUpParentChildAchievementReferences();
            RebuildCachedData();

            // Create the dump for sending to clients
            CreateDump();

            Logger.Info($"Initialized {_achievementInfoMap.Count} achievements in {stopwatch.ElapsedMilliseconds} ms");
            return true;
        }

        /// <summary>
        /// Returns the <see cref="AchievementInfo"/> with the specified id. Returns <see langword="null"/> if not found.
        /// </summary>
        public AchievementInfo GetAchievementInfoById(uint id)
        {
            if (_achievementInfoMap.TryGetValue(id, out AchievementInfo info) == false)
                return null;

            return info;
        }

        /// <summary>
        /// Returns all <see cref="AchievementInfo"/> instances that use the specified <see cref="ScoringEventType"/>.
        /// </summary>
        public IEnumerable<AchievementInfo> GetAchievementsByEventType(ScoringEventType eventType)
        {
            // TODO: Optimize this if needed
            foreach (AchievementInfo info in _achievementInfoMap.Values)
            {
                if (info.EventType == eventType)
                    yield return info;
            }
        }

        /// <summary>
        /// Returns a <see cref="NetMessageAchievementDatabaseDump"/> instance that contains a compressed dump of the <see cref="AchievementDatabase"/>.
        /// </summary>
        public NetMessageAchievementDatabaseDump GetDump() => _cachedDump;

        /// <summary>
        /// Clears the <see cref="AchievementDatabase"/> instance.
        /// </summary>
        private void Clear()
        {
            _achievementInfoMap.Clear();
            _localizedAchievementStringBuffer = Array.Empty<byte>();
            _cachedDump = NetMessageAchievementDatabaseDump.DefaultInstance;
            AchievementNewThresholdUS = TimeSpan.Zero;
        }

        /// <summary>
        /// Constructs relationships between achievements.
        /// </summary>
        private void HookUpParentChildAchievementReferences()
        {
            foreach (AchievementInfo info in _achievementInfoMap.Values)
            {
                if (info.ParentId == 0) continue;

                if (_achievementInfoMap.TryGetValue(info.ParentId, out AchievementInfo parent) == false)
                {
                    Logger.Warn($"HookUpParentChildAchievementReferences(): Parent info {info.ParentId} not found");
                    continue;
                }

                info.Parent = parent;
                parent.Children.Add(info);
            }
        }

        private void ImportAchievementStringsToCurrentLocale()
        {
            Locale currentLocale = LocaleManager.Instance.CurrentLocale;

            using (MemoryStream ms = new(_localizedAchievementStringBuffer))
                currentLocale.ImportStringStream("achievements", ms);
        }

        private void RebuildCachedData()
        {
            // TODO
        }

        /// <summary>
        /// Creates and caches a <see cref="NetMessageAchievementDatabaseDump"/> instance that will be sent to clients.
        /// </summary>
        private void CreateDump()
        {
            var dumpBuffer = AchievementDatabaseDump.CreateBuilder()
                .SetLocalizedAchievementStringBuffer(ByteString.CopyFrom(_localizedAchievementStringBuffer))
                .AddRangeAchievementInfos(_achievementInfoMap.Values.Select(info => info.ToNetStruct()))
                .SetAchievementNewThresholdUS((ulong)AchievementNewThresholdUS.TotalSeconds)
                .Build().ToByteArray();

            // NOTE: If you don't use the right library to compress this it's going to cause client-side errors.
            // See CompressionHelper.ZLibDeflate() for more details.
            byte[] compressedBuffer = CompressionHelper.ZLibDeflate(dumpBuffer);

            _cachedDump = NetMessageAchievementDatabaseDump.CreateBuilder()
                 .SetCompressedAchievementDatabaseDump(ByteString.CopyFrom(compressedBuffer))
                 .Build();
        }
    }
}
