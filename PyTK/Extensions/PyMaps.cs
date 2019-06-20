﻿using System.Collections.Generic;
using StardewValley;
using SObject = StardewValley.Object;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley.TerrainFeatures;
using StardewValley.Objects;
using StardewValley.Locations;
using xTile;
using xTile.Tiles;
using xTile.Layers;
using System.IO;
using PyTK.Types;
using xTile.Dimensions;
using System;
using Netcode;
using xTile.ObjectModel;
using PyTK.Tiled;
using Newtonsoft.Json;
using Microsoft.Xna.Framework.Graphics;
using xTile.Display;

namespace PyTK.Extensions
{
    public static class PyMaps
    {
        internal static IModHelper Helper { get; } = PyTKMod._helper;
        internal static IMonitor Monitor { get; } = PyTKMod._monitor;

        /// <summary>Looks for an object of the requested type on this map position.</summary>
        /// <returns>Return the object or null.</returns>
        public static T sObjectOnMap<T>(this Vector2 t) where T : SObject
        {
            if (Game1.currentLocation is GameLocation location)
            {
                if (location.netObjects.FieldDict.TryGetValue(t, out NetRef<SObject> netRaw) && netRaw.Value is T netValue)
                    return netValue;
                if (location.overlayObjects.TryGetValue(t, out SObject overlayRaw) && overlayRaw is T overlayValue)
                    return overlayValue;
            }
            return null;
        }

        /// <summary>Looks for an object of the requested type on this map position.</summary>
        /// <returns>Return the object or null.</returns>
        public static T terrainOnMap<T>(this Vector2 t) where T : TerrainFeature
        {
            if (Game1.currentLocation is GameLocation location)
            {
                if (location.terrainFeatures.FieldDict.TryGetValue(t, out NetRef<TerrainFeature> raw) && raw.Value is T value)
                    return value;
            }

            return null;
        }

        /// <summary>Looks for an object of the requested type on this map position.</summary>
        /// <returns>Return the object or null.</returns>
        public static T furnitureOnMap<T>(this Vector2 t) where T : Furniture
        {
            if (Game1.currentLocation is DecoratableLocation location)
            {
                List<Furniture> furniture = new List<Furniture>(location.furniture);
                return ((T) furniture.Find(f => f.getBoundingBox(t).Intersects(new Microsoft.Xna.Framework.Rectangle((int) t.X * Game1.tileSize, (int) t.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize))));
            }
            return null;
        }

        /// <summary>Looks for an object of the requested type on this map position.</summary>
        /// <returns>Return the object or null.</returns>
        public static SObject sObjectOnMap(this Vector2 t)
        {
            if (Game1.currentLocation is GameLocation location)
            {
                if (location.netObjects.FieldDict.TryGetValue(t, out NetRef<SObject> netObj))
                    return netObj;
                if (location.overlayObjects.TryGetValue(t, out SObject overlayObj))
                    return overlayObj;
            }
            return null;
        }

        public static bool setMapProperty(this Map map, string property, string value)
        {
            map.Properties[property] = value;
            return true;
        }

        public static string getMapProperty(this Map map, string property)
        {
            PropertyValue p = "";
            if (map.Properties.TryGetValue(property, out p))
            {
                return p.ToString();
            }
            return "";
        }

        public static bool hasTileSheet(this Map map, TileSheet tilesheet)
        {
            foreach (TileSheet ts in map.TileSheets)
                if (tilesheet.ImageSource.EndsWith(new FileInfo(ts.ImageSource).Name) || tilesheet.Id == ts.Id)
                    return true;

            return false;
        }

        public static Map enableMoreMapLayers(this Map map)
        {
            foreach (Layer layer in map.Layers)
            {
                if (layer.Properties.ContainsKey("OffestXReset"))
                {
                    layer.Properties["offsetx"] = layer.Properties["OffestXReset"];
                    layer.Properties["offsety"] = layer.Properties["OffestYReset"];
                }

                if (layer.Properties.Keys.Contains("DrawChecked"))
                    continue;

                if (layer.Properties.ContainsKey("Draw") && map.GetLayer(layer.Properties["Draw"]) is Layer maplayer)
                    maplayer.AfterDraw += (s, e) => drawLayer(layer, Location.Origin, layer.Properties.ContainsKey("WrapAround"));
                else if (layer.Properties.ContainsKey("DrawAbove") && map.GetLayer(layer.Properties["DrawAbove"]) is Layer maplayerAbove)
                    maplayerAbove.AfterDraw += (s, e) => drawLayer(layer, Location.Origin, layer.Properties.ContainsKey("WrapAround"));
                else if (layer.Properties.ContainsKey("DrawBefore") && map.GetLayer(layer.Properties["DrawBefore"]) is Layer maplayerBefore)
                    maplayerBefore.BeforeDraw += (s, e) => drawLayer(layer, Location.Origin, layer.Properties.ContainsKey("WrapAround"));

                layer.Properties["DrawChecked"] = true;
            }
            return map;
        }

