using BatterySystem.Configs;
using BepInEx.Configuration;
using BSG.CameraEffects;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BatterySystem
{
    public class HeadwearBatteries
    {
        private static Dictionary<string, float> deviceDrainMultiplier = new Dictionary<string, float>
        {
            { "5c0696830db834001d23f5da", 1f },// PNV-10T Night Vision Goggles, AA Battery
            { "5c0558060db834001b735271", 2f },// GPNVG-18 Night Vision goggles, CR123 battery pack
            { "5c066e3a0db834001b7353f0", 1f },// Armasight N-15 Night Vision Goggles, single CR123A lithium battery
            { "57235b6f24597759bf5a30f1", 0.5f },// AN/PVS-14 Night Vision Monocular, AA Battery
            { "5c110624d174af029e69734c", 3f },// T-7 Thermal Goggles with a Night Vision mount, Double AA
        };

        public static Item headWearItem = null;
        private static NightVisionComponent _headWearNvg = null;
        private static ThermalVisionComponent _headWearThermal = null;
        private static bool _drainingHeadWearBattery = false;
        public static ResourceComponent headWearBattery = null;

        public static void SetHeadWearComponents()
        {
            headWearItem = BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear).Items?.FirstOrDefault(); // default null else headwear
            _headWearNvg = headWearItem?.GetItemComponentsInChildren<NightVisionComponent>().FirstOrDefault(); //default null else nvg item
            _headWearThermal = headWearItem?.GetItemComponentsInChildren<ThermalVisionComponent>().FirstOrDefault(); //default null else thermal item
            headWearBattery = GetHeadwearSight()?.Parent.Item.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault(); //default null else resource

            CheckHeadWearIfDraining();
            BatterySystem.UpdateBatteryDictionary();
        }

        public static void TrackBatteries()
        {
            if (GetHeadwearSight() == null) return;
            if (BatterySystemPlugin.batteryDictionary.ContainsKey(GetHeadwearSight())) return; // headwear
            
            BatterySystemPlugin.batteryDictionary.Add(GetHeadwearSight(), _drainingHeadWearBattery);
        }

        public static Item GetHeadwearSight() // returns the special device goggles that are equipped
        {
            if (_headWearNvg != null)
                return _headWearNvg.Item;
            if (_headWearThermal != null)
                return _headWearThermal.Item;

            return null;
        }

        public static void CheckHeadWearIfDraining()
        {
            _drainingHeadWearBattery = headWearBattery != null && headWearBattery.Value > 0
                && (_headWearNvg == null && _headWearThermal != null
                ? (((ITogglableComponentContainer)_headWearThermal).Togglable.On && !CameraClass.Instance.ThermalVision.InProcessSwitching)
                : (_headWearNvg != null && _headWearThermal == null && ((ITogglableComponentContainer)_headWearNvg).Togglable.On && !CameraClass.Instance.NightVision.InProcessSwitching));
            // headWear has battery with resource installed and headwear (nvg/thermal) isn't switching and is on

            if (headWearBattery != null && BatterySystemPlugin.batteryDictionary.ContainsKey(GetHeadwearSight()))
                BatterySystemPlugin.batteryDictionary[GetHeadwearSight()] = _drainingHeadWearBattery;

            if (_headWearNvg != null)
                CameraClass.Instance.NightVision.On = _drainingHeadWearBattery;
            if (_headWearThermal != null)
                CameraClass.Instance.ThermalVision.On = _drainingHeadWearBattery;
        }

        public static void Drain(Item batteryItem)
        {
            if (headWearItem.GetItemComponentsInChildren<ITogglableComponent>().FirstOrDefault()?.On == false) return;
            if (!(GetHeadwearSight()?.TemplateId is string headwearId)) return;
            
            //Default battery lasts 1 hr * configmulti * itemmulti, itemmulti was Hazelify's idea!
            headWearBattery.Value -= Mathf.Clamp(1 / 36f
                    * BatterySystemConfig.DrainMultiplier.Value
                    * deviceDrainMultiplier[headwearId],
                    0f, 100f);

            if (batteryItem.GetItemComponentsInChildren<ResourceComponent>(false).First().Value < 0f)
            {
                batteryItem.GetItemComponentsInChildren<ResourceComponent>(false).First().Value = 0f;
                if (batteryItem.IsChildOf(BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear).ContainedItem))
                    CheckHeadWearIfDraining();
            }
        }
    }

    public class NvgHeadWearPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(NightVision).GetMethod(nameof(NightVision.StartSwitch));
        }

        [PatchPostfix]
        static void Postfix(ref NightVision __instance)
        {
            if (__instance.name == "FPS Camera" && BatterySystemPlugin.InGame())
            {
                if (__instance.InProcessSwitching)
                    StaticManager.BeginCoroutine(IsNVSwitching(__instance));
                else HeadwearBatteries.SetHeadWearComponents();
            }
        }
        //waits until InProcessSwitching is false and then 
        private static IEnumerator IsNVSwitching(NightVision nv)
        {
            while (nv.InProcessSwitching)
            {
                yield return new WaitForSeconds(1f / 100f);
            }
            HeadwearBatteries.SetHeadWearComponents();
            yield break;
        }
    }

    public class ThermalHeadWearPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ThermalVision).GetMethod(nameof(ThermalVision.StartSwitch));
        }

        [PatchPostfix]
        static void Postfix(ref ThermalVision __instance)
        {
            if (__instance.name == "FPS Camera" && BatterySystemPlugin.InGame())
            {
                if (__instance.InProcessSwitching)
                    StaticManager.BeginCoroutine(IsThermalSwitching(__instance));
                else HeadwearBatteries.SetHeadWearComponents();
            }
        }
        private static IEnumerator IsThermalSwitching(ThermalVision tv)
        {
            while (tv.InProcessSwitching)
            {
                yield return new WaitForSeconds(1f / 100f);
            }
            HeadwearBatteries.SetHeadWearComponents();
            yield break;
        }
    }
}
