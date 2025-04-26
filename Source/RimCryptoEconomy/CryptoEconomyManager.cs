using Verse;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using LudeonTK;

namespace RimCryptoEconomy
{
    public class CryptoEconomyManager : GameComponent
    {
        public Dictionary<string, CryptoCoinData> Coins = new Dictionary<string, CryptoCoinData>();
        private int lastUpdatedDay = -1;

        private string queuedCoinName = null;
        public static CryptoEconomyManager Instance => Verse.Current.Game.components.OfType<CryptoEconomyManager>().FirstOrDefault();

        // Market Curve Variables to make curve as random as possible
        private int baseP;
        private int offsetS;
        private int volatilityV;
        private int frequencyF;
        private int growthA;
        private int minimumM;


        public CryptoEconomyManager(Game game) : base()
        {
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Coins, "Coins", LookMode.Value, LookMode.Deep);

            Scribe_Values.Look(ref lastUpdatedDay, "lastUpdatedDay", -1);
            Scribe_Values.Look(ref baseP, "baseP");
            Scribe_Values.Look(ref offsetS, "offsetS");
            Scribe_Values.Look(ref volatilityV, "volatilityV");
            Scribe_Values.Look(ref frequencyF, "frequencyF");
            Scribe_Values.Look(ref growthA, "growthA");
            Scribe_Values.Look(ref minimumM, "minimumM");


            if (Scribe.mode == LoadSaveMode.PostLoadInit && Coins == null)
                Coins = new Dictionary<string, CryptoCoinData>();
        }
        public override void FinalizeInit()
        {
            base.FinalizeInit();

            queuedCoinName = null;
            if(lastUpdatedDay == -1) 
                lastUpdatedDay = GenTicks.TicksAbs / 60000;

            //Adds all exisiting coins into existance.
            if (Coins == null || Coins.Count == 0)
            {
                Coins = new Dictionary<string, CryptoCoinData>();

                AddCoin("RimCoin", "RimCoin");
                AddCoin("ThrumboCoin", "TurboThrumbo");
                AddCoin("BoomalopeCoin", "BoomalopeChain");
                AddCoin("GlitterCoin", "GlitterGold");
                AddCoin("LuciferiumCoin", "BitLuciferium");
                AddCoin("MuffaloCoin", "MetaMuffalo");
                AddCoin("PoplarCoin", "PoplarCash");
                AddCoin("RaidersCoin", "MiniRaiders");
                AddCoin("RitualCoin", "RitualOS");
                AddCoin("FalloutCoin", "CyberFallout");

            }

            //Variables for random Market Curve
            if (baseP == 0) // ensures only initialized once
            {
                baseP = Rand.Range(2_500_000, 5_000_000);
                offsetS = Rand.Range(0, 100);
                volatilityV = Rand.Range(10, 30);
                frequencyF = Rand.Range(1, 10);
                growthA = Rand.Range(1, 13) * 1000;
                minimumM = Rand.Range(20000, 200000);
            }

            if (Coins.Values.All(c => !c.IsReleased))
            {
                InitialCoinRelease();
            }

            //Set Any Crypto Servers
            foreach (var map in Find.Maps)
            {
                foreach (var building in map.listerBuildings.AllBuildingsColonistOfClass<Building_CryptoServer>())
                {
                    building.loadCoinToMine(); // Call the method to load the coin to mine
                }
            }

            SyncCoinMarketValuesToThingDefs();
        }
        public override void GameComponentTick()
        {
            // 6AM = 6 * 2500 = 15000 ticks into the day
            int currentDay = GenTicks.TicksAbs / 60000;
            int ticksThisDay = GenTicks.TicksAbs % 60000;

            if(currentDay > 0 && ticksThisDay >= 12500 && currentDay > lastUpdatedDay)
            {
                lastUpdatedDay = currentDay;
                UpdateEconomy(currentDay);
            }
        }



