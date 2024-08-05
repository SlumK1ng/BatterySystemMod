using BepInEx;
using System.Collections.Generic;
using Comfort.Common;
using UnityEngine;
using EFT;
using BatterySystem.Configs;
using EFT.InventoryLogic;
using System.Linq;

namespace BatterySystem
{
	/*TODO: 
	 * headset battery is 100% and not drained on bots
	 * Enable switching to iron sights when battery runs out
	 * equipping and removing headwear gives infinite nvg
	 * switch to coroutines
	 * flir does not require batteries, make recharge craft
	 * Sound when toggling battery runs out or is removed or added
	 * battery recharger - idea by Props
	 */
	[BepInPlugin("com.jiro.batterysystem", "BatterySystem", "1.5.0")]
	//[BepInDependency("com.AKI.core", "3.8.0")]
	public class BatterySystemPlugin : BaseUnityPlugin
	{
        public static Dictionary<Item, bool> batteryDictionary = new Dictionary<Item, bool>();
        private static Dictionary<string, float> _headWearDrainMultiplier = new Dictionary<string, float>
        {
            { "5c0696830db834001d23f5da", 1f },// PNV-10T Night Vision Goggles, AA Battery
            { "5c0558060db834001b735271", 2f },// GPNVG-18 Night Vision goggles, CR123 battery pack
            { "5c066e3a0db834001b7353f0", 1f },// Armasight N-15 Night Vision Goggles, single CR123A lithium battery
            { "57235b6f24597759bf5a30f1", 0.5f },// AN/PVS-14 Night Vision Monocular, AA Battery
            { "5c110624d174af029e69734c", 3f },// T-7 Thermal Goggles with a Night Vision mount, Double AA
        };
        //resource drain all batteries that are on // using dictionary to help and sync draining batteries

        public static Inventory localInventory;

		public void Awake()
		{
			BatterySystemConfig.Init(Config);
			if (!BatterySystemConfig.EnableMod.Value) return;

			new PlayerInitPatch().Enable();
			new AimSightPatch().Enable();
			//new GetBoneForSlotPatch().Enable();
			if (BatterySystemConfig.EnableHeadsets.Value)
				new UpdatePhonesPatch().Enable();
			new ApplyItemPatch().Enable();
			new SightDevicePatch().Enable();
			//new FoldableSightPatch().Enable();
			new TacticalDevicePatch().Enable();
			new NvgHeadWearPatch().Enable();
			new ThermalHeadWearPatch().Enable();

			InvokeRepeating(nameof(DrainBatteries), 1, 1);
		}

		public static bool InGame()
		{
            return Singleton<GameWorld>.Instance?.MainPlayer?.HealthController.IsAlive == true
					&& !(Singleton<GameWorld>.Instance.MainPlayer is HideoutPlayer);
		}
		//TODO: Throws InvalidOperationException: Collection was modified: enumeration operation may not execute.
		private static void DrainBatteries()
		{
			if (!InGame()) return;

			//here?
			foreach (Item item in batteryDictionary.Keys)
			{
				if (batteryDictionary[item]) // == true
				{
					// Drain headwear NVG/Thermal
					if (BatterySystem.headWearBattery != null && item.IsChildOf(BatterySystem.headWearItem)
						&& BatterySystem.headWearItem.GetItemComponentsInChildren<TogglableComponent>().FirstOrDefault()?.On == true)
					{
						//Default battery lasts 1 hr * configmulti * itemmulti, itemmulti was Hazelify's idea!
						BatterySystem.headWearBattery.Value -= Mathf.Clamp(1 / 36f
								* BatterySystemConfig.DrainMultiplier.Value
								* _headWearDrainMultiplier[BatterySystem.GetheadWearSight()?.TemplateId], 0f, 100f);
						if (item.GetItemComponentsInChildren<ResourceComponent>(false).First().Value < 0f)
						{
							item.GetItemComponentsInChildren<ResourceComponent>(false).First().Value = 0f;
							if (item.IsChildOf(BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear).ContainedItem))
								BatterySystem.CheckHeadWearIfDraining();

						}
					}
					//for sights, earpiece and tactical devices
					else if (item.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault() != null)
					{
						item.GetItemComponentsInChildren<ResourceComponent>(false).First().Value -= 1 / 100f
							* BatterySystemConfig.DrainMultiplier.Value; //2 hr

						//when battery has no charge left
						if (item.GetItemComponentsInChildren<ResourceComponent>(false).First().Value < 0f)
						{
							item.GetItemComponentsInChildren<ResourceComponent>(false).First().Value = 0f;
							if (item.IsChildOf(BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Earpiece).ContainedItem))
								BatterySystem.CheckEarPieceIfDraining();
							else if (item.IsChildOf(Singleton<GameWorld>.Instance.MainPlayer?.ActiveSlot.ContainedItem))
							{
								BatterySystem.CheckDeviceIfDraining();
								BatterySystem.CheckSightIfDraining();
							}
						}
					}
				}
			}
		}
	}
}