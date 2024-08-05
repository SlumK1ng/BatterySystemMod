using BepInEx.Configuration;

namespace BatterySystem.Configs
{
	internal class BatterySystemConfig
	{
		public static ConfigEntry<bool> EnableMod { get; private set; }
		public static ConfigEntry<float> DrainMultiplier { get; private set; }
		public static ConfigEntry<bool> EnableHeadsets { get; private set; }
		public static ConfigEntry<bool> AutoUnfold { get; private set; }
		//public static ConfigEntry<int> SpawnDurabilityMin { get; private set; }
		//public static ConfigEntry<int> SpawnDurabilityMax { get; private set; }

		//public static ConfigEntry<float> CompressorMixerVolume { get; private set; }
		//public static ConfigEntry<float> MainMixerVolume { get; private set; }
		//public static ConfigEntry<float> CompressorGain { get; private set; }

		private static string generalSettings = "General Settings";

		public static void Init(ConfigFile Config)
		{
			{
				EnableMod = Config.Bind(generalSettings, "Enable Mod", true,
					new ConfigDescription("Enable or disable the mod. Requires the game to be restarted.",
					null,
					new ConfigurationManagerAttributes { IsAdvanced = false, Order = 100 }));

				EnableHeadsets = Config.Bind(generalSettings, "Enable Headsets", true,
					new ConfigDescription("Enable BatterySystem for headsets. Disable this if your headsets behave weirdly with other mods such as Realism. Requires restart.",
					null,
					new ConfigurationManagerAttributes { IsAdvanced = false, Order = 75 }));

				DrainMultiplier = Config.Bind(generalSettings, "Battery Drain Multiplier", 1f,
					new ConfigDescription("Adjust the drain multiplier when NVG is on. By default a battery lasts an hour on NVGs and 2.5 hours on collimators.",
					new AcceptableValueRange<float>(0f, 10f),
					new ConfigurationManagerAttributes { IsAdvanced = false, Order = 0 }));

				AutoUnfold = Config.Bind(generalSettings, "Auto Unfold (NOT IMPLEMENTED)", true,
					new ConfigDescription("Automatically unfold iron sights when the main sight runs out of battery. Doesn't do anything yet.",
					null,
					new ConfigurationManagerAttributes { IsAdvanced = true, Order = -50 }));


				/*SpawnDurabilityMin = Config.Bind(generalSettings, "Spawn Durability Min", 5,
					new ConfigDescription("Adjust the minimum durability a battery can spawn with on bots.",
					new AcceptableValueRange<int>(0, 100),
					new ConfigurationManagerAttributes { IsAdvanced = false, Order = -50 }));

				SpawnDurabilityMax = Config.Bind(generalSettings, "Spawn Durability Max", 15,
					new ConfigDescription("Adjust the maximum durability a battery can spawn with on bots. This must be ATLEAST the same value as Spawn Durability Minimum.",
					new AcceptableValueRange<int>(0, 100),
					new ConfigurationManagerAttributes { IsAdvanced = false, Order = -100 }));
				
				CompressorMixerVolume = Config.Bind(generalSettings, "CompressorMixerVolume", -3f,
					new ConfigDescription("",
					new AcceptableValueRange<float>(-30f, 10f),
					new ConfigurationManagerAttributes { IsAdvanced = false, Order = -180 }));

				MainMixerVolume = Config.Bind(generalSettings, "MainMixerVolume", -0f,
					new ConfigDescription("",
					new AcceptableValueRange<float>(-30f, 10f),
					new ConfigurationManagerAttributes { IsAdvanced = false, Order = -230 }));
				*/
			}
		}
	}
}