        //Initial Calls
        public void InitialCoinRelease()
        {
            // Step 1: Always release RimCoin
            ReleaseCoin("RimCoin");

            // Step 2: Randomly select 2 unreleased coins
            var unreleased = Coins.Keys.Where(c => !Coins[c].IsReleased && c != "RimCoin").ToList();
            unreleased.Shuffle();
            var selected = unreleased.Take(2);

            foreach (var coin in selected)
                ReleaseCoin(coin);

            // Step 3: Calculate starting total market cap
            float totalCap = CalculateTotalMarketCap(GenTicks.TicksAbs / 60000);

            // Step 4: Distribute it among the 5 released coins
            InitialMarketDistribution(totalCap);

            Log.Message("[RimCrypto] Initial market distribution complete.");
        }
        private void InitialMarketDistribution(float totalCap)
        {
            var releasedCoins = Coins.Values.Where(c => c.IsReleased).ToList();
            int count = releasedCoins.Count;
            if (count == 0)
            {
                Log.Warning("[RimCrypto] InitialMarketDistribution called with no released coins.");
                return;
            }

            List<float> caps = SplitTotalMarketCapRandomly(totalCap, count);

            for (int i = 0; i < count; i++)
            {
                var coin = releasedCoins[i];
                float cap = caps[i];
                coin.MarketCap = cap;
                coin.HistoricalPrices.Add(coin.MarketCap);

                Log.Message($"[RimCrypto] {coin} initialized with cap={cap:F0}, supply={coin.CirculatingSupply}, price={coin.CurrentPrice:F2}");
            }
        }



        //Main Methods
        public float CalculateTotalMarketCap(int day)
        {
            float x = day + offsetS;
            float v = volatilityV;
            float sinWave = Mathf.Sin((x / v) + Mathf.Sin((frequencyF * x) / (2f * v)));
            return baseP * sinWave + baseP + (growthA * day) + minimumM;
        }
        public void AddCoin(string name, string marketName)
        {
            if (Coins.ContainsKey(name))
                return;

            Coins[name] = new CryptoCoinData
            {
                defName = name,
                MarketName = marketName,
                IsReleased = false,
                MarketCap = 0f,
                CirculatingSupply = 0f,
                HistoricalPrices = new List<float>()
            };
        }
        public void ReleaseCoin(string name)
        {
            if (!Coins.ContainsKey(name))
            {
                Log.Warning($"[RimCrypto] Attempted to release unknown coin: {name}");
                return;
            }

            var coin = Coins[name];
            if (coin.IsReleased)
                return;

            coin.CirculatingSupply = Rand.Range(1, 31) * 5_000;
            coin.IsReleased = true;
        }
        public void UpdateEconomy(int currentDay)
        {
            // Step 1: Get current and previous total market cap using your curve
            float yesterdayCap = CalculateTotalMarketCap(currentDay - 1);
            float todayCap = CalculateTotalMarketCap(currentDay);
            float difference = todayCap - yesterdayCap;

            // Try releasing a new coin first
            string newCoinToday = TryReleaseDailyCoin(currentDay, ref difference);

            // Step 2: Get all currently released coins
            var releasedCoins = Coins
                .Where(c => c.Value.IsReleased && c.Key != newCoinToday)
                .Select(kv => new { Name = kv.Key, Data = kv.Value })
                .ToList();

            if (releasedCoins.Count == 0)
            {
                Log.Warning("[RimCrypto] UpdateEconomy() called but no released coins found.");
                return;
            }

            // Step 3: Simulate market crashes or pumps (depending on positive/negative delta)
            float adjustedDelta = difference; 

            foreach (var coin in releasedCoins)
            {
                if (difference > 0)
                {
                    // 🟥 Market is up → simulate CRASHES
                    if (Rand.Chance(0.10f))
                    {
                        float lossPercent = Rand.Range(0.05f, 0.30f);
                        float lostValue = coin.Data.MarketCap * lossPercent;

                        coin.Data.MarketCap -= lostValue;
                        adjustedDelta += lostValue;

                        Log.Message($"[RimCrypto] {coin.Name} crashed! Lost {lostValue:F0} market cap.");
                    }
                }
                else
                {
                    // 🟩 Market is down → simulate PUMPS
                    if (Rand.Chance(0.10f))
                    {
                        float gainPercent = Rand.Range(0.05f, 0.30f);
                        float gainedValue = coin.Data.MarketCap * gainPercent;

                        coin.Data.MarketCap += gainedValue;
                        adjustedDelta -= gainedValue;

                        Log.Message($"[RimCrypto] {coin.Name} pumped! Gained {gainedValue:F0} market cap.");
                    }
                }
            }

            // Step 4: Redistribute the adjustedDelta among released coins
            List<float> caps = SplitTotalMarketCapRandomly(Mathf.Abs(adjustedDelta), releasedCoins.Count);

            for (int i = 0; i < releasedCoins.Count; i++)
            {
                var coin = releasedCoins[i];
                float capDelta = caps[i];

                if (difference > 0)
                {
                    // Market grew → increase market cap
                    coin.Data.MarketCap += capDelta;
                }
                else
                {
                    // Market shrank → decrease market cap
                    coin.Data.MarketCap -= capDelta;
                }
            }

            // Step 5 & Step 6: Handle collapsed coins and redistribute debt recursively
            float collapsedDebt = 0f;
            bool anyNegative;

            do
            {
                // Reset the flags for the next iteration
                anyNegative = false;
                List<string> collapsedCoins = new List<string>();

                // Identify coins that have collapsed (market cap <= 0)
                foreach (var coin in releasedCoins)
                {
                    if (coin.Data.MarketCap <= 0f)
                    {
                        collapsedCoins.Add(coin.Name);
                        collapsedDebt += Mathf.Abs(coin.Data.MarketCap);  // Add the absolute value of the collapsed market cap

                        // Reset coin data
                        coin.Data.IsReleased = false;
                        coin.Data.MarketCap = 0f;
                        coin.Data.CirculatingSupply = 0f;
                        coin.Data.HistoricalPrices.Clear();

                        // Destroy all instances of this coin in-world
                        DestroyAllCoinItems(coin.Name);

                        // Notify the player
                        Messages.Message($"{coin.Name} has collapsed! All tokens are now worthless and wiped from existence.", MessageTypeDefOf.NegativeEvent);
                    }
                }

                // Step 6: Redistribute the overflow from collapsed coins
                if (collapsedDebt > 0f)
                {
                    var survivors = releasedCoins
                        .Where(c => !collapsedCoins.Contains(c.Name) && c.Data.IsReleased)
                        .Select(kv => kv.Data)
                        .ToList();

                    if (survivors.Count > 0)
                    {
                        List<float> reductions = SplitTotalMarketCapRandomly(collapsedDebt, survivors.Count);
                        collapsedDebt = 0f;

                        for (int i = 0; i < survivors.Count; i++)
                        {
                            float reductionAmount = reductions[i];
                            float newMarketCap = survivors[i].MarketCap - reductionAmount;

                            // If the market cap goes below zero, adjust and mark for further redistribution
                            if (newMarketCap <= 0)
                            {
                                anyNegative = true;
                            }
                            else
                            {
                                // No issue, just reduce the market cap normally
                                survivors[i].MarketCap = newMarketCap;
                            }
                        }
                    }
                }

                // If any coin went negative, we repeat the process
            } while (anyNegative);  // Continue until no coins go below zero

            // Step 7: Recalculate prices for all surviving released coins
            foreach (var coin in Coins.Values.Where(c => c.IsReleased))
            {
                coin.HistoricalPrices.Add(coin.CurrentPrice);
            }

            SyncCoinMarketValuesToThingDefs();

            Log.Message($"[RimCrypto] Economy updated for day {currentDay}. Δ={difference:F0}, adjustedΔ={adjustedDelta:F0}");
            Messages.Message("Crypto prices updated.", MessageTypeDefOf.NeutralEvent, false);
        }

