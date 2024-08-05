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
		private static float _mainCooldown = 1f;
		private static Dictionary<string, float> _headWearDrainMultiplier = new Dictionary<string, float>();
		public static Dictionary<Item, bool> batteryDictionary = new Dictionary<Item, bool>();
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
			{
				_headWearDrainMultiplier.Add("5c0696830db834001d23f5da", 1f); // PNV-10T Night Vision Goggles, AA Battery
				_headWearDrainMultiplier.Add("5c0558060db834001b735271", 2f); // GPNVG-18 Night Vision goggles, CR123 battery pack
				_headWearDrainMultiplier.Add("5c066e3a0db834001b7353f0", 1f); // Armasight N-15 Night Vision Goggles, single CR123A lithium battery
				_headWearDrainMultiplier.Add("57235b6f24597759bf5a30f1", 0.5f); // AN/PVS-14 Night Vision Monocular, AA Battery
				_headWearDrainMultiplier.Add("5c110624d174af029e69734c", 3f); // T-7 Thermal Goggles with a Night Vision mount, Double AA
			}
		}

		public void Update() // battery is drained in Update() and applied
		{
			if (Time.time > _mainCooldown && BatterySystemConfig.EnableMod.Value)
			{
				_mainCooldown = Time.time + 1f;

				if (InGame()) DrainBatteries();
			}
		}

		public static bool InGame()
		{
            return Singleton<GameWorld>.Instance?.MainPlayer?.HealthController.IsAlive == true
					&& !(Singleton<GameWorld>.Instance.MainPlayer is HideoutPlayer);
		}
		//TODO: Throws InvalidOperationException: Collection was modified: enumeration operation may not execute.
		private static void DrainBatteries()
		{
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

		/* Credit to Nexus and Fontaine for showing me this!
		private static IEnumerator LowerThermalBattery(Player player)
		{
			if (player == null)
			{
				yield break;
			}

			while (player.HealthController != null && player.HealthController.IsAlive)
			{
				yield return null;
				ThermalVisionComponent thermalVisionComponent = player.ThermalVisionObserver.GetItemComponent();
				if (thermalVisionComponent == null)
				{
					continue;
				}

				if (thermalVisionComponent.Togglable.On)
				{
					IEnumerable<ResourceComponent> resourceComponents = thermalVisionComponent.Item.GetItemComponentsInChildren<ResourceComponent>(false);
					foreach (ResourceComponent resourceComponent in resourceComponents)
					{
						if (resourceComponent == null)
						{
							thermalVisionComponent.Togglable.Set(false);
							continue;
						}

						Single targetValue = resourceComponent.Value - Instance.BatteryDrainRate.Value * Time.deltaTime;
						if (targetValue <= 0f)
						{
							targetValue = 0f;
						}

						if ((resourceComponent.Value = targetValue).IsZero())
						{
							thermalVisionComponent.Togglable.Set(false);
						}
					}
				}
			}*/
	}
}