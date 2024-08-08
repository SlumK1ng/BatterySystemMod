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
					TacticalDeviceBatteries.CheckDeviceIfDraining();
					SightBatteries.CheckSightIfDraining();
					return;
				}
                HeadsetBatteries.SetEarPieceComponents();
                HeadwearBatteries.SetHeadWearComponents();
			}
		}
	}
}
