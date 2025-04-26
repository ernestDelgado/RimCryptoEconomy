using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimCryptoEconomy
{
    public class CryptoPatches : Mod
    {
        public CryptoPatches(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("yaboie88.rimcryptoeconomy");
            harmony.PatchAll();
        }
    }

    // RimCrypto trader comms target
    public class CryptoCommTarget : ICommunicable
    {
        public void TryOpenComms(Pawn negotiator)
        {
            if (negotiator.skills == null || negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
            {
                Messages.Message("This colonist cannot trade with RimCrypto (incapable of Social).", MessageTypeDefOf.RejectInput, false);
                return;
            }

            SoundDefOf.CommsWindow_Open.PlayOneShotOnCamera();

            var traderKind = DefDatabase<TraderKindDef>.GetNamed("RimCrypto_Trader", errorOnFail: false);
            if (traderKind == null)
            {
                Log.Error("[RimCrypto] TraderKindDef 'RimCrypto_Trader' not found.");
                return;
            }

            var map = negotiator?.Map ?? Find.CurrentMap;
            if (map == null)
            {
                Log.Error("[RimCrypto] Failed to get a valid map for CryptoTrader.");
                return;
            }

            var trader = new CryptoTrader();
            Find.WindowStack.Add(new Dialog_Trade(negotiator, trader));
        }

        public string GetCallLabel() => "Open RimCrypto Exchange";

        public string GetInfoText() =>
            "A mysterious decentralized trader offering high-risk, high-reward crypto assets.";

        public Texture2D CommsIcon => ContentFinder<Texture2D>.Get("UI/RimCoin", true);

        public Faction GetFaction() => Faction.OfPlayer;

        public FloatMenuOption CommFloatMenuOption(Building_CommsConsole console, Pawn negotiator)
        {
            return new FloatMenuOption(GetCallLabel(), () =>
            {
                Job job = JobMaker.MakeJob(JobDefOf.UseCommsConsole, console);
                job.commTarget = this;
                negotiator.jobs.TryTakeOrderedJob(job);
            });
        }
    }



    // Inject RimCrypto into comms console targets
    [HarmonyPatch(typeof(Building_CommsConsole), nameof(Building_CommsConsole.GetCommTargets))]
    public static class CryptoCommTargetInjector
    {
        static void Postfix(ref IEnumerable<ICommunicable> __result)
        {
            if (__result == null)
                __result = Enumerable.Empty<ICommunicable>();

            List<ICommunicable> list = __result.ToList();
            list.Add(new CryptoCommTarget());

            __result = list;
        }
    }



    // Custom pricing for crypto coins based on BaseMarketValue
    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
    public static class Patch_Tradeable_GetPriceFor_Crypto
    {
        static bool Prefix(Tradeable __instance, TradeAction action, ref float __result)
        {
            if (__instance.ThingDef?.thingCategories != null &&
                __instance.ThingDef.thingCategories.Contains(ThingCategoryDef.Named("Crypto")))
            {
                __result = __instance.BaseMarketValue;
                return false; // Skip vanilla pricing
            }

            return true; // Use default logic otherwise
        }
    }


    // Display Daily Price difference AND Player's Average Buy Price
    [HarmonyPatch(typeof(TradeUI), "DrawPrice")]
    public static class Patch_TradeUI_DrawPrice_Crypto
    {
        static bool Prefix(Rect rect, Tradeable trad, TradeAction action)
        {
            if (trad?.ThingDef?.thingCategories == null ||
                !trad.ThingDef.thingCategories.Contains(ThingCategoryDef.Named("Crypto")))
                return true;

            float price = trad.GetPriceFor(action);

            // Draw base price (center area)
            Rect priceRect = rect.LeftPartPixels(rect.width);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = TradeUI.NoTradeColor;
            Widgets.Label(priceRect, price.ToString("F2"));
            Text.Anchor = TextAnchor.MiddleRight;

            // Additional info (right side)
            Rect infoRect = rect.RightPartPixels(60f);
            string tooltip = "";

            if (CryptoEconomyManager.Instance?.Coins.TryGetValue(trad.ThingDef.defName, out var coin) == true)
            {
                if (action == TradeAction.PlayerBuys && coin.HistoricalPrices.Count >= 2)
                {
                    float prev = coin.HistoricalPrices[coin.HistoricalPrices.Count - 2];
                    float delta = coin.CurrentPrice - prev;
                    string deltaText = delta >= 0 ? $"+{delta:F2}" : $"{delta:F2}";

                    GUI.color = delta >= 0 ? Color.green : Color.red;
                    Widgets.Label(infoRect, deltaText);

                    tooltip = $"Daily Price Change\n\nMarket Cap: {Mathf.RoundToInt(coin.MarketCap):N0}\nCirculating Supply: {Mathf.RoundToInt(coin.CirculatingSupply):N0}";
                }
                else if (action == TradeAction.PlayerSells && coin.AverageBuyPrice > 0f)
                {
                    string avgText = $"{coin.AverageBuyPrice:F2}";
                    if (coin.AverageBuyPrice > price) GUI.color = new Color(0.5f, 0.0f, 0.0f);
                    else GUI.color = new Color(0.0f, 0.5f, 0.0f);
                    Widgets.Label(infoRect, avgText);

                    tooltip = $"Average Price Paid\n\nYou paid an average of {coin.AverageBuyPrice:F2} for this coin.";
                }
            }

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(infoRect, tooltip);

            // Reset
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            return false;
        }
    }




}
