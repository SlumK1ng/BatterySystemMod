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
				BatterySystem.sightMods.Clear(); // remove old sight entries that were saved from previous raid
				BatterySystem.lightMods.Clear(); // same for tactical devices
				BatterySystem.SetEarPieceComponents();
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

	//Check weapon sight when aiming down
	public class AimSightPatch : ModulePatch
	{
		//private static Type _pwaType;
		private static FieldInfo _firearmControllerField;
		private static MethodInfo _updateAimMethod;

		protected override MethodBase GetTargetMethod()
		{
			//Gives ProceduralWeaponAnimation type
			//unnecessary.
			//_pwaType = PatchConstants.EftTypes.Single(type => type.Name == "ProceduralWeaponAnimation");
			//TODO: use reflection instead of gclass
			_firearmControllerField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_firearmController");
			/* Unnecessary reflection.
			_firearmDataField = AccessTools.GetDeclaredFields(_pwaType).FirstOrDefault(field =>
		{
			return field.FieldType.Equals(typeof(Player.FirearmController));
		});*/
			
			//Finds a method that has a (bool forced = false) parameter. Works in 3.8.0
			//necessary, is method_NUM where NUM changes between patches.
			_updateAimMethod = AccessTools.GetDeclaredMethods(typeof(ProceduralWeaponAnimation)).FirstOrDefault(method =>
			{
				var parameters = method.GetParameters();
				return parameters.Length == 1 && parameters[0].Name == "forced" && parameters[0].ParameterType == typeof(bool);
			});

			return _updateAimMethod;
		}

		[PatchPostfix]
		public static void Postfix(ref ProceduralWeaponAnimation __instance)
		{
			if (__instance == null) return;

			var playerField = (Player.FirearmController)_firearmControllerField.GetValue(__instance);
			if (!BatterySystemPlugin.InGame()) return;
			if (playerField == null) return;
			if (playerField.Weapon == null) return;

			Player weaponOwnerPlayer = Singleton<GameWorld>.Instance.GetAlivePlayerByProfileID(playerField.Weapon.Owner.ID);
			if (weaponOwnerPlayer == null) return;
			if (!weaponOwnerPlayer.IsYourPlayer) return;
			
			BatterySystem.CheckSightIfDraining();
		}
	}
	//Throws NullRefError?
	//UNNECESSARY???? WHAT
	// Adds dummy bones for weapon modding window.
	//TODO: Use reflection instead of gclass
	/*
	public class GetBoneForSlotPatch : ModulePatch
	{
		private static GClass674.GClass675 _gClass = new GClass674.GClass675();
		private static Type _gClassType;
		private static string _methodName = "GetBoneForSlot";
		protected override MethodBase GetTargetMethod()
		{
			_gClassType = PatchConstants.EftTypes.Single(type => {
				//If type has a method called _methodName, select the type
				string methodInfo = AccessTools.GetMethodNames(type)
				.FirstOrDefault(name => name.Equals(_methodName));
				return methodInfo != null; 
			});
			Logger.LogWarning(_gClassType.FullName);
			return AccessTools.Method(_gClassType, _methodName);
		}

		[PatchPrefix]
		public static void Prefix(ref GClass674 __instance, IContainer container)
		{
			if (!__instance.ContainerBones.ContainsKey(container) && container.ID == "mod_equipment")
			{
				_gClass.Bone = null;
				_gClass.Item = null;
				_gClass.ItemView = null;
				__instance.ContainerBones.Add(container, _gClass);
			}
		}
	}*/

	public class UpdatePhonesPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(Player).GetMethod(nameof(Player.UpdatePhones));
		}
		[PatchPostfix]
		public static void PatchPostfix(ref Player __instance) //BetterAudio __instance
		{
			if (BatterySystemPlugin.InGame() && __instance.IsYourPlayer)
			{
				Singleton<BetterAudio>.Instance.Master.GetFloat("Compressor", out BatterySystem.compressor);
				Singleton<BetterAudio>.Instance.Master.GetFloat("CompressorMakeup", out BatterySystem.compressorMakeup);
				BatterySystem.SetEarPieceComponents();
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
					BatterySystem.SetEarPieceComponents();
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
					BatterySystem.CheckSightIfDraining();
					return;
				}
				BatterySystem.SetEarPieceComponents();
                HeadwearBatteries.SetHeadWearComponents();
			}
		}
	}

	public class SightDevicePatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(SightModVisualControllers).GetMethod(nameof(SightModVisualControllers.UpdateSightMode));
		}

		[PatchPostfix]
		static void Postfix(ref SightModVisualControllers __instance)
		{
			//only sights on equipped weapon are added
			if (BatterySystemPlugin.InGame() && BatterySystem.IsInSlot(__instance.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
			{
				BatterySystem.SetSightComponents(__instance);
			}
		}
	}

	/*
	public class FoldableSightPatch : ModulePatch
	{
		protected override MethodBase GetTargetMethod()
		{
			return typeof(ProceduralWeaponAnimation).GetMethod("FindAimTransforms");
		}
		[PatchPostfix]
		static void Postfix(ref ProceduralWeaponAnimation __instance)
		{
			if (BatterySystemConfig.AutoUnfold.Value)
				if (BatterySystemConfig.EnableLogs.Value)
					Logger.LogInfo("FindAimTransforms at " + Time.time);
			//AutofoldableSight.On == On when folds, unfold false
			//Invoke a method that folds sight when adding a sight to a weapon.
		}
	}*/

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
