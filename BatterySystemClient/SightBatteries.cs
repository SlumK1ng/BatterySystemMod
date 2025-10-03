using EFT.Animations;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT.CameraControl;
using EFT.InventoryLogic;
using UnityEngine.Experimental.GlobalIllumination;
using BatterySystem.Configs;
using UnityEngine;

namespace BatterySystem
{
    public class SightBatteries
    {
        public static Dictionary<SightModVisualControllers, ResourceComponent> sightMods = new Dictionary<SightModVisualControllers, ResourceComponent>();
        private static bool _drainingSightBattery;

        public static void TrackBatteries()
        {
            foreach (SightModVisualControllers sightController in sightMods.Keys) // sights on active weapon
                if (BatterySystem.IsInSlot(sightController.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)
                    && !BatterySystemPlugin.batteryDictionary.ContainsKey(sightController.SightMod.Item))
                    BatterySystemPlugin.batteryDictionary.Add(sightController.SightMod.Item, sightMods[sightController]?.Value > 0);
        }

        public static void SetSightComponents(SightModVisualControllers sightInstance)
        {
            CompoundItem lootItem = sightInstance.SightMod.Item as CompoundItem;

            bool _hasBatterySlot(CompoundItem loot, string[] filters = null)
            {
                //use default parameter if nothing specified (any drainable battery)
                filters = filters ?? new string[] { BatterySystemPlugin.AABatteryId, BatterySystemPlugin.CR2032BatteryId, BatterySystemPlugin.CR123BatteryId };
                foreach (Slot slot in loot.Slots)
                {
                    if (slot.Filters.FirstOrDefault()?.Filter.Any(sfilter => filters.Any(f => f == sfilter)) == true)
                        return true;
                }
                return false;
            }

            //before applying new sights, remove sights that are not on equipped weapon
            for (int i = sightMods.Keys.Count - 1; i >= 0; i--)
            {
                SightModVisualControllers key = sightMods.Keys.ElementAt(i);
                if (!BatterySystem.IsInSlot(key.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
                    sightMods.Remove(key);
            }

            if (BatterySystem.IsInSlot(sightInstance.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot) && _hasBatterySlot(lootItem))
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
            BatterySystem.UpdateBatteryDictionary();
        }

        public static void CheckSightIfDraining()
        {
            //for because modifying sightMods[key]
            var keys = sightMods.Keys.ToArray();
            foreach (SightModVisualControllers key in keys)
            {
                if (key?.SightMod?.Item == null) continue;

                sightMods[key] = key.SightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault();

                // Check if player is aiming down sights
                bool isAiming = Singleton<GameWorld>.Instance?.MainPlayer?.ProceduralWeaponAnimation?.IsAiming == true;

                _drainingSightBattery = (sightMods[key] != null && sightMods[key].Value > 0
                    && BatterySystem.IsInSlot(key.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)
                    && isAiming);

                if (BatterySystemPlugin.batteryDictionary.ContainsKey(key.SightMod.Item))
                    BatterySystemPlugin.batteryDictionary[key.SightMod.Item] = _drainingSightBattery;

                // true for finding inactive gameobject reticles
                foreach (CollimatorSight col in key.gameObject.GetComponentsInChildren<CollimatorSight>(true))
                {
                    /*
                    Color fadeColor = col.CollimatorMaterial.color;
                    fadeColor.a = .3f;
                    col.CollimatorMaterial.color = fadeColor;
                    */
                    col.gameObject.SetActive(_drainingSightBattery);
                }
                
                foreach (OpticSight optic in key.gameObject.GetComponentsInChildren<OpticSight>(true))
                {
					//for nv sights
					/*if (optic.NightVision != null)
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
            
            //Dont change iron sights unless there are optics attached
            //FoldableSightPatch.FoldIronSights(anyOpticsWithBattery);
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
            if (!BatterySystemPlugin.InGame()) return;
            if (!BatterySystem.IsInSlot(__instance.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)) return;

            SightBatteries.SetSightComponents(__instance);
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
            _firearmControllerField = AccessTools.Field(typeof(ProceduralWeaponAnimation), "_firearmController");

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

            SightBatteries.CheckSightIfDraining();
        }
    }
/*
    public class FoldableSightPatch : ModulePatch
    {
        private static ProceduralWeaponAnimation animator;
        
		protected override MethodBase GetTargetMethod()
		{
			return typeof(ProceduralWeaponAnimation).GetMethod("FindAimTransforms");
		}
		[PatchPostfix]
		static void Postfix(ref ProceduralWeaponAnimation __instance)
        {
            animator = __instance;
		}

        public static void FoldIronSights(bool folded)
        {
            if (animator?.HandsContainer?.Weapon == null) return;
            
            foreach (AutoFoldableSight autoFoldableSight in animator.HandsContainer.Weapon.GetComponentsInChildren<AutoFoldableSight>(true))
            {
                //(BatterySystemConfig.AutoUnfold.Value)
                autoFoldableSight.Mode = folded ? EAutoFoldableSightMode.On : EAutoFoldableSightMode.Off;
                autoFoldableSight.gameObject.SetActive(!folded);
                //TODO somehow sights still are folded even when always returning off
            }

            if (Singleton<GameWorld>.Instance?.MainPlayer
                    ?.HandsController is Player.FirearmController firearmController)
            {
                NotificationManagerClass.DisplayMessageNotification("Fold update");
                firearmController.WeaponModified();
                NotificationManagerClass.DisplayMessageNotification("Fold update done");
            }
            
            NotificationManagerClass.DisplayMessageNotification("Fold method "+folded);
        }
	}*/

    // Patches PoolManager to add dummy bones for battery slots during item instantiation
    public class PoolManagerBatteryBonePatch : ModulePatch
    {
        private static MethodInfo _addBoneMethod;

        protected override MethodBase GetTargetMethod()
        {
            // Find PoolManagerClass.method_3 which handles bone registration
            var poolManagerType = typeof(Item).Assembly.GetTypes().First(t => t.Name == "PoolManagerClass");

            return AccessTools.GetDeclaredMethods(poolManagerType).First(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length >= 2
                    && parameters[0].ParameterType.Name.Contains("GClass3050") // ContainerCollection
                    && parameters[1].ParameterType.Name.Contains("GClass746"); // ContainerCollectionView
            });
        }

        [PatchPostfix]
        public static void Postfix(object containerCollection, object collectionView)
        {
            try
            {
                if (_addBoneMethod == null)
                {
                    _addBoneMethod = collectionView.GetType().GetMethod("AddBone");
                }

                // Get the Containers property
                var containersProperty = containerCollection.GetType().GetProperty("Containers");
                var containers = containersProperty.GetValue(containerCollection) as System.Collections.IEnumerable;

                // Get the ContainerBones dictionary to check what's already registered
                var containerBonesField = collectionView.GetType().GetField("ContainerBones", BindingFlags.Public | BindingFlags.Instance);
                var containerBones = containerBonesField.GetValue(collectionView) as System.Collections.IDictionary;

                foreach (object container in containers)
                {
                    Slot slot = container as Slot;
                    if (slot != null && slot.ID == "mod_equipment_000")
                    {
                        // Only add if not already registered
                        if (!containerBones.Contains(slot))
                        {
                            // AddBone with null transform creates a dummy bone entry
                            _addBoneMethod.Invoke(collectionView, new object[] { slot, null });
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[BatterySystem] PoolManagerBatteryBonePatch error: {ex.Message}");
            }
        }
    }
}
