using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ValorantStatus {
	internal class Constants {
		private static readonly ReadOnlyDictionary<string, string> MapNames = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(){
			{ "/Game/Maps/Ascent/Ascent", "Ascent"},
			{"/Game/Maps/Bonsai/Bonsai", "Split" },
			{"/Game/Maps/Duality/Duality", "Bind" },
			{"/Game/Maps/Port/Port", "Icebox" },
			{"/Game/Maps/Triad/Triad", "Haven" },
			{"/Game/Maps/Foxtrot/Foxtrot", "Breeze" },
			{"/Game/Maps/Canyon/Canyon", "Fracture" },
			{"/Game/Maps/Poveglia/Range", "The Range" }
		});

		private static readonly ReadOnlyDictionary<string, string> ModeNames = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(){
			{ "/Game/GameModes/Bomb/BombGameMode.BombGameMode_C", "Standard"},
			{"/Game/GameModes/Deathmatch/DeathmatchGameMode.DeathmatchGameMode_C", "Deathmatch" },
			{"/Game/GameModes/GunGame/GunGameTeamsGameMode.GunGameTeamsGameMode_C", "Escalation" },
			{"/Game/GameModes/OneForAll/OneForAll_GameMode.OneForAll_GameMode_C", "Replication" },
			{"/Game/GameModes/QuickBomb/QuickBombGameMode.QuickBombGameMode_C", "Spike Rush" },
			{"/Game/GameModes/ShootingRange/ShootingRangeGameMode.ShootingRangeGameMode_C", "Shooting Range" }
		});

		internal static string GetMapName(string mapId) => MapNames.TryGetValue(mapId, out string mapName) ? mapName : "Unknown Map";

		internal static string GetModeName(string modeId) => ModeNames.TryGetValue(modeId, out string modeName) ? modeName : "Unknown Mode";

		internal enum CurrentStatus {
			Unknown,
			InMatch,
			InMenu,
			InRange,
		}
	}
}
