﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using Harmony;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Network;
using StardewValley.Quests;
using StardewModdingAPI.Events;
using StardewValley.Menus;
using StardewValley.Locations;
using StardewValley.Buildings;
using PyTK.Extensions;

namespace Ultiplayer
{
    public class UltiplayerMod : Mod
    {
        #region fields
        internal static IModHelper help;
        internal static IMonitor mon;
        internal static Random rnd;
        internal static List<NetRef<Farmer>> farmers;
        internal static string farmhandDirectory;
        internal static bool ultiplayer;
        internal const int maxDistance = 810000;
        internal const int overflow = 6;
        #endregion

        #region init
        public override void Entry(IModHelper helper)
        {
            help = Helper;
            mon = Monitor;
            rnd = new Random();
            ultiplayer = false;
            farmers = new List<NetRef<Farmer>>();
            farmhandDirectory = Path.Combine(help.DirectoryPath, "farmhands");

            #region harmony
            HarmonyInstance instance = HarmonyInstance.Create("Platonymous.Ultiplayer");
            instance.PatchAll(Assembly.GetExecutingAssembly());
            #endregion

            #region events
            helper.Events.GameLoop.DayStarted += (s, e) => SaveFarmhand();
            helper.Events.GameLoop.SaveLoaded += (s, e) => LoadFarmhands();
            helper.Events.Input.ButtonPressed += (s, e) => OnButtonPressed(e.Button);
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            #endregion
        }

        #region ui

        private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e)
        {
            string message = ultiplayer ? "Ultiplayer activated" : "Ultiplayer deactivated";

            if (Game1.activeClickableMenu is TitleMenu)
                Game1.spriteBatch.DrawString(Game1.smallFont, Game1.parseText(message, Game1.smallFont, 640), new Vector2(8f, (float)((double)Game1.viewport.Height - (double)Game1.smallFont.MeasureString(Game1.parseText(message, Game1.smallFont, 640)).Y - 4.0)), Color.Red);
        }
        #endregion

        #endregion

        #region inputHandling

        internal void OnButtonPressed(SButton key)
        {
            if (Game1.IsClient)
            {
                GameLocation location = null;
                try
                {
                  location =  Game1.getLocationFromName("FarmHouse_" + Game1.player.UniqueMultiplayerID);
                }
                catch
                {

                }

                if (location == null)
                {
                    Game1.getLocationFromName("FarmHouse").map.inject(@"Maps/FarmHouse_" + Game1.player.UniqueMultiplayerID);
                    Game1.locations.Add(new FarmHouse(@"Maps/FarmHouse_" + Game1.player.UniqueMultiplayerID, "FarmHouse_" + Game1.player.UniqueMultiplayerID));
                }
                if (key == SButton.J)
                    Game1.warpFarmer("FarmHouse_" + Game1.player.UniqueMultiplayerID, 100, 50, 1);
            }

            if (Game1.activeClickableMenu is TitleMenu t && key == SButton.U)
            {
                ultiplayer = !ultiplayer;
                mon.Log(ultiplayer ? "Ultiplayer activated" : "Ultiplayer deactivated", LogLevel.Info);
            }

        }

        #endregion

        #region FarmHand sync

        private static void LoadFarmhands()
        {
            if (!ultiplayer)
                return;

            farmers = new List<NetRef<Farmer>>();
            string[] files = Directory.GetFiles(farmhandDirectory, "*." + Game1.uniqueIDForThisGame + ".xml", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                FileStream fs = new FileStream(file, FileMode.Open);
                Farmer f = (Farmer)SaveGame.farmerSerializer.Deserialize(fs);
                farmers.Add(new NetRef<Farmer>(f));
            }
            
        }

        private static void SaveFarmhand()
        {
                    string path = Path.Combine(farmhandDirectory, Game1.player.Name + "_" + Game1.player.UniqueMultiplayerID + "." + Game1.uniqueIDForThisGame + ".xml");
                    FileStream fs = new FileStream(path, FileMode.Create);
                    SaveGame.farmerSerializer.Serialize(fs, Game1.player);
        }

        

        #endregion

        #region overrides

        internal static Dictionary<long, OutgoingMessage> cache = new Dictionary<long, OutgoingMessage>();
        internal static Dictionary<long,int> peers = new Dictionary<long, int>();

        [HarmonyPatch]
        internal class ServerSpeedFix
        {
            internal static MethodInfo TargetMethod()
            {
                return AccessTools.Method(getTypeSDV("Network.GameServer"), "sendMessage",new[] { typeof(long), typeof(OutgoingMessage) });
            }

