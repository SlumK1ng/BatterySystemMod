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
    public class NightVisionBatteries
    {
        private static Dictionary<string, float> deviceDrainMultiplier = new Dictionary<string, float>
        {
            { "5c0696830db834001d23f5da", 1f },// PNV-10T Night Vision Goggles, AA Battery
            { "5c0558060db834001b735271", 2f },// GPNVG-18 Night Vision goggles, CR123 battery pack
            { "5c066e3a0db834001b7353f0", 1f },// Armasight N-15 Night Vision Goggles, single CR123A lithium battery
            { "57235b6f24597759bf5a30f1", 0.5f },// AN/PVS-14 Night Vision Monocular, AA Battery
            { "5c110624d174af029e69734c", 3f },// T-7 Thermal Goggles with a Night Vision mount, Double AA
        };

        public static Item NightVisionItem = null;
        private static NightVisionComponent _nvgDevice = null;
        private static ThermalVisionComponent _thermalDevice = null;
        private static bool _drainingNightVisionBattery = false;
        public static ResourceComponent NightVisionBattery = null;

        public static void SetHeadWearComponents()
        {
            NightVisionItem = BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear).Items?.FirstOrDefault(); // default null else headwear
            _nvgDevice = NightVisionItem?.GetItemComponentsInChildren<NightVisionComponent>().FirstOrDefault(); //default null else nvg item
            _thermalDevice = NightVisionItem?.GetItemComponentsInChildren<ThermalVisionComponent>().FirstOrDefault(); //default null else thermal item
            NightVisionBattery = GetHeadwearSight()?.Parent.Item.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault(); //default null else resource

            CheckHeadWearIfDraining();
            BatterySystem.UpdateBatteryDictionary();
        }

        public static void TrackBatteries()
        {
            if (GetHeadwearSight() == null) return;
            if (BatterySystemPlugin.batteryDictionary.ContainsKey(GetHeadwearSight())) return; // headwear
            
            BatterySystemPlugin.batteryDictionary.Add(GetHeadwearSight(), _drainingNightVisionBattery);
        }

        public static Item GetHeadwearSight() // returns the special device goggles that are equipped
        {
            if (_nvgDevice != null)
                return _nvgDevice.Item;
            if (_thermalDevice != null)
                return _thermalDevice.Item;

            return null;
        }

        public static void CheckHeadWearIfDraining()
        {
            _drainingNightVisionBattery = NightVisionBattery != null && NightVisionBattery.Value > 0
                && (_nvgDevice == null && _thermalDevice != null
                ? (((ITogglableComponentContainer)_thermalDevice).Togglable.On && !CameraClass.Instance.ThermalVision.InProcessSwitching)
                : (_nvgDevice != null && _thermalDevice == null && ((ITogglableComponentContainer)_nvgDevice).Togglable.On && !CameraClass.Instance.NightVision.InProcessSwitching));
            // headWear has battery with resource installed and headwear (nvg/thermal) isn't switching and is on

            if (NightVisionBattery != null && BatterySystemPlugin.batteryDictionary.ContainsKey(GetHeadwearSight()))
                BatterySystemPlugin.batteryDictionary[GetHeadwearSight()] = _drainingNightVisionBattery;

            if (_nvgDevice != null)
                CameraClass.Instance.NightVision.On = _drainingNightVisionBattery;
            if (_thermalDevice != null)
                CameraClass.Instance.ThermalVision.On = _drainingNightVisionBattery;
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
            if (!BatterySystemPlugin.InGame()) return;
            if (__instance.name != "FPS Camera") return;
            
            if (__instance.InProcessSwitching)
                StaticManager.BeginCoroutine(IsNVSwitching(__instance));
            else 
                NightVisionBatteries.SetHeadWearComponents();
        }
        //waits until InProcessSwitching is false and then 
        private static IEnumerator IsNVSwitching(NightVision nv)
        {
            while (nv.InProcessSwitching)
                yield return new WaitForSeconds(1f / 100f);
                
            NightVisionBatteries.SetHeadWearComponents();
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
            if (!BatterySystemPlugin.InGame()) return;
            if (__instance.name != "FPS Camera") return;

            if (__instance.InProcessSwitching)
                StaticManager.BeginCoroutine(IsThermalSwitching(__instance));
            else 
                NightVisionBatteries.SetHeadWearComponents();
        }
        private static IEnumerator IsThermalSwitching(ThermalVision tv)
        {
            while (tv.InProcessSwitching)
                yield return new WaitForSeconds(1f / 100f);
            
            NightVisionBatteries.SetHeadWearComponents();
        }
    }
}
