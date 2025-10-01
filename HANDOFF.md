# Battery System Mod - Migration Handoff Document

**Last Updated:** 2025-10-01
**Project:** Battery System Mod for SPT-AKI
**Migration:** SPT 3.8.0 → SPT 3.11

---

## 📋 Project Overview

**Mod Name:** Jiro-BatterySystem
**Description:** Adds battery drain mechanics to NVGs, thermal goggles, red dot sights, tactical devices, and headsets in SPT-AKI
**Original Version:** Built for SPT 3.8.0
**Target Version:** SPT 3.11

### Key Locations
- **Project Root:** `C:\SPT\SPT Arcade Build\SPT Fika\Development\BatterySystemMod`
- **SPT Installation:** `C:\SPT\SPT Arcade Build\SPT Fika`
- **Deobfuscated Assembly:** `C:\SPT\Source Code\SPT311_Assembly`
- **Build Output:** `.\dist\`

---

## ✅ Current Status

### Completed
- [x] Updated Assembly-CSharp.dll reference from SPT 3.8.0 to SPT 3.11
- [x] Fixed `MissingMethodException` for `GetItemComponentsInChildren`
  - Root cause: Obfuscated class name changed from `GClass2771` to `GClass3176`
- [x] Fixed all API breaking changes (5 total)
- [x] Project builds successfully with only 1 warning (unused variable)
- [x] Mod deployed to SPT installation

### Status: ⚠️ NEEDS TESTING
The mod now compiles and has been installed, but needs in-game testing to verify functionality.

---

## 🔧 Changes Made

### Assembly References Updated
**Location:** `BatterySystemClient\Dependencies\`

Updated the following DLLs from SPT 3.11 installation:
```
Assembly-CSharp.dll
Comfort.dll
UnityEngine.CoreModule.dll
```

**Source:** `C:\SPT\SPT Arcade Build\SPT Fika\EscapeFromTarkov_Data\Managed\`

### Code Changes (API Migration)

#### 1. InventoryControllerClass → InventoryController
**Files:** `SpawnPatch.cs:35, 52`
```csharp
// BEFORE (SPT 3.8.0)
__instance.InventoryControllerClass.Inventory

// AFTER (SPT 3.11)
__instance.InventoryController.Inventory
```

#### 2. ItemFactory → ItemFactoryClass
**Files:** `SpawnPatch.cs:53, 54, 55`
```csharp
// BEFORE
Singleton<ItemFactory>.Instance

// AFTER
Singleton<ItemFactoryClass>.Instance
```

#### 3. LootItemClass → CompoundItem
**Files:** `SpawnPatch.cs:58`, `SightBatteries.cs:35, 37`, `HeadsetBatteries.cs:82`
```csharp
// BEFORE
if (!(item is LootItemClass lootItem)) continue;

// AFTER
if (!(item is CompoundItem lootItem)) continue;
```

#### 4. ItemAddress Navigation Change
**Files:** `NightVisionBatteries.cs:40`
```csharp
// BEFORE
GetHeadwearSight()?.Parent.Item.GetItemComponentsInChildren<ResourceComponent>(false)

// AFTER
GetHeadwearSight()?.Parent.Container.ParentItem.GetItemComponentsInChildren<ResourceComponent>(false)
```

#### 5. Fixed LINQ Extension Conflict
**Files:** `SightBatteries.cs:43`
```csharp
// BEFORE
if (slot.Filters.FirstOrDefault()?.Filter.Any(sfilter => filters.Contains(sfilter)) == true)

// AFTER
if (slot.Filters.FirstOrDefault()?.Filter.Any(sfilter => filters.Any(f => f == sfilter)) == true)
```
**Reason:** Avoided conflict with Unity's LODGroup.Contains() extension method

---

## 🐛 Known Issues

### Warnings (Non-Critical)
```
SightBatteries.cs:76 - Variable 'anyOpticsWithBattery' assigned but never used
```
**Impact:** None - compilation warning only
**Priority:** Low

### Pending Issues
- None identified (pending testing)

---

## 🧪 Testing Required

### Critical Tests
1. **Load Test**
   - [ ] Game loads past main menu without infinite loading screen
   - [ ] No exceptions in BepInEx logs related to BatterySystem

2. **NVG/Thermal Goggles**
   - [ ] Battery drains when NVG is active
   - [ ] Battery drains when thermal goggles are active
   - [ ] NVG/thermal turns off when battery depletes

3. **Red Dot Sights**
   - [ ] Battery drains when aiming down sights
   - [ ] Reticle disappears when battery depletes

4. **Tactical Devices**
   - [ ] Battery drains when flashlight/laser is active
   - [ ] Device turns off when battery depletes

5. **Headsets**
   - [ ] Battery drains when headset is equipped
   - [ ] Audio effects disabled when battery depletes

6. **Bot Functionality**
   - [ ] Bots spawn with batteries in their equipment
   - [ ] Bot battery charge varies by level

### Test Environment
- **SPT Version:** 3.11
- **Fika Installed:** Yes
- **Test Map:** Any (recommend Factory for quick tests)

### How to Check Logs
```
BepInEx\LogOutput.log
```
Search for: `BatterySystem`, `MissingMethodException`, `NullReferenceException`

---

## 📦 Build Instructions

### Prerequisites
- Visual Studio 2022 Community Edition
- MSBuild
- PowerShell

### Build Steps
```powershell
cd "C:\SPT\SPT Arcade Build\SPT Fika\Development\BatterySystemMod"
.\build.ps1
```

### Install Built Mod
```powershell
# Copy from .\dist\ to SPT installation
cp -r .\dist\BepInEx "C:\SPT\SPT Arcade Build\SPT Fika\"
cp -r .\dist\user "C:\SPT\SPT Arcade Build\SPT Fika\"
```

### Manual Build (if script fails)
```powershell
# Build client
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" .\BatterySystemClient\BatterySystemClient.csproj /p:Configuration=Release