        //Beta Methods
        private string TryReleaseDailyCoin(int currentDay, ref float difference)
        {
            var unreleased = Coins
                .Where(c => !c.Value.IsReleased)
                .Select(kv => kv.Key)
                .ToList();

            if (!unreleased.Any())
            {
                queuedCoinName = null;
                return null;
            }

            string newCoin = null;

            // Use queued coin if set
            if (queuedCoinName != null && Coins.TryGetValue(queuedCoinName, out var queuedCoin) && !queuedCoin.IsReleased)
            {
                newCoin = queuedCoinName;
                queuedCoinName = null;
            }
            else if (Rand.Chance(1f / 60f))
            {
                newCoin = unreleased.RandomElement();
            }

            if (newCoin == null)
                return null;

            ReleaseCoin(newCoin);

            float totalCap = CalculateTotalMarketCap(currentDay);
            float initialCap = Rand.Range(0.01f, 0.05f) * totalCap;
            difference -= initialCap;

            IntegrateCoinIntoEconomy(newCoin, initialCap);
            Log.Message($"[RimCrypto] {newCoin} entered the market with {initialCap:F0} market cap.");
            return newCoin;
        }
        private void IntegrateCoinIntoEconomy(string name, float cap)
        {
            if (!Coins.TryGetValue(name, out var coin) || !coin.IsReleased || coin.CirculatingSupply == 0f)
                return;

            coin.MarketCap = cap;
            float price = cap / coin.CirculatingSupply;
            coin.HistoricalPrices.Add(price);
        }


