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
            LootItemClass lootItem = sightInstance.SightMod.Item as LootItemClass;

            bool _hasBatterySlot(LootItemClass loot, string[] filters = null)
            {
                //use default parameter if nothing specified (any drainable battery)
                filters = filters ?? new string[] { BatterySystemPlugin.AABatteryId, BatterySystemPlugin.CR2032BatteryId, BatterySystemPlugin.CR123BatteryId };
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
                if (key?.SightMod?.Item != null)
                {
                    sightMods[key] = key.SightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault();
                    _drainingSightBattery = (sightMods[key] != null && sightMods[key].Value > 0
                        && BatterySystem.IsInSlot(key.SightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot));

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
		protected override MethodBase GetTargetMethod()
		{
			return typeof(ProceduralWeaponAnimation).GetMethod("FindAimTransforms");
		}
		[PatchPostfix]
		static void Postfix(ref ProceduralWeaponAnimation __instance)
		{
			foreach (AutoFoldableSight autoFoldableSight in componentsInChildren)
			{
				autoFoldableSight.gameObject.SetActive((autoFoldableSight.Mode == EAutoFoldableSightMode.On) ^ flag);
			}
            
			//if (BatterySystemConfig.AutoUnfold.Value)
			//AutofoldableSight.On == On when folds, unfold false
			//Invoke a method that folds sight when adding a sight to a weapon.
		}
	}*/

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
}
