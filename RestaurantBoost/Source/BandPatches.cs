using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace RestaurantBoost
{
    internal static class BandRules
    {
        [ThreadStatic] internal static int LoadingStoredMaximum;
        internal static bool OwnContext()
        { return ModBehaviour.Current != null && GameSession.Owner != null && GameSession.IsOwner && !GuestSessionManager.IsInGuestSession(); }
        internal static int TipLimit()
        {
            var mod = ModBehaviour.Current;
            return OwnContext() && mod.Settings.TipCapacityEnabled ? mod.Settings.TipCapacity : GameRule.nBandMaxNum;
        }
        internal static int LoadLimit()
        { return OwnContext() ? Rules.PreservedLoadLimit(TipLimit(), LoadingStoredMaximum) : GameRule.nBandMaxNum; }
        internal static IEnumerable<CodeInstruction> ReplaceLimit(IEnumerable<CodeInstruction> instructions, string method)
        {
            return Patches.Replace(instructions, AccessTools.Field(typeof(GameRule), "nBandMaxNum"), OpCodes.Ldsfld,
                AccessTools.Method(typeof(BandRules), method));
        }
    }
    [HarmonyPatch(typeof(BandManager), "AddTipToMusician")]
    internal static class TipAccumulationPatch
    {
        static bool Prefix(int __0)
        {
            // 已存数量高于新上限时暂停新增，避免原版Min把存量直接截断。
            return !BandRules.OwnContext() || Rules.CanAddTip(BandManager.GetTipNum(__0), BandRules.TipLimit());
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        { return BandRules.ReplaceLimit(instructions, "TipLimit"); }
    }
    [HarmonyPatch(typeof(BandManager), "LoadFromSave")]
    internal static class TipLoadPatch
    {
        static void Prefix(Dictionary<int, int> __1, out int __state)
        {
            __state = BandRules.LoadingStoredMaximum;
            BandRules.LoadingStoredMaximum = __1 == null ? 0 : __1.Where(x => BandManager.IsValidMusicianID(x.Key)).Select(x => Math.Max(0, x.Value)).DefaultIfEmpty(0).Max();
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        { return BandRules.ReplaceLimit(instructions, "LoadLimit"); }
        static Exception Finalizer(Exception __exception, int __state)
        { BandRules.LoadingStoredMaximum = __state; return __exception; }
    }
}
