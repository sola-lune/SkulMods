using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Characters.Gear.Items;
using Characters.Player;
using Data;
using HarmonyLib;
using UnityEngine;

namespace DestinyBone
{
    /// <summary>道具栏分页：把 items 列表扩到 27 格，并用“整体轮转”实现翻页显示</summary>
    public static class ItemPages
    {
        public const int SlotsPerPage = 9;
        public const int PageCount = 3;
        public const int TotalSlots = SlotsPerPage * PageCount;

        internal static int Page = 0;

        /// <summary>把背包列表补足到 27 格（新局与读档都会走 Awake）</summary>
        public static void Grow(ItemInventory inv)
        {
            if (inv == null) return;
            while (inv.items.Count < TotalSlots) inv.items.Add(null);
        }

        /// <summary>把存档里 9 长度的四个数组换成 27 长度（键名不变，老数据仍可读）</summary>
        public static void ResizeSave()
        {
            try
            {
                var save = GameData.Save.instance;
                var t = save.GetType();
                foreach (var name in new[] { "items", "itemStacks", "itemKeywords1", "itemKeywords2" })
                {
                    var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null) { Debug.LogWarning("[ItemPages] 找不到存档字段 " + name); continue; }

                    var old = prop.GetValue(save);
                    if (old == null) { Debug.LogWarning("[ItemPages] " + name + " 还没初始化"); continue; }

                    var oldType = old.GetType();
                    var lenField = oldType.GetField("length");
                    var keyField = oldType.GetField("key");
                    if (lenField == null || keyField == null) continue;

                    int oldLen = (int)lenField.GetValue(old);
                    if (oldLen >= TotalSlots) continue;

                    string key = (string)keyField.GetValue(old);
                    object neu = CreateArray(oldType, key, TotalSlots);
                    if (neu == null) { Debug.LogWarning("[ItemPages] 无法重建 " + name); continue; }

                    var setter = prop.GetSetMethod(true);
                    setter.Invoke(save, new[] { neu });
                    Debug.Log("[ItemPages] 存档数组 " + name + " 从 " + oldLen + " 扩到 " + TotalSlots);
                }
            }
            catch (Exception e) { Debug.LogError("[ItemPages] 扩存档失败: " + e); }
        }

        private static object CreateArray(Type type, string key, int len)
        {
            foreach (var ctor in type.GetConstructors())
            {
                var ps = ctor.GetParameters();
                try
                {
                    if (ps.Length == 3) return ctor.Invoke(new object[] { key, len, false });
                    if (ps.Length == 2) return ctor.Invoke(new object[] { key, len });
                }
                catch { }
            }
            return null;
        }

        private static UI.Inventory.Panel _panel;
        private static int _probe;

        internal static UI.Inventory.Panel PanelInstance => _panel;

        public static bool PanelOpen()
        {
            if (_panel == null && (++_probe % 30) != 0) return false;
            if (_panel == null) _panel = UnityEngine.Object.FindObjectOfType<UI.Inventory.Panel>(true);
            return _panel != null && _panel.gameObject.activeInHierarchy;
        }

        /// <summary>轮转一页：dir = -1 上一页 / +1 下一页</summary>
        public static void Turn(int dir)
        {
            var inv = Player()?.playerComponents?.inventory?.item;
            if (inv == null) return;

            int n = ((dir * SlotsPerPage) % TotalSlots + TotalSlots) % TotalSlots;
            Rotate(inv.items, n);

            Page = ((Page + dir) % PageCount + PageCount) % PageCount;

            var panel = UnityEngine.Object.FindObjectOfType<UI.Inventory.Panel>(true);
            if (panel != null && panel.gameObject.activeInHierarchy) panel.UpdateGearInfo();

            Debug.Log("[ItemPages] 翻到第 " + (Page + 1) + " 页");
        }

        private static void Rotate(List<Item> list, int n)
        {
            if (list == null || list.Count == 0) return;
            n %= list.Count;
            if (n == 0) return;
            var copy = new List<Item>(list);
            for (int i = 0; i < list.Count; i++) list[i] = copy[(i + n) % list.Count];
        }

