using SPT.Reflection.Patching;
using BatterySystem.Configs;
using BSG.CameraEffects;
using EFT.Animations;
using EFT.InventoryLogic;
using EFT;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Comfort.Common;
using SPT.Reflection.Utils;

namespace BatterySystem
{
	public class SpawnPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return AccessTools.Method(typeof(Player), "Init");
		}

		[PatchPostfix]
		public static async void Postfix(Player __instance, Task __result)
		{
			await __result;

			if (__instance.IsYourPlayer)
			{
				BatterySystemPlugin.localInventory = __instance.InventoryControllerClass.Inventory; //Player Inventory
                SightBatteries.sightMods.Clear(); // remove old sight entries that were saved from previous raid
                TacticalDeviceBatteries.lightMods.Clear(); // same for tactical devices
                HeadsetBatteries.SetEarPieceComponents();
				//__instance.OnSightChangedEvent -= sight => BatterySystem.CheckSightIfDraining();
			}
			else//Spawned bots have their batteries drained
            {
                //Delay draining batteries a bit, to allow mods like Realism-Mod to generate them first
                await Task.Delay(1000);

				AddBatteriesToBot(__instance);
			}
		}
		
        private static void AddBatteriesToBot(Player botPlayer)
        {
            Inventory _botInventory = botPlayer.InventoryControllerClass.Inventory;
            Item AABatteryItem = Singleton<ItemFactory>.Instance.GetPresetItem(BatterySystemPlugin.AABatteryId);
            Item CR2032Item = Singleton<ItemFactory>.Instance.GetPresetItem(BatterySystemPlugin.CR2032BatteryId);
            Item CR123Item = Singleton<ItemFactory>.Instance.GetPresetItem(BatterySystemPlugin.CR123BatteryId);
            foreach (Item item in _botInventory.Equipment.GetAllItems())
            {
                if (item is LootItemClass lootItem)
                {
                    foreach (Slot slot in lootItem.AllSlots)
                    {
						Item battery = null;
						if (slot.CheckCompatibility(AABatteryItem))
							battery = AABatteryItem.CloneItem();
                        if (slot.CheckCompatibility(CR2032Item))
                            battery = CR2032Item.CloneItem();
                        if (slot.CheckCompatibility(CR123Item))
                            battery = CR123Item.CloneItem();

						if (battery == null) continue;

                        slot.Add(battery, false);
                        DrainSpawnedBattery(battery, botPlayer);
                    }
                }
            }
        }

		private static void DrainSpawnedBattery(Item spawnedBattery, Player botPlayer)
        {
            System.Random random = new System.Random();
            //batteries charge depends on their max charge and bot level
            foreach (ResourceComponent batteryResource in spawnedBattery.GetItemComponentsInChildren<ResourceComponent>())
			{
				if (batteryResource.MaxResource > 0)
				{
					//TODO simplify & configurable avg value
					var resourceAvg = random.Next(0, 5);
					//Use player level to determine battery charge
					if (botPlayer.Side == EPlayerSide.Usec || botPlayer.Side == EPlayerSide.Bear)
						resourceAvg = (int)(botPlayer.Profile.Info.Level / 150f * batteryResource.MaxResource);
					
					//Boss always have full battery
					if(botPlayer.AIData?.BotOwner?.Boss?.IamBoss == true)
						resourceAvg = (int)batteryResource.MaxResource;
					
                    batteryResource.Value = Mathf.Clamp(random.Next(resourceAvg - 10, Mathf.Min(resourceAvg + 5)), 
	                    0, batteryResource.MaxResource);
				}
			}
		}
	}
}
