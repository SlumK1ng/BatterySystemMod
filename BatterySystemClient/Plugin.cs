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
        //resource drain all batteries that are on // using dictionary to help and sync draining batteries

        public static Inventory localInventory;

		public void Awake()
		{
			BatterySystemConfig.Init(Config);
			if (!BatterySystemConfig.EnableMod.Value) return;

			new PlayerInitPatch().Enable();
			new AimSightPatch().Enable();
			if (BatterySystemConfig.EnableHeadsets.Value)
				new UpdatePhonesPatch().Enable();
			new ApplyItemPatch().Enable();
			new SightDevicePatch().Enable();
			new TacticalDevicePatch().Enable();
			new NvgHeadWearPatch().Enable();
			new ThermalHeadWearPatch().Enable();

            //new GetBoneForSlotPatch().Enable();
            //new FoldableSightPatch().Enable();

            InvokeRepeating(nameof(Heartbeat), 1, 1);
		}

		//TODO: Throws InvalidOperationException: Collection was modified: enumeration operation may not execute.
		private static void DrainBatteries()
		{
			if (!InGame()) return;

			//here?
			var batteryKeys = batteryDictionary.Keys.ToArray();
            foreach (Item item in batteryKeys)
			{
				if (!batteryDictionary[item]) continue;

				// Drain headwear NVG/Thermal
				if (item.IsChildOf(HeadwearBatteries.headWearItem))
					HeadwearBatteries.Drain(item);

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
							HeadsetBatteries.CheckEarPieceIfDraining();

						else if (item.IsChildOf(Singleton<GameWorld>.Instance.MainPlayer?.ActiveSlot.ContainedItem))
						{
							BatterySystem.CheckDeviceIfDraining();
							SightBatteries.CheckSightIfDraining();
						}
					}
				}
			}
		}

        public static bool InGame()
        {
            return Singleton<GameWorld>.Instance?.MainPlayer?.HealthController.IsAlive == true
                    && !(Singleton<GameWorld>.Instance.MainPlayer is HideoutPlayer);
        }
    }
}