        //Dev Methods
        [DebugAction("RimCrypto", "Queue Random Coin", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Debug_QueueRandomCoin()
        {
            CryptoEconomyManager.Instance?.QueueCoinRelease();
            Messages.Message("Random Coin schedule for launch at 6AM.", MessageTypeDefOf.NeutralEvent, false);
        }

        public void QueueCoinRelease()
        {
            var unreleased = Coins
                .Where(c => !c.Value.IsReleased)
                .Select(kv => kv.Key)
                .ToList();

            if (unreleased.Any())
            {
                queuedCoinName = unreleased.RandomElement();
                Log.Message($"[RimCrypto] Queued random coin: {queuedCoinName} for tomorrow.");
            }
            else
            {
                Log.Message("[RimCrypto] No unreleased coins left to queue.");
            }
        }
        public void QueueCoinRelease(string coinName)
        {
            if (!Coins.ContainsKey(coinName))
            {
                Log.Warning($"[RimCrypto] Tried to queue unknown coin: {coinName}");
                return;
            }

            if (Coins[coinName].IsReleased)
            {
                Log.Message($"[RimCrypto] Coin '{coinName}' already released. No action taken.");
                return;
            }

            queuedCoinName = coinName;
            Log.Message($"[RimCrypto] Queued coin: {coinName} for tomorrow.");
        }
        


        //Helper Methods
        private List<float> SplitTotalMarketCapRandomly(float totalCap, int count)
        {
            List<float> cuts = new List<float>();
            for (int i = 0; i < count - 1; i++)
                cuts.Add(Rand.Range(0f, totalCap));

            cuts.Add(0f);
            cuts.Add(totalCap);
            cuts.Sort();

            List<float> splits = new List<float>();
            for (int i = 0; i < cuts.Count - 1; i++)
                splits.Add(cuts[i + 1] - cuts[i]);

            return splits;
        }
        private void DestroyAllCoinItems(string coinName)
        {
            ThingDef coinDef = DefDatabase<ThingDef>.GetNamedSilentFail(coinName);
            if (coinDef == null)
            {
                Log.Warning($"[RimCrypto] Tried to destroy items of unknown coin: {coinName}");
                return;
            }

            foreach (Map map in Find.Maps)
            {
                var thingsToDestroy = map.listerThings.AllThings
                    .Where(t => t.def == coinDef)
                    .ToList();

                //Destroy Coins on Map 
                foreach (Thing thing in thingsToDestroy)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }

                //Reset Servers from mining the Crashed Crypto
                foreach (var building in map.listerBuildings.AllBuildingsColonistOfClass<Building_CryptoServer>())
                {
                    if (building.CoinToMine.defName == coinName)
                        building.setCoinToMine(null);
                }
            }

           
        }
        public void SyncCoinMarketValuesToThingDefs()
        {
            if (Coins == null)
            {
                Log.Warning("[RimCrypto] Tried to sync coin prices, but Coins dictionary is null!");
                return;
            }

            foreach (var kv in Coins)
            {
                if (!kv.Value.IsReleased)
                    continue;

                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);
                if (def == null)
                {
                    Log.Warning($"[RimCrypto] Coin {kv.Key} is released but has no ThingDef!");
                    continue;
                }

                def.BaseMarketValue = kv.Value.CurrentPrice;
            }
        }
        
    }

    public class CryptoCoinData : IExposable
    {
        public string defName;
        public string MarketName;
        public float MarketCap = 0f;
        public float CirculatingSupply = 0f;
        public List<float> HistoricalPrices = new List<float>();
        public bool IsReleased = false;

        public float TotalCoinsBought = 0f;
        public float TotalSpentOnPurchases = 0f;

        public float CurrentPrice =>
            (CirculatingSupply > 0f) ? MarketCap / CirculatingSupply : 0f;

        public float AverageBuyPrice =>
            (TotalCoinsBought > 0f) ? TotalSpentOnPurchases / TotalCoinsBought : 0f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref MarketName, "MarketName");
            Scribe_Values.Look(ref MarketCap, "MarketCap");
            Scribe_Values.Look(ref CirculatingSupply, "CirculatingSupply");
            Scribe_Values.Look(ref IsReleased, "IsReleased");
            Scribe_Collections.Look(ref HistoricalPrices, "HistoricalPrices", LookMode.Value);

            Scribe_Values.Look(ref TotalCoinsBought, "TotalCoinsBought", 0f);
            Scribe_Values.Look(ref TotalSpentOnPurchases, "TotalSpentOnPurchases", 0f);
        }
    }



}