        public static void drawLayer(Layer layer, Location offset, bool wrap = false)
        {
            drawLayer(layer, Game1.mapDisplayDevice, Game1.viewport, Game1.pixelZoom, offset, wrap);
        }

        public static bool setColorForTiledLayer(Layer layer)
        {
            bool changed = false;

            Color color = (Game1.mapDisplayDevice as XnaDisplayDevice).ModulationColour;

            if (layer.Properties.ContainsKey("Color"))
            {
                changed = true;
                string[] c = layer.Properties["Color"].ToString().Split(' ');
                color = new Color(int.Parse(c[0]), int.Parse(c[1]), int.Parse(c[2]), c.Length > 3 ? int.Parse(c[3]) : 255);
            }

            float opacity = 1f;
            PropertyValue opacityString = "1";

            if (layer.Properties.TryGetValue("opacity", out opacityString))
                float.TryParse(opacityString.ToString(), out opacity);

            changed = changed || opacity != 1f;

            (Game1.mapDisplayDevice as XnaDisplayDevice).ModulationColour = color * opacity;

            return changed;
        }

        public static void drawLayer(Layer layer, xTile.Display.IDisplayDevice device, xTile.Dimensions.Rectangle viewport, int pixelZoom, Location offset, bool wrap = false)
        {

            if (layer.Properties.ContainsKey("DrawConditions") && (!layer.Properties.ContainsKey("DrawConditionsResult") || layer.Properties["DrawConditionsResult"] != "T"))
                return;

            if (layer.Properties.ContainsKey("offsetx") && layer.Properties.ContainsKey("offsety"))
            {
                offset = new Location(int.Parse(layer.Properties["offsetx"]), int.Parse(layer.Properties["offsety"]));
                if (!layer.Properties.ContainsKey("OffestXReset"))
                {
                    layer.Properties["OffestXReset"] = offset.X;
                    layer.Properties["OffestYReset"] = offset.Y;
                }
            }

            if (!layer.Properties.ContainsKey("StartX"))
            {
                Vector2 local = Game1.GlobalToLocal(new Vector2(offset.X, offset.Y));
                layer.Properties["StartX"] = local.X;
                layer.Properties["StartY"] = local.Y;
            }

            if (layer.Properties.ContainsKey("AutoScrollX"))
            {
                string[] ax = layer.Properties["AutoScrollX"].ToString().Split(',');
                int cx = int.Parse(ax[0]);
                int mx = 1;
                if (ax.Length > 1)
                    mx = int.Parse(ax[1]);

                if (cx < 0)
                    mx *= -1;

                if (Game1.currentGameTime.TotalGameTime.Ticks % cx == 0)
                    offset.X += mx;
            }

            if (layer.Properties.ContainsKey("AutoScrollY"))
            {
                string[] ay = layer.Properties["AutoScrollY"].ToString().Split(',');
                int cy = int.Parse(ay[0]);
                int my = 1;
                if (ay.Length > 1)
                    my = int.Parse(ay[1]);

                if (cy < 0)
                    my *= -1;

                if (Game1.currentGameTime.TotalGameTime.Ticks % cy == 0)
                    offset.Y += my;
            }


            layer.Properties["offsetx"] = offset.X;
            layer.Properties["offsety"] = offset.Y;

            bool resetColor = false;

            if (layer.Properties.ContainsKey("tempOffsetx") && layer.Properties.ContainsKey("tempOffsety"))
                offset = new Location(int.Parse(layer.Properties["tempOffsetx"]), int.Parse(layer.Properties["tempOffsety"]));

            if (layer.Properties.ContainsKey("isImageLayer"))
                drawImageLayer(layer, offset, wrap);
            else
            {
                resetColor = setColorForTiledLayer(layer);
                layer.Draw(device, viewport, offset, wrap, pixelZoom);
            }

            if (resetColor)
                (Game1.mapDisplayDevice as XnaDisplayDevice).ModulationColour = Color.White;

        }

        private static void drawImageLayer(Layer layer, Location offset, bool wrap = false)
        {
            drawImageLayer(layer, Game1.mapDisplayDevice, Game1.viewport, Game1.pixelZoom, offset, wrap);
        }

