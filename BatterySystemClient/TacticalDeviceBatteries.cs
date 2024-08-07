using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine;

namespace BatterySystem
{
    public class TacticalDeviceBatteries
    {
        public static Dictionary<TacticalComboVisualController, ResourceComponent> lightMods = new Dictionary<TacticalComboVisualController, ResourceComponent>();
        private static bool _drainingTacDeviceBattery;

        public static void TrackBatteries()
        {
            var lightModKeys = lightMods.Keys.ToArray();
            foreach (TacticalComboVisualController deviceController in lightModKeys) // tactical devices on active weapon
                if (BatterySystem.IsInSlot(deviceController.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)
                    && !BatterySystemPlugin.batteryDictionary.ContainsKey(deviceController.LightMod.Item))
                    BatterySystemPlugin.batteryDictionary.Add(deviceController.LightMod.Item, lightMods[deviceController]?.Value > 0);
        }

        public static void SetDeviceComponents(TacticalComboVisualController deviceInstance)
        {
            var lightModKeys = lightMods.Keys.ToArray();
            foreach (TacticalComboVisualController deviceController in lightModKeys)
                if (!BatterySystem.IsInSlot(deviceController.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
                    lightMods.Remove(deviceController);

            if (BatterySystem.IsInSlot(deviceInstance.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot))
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
            BatterySystem.UpdateBatteryDictionary();
        }
        public static void CheckDeviceIfDraining()
        {
            var lightModKeys = lightMods.Keys.ToArray();
            foreach (TacticalComboVisualController deviceController in lightModKeys) {
                if (deviceController?.LightMod?.Item == null) continue;

                ResourceComponent deviceBattery = deviceController.LightMod.Item.GetItemComponentsInChildren<ResourceComponent>().FirstOrDefault();
                lightMods[deviceController] = deviceBattery;

                _drainingTacDeviceBattery = (deviceBattery != null && deviceController.LightMod.IsActive && deviceBattery.Value > 0
                    && BatterySystem.IsInSlot(deviceController.LightMod.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot));

                if (BatterySystemPlugin.batteryDictionary.ContainsKey(deviceController.LightMod.Item))
                    BatterySystemPlugin.batteryDictionary[deviceController.LightMod.Item] = _drainingTacDeviceBattery;

                foreach (LaserBeam laser in deviceController.gameObject.GetComponentsInChildren<LaserBeam>(true))
                    laser.gameObject.gameObject.SetActive(_drainingTacDeviceBattery);
                foreach (Light light in deviceController.gameObject.GetComponentsInChildren<Light>(true))
                    light.gameObject.gameObject.SetActive(_drainingTacDeviceBattery);
            }
        }
    }
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
            if (!BatterySystemPlugin.InGame()) return;
            if (!BatterySystem.IsInSlot(__instance?.LightMod?.Item, Singleton<GameWorld>.Instance?.MainPlayer.ActiveSlot)) return;

            TacticalDeviceBatteries.SetDeviceComponents(__instance);
        }
    }
}