            internal static bool Prefix(GameServer __instance, long peerId, OutgoingMessage message)
            {
                return skipMessage(peerId, message);
            }
        }

        internal static double getDistance(Vector2 p1, Vector2 p2)
        {
            float distX = Math.Abs(p1.X - p2.X);
            float distY = Math.Abs(p1.Y - p2.Y);
            double dist = (distX * distX) + (distY * distY);
            return dist;
        }

        internal static bool skipMessage(long peerId, OutgoingMessage message)
        {
            if (!ultiplayer)
                return true;

            if (message.MessageType != 0)
                return true;

                if (!peers.ContainsKey(peerId))
                peers.Add(peerId, overflow);

            Farmer compare = Game1.player;

                if (message.Data[0] is Byte[] b)
                    compare = message.SourceFarmer;


            if (Game1.otherFarmers[peerId] == compare || (int.Parse(message.MessageType.ToString()) == 0 && (Game1.otherFarmers[peerId].currentLocation != compare.currentLocation || getDistance(new Vector2(compare.position.X, compare.position.Y), new Vector2(Game1.otherFarmers[peerId].position.X, Game1.otherFarmers[peerId].position.Y)) > maxDistance)))
            {
                peers[peerId]--;
                
                if (peers[peerId] > 0)
                    return true;
                else
                    return false;
                
            }
            else
            {
                peers[peerId] = overflow;
                return true;
            } 
        }

        [HarmonyPatch]
        internal class FarmerFix
        {
            internal static MethodInfo TargetMethod()
            {
                return AccessTools.Method(getTypeSDV("Multiplayer"), "receiveTeamDelta");
            }

            internal static bool Prefix(Farmer __instance)
            {
                /* List<INetSerializable>  list = help.Reflection.GetField<List<INetSerializable>>((object) __instance.NetFields, "fields").GetValue();
                 return false;*/
                return true;

            }
        }

        [HarmonyPatch]
        internal class ServerFix
        {
            internal static MethodInfo TargetMethod()
            {
                return AccessTools.Method(getTypeSDV("Network.GameServer"), "sendAvailableFarmhands");
            }

            internal static bool Prefix(GameServer __instance, string userID, Action<OutgoingMessage> sendMessage)
            {
                if (!ultiplayer)
                    return true;

                return sendAvailableFarmhands(userID, sendMessage);
            }
        }


        [HarmonyPatch]
        internal class ServerFix2
        {
            internal static MethodInfo TargetMethod()
            {
                return AccessTools.Method(getTypeSDV("Network.GameServer"), "checkFarmhandRequest");
            }

            internal static bool Prefix(GameServer __instance, string userID, NetFarmerRoot farmer, Action<OutgoingMessage> sendMessage, Action approve)
            {
                if (!ultiplayer)
                    return true;

                long id = farmer.Value.UniqueMultiplayerID;
                approve();
                Multiplayer multiplayer = (Multiplayer)typeof(Game1).GetField("multiplayer", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                farmer.Value.currentLocation = Game1.getLocationFromName("FarmHouse");
                farmer.Value.Position = new Vector2(640f, 320f);
                multiplayer.addPlayer(farmer);
                multiplayer.broadcastPlayerIntroduction(farmer);
                __instance.sendServerIntroduction(id);
                __instance.updateLobbyData();

                GameLocation location = null;
                try
                {
                    location = Game1.getLocationFromName("FarmHouse_" + farmer.Value.UniqueMultiplayerID);
                }
                catch
                {

                }

                if (location == null)
                {
                    Game1.getLocationFromName("FarmHouse").map.inject(@"Maps/FarmHouse_" + farmer.Value.UniqueMultiplayerID);
                    Game1.locations.Add(new FarmHouse(@"Maps/FarmHouse_" + farmer.Value.UniqueMultiplayerID, "FarmHouse_" + farmer.Value.UniqueMultiplayerID));
                }

                return false;
            }

        }

        [HarmonyPatch]
        internal class ServerFix3
        {
            internal static MethodInfo TargetMethod()
            {
                return AccessTools.Method(getTypeSDV("Menus.CoopMenu"), "addSaveFiles");
            }

            internal static void Prefix(GameServer __instance, ref List<Farmer> files)
            {
                if (!ultiplayer)
                    return;

                files.ForEach(file => file.slotCanHost = true);
            }
        }

        [HarmonyPatch]
        internal class ServerFix4
        {
            internal static MethodInfo TargetMethod()
            {
                return AccessTools.Method(getTypeSDV("Multiplayer"), "removeDisconnectedFarmers");
            }

