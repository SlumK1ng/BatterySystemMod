﻿using BepInEx;
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
	 * Enable switching to iron sights when battery runs out
	 * Change background color of empty items
	 * equipping and removing headwear gives infinite nvg
	 * Sound when toggling battery runs out or is removed or added
	 * flir does not require batteries, make recharge craft
	 * battery recharger - idea by Props
	 */
	[BepInPlugin("com.jiro.batterysystem", "BatterySystem", "1.6.0")]
	//[BepInDependency("com.AKI.core", "3.8.0")]
	public class BatterySystemPlugin : BaseUnityPlugin
	{
		public const string AABatteryId = "5672cb124bdc2d1a0f8b4568";
		public const string CR2032BatteryId = "5672cb304bdc2dc2088b456a";
		public const string CR123BatteryId = "590a358486f77429692b2790";
        public static Dictionary<Item, bool> batteryDictionary = new Dictionary<Item, bool>();
        //resource drain all batteries that are on // using dictionary to help and sync draining batteries

        public static Inventory localInventory;

		public void Awake()
		{
			BatterySystemConfig.Init(Config);
			if (!BatterySystemConfig.EnableMod.Value) return;

			new SpawnPatch().Enable();
			new AimSightPatch().Enable();
			if (BatterySystemConfig.EnableHeadsets.Value)
				new UpdatePhonesPatch().Enable();
			new ApplyItemPatch().Enable();
			new SightDevicePatch().Enable();
			new TacticalDevicePatch().Enable();
			new NvgHeadWearPatch().Enable();
			new ThermalHeadWearPatch().Enable();
			new PoolManagerBatteryBonePatch().Enable();
            //new FoldableSightPatch().Enable();

            InvokeRepeating(nameof(Heartbeat), 1, 1);
		}

		//Gets called every second
		private void Heartbeat() => DrainBatteries();

        private static void DrainBatteries()
		{
			if (!InGame()) return;

			// Update the draining status BEFORE draining
			HeadsetBatteries.CheckEarPieceIfDraining();
			NightVisionBatteries.CheckHeadWearIfDraining();
			TacticalDeviceBatteries.CheckDeviceIfDraining();
			SightBatteries.CheckSightIfDraining();

			var batteryKeys = batteryDictionary.Keys.ToArray();
            foreach (Item batteryItem in batteryKeys)
			{
				//Is draining disabled on this battery?
				if (!batteryDictionary[batteryItem]) continue;

				//for sights, earpiece and tactical devices
				if (batteryItem.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault() is ResourceComponent batteryResource)
				{
					batteryResource.Value -= 1 / 100f * BatterySystemConfig.DrainMultiplier.Value; //2 hr

					//when battery has no charge left
					if (batteryResource.Value < 0f)
					{
						batteryResource.Value = 0f;

						HeadsetBatteries.CheckEarPieceIfDraining();
						NightVisionBatteries.CheckHeadWearIfDraining();
						TacticalDeviceBatteries.CheckDeviceIfDraining();
						SightBatteries.CheckSightIfDraining();
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