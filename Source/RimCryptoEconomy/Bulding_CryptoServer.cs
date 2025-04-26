using System;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RimCryptoEconomy
{
    public class Building_CryptoServer : Building_WorkTable_HeatPush
    {
        private CompPowerTrader powerComp;
        private CompGlower glowerComp;
        private int productionDuration = 2500; // Default duration (1 hour in ticks)
        private int ticksRemaining = 0; // Tracks the time left for the next coin production
        private CryptoCoinData coinToMine; // The coin selected for mining
        private string savedCoinName; //named of coin for saving 
        private Graphic cryptoServerGraphic; //graphic for server on
        private Graphic cryptoServerRedGraphic; //graphic for server off
        private bool glowColorFlag = false; //set once per game to ensure proper glow color when using BuildCopy
        public CryptoCoinData CoinToMine { get { return coinToMine; } }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref savedCoinName, "savedCoinName");
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining");
            Scribe_Values.Look(ref productionDuration, "productionDuration");
            Scribe_Values.Look(ref glowColorFlag, "glowColorFlag");
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);


            this.powerComp = this.GetComp<CompPowerTrader>();
            this.glowerComp = this.GetComp<CompGlower>();
            


            //Load On Main Thread
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                this.cryptoServerGraphic = GraphicDatabase.Get<Graphic_Single>("Servers/CryptoServer");
                this.cryptoServerGraphic.data = this.def.graphicData;
                this.cryptoServerGraphic.drawSize = new Vector2(1.4f, 1.25f);

                this.cryptoServerRedGraphic = GraphicDatabase.Get<Graphic_Single>("Servers/CryptoServer_Red");
                this.cryptoServerRedGraphic.data = this.def.graphicData;
                this.cryptoServerRedGraphic.drawSize = new Vector2(1.4f, 1.25f);
            });
        }
        public override void Tick()
        {
            base.Tick();


            if (!CanMineNow || coinToMine == null)
            {
                if (!glowColorFlag) //flag to set proper glow color when using BuildCopy
                {
                    this.glowerComp.GlowColor = new ColorInt(100, 0, 0, 255);
                    glowColorFlag = true;
                }
                return;
            }

            // If we just started or the coin is ready to be produced
            if (ticksRemaining <= 0)
            {
                ProduceCoin();
                productionDuration = (int)(350f * coinToMine.CurrentPrice + 2500f);
                ticksRemaining = productionDuration;  // Reset the timer for the next coin
            }
            else
            {
                ticksRemaining--;  // Decrease the remaining time
                if (ticksRemaining <= 0)
                    ticksRemaining = 0;
            }
        }


        public void setCoinToMine(string coinName)
        {
            if (coinName == null)
            {
                glowerComp.GlowColor = new ColorInt(100, 0, 0, 255);
                this.coinToMine = null;
                productionDuration = 2500;
                ticksRemaining = 0;
                savedCoinName = coinName;
                return;
            }
            glowerComp.GlowColor = new ColorInt(0, 100, 0, 255);
            this.coinToMine = CryptoEconomyManager.Instance.Coins[coinName];
            productionDuration = (int)(350f * coinToMine.CurrentPrice + 2500f);
            ticksRemaining = productionDuration;
            savedCoinName = coinName;
        }
        public void loadCoinToMine()
        {
            if (savedCoinName == null)
                this.coinToMine = null;
            else
                this.coinToMine = CryptoEconomyManager.Instance.Coins[savedCoinName];
        }
        public bool CanMineNow
        {
            get
            {
                return this.powerComp != null && this.powerComp.PowerOn;
            }
        }
        private void ProduceCoin()
        {
            // Create a new RimCoin
            Thing coin = ThingMaker.MakeThing(ThingDef.Named(coinToMine.defName));
            coin.stackCount = 1;

            // Spawn the coin directly on the ground at the server's location (or nearby)
            IntVec3 spawnLocation = this.Position; // Spawn directly on top of the server

            if (spawnLocation.IsValid)
            {
                GenPlace.TryPlaceThing(coin, spawnLocation, this.Map, ThingPlaceMode.Near);
            }
        }
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selectedPawn)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Check if the server can mine (is powered on)
            if (!CanMineNow)
            {
                options.Add(new FloatMenuOption("Crypto Server: No power", null));
            }
            else
            {
                // Loop through released coins and add them as options
                foreach (var coin in CryptoEconomyManager.Instance.Coins)
                {
                    if (coin.Value.IsReleased)
                    {
                        string coinLabel = $"Mine {coin.Value.MarketName}";
                        options.Add(new FloatMenuOption(coinLabel, () => StartMining(coin.Value.defName)));
                    }
                }
            }

            return options;
        }




        // Start mining the selected coin
        private void StartMining(string coinName)
        {
            if (CryptoEconomyManager.Instance.Coins[coinName].IsReleased)
            {
                setCoinToMine(coinName);
            }
                
            else
            {
                setCoinToMine(null);
            }

        }
        public override string GetInspectString()
        {
            string progressText = string.Empty;
            if (coinToMine == null)
            {
                return base.GetInspectString() + "\nReady For Next Coin!\nNo Coin Selected";
            }
            if (ticksRemaining > 0)
            {
                float progress = 100f * (1 - (float)ticksRemaining / productionDuration); // Calculate the progress
                progressText = $"\nMining {coinToMine.MarketName} - ({productionDuration / 2500} Total Hrs)\n{progress:0.0}%";
            }
            else
            {
                progressText = "\nReady For Next Coin!\nNo Coin Selected";
            }

            return base.GetInspectString() + progressText;
        }
        public override Graphic Graphic
        {
            get
            {
                if (coinToMine == null)
                {
                    return cryptoServerRedGraphic;
                } 
                else
                {
                    return cryptoServerGraphic;
                }
            }
        }
    }



}
