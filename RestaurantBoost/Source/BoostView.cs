using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace RestaurantBoost
{
    public sealed class BoostView : MonoBehaviour, IEscHandler
    {
        internal ModBehaviour Owner;
        private GameObject canvasObject;
        private TMP_FontAsset font;
        private Material material;
        private Sprite panelSprite, confirmSprite, cancelSprite;
        private bool registered;
        private bool syncing;
        private TMP_Text status, employeeInfo, arrivalInfo, eatingInfo;
        private Button employeeToggle, arrivalToggle, eatingToggle, privateRooms;
        private TMP_InputField employeeInput, arrivalInput, eatingInput;
        private Slider employeeSlider, arrivalSlider, eatingSlider;
        private RectTransform efficiencyPage, bandPage;
        private Button efficiencyTab, bandTab, tipCapacityToggle, collectToggle;
        private TMP_InputField tipCapacityInput, collectInput;
        private Slider tipCapacitySlider, collectSlider;
        private TMP_Text tipInfo, collectInfo, collectionResult, footer;
        private bool showingBand;
        internal bool FloatingArrivalInteractive
        {
            get { return floatingArrival != null && floatingArrival.gameObject.activeInHierarchy
                && (mainRoot == null || !mainRoot.gameObject.activeSelf); }
        }
        private RectTransform mainRoot, floatingArrival;
        private Button floatingArrivalToggle;
        private Slider floatingArrivalSlider;
        private TMP_InputField floatingArrivalInput;
        private TMP_Text floatingArrivalInfo;
        private readonly Color ink = new Color32(83, 58, 40, 255);
        private readonly Color muted = new Color32(126, 104, 81, 255);
        internal void Open()
        {
            if (!Owner.Ready()) return;
            if (canvasObject == null) Build();
            canvasObject.SetActive(true);
            mainRoot.gameObject.SetActive(true);
            mainRoot.SetAsLastSibling();
            UpdateVisibility();
            Sync();
        }
        internal void Close()
        {
            if (mainRoot != null) mainRoot.gameObject.SetActive(false);
            UpdateVisibility();
            if (Owner != null) Owner.Save();
        }
        internal void CloseAll()
        {
            if (mainRoot != null) mainRoot.gameObject.SetActive(false);
            if (floatingArrival != null) floatingArrival.gameObject.SetActive(false);
            UpdateVisibility();
            if (Owner != null) Owner.Save();
        }
        private void CloseFloatingArrival()
        {
            if (floatingArrival != null) floatingArrival.gameObject.SetActive(false);
            UpdateVisibility();
            if (Owner != null) Owner.Save();
        }
        private void UpdateVisibility()
        {
            bool visible = (mainRoot != null && mainRoot.gameObject.activeSelf)
                || (floatingArrival != null && floatingArrival.gameObject.activeSelf);
            if (canvasObject != null) canvasObject.SetActive(visible);
            if (visible && !registered) { UIManager.RegisterEscHandler(this); registered = true; }
            else if (!visible && registered) { UIManager.UnregisterEscHandler(this); registered = false; }
        }
        public bool OnEscapePressed()
        {
            if (mainRoot != null && mainRoot.gameObject.activeSelf) Close(); else CloseFloatingArrival();
            return true;
        }
        internal void Shutdown()
        {
            CloseAll();
            if (canvasObject != null) Destroy(canvasObject);
            if (material != null) Destroy(material);
        }
        private void OnDestroy() { Shutdown(); }
        internal void Tick()
        {
            if (canvasObject == null || !canvasObject.activeSelf) return;
            if (!Owner.Ready()) CloseAll(); else { ClampFloatingArrival(); UpdateInfo(); }
        }
        private void Build()
        {
            font = BodyTypography.SelectFont(Resources.FindObjectsOfTypeAll<TMP_FontAsset>());
            material = BodyTypography.CreateMaterial(font);
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            panelSprite = sprites.FirstOrDefault(s => s.name == "Tooltip_Bg");
            confirmSprite = sprites.FirstOrDefault(s => s.name == "Btn_Confirm");
            cancelSprite = sprites.FirstOrDefault(s => s.name == "Btn_Cancel");
            ModBehaviour.Log("界面使用普通中文字体及独立干净材质：" + font.name);
            canvasObject = new GameObject("餐厅效率助手界面", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 3050;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var shade = Rect("背景遮罩", canvasObject.transform); Stretch(shade);
            mainRoot = shade;
            var shadeImage = Image(shade, new Color(0, 0, 0, .48f));
            var shadeButton = shade.gameObject.AddComponent<Button>(); shadeButton.targetGraphic = shadeImage;
            shadeButton.transition = Selectable.Transition.None; shadeButton.onClick.AddListener(Close);
            var panel = Box("设置面板", shade, 0, 0, 1020, 950, new Color32(255, 248, 231, 255));
            SetSprite(panel.GetComponent<Image>(), panelSprite, new Color32(255, 248, 231, 255));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            var block = panel.gameObject.AddComponent<Button>(); block.targetGraphic = panel.GetComponent<Image>(); block.transition = Selectable.Transition.None;
            Label(panel, "餐厅效率助手", 32, 22, 750, 48, 34, ink);
            Label(panel, "版本 1.0.0  ·  开关与数值自动保存", 34, 76, 770, 32, 20, muted);
            ActionButton(panel, "关闭", 872, 28, 116, 48, Close);
            efficiencyTab = ActionButton(panel, "经营效率", 28, 120, 226, 44, delegate { ShowPage(false); });
            bandTab = ActionButton(panel, "乐队小费", 270, 120, 226, 44, delegate { ShowPage(true); });
            efficiencyPage = Rect("经营效率页", panel); Place(efficiencyPage, 0, 52, 1020, 740);

            var employee = Box("雇员上限", efficiencyPage, 28, 126, 964, 180, Color.clear);
            Label(employee, "雇员总上限", 20, 12, 350, 36, 27, ink);
            employeeToggle = ActionButton(employee, "", 784, 12, 160, 40, delegate { Owner.Toggle(0); });
            employeeSlider = MakeSlider(employee, 22, 66, 512, 1, 50, delegate(float value) { CommitLimit(Mathf.RoundToInt(value).ToString()); });
            employeeInput = Input(employee, 552, 58, 96, "20", CommitLimit);
            Label(employee, "人（不含保安）", 660, 63, 270, 36, 21, ink);
            for (int i = 0; i < 5; i++)
            {
                int count = (i + 1) * 10;
                ActionButton(employee, count + "人", 22 + i * 102, 118, 90, 36, delegate { CommitLimit(count.ToString()); });
            }
            employeeInfo = Label(employee, "", 550, 112, 394, 54, 18, muted);

            var arrival = Box("顾客到店", efficiencyPage, 28, 320, 964, 222, Color.clear);
            Label(arrival, "顾客到店速度", 20, 12, 500, 36, 27, ink);
            arrivalToggle = ActionButton(arrival, "", 784, 12, 160, 40, delegate { Owner.Toggle(1); });
            Label(arrival, "到店间隔（秒/人）", 22, 62, 226, 40, 21, ink);
            arrivalSlider = MakeSlider(arrival, 270, 68, 384, 1, 100, delegate(float value) { CommitArrival(Rules.Format(Mathf.RoundToInt(value))); });
            arrivalInput = Input(arrival, 682, 58, 120, "1.0", CommitArrival);
            Label(arrival, "秒", 818, 63, 112, 36, 21, ink);
            ActionButton(arrival, "每5秒1人", 22, 118, 170, 38, delegate { SetInterval(50); });
            ActionButton(arrival, "每2秒1人", 204, 118, 170, 38, delegate { SetInterval(20); });
            ActionButton(arrival, "每1秒1人", 386, 118, 170, 38, delegate { SetInterval(10); });
            ActionButton(arrival, "悬浮调整", 590, 118, 196, 38, OpenFloatingArrival);
            Label(arrival, "步长0.1秒", 806, 119, 138, 38, 19, muted);
            arrivalInfo = Label(arrival, "", 22, 169, 922, 42, 19, muted);

            var eating = Box("用餐速度", efficiencyPage, 28, 556, 964, 184, Color.clear);
            Label(eating, "顾客用餐速度", 20, 12, 500, 36, 27, ink);
            eatingToggle = ActionButton(eating, "", 784, 12, 160, 40, delegate { Owner.Toggle(2); });
            eatingSlider = MakeSlider(eating, 22, 68, 512, 10, 200, delegate(float value) { CommitEating(Rules.Format(Mathf.RoundToInt(value))); });
            eatingInput = Input(eating, 552, 58, 112, "2.0", CommitEating);
            Label(eating, "倍（每格0.1）", 678, 63, 260, 36, 21, ink);
            privateRooms = ActionButton(eating, "", 22, 122, 226, 38, delegate
            {
                Owner.Settings.IncludePrivateRooms = !Owner.Settings.IncludePrivateRooms;
                Owner.Changed("包房用餐设置已更新。", false); Sync();
            });
            eatingInfo = Label(eating, "", 270, 118, 674, 54, 19, muted);
            BuildBandPage(panel);
            status = Label(panel, "", 34, 808, 952, 50, 19, ink);
            footer = Label(panel, "", 34, 868, 950, 52, 18, muted);
            ShowPage(false);
        }
        private void BuildBandPage(RectTransform panel)
        {
            bandPage = Rect("乐队小费页", panel); Place(bandPage, 28, 178, 964, 612);
            Label(bandPage, "每位乐手的小费存储上限", 20, 12, 710, 38, 27, ink);
            tipCapacityToggle = ActionButton(bandPage, "", 784, 12, 160, 40, delegate { Owner.Toggle(3); });
            tipCapacitySlider = MakeSlider(bandPage, 22, 78, 512, 2000, 100000, delegate(float value)
            { CommitTipCapacity((Mathf.RoundToInt(value / 100f) * 100).ToString()); });
            tipCapacityInput = Input(bandPage, 552, 68, 170, "100000", CommitTipCapacity);
            Label(bandPage, "笔 / 每位乐手", 738, 73, 206, 36, 20, ink);
            int[] caps = { 2000, 5000, 10000, 100000 };
            for (int i = 0; i < caps.Length; i++)
            {
                int cap = caps[i];
                ActionButton(bandPage, cap == 100000 ? "10万笔" : cap + "笔", 22 + i * 156, 132, 144, 42, delegate { CommitTipCapacity(cap.ToString()); });
            }
            Label(bandPage, "2000—10万笔\n滑块每格100", 672, 128, 272, 54, 18, muted);
            tipInfo = Label(bandPage, "", 22, 190, 922, 66, 20, muted);
            Label(bandPage, "自动收取小费", 20, 282, 700, 38, 27, ink);
            collectToggle = ActionButton(bandPage, "", 784, 282, 160, 40, delegate { Owner.Toggle(4); });
            collectSlider = MakeSlider(bandPage, 22, 350, 512, 5, 3600, delegate(float value)
            { CommitCollectInterval(Mathf.RoundToInt(value).ToString()); });
            collectInput = Input(bandPage, 552, 340, 170, "60", CommitCollectInterval);
            Label(bandPage, "秒尝试一次", 738, 345, 206, 36, 20, ink);
            ActionButton(bandPage, "每隔30秒", 22, 404, 200, 42, delegate { CommitCollectInterval("30"); });
            ActionButton(bandPage, "每隔60秒", 236, 404, 200, 42, delegate { CommitCollectInterval("60"); });
            Label(bandPage, "范围5—3600秒，按真实时间计时", 466, 406, 478, 38, 19, muted);
            collectInfo = Label(bandPage, "", 22, 466, 922, 46, 21, ink);
            collectionResult = Label(bandPage, "", 22, 530, 922, 60, 20, muted);
        }
        private void OpenFloatingArrival()
        {
            if (!Owner.Ready()) return;
            if (floatingArrival == null)
            {
                floatingArrival = Box("顾客到店悬浮窗", canvasObject.transform, 0, 0, 580, 310, Color.white);
                floatingArrival.anchorMin = floatingArrival.anchorMax = floatingArrival.pivot = Vector2.one;
                floatingArrival.anchoredPosition = new Vector2(Owner.Settings.ArrivalWindowX, Owner.Settings.ArrivalWindowY);
                SetSprite(floatingArrival.GetComponent<Image>(), panelSprite, new Color32(255, 248, 231, 255));
                Label(floatingArrival, "顾客到店调整", 22, 12, 420, 38, 27, ink);
                Label(floatingArrival, "拖动标题移动位置", 24, 48, 400, 23, 15, muted);
                var dragArea = Rect("拖动标题栏", floatingArrival); Place(dragArea, 12, 8, 440, 56);
                Image(dragArea, new Color32(255, 248, 231, 255));
                dragArea.SetAsFirstSibling();
                var drag = dragArea.gameObject.AddComponent<ArrivalWindowDrag>();
                drag.Target = floatingArrival; drag.CanvasRect = canvasObject.GetComponent<RectTransform>(); drag.Owner = Owner;
                ActionButton(floatingArrival, "关闭", 470, 14, 88, 38, CloseFloatingArrival);
                Label(floatingArrival, "到店间隔（秒/人）", 22, 76, 360, 38, 22, ink);
                floatingArrivalToggle = ActionButton(floatingArrival, "", 390, 74, 168, 38, delegate { Owner.Toggle(1); });
                floatingArrivalSlider = MakeSlider(floatingArrival, 22, 136, 340, 1, 100,
                    delegate(float value) { CommitArrival(Rules.Format(Mathf.RoundToInt(value))); });
                floatingArrivalInput = Input(floatingArrival, 386, 126, 118, "1.0", CommitArrival);
                Label(floatingArrival, "秒", 516, 130, 40, 38, 21, ink);
                ActionButton(floatingArrival, "每5秒1人", 22, 190, 166, 40, delegate { SetInterval(50); });
                ActionButton(floatingArrival, "每2秒1人", 202, 190, 166, 40, delegate { SetInterval(20); });
                ActionButton(floatingArrival, "每1秒1人", 382, 190, 176, 40, delegate { SetInterval(10); });
                floatingArrivalInfo = Label(floatingArrival, "", 22, 244, 536, 50, 18, muted);
            }
            floatingArrival.gameObject.SetActive(true);
            floatingArrival.SetAsLastSibling();
            ClampFloatingArrival();
            Close();
            UpdateVisibility();
            Sync();
        }
        private void ClampFloatingArrival()
        {
            if (floatingArrival == null || canvasObject == null) return;
            ArrivalWindowDrag.Clamp(floatingArrival, canvasObject.GetComponent<RectTransform>());
        }
        private void ShowPage(bool band)
        {
            showingBand = band;
            efficiencyPage.gameObject.SetActive(!band); bandPage.gameObject.SetActive(band);
            SetSprite(efficiencyTab.GetComponent<Image>(), !band ? confirmSprite : cancelSprite, Color.white);
            SetSprite(bandTab.GetComponent<Image>(), band ? confirmSprite : cancelSprite, Color.white);
            if (footer != null) footer.text = band
                ? "降低容量保留现有小费，超出新上限时暂停新增；卸载前请先收取超出基础上限的小费。"
                : "降低上限不解雇已有雇员；客流仍受入口、空座、库存和原版营业规则限制。";
        }
        private void CommitTipCapacity(string text)
        {
            if (syncing) return;
            int value;
            if (!Rules.TryInteger(text, 2000, 100000, out value))
            { Owner.Status = "未应用：请输入2000—100000的整数笔数。"; Sync(); return; }
            Owner.Settings.TipCapacity = value;
            Owner.Changed("每位乐手的容量已设为 " + value + " 笔；请开启小费容量功能。", false); Sync();
        }
        private void CommitCollectInterval(string text)
        {
            if (syncing) return;
            int value;
            if (!Rules.TryInteger(text, 5, 3600, out value))
            { Owner.Status = "未应用：请输入5—3600的整数秒数。"; Sync(); return; }
            Owner.Settings.CollectIntervalSeconds = value; Owner.CollectionTimer.Reset();
            Owner.Changed("自动收取间隔已设为 " + value + " 秒；开启后从新间隔开始计时。", false); Sync();
        }
        private void SetInterval(int units)
        {
            Owner.Settings.ArrivalIntervalUnits = units;
            Owner.Changed("到店间隔已设为 " + Rules.Format(units) + " 秒；请开启到店功能。", false); Sync();
        }
        private void CommitLimit(string text)
        {
            if (syncing) return;
            int value;
            if (!Rules.TryLimit(text, out value)) { Owner.Status = "未应用：雇员上限必须是1—50的整数。"; Sync(); return; }
            Owner.Settings.EmployeeLimit = value;
            Owner.Changed("雇员上限已设为 " + value + " 人；开启后可在原版人才市场招聘。", true); Sync();
        }
        private void CommitArrival(string text)
        {
            if (syncing) return;
            int units;
            if (!Rules.TryUnits(text, 1, 100, out units))
            { Owner.Status = "未应用：请输入0.1—10.0秒，步长0.1。"; Sync(); return; }
            Owner.Settings.ArrivalIntervalUnits = units;
            Owner.Changed("到店参数已更新，下一轮补客开始生效。", false); Sync();
        }
        private void CommitEating(string text)
        {
            if (syncing) return;
            int units;
            if (!Rules.TryUnits(text, 10, 200, out units)) { Owner.Status = "未应用：请输入1.0—20.0倍，间隔0.1。"; Sync(); return; }
            Owner.Settings.EatingMultiplierUnits = units;
            Owner.Changed("用餐倍率已设为 " + Rules.Format(units) + " 倍，从下一次开始用餐生效。", false); Sync();
        }
        internal void Sync()
        {
            if (canvasObject == null) return;
            syncing = true;
            var p = Owner.Settings;
            SetToggle(employeeToggle, p.EmployeeEnabled); SetToggle(arrivalToggle, p.ArrivalEnabled); SetToggle(eatingToggle, p.EatingEnabled);
            Caption(privateRooms, p.IncludePrivateRooms ? "包房：同步加速" : "包房：保持原版");
            employeeSlider.SetValueWithoutNotify(p.EmployeeLimit);
            employeeInput.SetTextWithoutNotify(p.EmployeeLimit.ToString());
            int arrival = p.ArrivalIntervalUnits;
            arrivalSlider.SetValueWithoutNotify(arrival); arrivalInput.SetTextWithoutNotify(Rules.Format(arrival));
            eatingSlider.SetValueWithoutNotify(p.EatingMultiplierUnits); eatingInput.SetTextWithoutNotify(Rules.Format(p.EatingMultiplierUnits));
            SetToggle(tipCapacityToggle, p.TipCapacityEnabled); SetToggle(collectToggle, p.AutoCollectTips);
            tipCapacitySlider.SetValueWithoutNotify(p.TipCapacity); tipCapacityInput.SetTextWithoutNotify(p.TipCapacity.ToString());
            collectSlider.SetValueWithoutNotify(p.CollectIntervalSeconds); collectInput.SetTextWithoutNotify(p.CollectIntervalSeconds.ToString());
            if (floatingArrival != null)
            {
                SetToggle(floatingArrivalToggle, p.ArrivalEnabled);
                floatingArrivalSlider.SetValueWithoutNotify(arrival);
                floatingArrivalInput.SetTextWithoutNotify(Rules.Format(arrival));
            }
            syncing = false;
            UpdateInfo();
        }
        private void UpdateInfo()
        {
            if (!Owner.Ready()) return;
            var p = Owner.Settings;
            employeeInfo.text = "当前 " + Owner.EmployeeCount() + " 人 / 上限 " + (p.EmployeeEnabled ? p.EmployeeLimit : Owner.BaseLimit()) + " 人\n关闭时按基础上限 " + Owner.BaseLimit() + " 人";
            float baseline = Owner.BaseArrival(), effective = p.ArrivalEnabled ? Rules.Arrival(baseline, p) : baseline;
            arrivalInfo.text = "范围0.1—10.0秒/人；基础 " + baseline.ToString("0.00") + " 秒 → 当前 " + effective.ToString("0.00") + " 秒/人（大厅）";
            if (floatingArrivalInfo != null) floatingArrivalInfo.text = (p.ArrivalEnabled ? "已开启" : "已关闭，使用基础间隔")
                + " · 当前 " + effective.ToString("0.00") + " 秒/人\n设置范围0.1—10.0秒，步长0.1；下轮补客生效。";
            float hall = p.EatingEnabled ? Rules.Eating(GameRule.fEatTimeSec, p) : GameRule.fEatTimeSec;
            float room = p.EatingEnabled && p.IncludePrivateRooms ? Rules.Eating(GameRule.fPalcoEatingTime, p) : GameRule.fPalcoEatingTime;
            eatingInfo.text = "大厅约 " + hall.ToString("0.00") + " 秒；包房约 " + room.ToString("0.00") + " 秒\n上菜后的用餐阶段；不改变移动和结账速度。";
            status.text = Owner.Status;
            long total = 0;
            int maximum = 0;
            foreach (int id in BandManager.GetActiveBandIDs())
            { int count = BandManager.GetTipNum(id); total += count; maximum = Math.Max(maximum, count); }
            tipInfo.text = "当前每位上限 " + BandRules.TipLimit() + " 笔；基础上限 " + GameRule.nBandMaxNum + " 笔\n当前合计存储 " + total + " 笔，单人最多 " + maximum + " 笔。";
            collectInfo.text = p.AutoCollectTips
                ? "自动收取已开启，下一次约 " + Owner.CollectionTimer.Remaining(Time.unscaledTime) + " 秒后尝试。"
                : "自动收取已关闭；原版满 " + GameRule.nBandHarvestThreshold + " 笔才可收取，每笔 " + GameRule.nBandTipGoldPerNum + " 金币。";
            collectionResult.text = Owner.LastCollection + "\n仅在自己的餐厅运行；做客或离开餐厅后重新计时。";
        }
        private void SetToggle(Button b, bool enabled)
        {
            Caption(b, enabled ? "已开启" : "已关闭");
            SetSprite(b.GetComponent<Image>(), enabled ? confirmSprite : cancelSprite,
                enabled ? (Color)new Color32(146, 190, 104, 255) : (Color)new Color32(238, 204, 132, 255));
        }
        private void Caption(Button button, string text) { button.GetComponentInChildren<TMP_Text>().text = text; }
        private Button ActionButton(Transform parent, string caption, float x, float y, float w, float h, Action action)
        {
            var rect = Box("按钮", parent, x, y, w, h, new Color32(238, 204, 132, 255));
            SetSprite(rect.GetComponent<Image>(), cancelSprite, new Color32(238, 204, 132, 255));
            var b = rect.gameObject.AddComponent<Button>(); b.targetGraphic = rect.GetComponent<Image>();
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            b.onClick.AddListener(delegate { action(); });
            var label = Label(rect, caption, 6, 0, w - 12, h, 20, ink); label.alignment = TextAlignmentOptions.Center;
            return b;
        }
        private TMP_InputField Input(Transform parent, float x, float y, float w, string value, UnityAction<string> commit)
        {
            var rect = Box("数值输入", parent, x, y, w, 48, new Color32(255, 253, 245, 255));
            var viewport = Rect("文字范围", rect); Stretch(viewport); viewport.offsetMin = new Vector2(5, 4); viewport.offsetMax = new Vector2(-5, -4);
            viewport.gameObject.AddComponent<RectMask2D>();
            var label = Label(viewport, value, 0, 0, w - 10, 40, 24, ink); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
            var input = rect.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = rect.GetComponent<Image>();
            input.textViewport = viewport; input.textComponent = label; input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard; input.characterLimit = 24; input.richText = false;
            input.customCaretColor = true; input.caretColor = ink; input.caretWidth = 2;
            input.SetTextWithoutNotify(value); input.onEndEdit.AddListener(commit); return input;
        }
        private Slider MakeSlider(Transform parent, float x, float y, float w, int min, int max, UnityAction<float> change)
        {
            var root = Rect("滑块", parent); Place(root, x, y, w, 34);
            Image(root, new Color(1, 1, 1, .005f));
            var bg = Box("滑轨", root, 0, 12, w, 10, new Color32(205, 184, 142, 255));
            bg.GetComponent<Image>().raycastTarget = false;
            var fillArea = Rect("填充范围", root); Stretch(fillArea);
            fillArea.offsetMin = new Vector2(12, 12); fillArea.offsetMax = new Vector2(-12, -12);
            var fill = Rect("当前数值", fillArea); Stretch(fill);
            Image(fill, new Color32(127, 174, 67, 255)).raycastTarget = false;
            var area = Rect("手柄区域", root); Stretch(area); area.offsetMin = new Vector2(12, 0); area.offsetMax = new Vector2(-12, 0);
            var handle = Rect("手柄", area);
            handle.anchorMin = handle.anchorMax = new Vector2(0, .5f);
            handle.pivot = new Vector2(.5f, .5f);
            handle.sizeDelta = new Vector2(26, 34);
            SetSprite(Image(handle, Color.white), confirmSprite, new Color32(146, 190, 104, 255));
            var slider = root.gameObject.AddComponent<Slider>(); slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill;
            handle.sizeDelta = new Vector2(26, 0);
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = true;
            var nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;
            slider.onValueChanged.AddListener(change); return slider;
        }
        private TMP_Text Label(Transform parent, string value, float x, float y, float w, float h, float size, Color color)
        {
            var rect = Rect("文字", parent); Place(rect, x, y, w, h);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); BodyTypography.Apply(label, font, material);
            label.text = value; label.fontSize = size; label.color = color; label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = true; label.raycastTarget = false; return label;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>();
        }
        private static void Place(RectTransform r, float x, float y, float w, float h)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
        private static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = r.offsetMax = Vector2.zero; }
        private static Image Image(RectTransform r, Color color) { var i = r.gameObject.AddComponent<Image>(); i.color = color; return i; }
        private static void SetSprite(Image image, Sprite sprite, Color fallback)
        {
            image.sprite = sprite;
            image.type = sprite == null ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            image.color = sprite == null ? fallback : Color.white;
        }
        private static RectTransform Box(string name, Transform parent, float x, float y, float w, float h, Color color)
        { var r = Rect(name, parent); Place(r, x, y, w, h); Image(r, color); return r; }
    }
}