        private static Characters.Character Player()
        {
            try { return Singletons.Singleton<Services.Service>.Instance.levelManager.player; }
            catch { return null; }
        }
    }

    /// <summary>SkulInsight QoL 在平台初始化前强拉 Service，导致 GameData.Initialize 空引用、Steam 初始化失败</summary>
    public static class SkulInsightCompat
    {
        private static bool _done;

        public static bool Prefix()
        {
            // 平台/服务还没就绪时，跳过它的 Update，避免强制创建 Service
            return Services.Service.Instantiated;
        }

        /// <summary>它比我们后加载，只能在运行时补</summary>
        public static void TryPatch(string guid)
        {
            if (_done) return;
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "DamageInsight");
                if (asm == null) return;

                var type = asm.GetType("DamageInsight.Plugin");
                if (type == null) return;

                var target = AccessTools.Method(type, "Update");
                if (target == null) { _done = true; return; }

                var prefix = new HarmonyMethod(typeof(SkulInsightCompat).GetMethod("Prefix"));
                new Harmony(guid).Patch(target, prefix);
                _done = true;
                Debug.Log("[DestinyBone] 已给 SkulInsight 打上防早初始化补丁");
            }
            catch (Exception e) { _done = true; Debug.LogWarning("[DestinyBone] SkulInsight 兼容补丁失败: " + e.Message); }
        }
    }

    [HarmonyPatch]
    public static class SaveInitPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("Data.GameData+Save"), "Initialize");

        [HarmonyPostfix]
        private static void Postfix()
        {
            ItemPages.ResizeSave();
        }
    }

    [HarmonyPatch]
    public static class ItemPagePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemInventory), "Awake")]
        private static void AwakePostfix(ItemInventory __instance)
        {
            ItemPages.Grow(__instance);
            Debug.Log("[ItemPages] 背包已扩到 " + __instance.items.Count + " 格");
        }

        // 拾取前确保列表已扩到 27 格：原版在 TryEquip 失败时会 EquipAt(item, 0) 直接覆盖第 0 格
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Level.DroppedItem), "InteractWith")]
        private static void BeforePickup(Characters.Character character)
        {
            try
            {
                var inv = character == null ? null : character.playerComponents.inventory.item;
                if (inv != null) ItemPages.Grow(inv);
            }
            catch { }
        }

        // 诊断：道具自我销毁的两个入口（原版 Remove(int) 内部会 Destroy）
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Characters.Gear.Items.Item), "RemoveOnInventory")]
        private static void RemoveOnInvLog(Characters.Gear.Items.Item __instance)
        {
            try { Debug.LogWarning("[DestinyBone][diag] Item.RemoveOnInventory " + __instance.name + Environment.StackTrace); }
            catch { }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Characters.Gear.Items.Item), "DiscardOnInventory")]
        private static void DiscardOnInvLog(Characters.Gear.Items.Item __instance)
        {
            try { Debug.LogWarning("[DestinyBone][diag] Item.DiscardOnInventory " + __instance.name + Environment.StackTrace); }
            catch { }
        }

        // 诊断：谁把道具从背包里拿走的
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemInventory), "Remove", new[] { typeof(int) })]
        private static void RemoveLog(ItemInventory __instance, int index)
        {
            try
            {
                var it = index >= 0 && index < __instance.items.Count ? __instance.items[index] : null;
                Debug.LogWarning("[DestinyBone][diag] Remove(idx=" + index + ") 移除 " + (it == null ? "null" : it.name) + Environment.StackTrace);
            }
            catch { }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemInventory), "Drop", new[] { typeof(int) })]
        private static void DropLog(ItemInventory __instance, int index)
        {
            try
            {
                var it = index >= 0 && index < __instance.items.Count ? __instance.items[index] : null;
                Debug.LogWarning("[DestinyBone][diag] Drop(idx=" + index + ") 掉出 " + (it == null ? "null" : it.name)
                                 + (it != null && it.name.StartsWith("Custom-") ? Environment.StackTrace : ""));
            }
            catch { }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemInventory), "Discard", new[] { typeof(Characters.Gear.Items.Item) })]
        private static void DiscardLog(Characters.Gear.Items.Item item)
        {
            try { Debug.LogWarning("[DestinyBone][diag] Discard " + (item == null ? "null" : item.name) + Environment.StackTrace); }
            catch { }
        }

        // 诊断：每次装备进背包都记录一次，用来定位道具被覆盖的路径
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemInventory), "EquipAt")]
        private static void EquipAtLog(ItemInventory __instance, Item item, int index)
        {
            try
            {
                int occ = 0;
                foreach (var it in __instance.items) if (it != null) occ++;
                Debug.Log("[DestinyBone][diag] EquipAt idx=" + index + " item=" + (item == null ? "null" : item.name)
                          + " count=" + __instance.items.Count + " occupied=" + occ);
            }
            catch { }
        }

        // 打开装备栏时刷新到当前页
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UI.Inventory.Panel), "OnEnable")]
        private static void PanelEnable(UI.Inventory.Panel __instance)
        {
            try { __instance.UpdateGearInfo(); } catch { }
        }
    }

    /// <summary>装备栏下方的分页棱形指示：实心 = 当前页，空心 = 其它页</summary>
    public static class PageOverlay
    {
        private static Texture2D _filled, _hollow;

        public static void Draw()
        {
            if (!ItemPages.PanelOpen()) return;
            var panel = ItemPages.PanelInstance;
            if (panel == null) return;

            Rect bounds;
            if (!TryBounds(panel, out bounds)) return;

            if (_filled == null)
            {
                _filled = MakeDiamond(64, true);
                _hollow = MakeDiamond(64, false);
            }

            int size = Mathf.Clamp((int)(Screen.height / 55f), 10, 22);
            float gap = size * 1.7f;
            float total = (ItemPages.PageCount - 1) * gap;
            float cx = bounds.center.x - total / 2f;
            float cy = bounds.yMax + size * 1.2f;

            var oldColor = GUI.color;
            int oldDepth = GUI.depth;
            GUI.depth = -100;

            for (int i = 0; i < ItemPages.PageCount; i++)
            {
                bool current = i == ItemPages.Page;
                GUI.color = current ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                var tex = current ? _filled : _hollow;
                var r = new Rect(cx + i * gap - size / 2f, cy - size / 2f, size, size);
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
            }

            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }

        private static bool TryBounds(UI.Inventory.Panel panel, out Rect bounds)
        {
            bounds = default(Rect);
            try
            {
                var f = AccessTools.Field(typeof(UI.Inventory.Panel), "_items");
                var arr = f == null ? null : f.GetValue(panel) as UI.Inventory.GearElement[];
                if (arr == null) return false;

                float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                bool any = false;

                foreach (var e in arr)
                {
                    if (e == null) continue;
                    var rt = e.transform as RectTransform;
                    if (rt == null) continue;

                    var canvas = e.GetComponentInParent<Canvas>();
                    Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
                    Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
                    float y = Screen.height - sp.y;

                    if (sp.x < minX) minX = sp.x;
                    if (sp.x > maxX) maxX = sp.x;
                    if (y > maxY) maxY = y;
                    any = true;
                }

                if (!any) return false;
                bounds = Rect.MinMaxRect(minX, 0f, maxX, maxY);
                return true;
            }
            catch { return false; }
        }

        /// <summary>程序化生成棱形贴图（filled = 实心，否则为空心描边）</summary>
        private static Texture2D MakeDiamond(int n, bool filled)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = (Mathf.Abs(x - c) + Mathf.Abs(y - c)) / c;   // 0 中心 → 1 边缘
                    float aa = c / 1.5f;                                   // 1.5px 抗锯齿
                    float a = Mathf.Clamp01((1f - d) * aa);
                    if (!filled) a *= Mathf.Clamp01((d - 0.62f) * aa);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