        private static void drawImageLayer(Layer layer, xTile.Display.IDisplayDevice device, xTile.Dimensions.Rectangle viewport, int pixelZoom, Location offset, bool wrap = false)
        {
            string ts = "zImageSheet_" + layer.Id;

            if (layer.Properties.ContainsKey("UseImageFrom"))
                ts = "zImageSheet_" + layer.Properties["UseImageFrom"];

            Texture2D texture = Helper.Content.Load<Texture2D>(layer.Map.GetTileSheet(ts).ImageSource, ContentSource.GameContent);
            Vector2 pos = new Vector2(offset.X, offset.Y);


            pos = Game1.GlobalToLocal(pos);

            if (layer.Properties.ContainsKey("ParallaxX") || layer.Properties.ContainsKey("ParallaxY"))
            {
                Vector2 end = pos;
                if (layer.Properties.ContainsKey("OffestXReset"))
                {
                    end.X = layer.Properties["OffestXReset"];
                    end.Y = layer.Properties["OffestYReset"];
                }
                end = Game1.GlobalToLocal(end);

                Vector2 start = new Vector2(layer.Properties["StartX"], layer.Properties["StartY"]);

                Vector2 dif = start - end;

                if (layer.Properties.ContainsKey("ParallaxX"))
                    pos.X += ((float.Parse(layer.Properties["ParallaxX"]) * dif.X) / 100f) - dif.X;

                if (layer.Properties.ContainsKey("ParallaxY"))
                    pos.Y += ((float.Parse(layer.Properties["ParallaxY"]) * dif.Y) / 100f) - dif.Y;

            }

            Color color = Color.White;

            if (layer.Properties.ContainsKey("Color"))
            {
                string[] c = layer.Properties["Color"].ToString().Split(' ');
                color = new Color(int.Parse(c[0]), int.Parse(c[1]), int.Parse(c[2]), c.Length > 3 ? int.Parse(c[3]) : 255);
            }



            Microsoft.Xna.Framework.Rectangle dest = new Microsoft.Xna.Framework.Rectangle((int)pos.X, (int)pos.Y, texture.Width * Game1.pixelZoom, texture.Height * Game1.pixelZoom);
            if (!wrap)
                Game1.spriteBatch.Draw(texture, dest, color * (float)float.Parse(layer.Properties["opacity"]));

            var vp = new Microsoft.Xna.Framework.Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height);
            if (wrap)
            {
                Vector2 s = pos;

                while (s.X > (vp.X - (dest.Width * 2)) || s.Y > (vp.Y - (dest.Height * 2)))
                {
                    s.X -= dest.Width;
                    s.Y -= dest.Height;
                }

                Vector2 e = new Vector2(vp.X + vp.Width + (dest.Width * 2), vp.Height + vp.Y + (dest.Height * 2));

                for (float x = s.X; x <= e.X; x += dest.Width)
                    for (Microsoft.Xna.Framework.Rectangle n = new Microsoft.Xna.Framework.Rectangle((int)x, (int)s.Y, dest.Width, dest.Height); n.Y <= e.Y; n.Y += dest.Height)
                        if ((layer.Properties["WrapAround"] != "Y" || n.X == dest.X) && (layer.Properties["WrapAround"] != "X" || n.Y == dest.Y))
                            Game1.spriteBatch.Draw(texture, n, color * (float)float.Parse(layer.Properties["opacity"]));
            }
        }


        public static Map switchLayers(this Map t, string layer1, string layer2)
        {
            Layer newLayer1 = t.GetLayer(layer1);
            Layer newLayer2 = t.GetLayer(layer2);

            t.RemoveLayer(t.GetLayer(layer1));
            t.RemoveLayer(t.GetLayer(layer2));

            newLayer1.Id = layer2;
            newLayer2.Id = layer1;
            
            t.AddLayer(newLayer1);
            t.AddLayer(newLayer2);
            
            return t;
        }

        public static Map switchTileBetweenLayers(this Map t, string layer1, string layer2, int x, int y)
        {
            Location tileLocation = new Location(x , y);

            Tile tile1 = t.GetLayer(layer1).Tiles[tileLocation];
            Tile tile2 = t.GetLayer(layer2).Tiles[tileLocation];

            t.GetLayer(layer1).Tiles[tileLocation] = tile2;
            t.GetLayer(layer2).Tiles[tileLocation] = tile1;
            
            return t;
        }

        public static GameLocation clearArea(this GameLocation l, Microsoft.Xna.Framework.Rectangle area)
        {

            for (int x = area.X; x < area.Width; x++)
                for (int y = area.Y; y < area.Height; y++)
                {
                    l.objects.Remove(new Vector2(x, y));
                    l.largeTerrainFeatures.Remove(new List<LargeTerrainFeature>(l.largeTerrainFeatures).Find(p => p.tilePosition.Value == new Vector2(x,y)));
                    l.terrainFeatures.Remove(new Vector2(x, y));
                }

            return l;
        }

