using System.Linq;
using System.Reflection;
using SPT.Reflection.Patching;
using HarmonyLib;
using Comfort.Common;
using UnityEngine;
using EFT;
using EFT.InventoryLogic;
using BSG.CameraEffects;
using BatterySystem.Configs;
using System.Threading.Tasks;
using BepInEx.Logging;
using System.Collections.Generic;
using EFT.CameraControl;
using EFT.Animations;
using System.Collections;
using EFT.Visual;

namespace BatterySystem
{
	public class BatterySystem
	{
        public static void UpdateBatteryDictionary()
		{
			// Remove unequipped items
			var batteryKeys = BatterySystemPlugin.batteryDictionary.Keys.ToArray();
            foreach (Item key in batteryKeys)
			{
				if (IsInSlot(key, BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Earpiece))) continue;
                if (IsInSlot(key, BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear))) continue;
                if (IsInSlot(key, Singleton<GameWorld>.Instance.MainPlayer.ActiveSlot)) continue;

				BatterySystemPlugin.batteryDictionary.Remove(key);
			}

			HeadsetBatteries.TrackBatteries();
			HeadwearBatteries.TrackBatteries();
			SightBatteries.TrackBatteries();
			TacticalDeviceBatteries.TrackBatteries();
		}

        public static bool IsInSlot(Item item, Slot slot)
        {
            if (item == null) return false;
            if (slot == null) return false;
            if (slot.ContainedItem == null) return false;

            return item.IsChildOf(slot.ContainedItem);
        }
    }
}