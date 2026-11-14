using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace RestaurantBoost
{
    internal static class Patches
    {
        internal static readonly FieldInfo EmployeeField = AccessTools.Field(typeof(PlayerLevelConstData), "nMaxEmployee");
        internal static readonly FieldInfo EatField = AccessTools.Field(typeof(GameRule), "fEatTimeSec");
        internal static readonly MethodInfo SpawnMethod = AccessTools.Method(typeof(RestaurantAI), "GetSpawnInterval");
        internal static MethodInfo DineMoveNext()
        {
            var candidates = typeof(CustomerState_Dine).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .Where(t => t.Name.StartsWith("<OnExecute>", StringComparison.Ordinal)).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("用餐状态机接口已变化。");
            var method = AccessTools.Method(candidates[0], "MoveNext");
            if (method == null) throw new InvalidOperationException("找不到用餐状态机。");
            return method;
        }
        internal static int EffectiveLimit(PlayerLevelConstData row)
        {
            var mod = ModBehaviour.Current;
            return mod != null && mod.ActiveForOwnRestaurant() && mod.Settings.EmployeeEnabled
                ? mod.Settings.EmployeeLimit : row.nMaxEmployee;
        }
        internal static float HallEatingSeconds()
        {
            var mod = ModBehaviour.Current;
            float original = GameRule.fEatTimeSec;
            if (mod == null || !mod.ActiveForOwnRestaurant() || !mod.Settings.EatingEnabled) return original;
            mod.MealsStarted++;
            if (mod.MealsStarted == 1) ModBehaviour.Log("大厅用餐补丁实际执行：" + original + "秒 → " + Rules.Eating(original, mod.Settings) + "秒。");
            return Rules.Eating(original, mod.Settings);
        }
        internal static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> codes, FieldInfo field,
            OpCode oldOpcode, MethodInfo replacement)
        {
            var list = codes.ToList();
            int matches = 0;
            foreach (var code in list)
                if (code.opcode == oldOpcode && Equals(code.operand, field))
                { code.opcode = OpCodes.Call; code.operand = replacement; matches++; }
            if (matches != 1) throw new InvalidOperationException("游戏接口校验失败，停止加载：预期一个字段读取，实际为 " + matches);
            return list;
        }
        internal static void RefreshHireUI()
        {
            foreach (var panel in UnityEngine.Resources.FindObjectsOfTypeAll<DockPanel>())
                if (panel.gameObject.scene.IsValid() && panel.gameObject.activeInHierarchy)
                    AccessTools.Method(typeof(DockPanel), "RefreshHireCountText").Invoke(panel, null);
            foreach (var panel in UnityEngine.Resources.FindObjectsOfTypeAll<EmployeeMarketPanel>())
                if (panel.gameObject.scene.IsValid() && panel.gameObject.activeInHierarchy)
                    AccessTools.Method(typeof(EmployeeMarketPanel), "RefreshCountUI").Invoke(panel, null);
        }
    }
    [HarmonyPatch]
    internal static class EmployeeLimitPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DockPanel), "RefreshHireCountText");
            yield return AccessTools.Method(typeof(EmployeeMarketPanel), "RefreshCountUI");
            yield return AccessTools.Method(typeof(EmployeeMarketPanel), "TrySelectJob");
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        { return Patches.Replace(instructions, Patches.EmployeeField, OpCodes.Ldfld, AccessTools.Method(typeof(Patches), "EffectiveLimit")); }
    }
    [HarmonyPatch(typeof(RestaurantAI), "GetSpawnInterval")]
    internal static class ArrivalPatch
    {
        static void Postfix(ref float __result)
        {
            var mod = ModBehaviour.Current;
            if (mod == null || !mod.ActiveForOwnRestaurant() || !mod.Settings.ArrivalEnabled) return;
            float original = __result;
            __result = Rules.Arrival(__result, mod.Settings);
            mod.ArrivalChecks++;
            if (mod.ArrivalChecks == 1) ModBehaviour.Log("到店补丁实际执行：" + original + "秒 → " + __result + "秒。");
        }
    }
    [HarmonyPatch]
    internal static class HallEatingPatch
    {
        static MethodBase TargetMethod() { return Patches.DineMoveNext(); }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        { return Patches.Replace(instructions, Patches.EatField, OpCodes.Ldsfld, AccessTools.Method(typeof(Patches), "HallEatingSeconds")); }
    }
    [HarmonyPatch(typeof(PalcoConfig), "get_EATING_TIME")]
    internal static class PrivateEatingPatch
    {
        static void Postfix(ref float __result)
        {
            var mod = ModBehaviour.Current;
            if (mod != null && mod.ActiveForOwnRestaurant() && mod.Settings.EatingEnabled && mod.Settings.IncludePrivateRooms)
                __result = Rules.Eating(__result, mod.Settings);
        }
    }
    [HarmonyPatch(typeof(RestaurantTimeCalculator), "get_AS_EAT_TIME_SEC")]
    internal static class EstimatePatch
    {
        static void Postfix(ref float __result)
        {
            var mod = ModBehaviour.Current;
            if (mod != null && mod.ActiveForOwnRestaurant() && mod.Settings.EatingEnabled)
                __result = Rules.Eating(__result, mod.Settings);
        }
    }
    // 显示原版退出确认前关闭本模组，避免独立画布遮住游戏确认框。
    [HarmonyPatch(typeof(GameWorld), "RequestExit")]
    internal static class ExitViewPatch
    {
        static void Prefix()
        {
            var mod = ModBehaviour.Current;
            if (mod != null && mod.View != null) mod.View.CloseAll();
        }
    }
}