# Build server
cd BatterySystemServer
npm install
npm run build
```

---

## 📁 Project Structure

```
BatterySystemMod/
├── BatterySystemClient/           # C# BepInEx plugin
│   ├── Dependencies/              # Game assembly references
│   │   ├── Assembly-CSharp.dll    # ⚠️ Must match SPT version
│   │   └── ...
│   ├── Plugin.cs                  # Main plugin entry point
│   ├── BatterySystem.cs           # Core battery logic
│   ├── SpawnPatch.cs             # Player/bot initialization
│   ├── SightBatteries.cs         # Optics battery logic
│   ├── TacticalDeviceBatteries.cs
│   ├── NightVisionBatteries.cs
│   ├── HeadsetBatteries.cs
│   └── BatterySystemClient.csproj
├── BatterySystemServer/           # TypeScript server mod
│   └── src/
├── dist/                          # Build output
├── build.ps1                      # Build script
├── HANDOFF.md                     # This document
└── README.md
```

---

## 🔍 Debugging Tips

### Common Issues & Solutions

#### Issue: Mod not loading
**Check:**
1. BepInEx log for plugin initialization
2. Mod is in correct folder: `BepInEx\plugins\Jiro-BatterySystem\`
3. Dependencies present in same folder

#### Issue: MissingMethodException
**Solution:**
1. Check Assembly-CSharp.dll version matches SPT version
2. Verify method exists in deobfuscated assembly at:
   ```
   C:\SPT\Source Code\SPT311_Assembly
   ```
3. Use grep to find correct method signature:
   ```bash
   grep -r "MethodName" "C:\SPT\Source Code\SPT311_Assembly"
   ```

#### Issue: Type not found errors
**Solution:**
1. Check if type was renamed in SPT 3.11
2. Search deobfuscated assembly for correct type name
3. Common renames:
   - `LootItemClass` → `CompoundItem`
   - `ItemFactory` → `ItemFactoryClass`
   - `InventoryControllerClass` → `InventoryController`

---

## 🎯 Next Steps

### Immediate (Before Continuing Development)
1. [ ] Run in-game testing checklist above
2. [ ] Review BepInEx logs for any errors
3. [ ] Verify battery drain mechanics work correctly
4. [ ] Test bot battery spawning

### Future Enhancements (From Original TODO)
- [ ] Enable switching to iron sights when battery runs out
- [ ] Change background color of empty battery items
- [ ] Fix: Equipping/removing headwear gives infinite NVG
- [ ] Add sound effects for battery events
- [ ] FLIR battery mechanics / recharge craft
- [ ] Battery recharger feature

### Code Quality
- [ ] Remove unused variable `anyOpticsWithBattery` (SightBatteries.cs:76)
- [ ] Update PostBuildEvent path in .csproj (currently points to old SPT 3.8.0 path)

---

## 📚 Reference Links

### SPT Resources
- **SPT Docs:** https://dev.sp-tarkov.com/
- **SPT Discord:** Development channel for API changes
- **Assembly Deobfuscator:** Already completed at `C:\SPT\Source Code\SPT311_Assembly`

### API Breaking Changes (3.8.0 → 3.11)
Reference this document for common changes when updating other mods.

### Key Files for Reference
- **GClass3176.cs** - Extension methods for Item (GetItemComponentsInChildren)
- **Player.cs** - Player class properties (InventoryController)
- **Item.cs** - Base item class
- **CompoundItem.cs** - Item with slots/containers

---

## 💾 Backup & Version Control

### Git Status
```
Current branch: master
Status: Clean (as of last commit)
```

### Important: Before Major Changes
1. Commit current working state
2. Create feature branch for new changes
3. Test thoroughly before merging to master

### Backup Locations
- Source code: Already in git
- Built DLL: `.\dist\BepInEx\plugins\Jiro-BatterySystem\`
- Dependencies: `.\BatterySystemClient\Dependencies\`

---

## 🤝 Handoff Notes

### If Resuming Work
1. Read "Current Status" section above
2. Check "Known Issues" for any blockers
3. Review "Testing Required" for what needs verification
4. If game updated, check if Assembly-CSharp.dll needs updating

### If Issues Arise
1. Check BepInEx log first: `BepInEx\LogOutput.log`
2. Search deobfuscated assembly for correct API usage
3. Compare with other working SPT 3.11 mods
4. Check SPT Discord/Hub for breaking changes

### Quick Commands
```bash
# Navigate to project
cd "C:\SPT\SPT Arcade Build\SPT Fika\Development\BatterySystemMod"

# Build
.\build.ps1

# Check logs
cat "C:\SPT\SPT Arcade Build\SPT Fika\BepInEx\LogOutput.log" | grep BatterySystem

# Search deobfuscated assembly
grep -r "YourSearchTerm" "C:\SPT\Source Code\SPT311_Assembly"
```

---

**Document maintained by:** Claude Code
**For questions/issues:** Review BepInEx logs and search deobfuscated assembly first
