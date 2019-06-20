﻿using System.Collections.Generic;
using StardewValley;
using Microsoft.Xna.Framework;
using StardewValley.TerrainFeatures;

namespace HarpOfYobaRedux
{
    class SeedMagic : IMagic
    {

        public SeedMagic()
        {

        }

        public void doMagic(bool playedToday)
        {
            if (!playedToday)
            {
               List<Vector2> tiles =  Utility.getAdjacentTileLocations(Game1.player.getTileLocation());
                Vector2 playerTile = Game1.player.getTileLocation();
                tiles.Add(playerTile);
                tiles.Add(playerTile + new Vector2(1f, 1f));
                tiles.Add(playerTile + new Vector2(-1f, -1f));
                tiles.Add(playerTile + new Vector2(1f, -1f));
                tiles.Add(playerTile + new Vector2(-1f, 1f));

                foreach (Vector2 tile in tiles)
                {
                    if(Game1.currentLocation.terrainFeatures.ContainsKey(tile) && Game1.currentLocation.terrainFeatures[tile] is HoeDirt)
                    {
                        HoeDirt hd = (HoeDirt) Game1.currentLocation.terrainFeatures[tile];
                        if (hd.crop == null)
                        {
                            int seeds = 770;

                            if (Game1.IsWinter)
                                seeds = 498;

                            hd.plant(seeds,(int)tile.X, (int)tile.Y,Game1.player, false, Game1.currentLocation);

                            if(hd.crop != null)
                            {
                                hd.crop.newDay(1, 0, (int)tile.X, (int)tile.Y, Game1.currentLocation);
                                Game1.playSound("leafrustle");
                            }
                            
                        }
                        
                    }
                }

            }
        }
    }
}
