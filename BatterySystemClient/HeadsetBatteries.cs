﻿using BatterySystem.Configs;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BatterySystem
{
    public class HeadsetBatteries
    {
        private static Item headsetItem = null;
        private static ResourceComponent headsetBattery = null;
        private static bool _drainingEarPieceBattery = false;
        public static float compressorMakeup;
        // compressor is used because the default 
        public static float compressor;

        public static void TrackBatteries()
        {
            if (!BatterySystemConfig.EnableHeadsets.Value) return;
            if (headsetItem == null) return;
            if (BatterySystemPlugin.batteryDictionary.ContainsKey(headsetItem)) return;

            BatterySystemPlugin.batteryDictionary.Add(headsetItem, _drainingEarPieceBattery);
        }
        
        public static void SetEarPieceComponents()
        {
            if (BatterySystemConfig.EnableHeadsets.Value)
            {
                headsetItem = GetEarpiece();
                headsetBattery = headsetItem?.GetItemComponentsInChildren<ResourceComponent>(false).FirstOrDefault();
                CheckEarPieceIfDraining();
                BatterySystem.UpdateBatteryDictionary();
            }
        }

        public static void CheckEarPieceIfDraining()
        {
            _drainingEarPieceBattery = false;
            if (!BatterySystemConfig.EnableHeadsets.Value) return;

            //headset has charged battery installed
            if (headsetBattery != null && headsetBattery.Value > 0)
            {
                MethodInvoker.GetHandler(AccessTools.Method(typeof(Player), "UpdatePhonesReally"));
                _drainingEarPieceBattery = true;
            }
            //headset has no battery
            else if (headsetItem != null)
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

            if (headsetItem != null && BatterySystemPlugin.batteryDictionary.ContainsKey(headsetItem))
                BatterySystemPlugin.batteryDictionary[headsetItem] = _drainingEarPieceBattery;
        }

        private static Item GetEarpiece()
        {
            if (BatterySystemPlugin.localInventory == null) return null;
            //Try get headphones from "Earpiece" slot
            if(BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Earpiece).Items?.FirstOrDefault() is Item headphones) return headphones;
            //Try get headphones from helmet attachment slot
            const string headphonesParentId = "5645bcb74bdc2ded0b8b4578";
            if (BatterySystemPlugin.localInventory.Equipment.GetSlot(EquipmentSlot.Headwear).Items?.FirstOrDefault() is CompoundItem helmet)
            {
                foreach (Item helmetAttachment in helmet.GetAllItems())
                {
                    if (helmetAttachment == null) continue;
                    if (!helmetAttachment.Template.Parent._id.Equals(headphonesParentId)) continue;
                    
                    return helmetAttachment;
                }
            }

            return null;
        }
    }

    public class UpdatePhonesPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod(nameof(Player.UpdatePhones));
        }
        [PatchPostfix]
        public static void PatchPostfix(ref Player __instance) //BetterAudio __instance
        {
            if (!BatterySystemPlugin.InGame()) return;
            if (!__instance.IsYourPlayer) return;
            
            Singleton<BetterAudio>.Instance.Master.GetFloat("Compressor", out HeadsetBatteries.compressor);
            Singleton<BetterAudio>.Instance.Master.GetFloat("CompressorMakeup", out HeadsetBatteries.compressorMakeup);
            HeadsetBatteries.SetEarPieceComponents();
        }
    }

}
