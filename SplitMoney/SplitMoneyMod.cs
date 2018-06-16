﻿using Harmony;
using StardewModdingAPI;
using StardewValley;
using System.Reflection;
using PyTK;
using PyTK.CustomElementHandler;
using PyTK.Types;
using PyTK.Extensions;
using StardewValley.Menus;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System;
using SObject = StardewValley.Object;
using System.Threading.Tasks;

namespace SplitMoney
{
    public class SplitMoneyMod : Mod
    {
        internal static PyResponder<bool, int> moneyReceiver;
        internal static string moneyReceiverName = "Platonymous.MoneyReceiver";

        public override void Entry(IModHelper helper)
        {
            moneyReceiver = new PyResponder<bool, int>(moneyReceiverName, (i) => { Game1.player.Money += i; return true; }, 30);
            moneyReceiver.start();
            #region moneyItem
            new CustomObjectData("Platonymous.G", "G/1/-300/Basic/G/The common currency of Pelican Town.", Game1.mouseCursors.getArea(new Rectangle(280, 410, 16, 16)), Color.White, type: typeof(GoldItem));
            ButtonClick.ActionButton.onClick((pos) => new List<IClickableMenu>(Game1.onScreenMenus).Exists(m => m is DayTimeMoneyBox && m.isWithinBounds(pos.X, pos.Y - 180) && m.isWithinBounds(pos.X, pos.Y) && m.isWithinBounds(pos.X, pos.Y + 50)), (p) => convertMoney());
            #endregion

            #region harmony
            HarmonyInstance instance = HarmonyInstance.Create("Platonymous.SplitMoney");
            instance.PatchAll(Assembly.GetExecutingAssembly());
            #endregion
        }

        #region methods
        private void convertMoney()
        {
            if (Game1.player.ActiveObject != null)
            {
                if (Game1.player.ActiveObject is GoldItem s)
                {
                    Game1.player.money += s.Stack;
                    Game1.player.removeItemFromInventory(s);
                    Game1.playSound("sell");
                }
                return;
            }

            int a = Math.Min(999, Game1.player.money);

            if (a <= 0)
                return;

            Game1.player.money -= a;
            Item gold = CustomObjectData.collection["Platonymous.G"].getObject();
            gold.Stack = a;
            Game1.player.addItemByMenuIfNecessary(gold);
            Game1.playSound("purchase");
        }
        #endregion

        #region overrides
        [HarmonyPatch]
        internal class MoneyGetter
        {
            internal static MethodInfo TargetMethod()
            {
                return PyUtils.getTypeSDV("Farmer").GetProperty("money").GetGetMethod();
            }

            internal static bool Prefix(Farmer __instance, ref int __result)
            {
                if (!Game1.IsMultiplayer || Game1.IsServer || Game1.MasterPlayer == __instance)
                    return true;

                if (__instance.friendships == null)
                    __instance.friendships = new SerializableDictionary<string, int[]>();

                if (!__instance.friendships.ContainsKey("money"))
                    __instance.friendships.Add("money", new int[] { 500 });

                    __result = __instance.friendships["money"][0];

                return false;
            }
        }

        [HarmonyPatch]
        internal class MoneySetter
        {
            internal static MethodInfo TargetMethod()
            {
                return PyUtils.getTypeSDV("Farmer").GetProperty("money").GetSetMethod();
            }

            internal static bool Prefix(Farmer __instance, int value)
            {
                if (!Game1.IsMultiplayer || Game1.IsServer || Game1.MasterPlayer == __instance)
                    return true;

                if (__instance.friendships == null)
                    __instance.friendships = new SerializableDictionary<string, int[]>();

                if (!__instance.friendships.ContainsKey("money"))
                    __instance.friendships.Add("money", new int[] { value });
                else
                    __instance.friendships["money"][0] = value;

                return false;
            }
        }

        [HarmonyPatch]
        internal class ProposalFix
        {
            internal static MethodInfo TargetMethod()
            {
                return AccessTools.Method(PyUtils.getTypeSDV("FarmerTeam"), "SendProposal");
            }

            internal static bool Prefix(Farmer receiver, ProposalType proposalType, Item gift)
            {
                if (proposalType == ProposalType.Gift && gift is GoldItem)
                {
                    int money = Game1.player.ActiveObject.Stack;
                    Task.Run(async () =>
                   {
                       await PyNet.sendRequestToFarmer<bool>(moneyReceiverName, money, receiver, (b) => { if (!b) { Game1.player.Money += money; } }, SerializationType.PLAIN, 3000);
                   });
                    Game1.player.removeItemFromInventory(Game1.player.ActiveObject);
                    return false;
                }
                else
                    return true;
            }
        }
        #endregion

    }
}