            internal static void Prefix(Multiplayer __instance)
            {
                if (!ultiplayer)
                    return;

                List<long> disc = help.Reflection.GetField<List<long>>(__instance, "disconnectingFarmers").GetValue();
               /* foreach (long id in disc)
                    SaveFarmhand(id); */
            }
        }

        internal static NetRef<Farmer> getNewFarmHand()
        {
            NetRef<Farmer> farmer = new NetRef<Farmer>();
            farmer.Value = new Farmer(new FarmerSprite((string)null), new Vector2(0.0f, 0.0f), 1, "", Farmer.initialTools(), true);
            farmer.Value.UniqueMultiplayerID = Utility.RandomLong(rnd);
            farmer.Value.questLog.Add((Quest)(Quest.getQuestFromId(9) as SocializeQuest));
            farmer.Value.farmName.Value = Game1.MasterPlayer.farmName.Value;
            farmer.Value.homeLocation.Value ="FarmHouse";
            farmer.Value.currentLocation = Game1.getLocationFromName("FarmHouse");
            farmer.Value.Position = new Vector2(640f, 320f);
            return farmer;
        }

        #endregion

        #region override helper

        internal static Type getTypeSDV(string type)
        {
            string prefix = "StardewValley.";
            Type defaulSDV = Type.GetType(prefix + type + ", Stardew Valley");

            if (defaulSDV != null)
                return defaulSDV;
            else
                return Type.GetType(prefix + type + ", StardewValley");

        }

        internal static bool authCheck(string userID, Farmer farmhand)
        {
            if (!Game1.options.enableFarmhandCreation && !farmhand.isCustomized.Value)
                return false;
            if (!(userID == "") && !(farmhand.userID.Value == ""))
                return farmhand.userID.Value == userID;
            return true;
        }

        internal static IEnumerable<Cabin> cabins()
        {
            if (Game1.getFarm() != null)
                foreach (Building building in Game1.getFarm().buildings)
                    if (building.daysOfConstructionLeft.Value <= 0 && building.indoors.Value is Cabin)
                        yield return building.indoors.Value as Cabin;
        }

        internal static bool sendAvailableFarmhands(string userID, Action<OutgoingMessage> sendMessage)
        {
            Multiplayer multiplayer = (Multiplayer)typeof(Game1).GetField("multiplayer", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            List<NetRef<Farmer>> netRefList = new List<NetRef<Farmer>>();

            /* foreach (Cabin cabin in cabins())
                if (cabin.getFarmhand() is NetRef<Farmer> farmhand && ((!farmhand.Value.isActive() || multiplayer.isDisconnecting(farmhand.Value.UniqueMultiplayerID)) && authCheck(userID, farmhand.Value)))
                    netRefList.Add(farmhand);

            if (netRefList.Count > 0)
                return true;*/

            foreach (NetRef<Farmer> f in farmers)
                if ((!f.Value.isActive() || multiplayer.isDisconnecting(f.Value.UniqueMultiplayerID)) && authCheck(userID, f.Value))
                {
                    f.Value.currentLocation = Game1.getLocationFromName("FarmHouse");
                    f.Value.homeLocation.Value = "FarmHouse";
                    f.Value.Position = new Vector2(640f, 320f);
                    netRefList.Add(f);
                }

            if (netRefList.Count < 1)
            {
                NetRef<Farmer> newF = getNewFarmHand();
                farmers.Add(newF);
                netRefList.Add(newF);
            }

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter((Stream)memoryStream))
                {
                    writer.Write(Game1.year);
                    writer.Write(Utility.getSeasonNumber(Game1.currentSeason));
                    writer.Write(Game1.dayOfMonth);
                    writer.Write((byte)netRefList.Count);
                    foreach (NetRef<Farmer> netRef in netRefList)
                    {
                        try
                        {
                            netRef.Serializer = SaveGame.farmerSerializer;
                            netRef.WriteFull(writer);
                        }
                        catch (Exception e)
                        {
                            mon.Log(e.Message + ":" + e.StackTrace);
                        }
                        finally
                        {
                            netRef.Serializer = (XmlSerializer)null;
                        }
                    }
                    memoryStream.Seek(0L, SeekOrigin.Begin);
                    sendMessage(new OutgoingMessage((byte)9, Game1.player, new object[1]
                    {
            (object) memoryStream.ToArray()
                    }));
                }


            }

            return false;
        }
        #endregion
    }

}
