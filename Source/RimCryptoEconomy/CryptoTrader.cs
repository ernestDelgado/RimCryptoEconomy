using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
using RimWorld.Planet;

namespace RimCryptoEconomy
{
    public class CryptoTrader : ITrader
    {
        private readonly int randomSeed;
        private readonly TraderKindDef traderKind;
        private readonly List<Thing> goods = new List<Thing>();

        public CryptoTrader()
        {
            traderKind = DefDatabase<TraderKindDef>.GetNamed("RimCrypto_Trader");
            randomSeed = Gen.HashCombineInt(Find.World.info.Seed, 8888);

            goods = new List<Thing>();

            // Fixed amount of silver
            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = 80_000;
            goods.Add(silver);

            // Add exactly 10,000 of each released coin
            foreach (var kv in CryptoEconomyManager.Instance.Coins)
            {
                if (!kv.Value.IsReleased)
                    continue;

                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);
                if (def == null)
                {
                    Log.Warning($"[RimCrypto] Released coin {kv.Key} has no ThingDef!");
                    continue;
                }

                Thing coin = ThingMaker.MakeThing(def);
                coin.stackCount = 10_000;
                goods.Add(coin);
            }
        }


        public TraderKindDef TraderKind => traderKind;
        public Faction Faction => Faction.OfPlayer;
        public string TraderName => "Thrumbo Swap";
        public IEnumerable<Thing> Goods => goods;

        public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
        {
            var map = playerNegotiator?.Map ?? Find.CurrentMap;
            foreach (var thing in TradeUtility.AllLaunchableThingsForTrade(map))
            {
                if (thing.def == ThingDefOf.Silver)
                    yield return thing;

                if (thing.def.thingCategories != null && thing.def.thingCategories.Contains(ThingCategoryDef.Named("Crypto")))
                    yield return thing;
            }
        }

        public bool TryGetPriceType(ThingDef thingDef, TradeAction action, out PriceType priceType)
        {
            if (thingDef.thingCategories != null && thingDef.thingCategories.Contains(ThingCategoryDef.Named("Crypto")))
            {
                priceType = PriceType.Normal;
                return true;
            }

            priceType = PriceType.Undefined;
            return false;
        }


        public int RandomPriceFactorSeed => randomSeed;
        public float TradePriceImprovementOffsetForPlayer => 0f;
        public TradeCurrency TradeCurrency => TradeCurrency.Silver;
        public bool CanTradeNow => true;
        public bool EverPlayerSellable => true;

        public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            if (toGive.def.thingCategories == null || !toGive.def.thingCategories.Contains(ThingCategoryDef.Named("Crypto")))
                return;

            // 🔍 Find the Tradeable entry for crypto
            Tradeable tradeable = TradeSession.deal.AllTradeables
                .FirstOrDefault(t => t.ThingDef == toGive.def);

            float unitPrice = tradeable?.GetPriceFor(TradeAction.PlayerBuys) ?? toGive.MarketValue;
            int totalCost = Mathf.RoundToInt(unitPrice * countToGive);

            // 🔥 Remove silver manually from beacon-accessible piles
            Map map = playerNegotiator?.Map ?? Find.CurrentMap;
            int remaining = totalCost;
            var silvers = TradeUtility.AllLaunchableThingsForTrade(map)
                .Where(t => t.def == ThingDefOf.Silver)
                .ToList();

            foreach (var silver in silvers)
            {
                if (remaining <= 0) break;
                int take = Mathf.Min(remaining, silver.stackCount);
                silver.SplitOff(take).Destroy(DestroyMode.Vanish);
                remaining -= take;
            }

            if (remaining > 0)
                Log.Warning($"[RimCrypto] Not enough silver! Still owed {remaining}");

            if (CryptoEconomyManager.Instance.Coins.TryGetValue(toGive.def.defName, out var coinData))
            {
                coinData.TotalSpentOnPurchases += totalCost;
                coinData.TotalCoinsBought += countToGive;
            }

            // 🎯 Spawn the crypto
            Thing coin = ThingMaker.MakeThing(toGive.def);
            coin.stackCount = countToGive;

            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.MakeDropPodAt(dropSpot, map, new ActiveDropPodInfo
            {
                SingleContainedThing = coin,
                leaveSlag = false,
                openDelay = 60
            });

            Log.Message($"[RimCrypto] Bought {countToGive}x {toGive.def.defName} for {totalCost} silver (drop pod at {dropSpot})");
        }
        public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
        {
            Log.Message($"[RimCrypto] GiveSoldThingTOTrader countToGive: {countToGive}");
            if (toGive.def.thingCategories == null || !toGive.def.thingCategories.Contains(ThingCategoryDef.Named("Crypto")))
                return;


            // 🔥 Remove sold crypto coins from the player's inventory
            Map map = playerNegotiator?.Map ?? Find.CurrentMap;
            int remaining = countToGive;  // The number of coins the player wants to sell
            var coinsToRemove = TradeUtility.AllLaunchableThingsForTrade(map)
                .Where(t => t.def == toGive.def && t.stackCount > 0)
                .ToList();

            foreach (var stack in coinsToRemove)
            {
                if (remaining <= 0)
                    break;

                int take = Mathf.Min(stack.stackCount, remaining);
                stack.SplitOff(take).Destroy(DestroyMode.Vanish);
                remaining -= take;
            }

            if (CryptoEconomyManager.Instance.Coins.TryGetValue(toGive.def.defName, out var coinData))
            {
                int stillOwned = map.listerThings.ThingsOfDef(toGive.def).Sum(t => t.stackCount);
                if (stillOwned == 0)
                {
                    coinData.TotalSpentOnPurchases = 0f;
                    coinData.TotalCoinsBought = 0;
                }
            }

            // Determine price using TradeSession
            Tradeable tradeable = TradeSession.deal.AllTradeables
                .FirstOrDefault(t => t.ThingDef == toGive.def);

            float unitPrice = tradeable?.GetPriceFor(TradeAction.PlayerSells) ?? toGive.MarketValue;
            int totalSilver = Mathf.RoundToInt(unitPrice * countToGive);

            if (countToGive > 0 && totalSilver > 0)
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = totalSilver;

                IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.MakeDropPodAt(dropSpot, map, new ActiveDropPodInfo
                {
                    SingleContainedThing = silver,
                    leaveSlag = false,
                    openDelay = 60
                });

                Log.Message($"[RimCrypto] Player sold {countToGive}x Coins for {totalSilver} silver (drop pod at {dropSpot})");
            }
            else
            {
                Log.Warning($"[RimCrypto] Tried to sell {countToGive}x RimBitcoin but only removed {countToGive}, and market value was {unitPrice}?");
            }
        }


    }
}