        public static Map mergeInto(this Map t, Map map, Vector2 position, Microsoft.Xna.Framework.Rectangle? sourceArea = null, bool includeEmpty = true, bool properties = true)
        {
            Microsoft.Xna.Framework.Rectangle sourceRectangle = sourceArea.HasValue ? sourceArea.Value : new Microsoft.Xna.Framework.Rectangle(0, 0, t.DisplayWidth / Game1.tileSize, t.DisplayHeight / Game1.tileSize);

            foreach (TileSheet tilesheet in t.TileSheets)
                if (!map.hasTileSheet(tilesheet))
                    map.AddTileSheet(new TileSheet(tilesheet.Id, map, tilesheet.ImageSource, tilesheet.SheetSize, tilesheet.TileSize));

            if(properties)
            foreach (KeyValuePair<string, PropertyValue> p in t.Properties)
                if (map.Properties.ContainsKey(p.Key))
                    if (p.Key == "EntryAction")
                        map.Properties[p.Key] = map.Properties[p.Key] + ";" + p.Value;
                    else
                        map.Properties[p.Key] = p.Value;
                else
                    map.Properties.Add(p);

            for(int x = 0; x < sourceRectangle.Width; x++)
                for(int y = 0; y < sourceRectangle.Height; y++)
                    foreach(Layer layer in t.Layers)
                    {
                        int px = (int)position.X + x;
                        int py = (int)position.Y + y;

                        int sx = (int)sourceRectangle.X + x;
                        int sy = (int)sourceRectangle.Y + y;

                        Tile sourceTile = layer.Tiles[(int)sx, (int)sy];
                        Layer mapLayer = map.GetLayer(layer.Id);

                        if (mapLayer == null)
                        {
                            map.InsertLayer(new Layer(layer.Id, map, map.Layers[0].LayerSize, map.Layers[0].TileSize), map.Layers.Count);
                            mapLayer = map.GetLayer(layer.Id);
                        }

                        if (properties)
                            foreach (var prop in layer.Properties)
                                if (!mapLayer.Properties.ContainsKey(prop.Key))
                                    mapLayer.Properties.Add(prop);
                                else
                                    mapLayer.Properties[prop.Key] = prop.Value;

                        if (sourceTile == null)
                        {
                            if (includeEmpty)
                            {
                                try
                                {
                                    mapLayer.Tiles[(int)px, (int)py] = null;
                                }
                                catch { }
                            }
                            continue;
                        }
                        
                        TileSheet tilesheet = map.GetTileSheet(sourceTile.TileSheet.Id);
                        Tile newTile = new StaticTile(mapLayer, tilesheet, BlendMode.Additive, sourceTile.TileIndex);

                        if (sourceTile is AnimatedTile aniTile)
                        {
                            List<StaticTile> staticTiles = new List<StaticTile>();

                            foreach (StaticTile frame in aniTile.TileFrames)
                                staticTiles.Add(new StaticTile(mapLayer, tilesheet, BlendMode.Additive, frame.TileIndex));

                            newTile = new AnimatedTile(mapLayer, staticTiles.ToArray(), aniTile.FrameInterval);
                        }

                        if (properties)
                            foreach (var prop in sourceTile.Properties)
                                newTile.Properties.Add(prop);
                        try
                        {
                            mapLayer.Tiles[(int)px, (int)py] = newTile;
                        }
                        catch (Exception e)
                        {
                          Monitor.Log($"{e.Message} ({map.DisplayWidth} -> {layer.Id} -> {px}:{py})");
                        }
                    }
           
            return map;
        }

        public static void addAction(this Map m, Vector2 position, TileAction action, string args)
        {
            m.GetLayer("Buildings").PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size).Properties.AddOrReplace("Action", action.trigger + " " + args);
        }

        public static void addAction(this Map m, Vector2 position, string trigger, string args)
        {
            m.GetLayer("Buildings").PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size).Properties.AddOrReplace("Action", trigger + " " + args);
        }

        public static void addTouchAction(this Map m, Vector2 position, TileAction action, string args)
        {
            m.GetLayer("Back").PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size).Properties.AddOrReplace("TouchAction", action.trigger + " " + args);
        }

        public static void addTouchAction(this Map m, Vector2 position, string trigger, string args)
        {
            m.GetLayer("Back").PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size).Properties.AddOrReplace("TouchAction", trigger + " " + args);
        }
    }
}
