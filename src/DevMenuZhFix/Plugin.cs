using System;
using BepInEx;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DevMenuZhFix
{
    /// <summary>
    /// DevMenuTurbo 汉化版的补充翻译。
    /// 原版汉化只处理了主面板的 24 个按钮/滑条，下面这些它没碰：
    ///   · 主面板：_chapter1~6（六个章节按钮）、_version、_mapName、_reviveCountText（韩文）、_hardmodeClearedCountSlider（通关次数滑条）
    ///   · 数据控制面板：12 个按钮 + 2 个滑条，全英文
    ///   · 日志面板：复制 / 清除 两个按钮
    /// </summary>
    [BepInPlugin(Guid, "DevMenuTurbo 补充汉化", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "devmenuzhfix.local";

        private void Awake()
        {
            try
            {
                new Harmony(Guid).PatchAll(typeof(Patch));
                Logger.LogInfo("[DevMenuZhFix] 补充汉化已加载（主面板 / 数据控制 / 日志）");
            }
            catch (Exception e)
            {
                Logger.LogError("[DevMenuZhFix] 加载失败: " + e);
            }
        }
    }

    [HarmonyPatch]
    public static class Patch
    {
        // ───────────────────── 主面板 ─────────────────────

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]      // 在 DevMenuTurbo 之后跑，覆盖重叠项
        [HarmonyPatch(typeof(UI.TestingTool.Panel), "Awake")]
        private static void PanelAwake(UI.TestingTool.Panel __instance)
        {
            try
            {
                var t = __instance.GetType();

                // 六个章节按钮（预制体原文没被翻译）
                string[] chapters = { "第 1 章", "第 2 章", "第 3 章", "第 4 章", "第 5 章", "第 6 章" };
                for (int i = 1; i <= 6; i++)
                    SetText(Get(t, "_chapter" + i, __instance), chapters[i - 1]);

                // 通关次数滑条：DevMenuTurbo 只做了等级/解锁两个，漏了这个
                SetText2(Get(t, "_hardmodeClearedCountSlider", __instance), "通关次数");

                FixRuntimeTexts(__instance);
            }
            catch (Exception e) { Debug.LogWarning("[DevMenuZhFix] 主面板: " + e.Message); }
        }

        /// <summary>_mapName / _version / _reviveCountText 每次打开都会重设，必须跟着改</summary>
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(UI.TestingTool.Panel), "OnEnable")]
        private static void PanelOnEnable(UI.TestingTool.Panel __instance)
        {
            FixRuntimeTexts(__instance);
        }

        // 复活计数在战斗中还会刷新，单独再兜一层
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UI.TestingTool.Panel), "Revive")]
        private static void PanelRevive(UI.TestingTool.Panel __instance) => FixRuntimeTexts(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UI.TestingTool.Panel), "InitReviveCount")]
        private static void PanelInitRevive(UI.TestingTool.Panel __instance) => FixRuntimeTexts(__instance);

        private static void FixRuntimeTexts(UI.TestingTool.Panel p)
        {
            try
            {
                var t = p.GetType();

                // "Map : xxx/yyy/zzz\nSeed : 123" → 中文
                var map = Get(t, "_mapName", p) as TMP_Text;
                if (map != null && !string.IsNullOrEmpty(map.text))
                    map.text = map.text.Replace("Map :", "地图：").Replace("Seed :", "种子：");

                // "version : 1.9.1"
                var ver = Get(t, "_version", p) as TMP_Text;
                if (ver != null && !string.IsNullOrEmpty(ver.text))
                    ver.text = ver.text.Replace("version :", "版本：").Replace("version:", "版本：");

                // 韩文："부활횟수 (3)" / "무한부활"
                var rev = Get(t, "_reviveCountText", p) as TMP_Text;
                if (rev != null && !string.IsNullOrEmpty(rev.text))
                    rev.text = rev.text.Replace("부활횟수", "复活次数").Replace("무한부활", "无限复活");
            }
            catch { }
        }

        // ───────────────────── 数据控制面板 ─────────────────────

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UI.TestingTool.DataControl), "Awake")]
        private static void DataControlAwake(UI.TestingTool.DataControl __instance)
        {
            try
            {
                var t = __instance.GetType();

                SetText(Get(t, "_resetSeed", __instance), "重置种子");
                SetText(Get(t, "_firstClear", __instance), "一键首通");
                SetText(Get(t, "_resetProgress", __instance), "重置全部进度");
                SetText(Get(t, "_randomItem", __instance), "装备随机道具");
                SetText(Get(t, "_unlockAllItems", __instance), "解锁全部道具");
                SetText(Get(t, "_unlockAllUpgrades", __instance), "解锁全部升级");
                SetText(Get(t, "_lockAllUpgrades", __instance), "锁定全部升级");
                SetText(Get(t, "_itemReset", __instance), "道具重置");
                SetText(Get(t, "_upgradeReset", __instance), "升级重置");
                SetText(Get(t, "_allGearReset", __instance), "装备全部重置");
                SetText(Get(t, "_dCDefenseCleared", __instance), "黑暗镜面：已通关");
                SetText(Get(t, "_dCDefenseHint", __instance), "黑暗镜面：提示");

                SetText2(Get(t, "_hardmodeClearedCountSlider", __instance), "通关次数");
                SetText2(Get(t, "_hintTypeSlider", __instance), "提示类型");
            }
            catch (Exception e) { Debug.LogWarning("[DevMenuZhFix] 数据控制: " + e.Message); }
        }

        // ───────────────────── 装备清单面板 ─────────────────────

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UI.TestingTool.GearList), "Awake")]
        private static void GearListAwake(UI.TestingTool.GearList __instance)
        {
            try
            {
                var t = __instance.GetType();

                SetText(Get(t, "_head", __instance), "头骨");
                SetText(Get(t, "_item", __instance), "道具");
                SetText(Get(t, "_essence", __instance), "精华");
                SetText(Get(t, "_upgrade", __instance), "升级");

                SetText(Get(t, "_unlockSetting", __instance), "拾取即解锁");
                SetText(Get(t, "_lockSetting", __instance), "拾取即锁定");

                var input = Get(t, "_inputField", __instance) as TMP_InputField;
                if (input != null)
                {
                    var ph = input.placeholder as TMP_Text;
                    if (ph != null) ph.SetText("搜索…", true);
                }
            }
            catch (Exception e) { Debug.LogWarning("[DevMenuZhFix] 装备清单: " + e.Message); }
        }

        // ───────────────────── 日志面板 ─────────────────────

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UI.TestingTool.Log), "Awake")]
        private static void LogAwake(UI.TestingTool.Log __instance)
        {
            try
            {
                var t = __instance.GetType();
                SetText(Get(t, "_copy", __instance), "复制");
                SetText(Get(t, "_clear", __instance), "清除");
            }
            catch (Exception e) { Debug.LogWarning("[DevMenuZhFix] 日志: " + e.Message); }
        }

        // ───────────────────── 工具 ─────────────────────

        private static object Get(Type t, string field, object inst)
        {
            var f = AccessTools.Field(t, field);
            return f == null ? null : f.GetValue(inst);
        }

        /// <summary>把组件下第一个 TMP 文本换掉（按钮用）</summary>
        private static void SetText(object obj, string text)
        {
            var comp = obj as Component;
            if (comp == null) return;
            var tmp = comp.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) tmp.SetText(text, true);
        }

        /// <summary>滑条：第二个 TMP 才是标签（第一个通常是当前值）</summary>
        private static void SetText2(object obj, string text)
        {
            var comp = obj as Component;
            if (comp == null) return;
            var arr = comp.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (arr != null && arr.Length > 1) arr[1].SetText(text, true);
            else if (arr != null && arr.Length == 1) arr[0].SetText(text, true);
        }
    }
}
