using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimCryptoEconomy
{
    public class HistoryAutoRecorderWorker_CryptoMarketCap : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            int day = Find.TickManager.TicksAbs / 60000;
            return CryptoEconomyManager.Instance?.CalculateTotalMarketCap(day) ?? 0f;
        }
    }
    public class HistoryAutoRecorderWorker_RimCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("RimCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_ThrumboCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("ThrumboCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_BoomalopeCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("BoomalopeCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_GlitterCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("GlitterCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_LuciferiumCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("LuciferiumCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_MuffaloCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("MuffaloCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_PoplarCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("PoplarCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_RaidersCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("RaidersCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_FalloutCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("FalloutCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }
    public class HistoryAutoRecorderWorker_RitualCoin : HistoryAutoRecorderWorker
    {
        public override float PullRecord()
        {
            if (CryptoEconomyManager.Instance?.Coins.TryGetValue("RitualCoin", out var coin) == true)
                return coin.MarketCap;

            return 0f;
        }
    }



    [HarmonyPatch(typeof(MainTabWindow_History), "DoGraphPage")]
    public static class Patch_History_SetUnreleasedCoinTransparency
    {
        static void Prefix(MainTabWindow_History __instance)
        {
            var group = Traverse.Create(__instance).Field("historyAutoRecorderGroup").GetValue<HistoryAutoRecorderGroup>();
            if (group == null || group.recorders == null) return;

            group.recorders.Sort((a, b) =>
            {
                bool aIsCoin = a.def.defName.EndsWith("Coin");
                bool bIsCoin = b.def.defName.EndsWith("Coin");

                // ⬆️ Push non-coins to the top (keep their original order)
                if (!aIsCoin && !bIsCoin) return 0;
                if (!aIsCoin) return -1;
                if (!bIsCoin) return 1;

                // 🪙 Compare coin market caps
                var coins = CryptoEconomyManager.Instance.Coins;
                coins.TryGetValue(b.def.defName, out var bCoin);
                coins.TryGetValue(a.def.defName, out var aCoin);

                return (bCoin?.MarketCap ?? 0f).CompareTo(aCoin?.MarketCap ?? 0f);
            });

            foreach (var recorder in group.recorders)
            {
                var def = recorder.def;
                if (def == null || !def.defName.EndsWith("Coin")) continue;

                if (CryptoEconomyManager.Instance.Coins.TryGetValue(def.defName, out var coin))
                {
                    def.graphColor = new Color(def.graphColor.r, def.graphColor.g, def.graphColor.b, coin.IsReleased ? 1f : 0f);
                    def.label = coin.IsReleased ? coin.MarketName : "";
                }
            }
        }
    }

}
