using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

[assembly: AssemblyTitle("时光餐厅：餐厅效率助手")]
[assembly: AssemblyDescription("自定义雇员上限、顾客到店间隔、用餐速度及乐队小费管理")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace RestaurantBoost
{
    public sealed class ModBehaviour : global::ModBehaviour
    {
        internal static ModBehaviour Current;
        internal Preferences Settings = new Preferences();
        internal BoostView View;
        internal string Status = "各项功能独立控制，设置自动保存。";
        internal int ArrivalChecks;
        internal int MealsStarted;
        internal string LastCollection = "尚未自动收取。";
        internal readonly CollectionClock CollectionTimer = new CollectionClock();
        private Harmony harmony;
        private string settingsPath;
        private bool stopped;
        private float nextTick;
        private bool pendingSave;
        private float saveAt;
        private GameObject entry;
        private Button entryButton;
        private Texture2D texture;
        private Sprite icon;
        private MenuGroupUI mountedMenu;
        private static readonly FieldInfo RestaurantButton = AccessTools.Field(typeof(MenuGroupUI), "m_btnRestaurant");

        protected override void OnAfterSetup()
        {
            if (Current != null) throw new InvalidOperationException("餐厅效率助手已加载，更新后请重启游戏。");
            if (RestaurantButton == null || Patches.SpawnMethod == null || Patches.EmployeeField == null || Patches.EatField == null)
                throw new InvalidOperationException("游戏接口不兼容，无法加载餐厅效率助手。");
            settingsPath = Path.Combine(Path.GetDirectoryName(GameWorld.GetCurrentSavePath()), "RestaurantBoost", "settings.json");
            Load();
            Current = this;
            harmony = new Harmony("SiH.RestaurantBoost.0.1");
            try
            {
                harmony.PatchAll(typeof(ModBehaviour).Assembly);
                View = gameObject.AddComponent<BoostView>();
                View.Owner = this;
                EventManager.Register("ENTER_MAIN_PANEL", OnLeave);
                EventManager.Register("GUEST_MODE_CHANGE", OnGuest);
                Log("1.0.0加载成功；雇员=" + Settings.EmployeeEnabled + "，到店=" + Settings.ArrivalEnabled + "，用餐=" + Settings.EatingEnabled
                    + "，小费容量=" + Settings.TipCapacityEnabled + "/" + Settings.TipCapacity + "，自动收取=" + Settings.AutoCollectTips + "/" + Settings.CollectIntervalSeconds + "秒");
            }
            catch { harmony.UnpatchAll(harmony.Id); Current = null; throw; }
        }
        internal bool ActiveForOwnRestaurant()
        {
            return !stopped && GameWorld.Instance != null && GameWorld.g_SQLiteSave != null
                && GameWorld.g_SQLiteSave.IsInitialized && !GameWorld.IsLoadingScene()
                && !GameWorld.IsExiting && !GameWorld.IsAppQuitting && !GuestSessionManager.IsInGuestSession();
        }
        internal bool Ready()
        { return ActiveForOwnRestaurant() && mountedMenu != null && mountedMenu.gameObject.activeInHierarchy; }
        private void Update()
        {
            if (stopped) return;
            if (pendingSave && Time.unscaledTime >= saveAt) Save();
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            try
            {
                MountEntry();
                if (entryButton != null) entryButton.interactable = Ready();
                TickCollection();
                if (View != null) View.Tick();
            }
            catch (Exception ex) { Status = "界面暂未就绪：" + ex.Message; Log(ex.ToString()); nextTick += 4f; }
        }
        private void MountEntry()
        {
            if (entry == null || mountedMenu == null)
            {
                mountedMenu = Resources.FindObjectsOfTypeAll<MenuGroupUI>().FirstOrDefault(x => x.gameObject.scene.IsValid());
                if (mountedMenu == null) return;
                var original = RestaurantButton.GetValue(mountedMenu) as Button;
                if (original == null) return;
                if (icon == null)
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Path.Combine(Info.szPath, "icon.png")), true))
                        throw new InvalidDataException("图标读取失败，请重新安装完整模组包。");
                    texture.filterMode = FilterMode.Bilinear;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
                }
                entry = Instantiate(original.gameObject, original.transform.parent, false);
                entry.name = "RestaurantBoost_Entry";
                // 沿用餐厅管家的挂载方式：找到已有模组入口所在区段的空位。
                // 新入口加入时由原版布局一起排开；仅插入一次，不固定左右位置或持续重排。
                int index = original.transform.GetSiblingIndex();
                var nativeButtons = new System.Collections.Generic.HashSet<Button>();
                foreach (string fieldName in new[] { "m_btnRestaurant", "m_btnFarm", "m_btnRanch", "m_btnMarket", "m_btnWorkshop" })
                {
                    var field = AccessTools.Field(typeof(MenuGroupUI), fieldName);
                    var native = field == null ? null : field.GetValue(mountedMenu) as Button;
                    if (native != null) nativeButtons.Add(native);
                }
                while (index > 0)
                {
                    var previous = original.transform.parent.GetChild(index - 1).GetComponent<Button>();
                    if (previous == null || nativeButtons.Contains(previous)) break;
                    index--;
                }
                entry.transform.SetSiblingIndex(index);
                entryButton = entry.GetComponent<Button>();
                entryButton.onClick = new Button.ButtonClickedEvent();
                entryButton.onClick.AddListener(delegate { if (Ready()) View.Open(); });
                var image = entry.GetComponent<Image>();
                image.sprite = icon; image.overrideSprite = null; image.type = Image.Type.Simple; image.preserveAspect = true;
                var cursor = entry.GetComponent<UICursorHandler>();
                if (cursor != null) Destroy(cursor);
                var tip = entry.GetComponent<TooltipTrigger>();
                if (tip != null) tip.SetTextCallback(delegate(string a, string b) { return "餐厅效率助手\n雇员上限 · 顾客到店 · 用餐速度 · 乐队小费"; }, "", false);
                LayoutRebuilder.MarkLayoutForRebuild(mountedMenu.transform as RectTransform);
                Log("已加入右上角模组入口区，按各模组挂载顺序自动排布。");
            }
        }
        internal float BaseArrival()
        {
            float speed = PlayerLevelConstDataList.GetCustomerSpeed(Player.m_nLevel);
            return speed > 0 ? 3600f / speed : 5f;
        }
        internal int BaseLimit()
        {
            var row = PlayerLevelConstDataList.GetConstData(Player.m_nLevel);
            return row == null ? 0 : row.nMaxEmployee;
        }
        internal int EmployeeCount()
        { return Restaurant.m_EmployeeData.GetNormalEmployeeCount() - Restaurant.m_EmployeeData.GetGuardCount(); }
        private void TickCollection()
        {
            bool ready = Ready() && GameSession.IsOwner;
            string key = ready ? GameWorld.GetCurrentSavePath() + "|" + Player.m_szPlayerId + "|" + Player.m_nStartGameTime : "";
            if (CollectionTimer.Tick(ready, Settings.AutoCollectTips, key, Settings.CollectIntervalSeconds, Time.unscaledTime)) CollectTips();
        }
        internal void CollectTips()
        {
            if (!Ready() || !GameSession.IsOwner) return;
            try
            {
                long gold = BandManager.HarvestAll();
                if (gold > 0) Restaurant.SaveImmediate();
                LastCollection = DateTime.UtcNow.AddHours(8).ToString("HH:mm:ss") + (gold > 0 ? " 原版结算收取 " + gold + " 金币。" : " 已尝试，暂无达到原版门槛的小费。");
                Log("自动收取乐队小费：" + LastCollection);
            }
            catch (Exception ex)
            {
                Settings.AutoCollectTips = false;
                CollectionTimer.Reset();
                Status = "自动收取已停止：" + ex.Message;
                Save(); Log(ex.ToString());
            }
        }
        internal void Changed(string message, bool refreshHire)
        {
            Status = message;
            pendingSave = true;
            saveAt = Time.unscaledTime + .35f;
            if (refreshHire) Patches.RefreshHireUI();
        }
        internal void Toggle(int feature)
        {
            if (!Ready()) return;
            if (feature == 0) Settings.EmployeeEnabled = !Settings.EmployeeEnabled;
            if (feature == 1) { Settings.ArrivalEnabled = !Settings.ArrivalEnabled; ArrivalChecks = 0; }
            if (feature == 2) { Settings.EatingEnabled = !Settings.EatingEnabled; MealsStarted = 0; }
            if (feature == 3) Settings.TipCapacityEnabled = !Settings.TipCapacityEnabled;
            if (feature == 4) { Settings.AutoCollectTips = !Settings.AutoCollectTips; CollectionTimer.Reset(); }
            string message = feature == 3 ? "小费容量设置已应用，已存小费保持保留。"
                : feature == 4 ? (Settings.AutoCollectTips ? "自动收取已开启，每隔 " + Settings.CollectIntervalSeconds + " 秒尝试一次。" : "自动收取已关闭。")
                : "设置已应用。正在等待中的顾客从下一轮开始使用新设置。";
            Changed(message, feature == 0);
            Save();
            View.Sync();
            Log("设置变化：雇员=" + Settings.EmployeeEnabled + "/" + Settings.EmployeeLimit + "，到店=" + Settings.ArrivalEnabled
                + "/" + Rules.Format(Settings.ArrivalIntervalUnits) + "秒"
                + "，用餐=" + Settings.EatingEnabled + "/" + Rules.Format(Settings.EatingMultiplierUnits) + "倍"
                + "，小费容量=" + Settings.TipCapacityEnabled + "/" + Settings.TipCapacity + "，自动收取=" + Settings.AutoCollectTips + "/" + Settings.CollectIntervalSeconds + "秒");
        }
        private void Load()
        {
            try { if (File.Exists(settingsPath)) Settings = JsonConvert.DeserializeObject<Preferences>(File.ReadAllText(settingsPath, Encoding.UTF8)) ?? new Preferences(); }
            catch (Exception ex) { Log("设置读取失败，使用初装默认值：" + ex.Message); Settings = new Preferences(); }
            Rules.Normalize(Settings);
        }
        internal void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                string temp = settingsPath + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(Settings, Formatting.Indented), new UTF8Encoding(false));
                if (File.Exists(settingsPath)) File.Replace(temp, settingsPath, settingsPath + ".bak");
                else File.Move(temp, settingsPath);
                pendingSave = false;
            }
            catch (Exception ex) { pendingSave = false; Status = "设置生效，但保存失败：" + ex.Message; Log(Status); }
        }
        private void OnLeave() { CollectionTimer.Reset(); if (View != null) View.CloseAll(); Save(); }
        private void OnGuest(object value) { CollectionTimer.Reset(); if (View != null) View.CloseAll(); }
        protected override void OnBeforeDeactivate()
        {
            if (stopped) return;
            Save(); stopped = true;
            if (harmony != null) harmony.UnpatchAll(harmony.Id);
            EventManager.Unregister("ENTER_MAIN_PANEL", OnLeave);
            EventManager.Unregister("GUEST_MODE_CHANGE", OnGuest);
            if (View != null) { View.Shutdown(); Destroy(View); }
            if (entry != null) Destroy(entry);
            if (icon != null) Destroy(icon);
            if (texture != null) Destroy(texture);
            if (Current == this) Current = null;
            Patches.RefreshHireUI();
        }
        private void OnDestroy() { if (Current == this && !stopped) OnBeforeDeactivate(); }
        internal static void Log(string text) { Debug.Log("[餐厅效率助手] " + text); }
    }
}
