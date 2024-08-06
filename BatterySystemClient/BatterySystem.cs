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
		public static Dictionary<TacticalComboVisualController, ResourceComponent> lightMods = new Dictionary<TacticalComboVisualController, ResourceComponent>();
        private static bool _drainingTacDeviceBattery;

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

            var lightModKeys = lightMods.Keys.ToArray();
            foreach (TacticalComboVisualController deviceController in lightModKeys) // tactical devices on active weapon
				if (IsInSlot(deviceController.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)
					&& !BatterySystemPlugin.batteryDictionary.ContainsKey(deviceController.LightMod.Item))
					BatterySystemPlugin.batteryDictionary.Add(deviceController.LightMod.Item, lightMods[deviceController]?.Value > 0);
		}

		public static void SetDeviceComponents(TacticalComboVisualController deviceInstance)
		{
			//before applying new sights, remove sights that are not on equipped weapon
			for (int i = lightMods.Keys.Count - 1; i >= 0; i--)
			{
				TacticalComboVisualController key = lightMods.Keys.ElementAt(i);
				if (!IsInSlot(key.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
				{
					lightMods.Remove(key);
				}
			}

			if (IsInSlot(deviceInstance.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
			{
				// if sight is already in dictionary, dont add it
				if (!lightMods.Keys.Any(key => key.LightMod.Item == deviceInstance.LightMod.Item)
					&& (deviceInstance.LightMod.Item.Template.Parent._id == "55818b084bdc2d5b648b4571" //flashlight
					|| deviceInstance.LightMod.Item.Template.Parent._id == "55818b0e4bdc2dde698b456e" //laser
					|| deviceInstance.LightMod.Item.Template.Parent._id == "55818b164bdc2ddc698b456c")) //combo
				{
					lightMods.Add(deviceInstance, deviceInstance.LightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault());
				}
			}
			CheckDeviceIfDraining();
			UpdateBatteryDictionary();
		}
		public static void CheckDeviceIfDraining()
		{
			for (int i = 0; i < lightMods.Keys.Count; i++)
			{
				TacticalComboVisualController key = lightMods.Keys.ElementAt(i);
				if (key?.LightMod?.Item != null)
				{
					lightMods[key] = key.LightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault();
					_drainingTacDeviceBattery = (lightMods[key] != null && key.LightMod.IsActive && lightMods[key].Value > 0
						&& IsInSlot(key.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot));

					if (BatterySystemPlugin.batteryDictionary.ContainsKey(key.LightMod.Item))
						BatterySystemPlugin.batteryDictionary[key.LightMod.Item] = _drainingTacDeviceBattery;

					// true for finding inactive gameobject reticles
					foreach (LaserBeam laser in key.gameObject.GetComponentsInChildren<LaserBeam>(true))
					{
						laser.gameObject.gameObject.SetActive(_drainingTacDeviceBattery);
					}
					foreach (Light light in key.gameObject.GetComponentsInChildren<Light>(true))
					{
						light.gameObject.gameObject.SetActive(_drainingTacDeviceBattery);
					}
				}
			}
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