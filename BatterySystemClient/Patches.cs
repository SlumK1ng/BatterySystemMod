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
	public class PlayerInitPatch : ModulePatch
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
				BatterySystem.lightMods.Clear(); // same for tactical devices
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
            Item AABatteryItem = Singleton<ItemFactory>.Instance.GetPresetItem("5672cb124bdc2d1a0f8b4568");
            Item CR2032Item = Singleton<ItemFactory>.Instance.GetPresetItem("5672cb304bdc2dc2088b456a");
            Item CR123Item = Singleton<ItemFactory>.Instance.GetPresetItem("590a358486f77429692b2790");
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
				int resourceAvg = random.Next(0, 5);
				if (batteryResource.MaxResource > 0)
				{
					//TODO simplify & configurable avg value
					if (botPlayer.Side != EPlayerSide.Savage)
					{
						resourceAvg = (int)(botPlayer.Profile.Info.Level / 150f * batteryResource.MaxResource);
					}
                    batteryResource.Value = random.Next(Mathf.Max(resourceAvg - 10, 0), (int)Mathf.Min(resourceAvg + 5, batteryResource.MaxResource));
				}
			}
		}
	}

	public class ApplyItemPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(Slot).GetMethod(nameof(Slot.ApplyContainedItem));
		}

		[PatchPostfix]
		static void Postfix(ref Slot __instance) // limit to only player asap
		{
			if (BatterySystemPlugin.InGame() && __instance.ContainedItem.ParentRecursiveCheck(BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear).ParentItem))
			{
				if (BatterySystem.IsInSlot(__instance.ContainedItem, BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Earpiece)))
				{
                    HeadsetBatteries.SetEarPieceComponents();
					return;
				}
				else if (BatterySystem.IsInSlot(__instance.ParentItem, BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear)))
				{ //if item in headwear slot applied
					HeadwearBatteries.SetHeadWearComponents();
					return;
				}
				else if (BatterySystem.IsInSlot(__instance.ContainedItem, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
				{ // if sight is removed and empty slot is applied, then remove the sight from sightdb
					BatterySystem.CheckDeviceIfDraining();
					SightBatteries.CheckSightIfDraining();
					return;
				}
                HeadsetBatteries.SetEarPieceComponents();
                HeadwearBatteries.SetHeadWearComponents();
			}
		}
	}

	public class TacticalDevicePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(TacticalComboVisualController).GetMethod(nameof(TacticalComboVisualController.UpdateBeams));
		}

		[PatchPostfix]
		static void Postfix(ref TacticalComboVisualController __instance)
		{
			//only sights on equipped weapon are added
			if (BatterySystemPlugin.InGame()
				&& BatterySystem.IsInSlot(__instance?.LightMod?.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
			{
				BatterySystem.SetDeviceComponents(__instance);
			}
		}
	}
}
