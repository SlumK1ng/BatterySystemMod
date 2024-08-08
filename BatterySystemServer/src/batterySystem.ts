import { DependencyContainer } from "tsyringe";

import { ILogger } from "@spt/models/spt/utils/ILogger"
import { IPostDBLoadMod } from "@spt/models/external/IPostDBLoadMod";
//import { CustomItemService } from "@spt/services/mod/CustomItemService";
//import { NewItemFromCloneDetails } from "@spt/models/spt/mod/NewItemDetails";
import { DatabaseServer } from "@spt/servers/DatabaseServer";
import { BaseClasses } from "@spt/models/enums/BaseClasses"
import * as config from "../config/config.json";
import { TraderPurchaseData } from "@spt/models/eft/profile/ISptProfile";

class Mod implements IPostDBLoadMod {
    private batteryType = "";

    public postDBLoad(container: DependencyContainer): void {
        //const CustomItem = container.resolve<CustomItemService>("CustomItemService");
        const logger = container.resolve<ILogger>("WinstonLogger");
        const db = container.resolve<DatabaseServer>("DatabaseServer");
        const locales = Object.values(db.getTables().locales.global) as Record<string, string>[];
        const botDB = db.getTables().bots.types;
        const items = db.getTables().templates.items;
        const hideoutProduction = db.getTables().hideout.production;
        const aaBatteryID = "5672cb124bdc2d1a0f8b4568";
        const dBatteryID = "5672cb304bdc2dc2088b456a";
        const rchblBatteryID = "590a358486f77429692b2790";
        const carBatteryID = "5733279d245977289b77ec24";
        //const flirID = "5d1b5e94d7ad1a2b865a96b0";
        //Flir has a built-in battery. the battery doesn't show anywhere so no point
        //items[flirID]._props.MaxResource = 100;
        //items[flirID]._props.Resource = 0.05;


        items[aaBatteryID]._props.MaxResource = 100;
        items[aaBatteryID]._props.Resource = 100;
        items[aaBatteryID]._props.ItemSound = "food_tin_can";
        items[rchblBatteryID]._props.MaxResource = 100;
        items[rchblBatteryID]._props.Resource = 100;
        items[rchblBatteryID]._props.Prefab.path = "batteries/cr123.bundle"
        items[rchblBatteryID]._props.ItemSound = "food_tin_can";
        items[dBatteryID]._props.MaxResource = 100;
        items[dBatteryID]._props.Resource = 100;
        items[dBatteryID]._props.Prefab.path = "batteries/cr2032.bundle";
        items[dBatteryID]._props.ItemSound = "food_tin_can";
        items[carBatteryID]._props.MaxResource = 100;
        items[carBatteryID]._props.Resource = 100;

        //Credit to Jehree! // 16 locales, wtf?
        for (const locale of locales) {
            locale[`${rchblBatteryID} Name`] = "CR123 Rechargeable Battery";
            locale[`${rchblBatteryID} ShortName`] = "CR123";
            locale[`${rchblBatteryID} Description`] = "A singular CR123A Battery. These are commonly used in military and hunting sights.";
            locale[`${dBatteryID} Name`] = "CR2032 Battery";
            locale[`${dBatteryID} ShortName`] = "CR2032";
            locale[`${dBatteryID} Description`] = "A multipurpose CR2032 Battery. Used from personal computers to military grade sights.";
        };

        // huge thanks and credit to jbs4mx! https://github.com/jbs4bmx/SpecialSlots/
        const pockets = items["627a4e6b255f7527fb05a0f6"];
        pockets._props.Slots[0]._props.filters[0].Filter.push(dBatteryID, rchblBatteryID, aaBatteryID);
        pockets._props.Slots[1]._props.filters[0].Filter.push(dBatteryID, rchblBatteryID, aaBatteryID);
        pockets._props.Slots[2]._props.filters[0].Filter.push(dBatteryID, rchblBatteryID, aaBatteryID);

        //S I C C case now fits batteries in it
        items["5d235bb686f77443f4331278"]._props.Grids[0]._props.filters[0].Filter.push(dBatteryID, rchblBatteryID, aaBatteryID);

        //add battery slots to wanted items
        for (let id in items) {
            if (id != BaseClasses.NIGHTVISION && id != BaseClasses.THERMAL_VISION
                && !config.NoBattery.includes(id)
                && ((items[id]._parent == BaseClasses.SPECIAL_SCOPE) //flir
                    || (items[id]._parent == BaseClasses.NIGHTVISION || items[id]._parent == BaseClasses.THERMAL_VISION)) // headwear
                || (items[id]._parent == BaseClasses.COLLIMATOR || items[id]._parent == BaseClasses.COMPACT_COLLIMATOR) //sight
                || (items[id]._parent == BaseClasses.HEADPHONES) //earpiece
                || (items[id]._parent == BaseClasses.FLASHLIGHT || items[id]._parent == BaseClasses.LIGHT_LASER_DESIGNATOR
                    || items[id]._parent == BaseClasses.TACTICAL_COMBO)) { //tactical device

                if (config.AA.includes(id))
                    this.batteryType = aaBatteryID;     //AA Battery stays AA Battery              
                else if (config.CR123.includes(id))
                    this.batteryType = rchblBatteryID;  //CR123, for now rchbl battery
                else if (config.CR2032.includes(id))
                    this.batteryType = dBatteryID;      //CR2032, for now d battery
                else if (config.CR1225.includes(id))
                    this.batteryType = dBatteryID;
                else if (config.CR1632.includes(id))
                    this.batteryType = dBatteryID;
                else {
                    logger.warning("BatterySystem: Item " + id + " has no defined battery, defaulting to CR2032!");
                    this.batteryType = dBatteryID;
                }
                for (const locale of locales) { // Item description now includes the battery type
                    const newDescription = "Uses " + locale[`${this.batteryType} Name`] + "\n\n" + locale[`${id} Description`];
                    locale[`${id} Description`] = newDescription;
                }
                //logger.info("Adding slot to: " + items[id]._name);
                items[id]._props.Slots.push(
                    {
                        "_name": "mod_equipment",
                        "_id": "id_" + id.toLowerCase(),
                        "_parent": "parent_" + id.toLowerCase(),
                        "_props": {
                            "filters": [
                                {
                                    "Shift": 0,
                                    "Filter": [
                                        this.batteryType
                                    ]
                                }
                            ]
                        },
                        "_required": false,
                        "_mergeSlotWithChildren": false,
                        "_proto": "55d30c4c4bdc2db4468b457e"
                    }
                );
            }
        }
        //change spawn% for batteries on bots. the durability is adjusted in a patch.
        //make spawn chance lower for scavs in the future?
        for (let bot in botDB) {
            if (botDB[bot].chances.equipmentMods != undefined) {
                botDB[bot].chances.equipmentMods.mod_equipment = 50;
            }
            if (botDB[bot].chances.weaponMods != undefined) {
                botDB[bot].chances.weaponMods.mod_equipment = 50;
            }
        }
        //Jaeger trade for cr2032
        db.getTables().traders["5c0647fdd443bc2504c2d371"].assort.items.push({
            "_id": "cr2032barter1",
            "_tpl": dBatteryID,
            "parentId": "hideout",
            "slotId": "hideout",
            "upd": {
                "StackObjectsCount": 14993,
                "BuyRestrictionMax": 4,
                "BuyRestrictionCurrent": 0
            }
        })
        db.getTables().traders["5c0647fdd443bc2504c2d371"].assort.barter_scheme["cr2032barter1"] =
            [
                [
                    {
                        "count": 3,
                        "_tpl": dBatteryID
                    }
                ]
            ];
        db.getTables().traders["5c0647fdd443bc2504c2d371"].assort.loyal_level_items["cr2032barter1"] = 1;
        
        //add hideout crafts for batteries
        hideoutProduction.push(
            {
                "_id": "cr2032Craft0",
                "areaType": 10,
                "requirements": [
                    {
                        "areaType": 10, //Lavatory = 2, WorkBench = 10
                        "requiredLevel": 1,
                        "type": "Area"
                    },
                    {
                        "templateId": "544fb5454bdc2df8738b456a", //multiTool
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Tool"
                    },
                    {
                        "templateId": aaBatteryID, //aa battery
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Item"
                    }
                ],
                "productionTime": 600, // seconds
                "needFuelForAllProductionTime": false,
                "locked": false,
                "endProduct": dBatteryID,
                "continuous": false,
                "count": 2,
                "productionLimitCount": 0,
                "isEncoded": false
            },
            {
                // Induction!
                "_id": "cr123Recharge0",
                "areaType": 2,
                "requirements": [
                    {
                        "areaType": 2, //Lavatory = 2, WorkBench = 10
                        "requiredLevel": 1,
                        "type": "Area"
                    },
                    {
                        "templateId": "590a391c86f774385a33c404", //magnet
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Tool"
                    },
                    {
                        "templateId": "5c06779c86f77426e00dd782", //Bundle of wires
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Item"
                    },
                    {
                        "templateId": rchblBatteryID,
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Item"
                    }
                ],
                "productionTime": 600, // seconds
                "needFuelForAllProductionTime": false,
                "locked": false,
                "endProduct": rchblBatteryID,
                "continuous": false,
                "count": 1,
                "productionLimitCount": 0,
                "isEncoded": false
            },
            /*{ // Car Battery Recharge Test
                "_id": "carBatteryTest1",
                "areaType": 2,
                "requirements": [
                    {
                        "areaType": 2,
                        "requiredLevel": 2,
                        "type": "Area"
                    },
                    {
                        "templateId": carBatteryID, //Car battery
                        "count": 1,
                        "isFunctional": false,
                        "resource": 110,
                        "isEncoded": false,
                        "type": "Tool"
                    }
                ],
                "productionTime": 10, // seconds
                "needFuelForAllProductionTime": false,
                "locked": false,
                "endProduct": carBatteryID,
                "continuous": false,
                "count": 1,
                "productionLimitCount": 0,
                "isEncoded": false
            },**/
            { // Car Battery!
                "_id": "cr123Recharge1",
                "areaType": 2,
                "requirements": [
                    {
                        "areaType": 2,
                        "requiredLevel": 2,
                        "type": "Area"
                    },
                    {
                        "templateId": "5733279d245977289b77ec24", //Car battery
                        "count": 1,
                        "isFunctional": false,
                        "resource": 100,
                        "isEncoded": false,
                        "type": "Tool"
                    },
                    {
                        "templateId": "5c06779c86f77426e00dd782", //Bundle of wires
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Item"
                    },
                    {
                        "templateId": rchblBatteryID,
                        "count": 2,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Item"
                    }
                ],
                "productionTime": 600, // seconds
                "needFuelForAllProductionTime": false,
                "locked": false,
                "endProduct": rchblBatteryID,
                "continuous": false,
                "count": 2,
                "productionLimitCount": 0,
                "isEncoded": false
            },
            { // Normal charging :(
                "_id": "cr123Recharge2",
                "areaType": 10,
                "requirements": [
                    {
                        "areaType": 10,
                        "requiredLevel": 3,
                        "type": "Area"
                    },
                    {
                        "templateId": "5909e99886f7740c983b9984", //USB Adapter
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Tool"
                    },
                    {
                        "templateId": "5c06779c86f77426e00dd782", //Bundle of wires
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Item"
                    },
                    {
                        "templateId": rchblBatteryID,
                        "count": 3,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Item"
                    }
                ],
                "productionTime": 600, // seconds
                "needFuelForAllProductionTime": false,
                "locked": false,
                "endProduct": rchblBatteryID,
                "continuous": false,
                "count": 3,
                "productionLimitCount": 0,
                "isEncoded": false
            },/*
            { // Normal charging FLIR :(
                "_id": "flirRecharge0",
                "areaType": 10,
                "requirements": [
                    {
                        "areaType": 10,
                        "requiredLevel": 3,
                        "type": "Area"
                    },
                    {
                        "templateId": "5909e99886f7740c983b9984", //USB Adapter
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Tool"
                    },
                    {
                        "templateId": flirID,
                        "count": 1,
                        "isFunctional": false,
                        "isEncoded": false,
                        "type": "Item"
                    }
                ],
                "productionTime": 600, // seconds
                "needFuelForAllProductionTime": false,
                "locked": false,
                "endProduct": flirID,
                "continuous": false,
                "count": 1,
                "productionLimitCount": 0,
                "isEncoded": false
            },*/
        );
        logger.success("BatterySystem has been applied!");
    }
}

module.exports = { mod: new Mod() }