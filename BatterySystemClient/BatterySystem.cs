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
		public static Item headWearItem = null;
		private static NightVisionComponent _headWearNvg = null;
		private static ThermalVisionComponent _headWearThermal = null;
		private static bool _drainingHeadWearBattery = false;
		public static ResourceComponent headWearBattery = null;

		private static Item _earPieceItem = null;
		private static ResourceComponent _earPieceBattery = null;
		private static bool _drainingEarPieceBattery = false;
		public static float compressorMakeup;
		// compressor is used because the default 
		public static float compressor;

		public static Dictionary<SightModVisualControllers, ResourceComponent> sightMods = new Dictionary<SightModVisualControllers, ResourceComponent>();
		public static Dictionary<TacticalComboVisualController, ResourceComponent> lightMods = new Dictionary<TacticalComboVisualController, ResourceComponent>();
		private static bool _drainingSightBattery;

		public static Item GetheadWearSight() // returns the special device goggles that are equipped
		{
			if (_headWearNvg != null)
				return _headWearNvg.Item;
			else if (_headWearThermal != null)
				return _headWearThermal.Item;
			else
				return null;
		}

		public static bool IsInSlot(Item item, Slot slot)
		{
			if (item != null && slot?.ContainedItem != null && item.IsChildOf(slot.ContainedItem)) return true;
			else return false;
		}

		public static void UpdateBatteryDictionary()
		{
			// Remove unequipped items
			for (int i = BatterySystemPlugin.batteryDictionary.Count - 1; i >= 0; i--)
			{
				Item key = BatterySystemPlugin.batteryDictionary.Keys.ElementAt(i);
				if (!(IsInSlot(key, BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Earpiece))
					|| IsInSlot(key, BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear))
					|| IsInSlot(key, Singleton<GameWorld>.Instance.MainPlayer.ActiveSlot)))
					BatterySystemPlugin.batteryDictionary.Remove(key);
			}

			if (BatterySystemConfig.EnableHeadsets.Value && _earPieceItem != null
				&& !BatterySystemPlugin.batteryDictionary.ContainsKey(_earPieceItem)) // earpiece
				BatterySystemPlugin.batteryDictionary.Add(_earPieceItem, _drainingEarPieceBattery);

			if (GetheadWearSight() != null && !BatterySystemPlugin.batteryDictionary.ContainsKey(GetheadWearSight())) // headwear
				BatterySystemPlugin.batteryDictionary.Add(GetheadWearSight(), _drainingHeadWearBattery);

			foreach (SightModVisualControllers sightController in sightMods.Keys) // sights on active weapon
				if (IsInSlot(sightController.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)
					&& !BatterySystemPlugin.batteryDictionary.ContainsKey(sightController.SightMod.Item))
					BatterySystemPlugin.batteryDictionary.Add(sightController.SightMod.Item, sightMods[sightController]?.Value > 0);

			foreach (TacticalComboVisualController deviceController in lightMods.Keys) // tactical devices on active weapon
				if (IsInSlot(deviceController.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)
					&& !BatterySystemPlugin.batteryDictionary.ContainsKey(deviceController.LightMod.Item))
					BatterySystemPlugin.batteryDictionary.Add(deviceController.LightMod.Item, lightMods[deviceController]?.Value > 0);
		}

		public static void SetEarPieceComponents()
		{
			if (BatterySystemConfig.EnableHeadsets.Value)
			{
				_earPieceItem = BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Earpiece).Items?.FirstOrDefault();
				_earPieceBattery = _earPieceItem?.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault();
				_drainingEarPieceBattery = false;
				CheckEarPieceIfDraining();
				UpdateBatteryDictionary();
			}
		}

		public static void CheckEarPieceIfDraining()
		{
			if (BatterySystemConfig.EnableHeadsets.Value)
			{
				//headset has charged battery installed
				if (_earPieceBattery != null && _earPieceBattery.Value > 0)
				{
					MethodInvoker.GetHandler(AccessTools.Method(typeof(Player), "UpdatePhonesReally"));
					_drainingEarPieceBattery = true;
				}
				//headset has no battery
				else if (_earPieceItem != null)
				{
					Singleton<BetterAudio>.Instance.Master.SetFloat("CompressorMakeup", 0f);
					Singleton<BetterAudio>.Instance.Master.SetFloat("Compressor", compressor - 15f);
					Singleton<BetterAudio>.Instance.Master.SetFloat("MainVolume", -10f);
					_drainingEarPieceBattery = false;
				}
				//no headset equipped
				else
				{
					MethodInvoker.GetHandler(AccessTools.Method(typeof(Player), "UpdatePhonesReally"));
					_drainingEarPieceBattery = false;
				}

				if (_earPieceItem != null && BatterySystemPlugin.batteryDictionary.ContainsKey(_earPieceItem))
					BatterySystemPlugin.batteryDictionary[_earPieceItem] = _drainingEarPieceBattery;
			}
		}

		public static void SetHeadWearComponents()
		{
			headWearItem = BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear).Items?.FirstOrDefault(); // default null else headwear
			_headWearNvg = headWearItem?.GetItemComponentsInChildren<NightVisionComponent>().FirstOrDefault(); //default null else nvg item
			_headWearThermal = headWearItem?.GetItemComponentsInChildren<ThermalVisionComponent>().FirstOrDefault(); //default null else thermal item
			headWearBattery = GetheadWearSight()?.Parent.Item.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault(); //default null else resource

			CheckHeadWearIfDraining();
			UpdateBatteryDictionary();
		}

		public static void CheckHeadWearIfDraining()
		{
			_drainingHeadWearBattery = headWearBattery != null && headWearBattery.Value > 0
				&& (_headWearNvg == null && _headWearThermal != null
				? (_headWearThermal.Togglable.On && !CameraClass.Instance.ThermalVision.InProcessSwitching)
				: (_headWearNvg != null && _headWearThermal == null && _headWearNvg.Togglable.On && !CameraClass.Instance.NightVision.InProcessSwitching));
			// headWear has battery with resource installed and headwear (nvg/thermal) isn't switching and is on

			if (headWearBattery != null && BatterySystemPlugin.batteryDictionary.ContainsKey(GetheadWearSight()))
				BatterySystemPlugin.batteryDictionary[GetheadWearSight()] = _drainingHeadWearBattery;

			if (_headWearNvg != null)
				PlayerInitPatch.nvgOnField.SetValue(CameraClass.Instance.NightVision, _drainingHeadWearBattery);

			else if (_headWearThermal != null)
				PlayerInitPatch.thermalOnField.SetValue(CameraClass.Instance.ThermalVision, _drainingHeadWearBattery);
		}

		public static void SetSightComponents(SightModVisualControllers sightInstance)
		{
			LootItemClass lootItem = sightInstance.SightMod.Item as LootItemClass;

			bool _hasBatterySlot(LootItemClass loot, string[] filters = null)
			{
				//use default parameter if nothing specified (any drainable battery)
				filters = filters ?? new string[] { "5672cb124bdc2d1a0f8b4568", "5672cb304bdc2dc2088b456a", "590a358486f77429692b2790" };
				foreach (Slot slot in loot.Slots)
				{
					if (slot.Filters.FirstOrDefault()?.Filter.Any(sfilter => filters.Contains(sfilter)) == true)
						return true;
				}
				return false;
			}

			//before applying new sights, remove sights that are not on equipped weapon
			for (int i = sightMods.Keys.Count - 1; i >= 0; i--)
			{
				SightModVisualControllers key = sightMods.Keys.ElementAt(i);
				if (!IsInSlot(key.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
				{
					sightMods.Remove(key);
				}
			}

			if (IsInSlot(sightInstance.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot) && _hasBatterySlot(lootItem))
			{
				// if sight is already in dictionary, dont add it
				if (!sightMods.Keys.Any(key => key.SightMod.Item == sightInstance.SightMod.Item)
					&& (sightInstance.SightMod.Item.Template.Parent._id == "55818acf4bdc2dde698b456b" //compact collimator
					|| sightInstance.SightMod.Item.Template.Parent._id == "55818ad54bdc2ddc698b4569" //collimator
					|| sightInstance.SightMod.Item.Template.Parent._id == "55818aeb4bdc2ddc698b456a")) //Special Scope
				{
					sightMods.Add(sightInstance, sightInstance.SightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault());
				}
			}
			CheckSightIfDraining();
			UpdateBatteryDictionary();
		}
		public static void CheckSightIfDraining()
		{
			//for because modifying sightMods[key]
			for (int i = 0; i < sightMods.Keys.Count; i++)
			{
				SightModVisualControllers key = sightMods.Keys.ElementAt(i);
				if (key?.SightMod?.Item != null)
				{
					sightMods[key] = key.SightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault();
					_drainingSightBattery = (sightMods[key] != null && sightMods[key].Value > 0
						&& IsInSlot(key.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot));

					if (BatterySystemPlugin.batteryDictionary.ContainsKey(key.SightMod.Item))
						BatterySystemPlugin.batteryDictionary[key.SightMod.Item] = _drainingSightBattery;

					// true for finding inactive gameobject reticles
					foreach (CollimatorSight col in key.gameObject.GetComponentsInChildren<CollimatorSight>(true))
					{
						col.gameObject.SetActive(_drainingSightBattery);
					}
					foreach (OpticSight optic in key.gameObject.GetComponentsInChildren<OpticSight>(true))
					{
						/*
						//for nv sights
						if (optic.NightVision != null)
						{
							Logger.LogWarning("OPTIC ENABLED: " + optic.NightVision?.enabled);
							//PlayerInitPatch.nvgOnField.SetValue(optic.NightVision, _drainingSightBattery);
							optic.NightVision.enabled = _drainingSightBattery;
							Logger.LogWarning("OPTIC ON: " + optic.NightVision.On);
							continue;
						}*/

						if (key.SightMod.Item.Template.Parent._id != "55818ad54bdc2ddc698b4569" &&
							key.SightMod.Item.Template.Parent._id != "5c0a2cec0db834001b7ce47d") //Exceptions for hhs-1 (tan)
							optic.enabled = _drainingSightBattery;
					}
				}
			}
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
					_drainingSightBattery = (lightMods[key] != null && key.LightMod.IsActive && lightMods[key].Value > 0
						&& IsInSlot(key.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot));

					if (BatterySystemPlugin.batteryDictionary.ContainsKey(key.LightMod.Item))
						BatterySystemPlugin.batteryDictionary[key.LightMod.Item] = _drainingSightBattery;

					// true for finding inactive gameobject reticles
					foreach (LaserBeam laser in key.gameObject.GetComponentsInChildren<LaserBeam>(true))
					{
						laser.gameObject.gameObject.SetActive(_drainingSightBattery);
					}
					foreach (Light light in key.gameObject.GetComponentsInChildren<Light>(true))
					{
						light.gameObject.gameObject.SetActive(_drainingSightBattery);
					}
				}
			}
		}
	}
}