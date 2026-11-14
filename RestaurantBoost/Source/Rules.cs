using System;
using System.Globalization;

namespace RestaurantBoost
{
    public sealed class Preferences
    {
        public bool EmployeeEnabled;
        public int EmployeeLimit = 20;
        public bool ArrivalEnabled;
        public int ArrivalIntervalUnits = 10;
        public bool EatingEnabled;
        public int EatingMultiplierUnits = 20;
        public bool IncludePrivateRooms;
        public bool TipCapacityEnabled;
        public int TipCapacity = 100000;
        public bool AutoCollectTips;
        public int CollectIntervalSeconds = 60;
        public float ArrivalWindowX = -24f;
        public float ArrivalWindowY = -124f;
    }

    public static class Rules
    {
        public static bool TryUnits(string text, int min, int max, out int units)
        {
            units = 0;
            decimal value;
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out value)) return false;
            if (value < min / 10m || value > max / 10m) return false;
            decimal scaled = value * 10m;
            if (scaled != decimal.Truncate(scaled) || scaled < min || scaled > max) return false;
            units = (int)scaled;
            return true;
        }
        public static bool TryLimit(string text, out int limit)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit >= 1 && limit <= 50;
        }
        public static string Format(int units) { return (units / 10m).ToString("0.0", CultureInfo.InvariantCulture); }
        public static void Normalize(Preferences p)
        {
            if (p.EmployeeLimit < 1 || p.EmployeeLimit > 50) p.EmployeeLimit = 20;
            if (p.ArrivalIntervalUnits < 1 || p.ArrivalIntervalUnits > 100) p.ArrivalIntervalUnits = 10;
            if (p.EatingMultiplierUnits < 10 || p.EatingMultiplierUnits > 200) p.EatingMultiplierUnits = 20;
            if (p.TipCapacity < 2000 || p.TipCapacity > 100000) p.TipCapacity = 100000;
            if (p.CollectIntervalSeconds < 5 || p.CollectIntervalSeconds > 3600) p.CollectIntervalSeconds = 60;
            if (float.IsNaN(p.ArrivalWindowX) || float.IsInfinity(p.ArrivalWindowX)) p.ArrivalWindowX = -24f;
            if (float.IsNaN(p.ArrivalWindowY) || float.IsInfinity(p.ArrivalWindowY)) p.ArrivalWindowY = -124f;
        }
        public static float Arrival(float original, Preferences p)
        {
            return p.ArrivalIntervalUnits / 10f;
        }
        public static float Eating(float original, Preferences p) { return Math.Max(.1f, original * 10f / p.EatingMultiplierUnits); }
        public static bool TryInteger(string text, int min, int max, out int value)
        { return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max; }
        public static int PreservedLoadLimit(int configured, int stored) { return Math.Max(configured, stored); }
        public static bool CanAddTip(int stored, int limit) { return stored < limit; }
    }

    // 只安排下一次检查；做客、离开餐厅和切换存档均重新计时，不补跑遗漏周期。
    public sealed class CollectionClock
    {
        private string context;
        private double deadline;
        private int interval;
        public void Reset() { context = null; deadline = 0; interval = 0; }
        public bool Tick(bool ready, bool enabled, string key, int seconds, double now)
        {
            if (!ready || !enabled) { Reset(); return false; }
            if (context != key || interval != seconds || deadline == 0)
            { context = key; interval = seconds; deadline = now + seconds; return false; }
            if (now < deadline) return false;
            deadline = now + seconds;
            return true;
        }
        public int Remaining(double now) { return deadline == 0 ? 0 : (int)Math.Max(0, Math.Ceiling(deadline - now)); }
    }
}
