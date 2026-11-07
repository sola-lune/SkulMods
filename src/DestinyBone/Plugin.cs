using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using Characters;
using Characters.Gear;
using Characters.Gear.Items;
using Characters.Gear.Synergy.Inscriptions;
using Characters.Player;
using GameResources;
using HarmonyLib;
using Level;
using Services;
using Singletons;
using UnityEngine;

namespace DestinyBone
{
    public class EvoReq
    {
        public int Kills;
        public int Gold;
        public int Procs;

        public string Text()
        {
            var parts = new List<string>();
            if (Kills > 0) parts.Add("击杀 " + Kills);
            if (Gold > 0) parts.Add("获得 " + Gold + " 金币");
            if (Procs > 0) parts.Add("触发 " + Procs + " 次负面状态");
            return parts.Count == 0 ? "无需条件" : string.Join("、", parts);
        }
    }

    public class BoneTier
    {
        public string Id;
        public string Name;
        public string Flavor;
        public int RarityValue;         // 0 普通 1 稀有 2 独特 3 传说
        public double Pct;              // 全属性幅度，同时作为攻击上状态的概率
        public double Lifesteal;        // 吸血比例（0 表示没有）
        public double ShieldCap;        // 溢出治疗转护盾的上限（占最大生命）
        public int InscriptionBonus;    // 全刻印加值
        public bool Revive;             // 首次死亡满血复活
        public EvoReq Req;              // 进化条件
    }

    [BepInPlugin(Guid, "Destiny Bone", "0.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.solalune.destinybone";

        internal static readonly BoneTier[] Tiers =
        {
            new BoneTier { Id = "Custom-BoneHuman", Name = "人魂之骨", RarityValue = 0, Pct = 0.10,
                           Req = new EvoReq { Kills = 50 },
                           Flavor = "凡人的骨头，也会做梦。" },
            new BoneTier { Id = "Custom-BoneEarth", Name = "地魄之骨", RarityValue = 1, Pct = 0.15,
                           Lifesteal = 0.05, ShieldCap = 0.10,
                           Req = new EvoReq { Kills = 100, Gold = 500 },
                           Flavor = "埋得越深，压得越实。" },
            new BoneTier { Id = "Custom-BoneDestiny", Name = "天命之骨", RarityValue = 2, Pct = 0.20,
                           Lifesteal = 0.05, ShieldCap = 0.20, InscriptionBonus = 1,
                           Req = new EvoReq { Kills = 200, Gold = 2000, Procs = 50 },
                           Flavor = "命数写在骨头上，只是没人读得懂。" },
            new BoneTier { Id = "Custom-BoneMyth", Name = "神话之骨", RarityValue = 3, Pct = 0.25,
                           Lifesteal = 0.10, ShieldCap = 0.25, InscriptionBonus = 2, Revive = true,
                           Flavor = "骨头也会老，只是它老得比别人慢。" },
        };

        internal static BoneTierItem[] Refs;

        private void Awake()
        {
            Refs = new BoneTierItem[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++) Refs[i] = new BoneTierItem(Tiers[i], i);

            new Harmony(Guid).PatchAll(typeof(Patches));
            new Harmony(Guid + ".pages").PatchAll(typeof(ItemPagePatches));
            new Harmony(Guid + ".save").PatchAll(typeof(SaveInitPatch));
            Logger.LogInfo("[DestinyBone] 骨头进化链已加载：人魂 → 地魄 → 天命 → 神话");
            Logger.LogInfo("[DestinyBone] 道具栏分页已加载：Q/E 翻页");
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            PageOverlay.Draw();
        }

        private void Update()
        {
            SkulInsightCompat.TryPatch(Guid + ".si");
            if (!ItemPages.PanelOpen()) return;
            if (Input.GetKeyDown(KeyCode.Q)) ItemPages.Turn(-1);
            else if (Input.GetKeyDown(KeyCode.E)) ItemPages.Turn(1);
        }
    }

    /// <summary>一档骨头道具</summary>
    public class BoneTierItem : ItemReference
    {
        public readonly BoneTier Tier;
        public readonly int Index;

        private static GameObject _root;

        public BoneTierItem(BoneTier tier, int index)
        {
            Tier = tier;
            Index = index;
            obtainable = false;      // 不进任何掉落/商店/事件池
            needUnlock = false;
            rarity = (Rarity)tier.RarityValue;
            path = "Assets/Gear/Items/BasicCarleonSword.prefab";
            name = tier.Id;
            guid = "custom_item://" + tier.Id;
            displayNameKey = "item/" + tier.Id + "/name";
            prefabKeyword1 = (Inscription.Key)4;   // Bone
            prefabKeyword2 = (Inscription.Key)4;   // Bone
        }

        public Item GetItem(Item baseItem)
        {
            // 每次都给新实例：读档/换图时游戏会二次请求同一个引用，
            // 复用同一个对象会导致它在背包里被两处引用，删一处另一处也失效
            if (_root == null)
            {
                _root = new GameObject("DestinyBoneRoot");
                _root.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }

            var _item = UnityEngine.Object.Instantiate(baseItem, _root.transform);
            UnityEngine.Object.DontDestroyOnLoad(_item);
            _item.gameObject.name = Tier.Id;
            // 注意：屏蔽掉落靠的是 GearReference.obtainable，实例上的 obtainable 必须保持 true，
            // 否则游戏里按实例 obtainable 过滤的系统（CanDrop / 改刻印 / 图鉴）会把它当非法道具
            _item.obtainable = true;
            _item.keyword1 = (Inscription.Key)4;
            _item.keyword2 = (Inscription.Key)4;

            AccessTools.Field(typeof(Gear), "_stat").SetValue(_item, new Stat.Values(new Stat.Value[0]));
            AccessTools.Field(typeof(Gear), "_rarity").SetValue(_item, (Rarity)Tier.RarityValue);
            AccessTools.Field(typeof(Gear), "_groupItemKeys").SetValue(_item, new string[0]);

            LoadSprites();
            if (icon != null && _item.dropped != null && _item.dropped.spriteRenderer != null)
                _item.dropped.spriteRenderer.sprite = icon;

            return _item;
        }

        public void LoadSprites()
        {
            if (icon == null) icon = LoadSprite("Icon", 32f);
            if (thumbnail == null) thumbnail = LoadSprite("Thumbnail", 32f);
        }

        private Sprite LoadSprite(string kind, float ppu)
        {
            try
            {
                var asm = typeof(BoneTierItem).Assembly;
                using (var s = asm.GetManifestResourceStream("DestinyBone.Sprites." + kind + ".png"))
                {
                    if (s == null) return null;
                    var bytes = new byte[s.Length];
                    s.Read(bytes, 0, bytes.Length);
                    var tex = new Texture2D(2, 2);
                    ImageConversion.LoadImage(tex, bytes);
                    tex.filterMode = FilterMode.Point;
                    var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
                    sp.name = Tier.Id + "_" + kind;
                    return sp;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[DestinyBone] 贴图加载失败(" + kind + "): " + e.Message);
                return null;
            }
        }
    }

    public static class Patches
    {
        private static readonly Dictionary<ItemRequest, BoneTierItem> Requests =
            new Dictionary<ItemRequest, BoneTierItem>();

        internal static readonly Dictionary<string, string> Strings = BuildStrings();

        private static Dictionary<string, string> BuildStrings()
        {
            var d = new Dictionary<string, string>();
            foreach (var t in Plugin.Tiers)
            {
                d["item/" + t.Id + "/name"] = t.Name;
                d["item/" + t.Id + "/flavor"] = t.Flavor;

                int pct = (int)Math.Round(t.Pct * 100);
                var desc = "所有属性朝<u>有利方向</u>调整 " + pct + "%：\n" +
                           "　· 攻击 / 攻速 / 暴击 / 移速 / 冷却加速等 <color=#F25D1C>+" + pct + "%</color>\n" +
                           "　· 受到伤害 <color=#1787D8>-" + pct + "%</color>\n" +
                           "攻击时，每种负面效果各有 " + pct + "% 概率触发（彼此独立）。\n";

                if (t.Lifesteal > 0)
                {
                    desc += "<color=#F25D1C>吸血 " + (int)Math.Round(t.Lifesteal * 100) + "%</color>，" +
                            "溢出治疗转化为护盾（上限 " + (int)Math.Round(t.ShieldCap * 100) + "% 最大生命）。\n";
                }
                if (t.InscriptionBonus > 0)
                    desc += "所有已激活刻印 <color=#F25D1C>+" + t.InscriptionBonus + "</color>。\n";
                if (t.Revive)
                    desc += "<color=#F25D1C>第一次死亡时满血复活</color>。\n";

                if (t.Id == "Custom-BoneHuman") desc += "（开局自动获得）\n";
                else if (t.Req != null)
                    desc += "进化条件：<color=#F25D1C>" + t.Req.Text() + "</color>（全部达成）";

                d["item/" + t.Id + "/desc"] = desc.TrimEnd('\n');
            }
            return d;
        }

        // ---------- 注册到道具池（仅为存档按名反查，obtainable=false 不会掉落） ----------
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GearResource), "Initialize")]
        private static void InjectItem(ref GearResource __instance)
        {
            try
            {
                foreach (var r in Plugin.Refs) r.LoadSprites();

                var f = AccessTools.Field(typeof(GearResource), "_items");
                var list = ((ItemReference[])f.GetValue(__instance)).ToList();
                list.AddRange(Plugin.Refs);
                f.SetValue(__instance, list.ToArray());

                var ft = AccessTools.Field(typeof(GearResource), "_gearThumbnails");
                if (ft != null)
                {
                    var tl = ((Sprite[])ft.GetValue(__instance)).ToList();
                    foreach (var r in Plugin.Refs) if (r.thumbnail != null) tl.Add(r.thumbnail);
                    ft.SetValue(__instance, tl.ToArray());
                }

                Debug.Log("[DestinyBone] 已注入 " + Plugin.Refs.Length + " 档骨头，道具总数 " + list.Count);
            }
            catch (Exception e) { Debug.LogError("[DestinyBone] 注入失败: " + e); }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ItemReference), "LoadAsync")]
        private static void MapRequest(ItemReference __instance, ItemRequest __result)
        {
            if (__instance is BoneTierItem b && __result != null) Requests[__result] = b;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LevelManager), "DropItem", new[] { typeof(ItemRequest), typeof(Vector3) })]
        private static bool DropCustomItem(LevelManager __instance, ItemRequest gearRequest, Vector3 position, ref Item __result)
        {
            if (!Requests.TryGetValue(gearRequest, out var b)) return true;
            var item = b.GetItem(gearRequest.asset);
            __result = __instance.DropItem(item, position);
            Requests.Remove(gearRequest);
            return false;
        }

        // ---------- 本地化 ----------
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Localization), "GetLocalizedString", new[] { typeof(string) })]
        private static bool Localize(string key, ref string __result)
        {
            if (key == null || !Strings.TryGetValue(key, out var v)) return true;
            __result = v;
            return false;
        }

        // ---------- 刻印加成（按当前档位） ----------
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), "UpdateSynergy")]
        private static void SynergyPrefix(Inventory __instance)
        {
            int tier = BoneEffect.ActiveTierOf(__instance);
            if (tier < 0) return;
            int bonus = Plugin.Tiers[tier].InscriptionBonus;
            if (bonus <= 0) return;
            foreach (var ins in __instance.synergy.inscriptions) ins.bonusCount += bonus;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Inventory), "UpdateSynergy")]
        private static void SynergyFinalizer(Inventory __instance)
        {
            int tier = BoneEffect.ActiveTierOf(__instance);
            if (tier < 0) return;
            int bonus = Plugin.Tiers[tier].InscriptionBonus;
            if (bonus <= 0) return;
            foreach (var ins in __instance.synergy.inscriptions) ins.bonusCount -= bonus;
        }

        // ---------- 效果与开局发放 ----------
        private static Character _lastChar;
        private static ItemInventory _lastInv;
        private static int _lastTier = -1;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), "UpdateSynergy")]
        private static void RefreshEffect(Inventory __instance)
        {
            var ch = AccessTools.Field(typeof(Inventory), "_character").GetValue(__instance) as Character;
            if (ch == null) return;

            // 以 ItemInventory 实例为准：玩家对象可能复用，但背包组件会被重建
            var invNow = AccessTools.Field(typeof(Inventory), "item").GetValue(__instance) as ItemInventory;
            if (invNow != null && invNow != _lastInv)
            {
                _lastInv = invNow;
                _lastChar = ch;
                ItemPages.Grow(invNow);
                Debug.Log("[DestinyBone][diag] 检测到新背包实例，格数=" + invNow.items.Count);
                TryDropAtStageStart(ch, invNow);
            }

            var comp = ch.GetComponent<BoneEffect>();
            if (comp == null) comp = ch.gameObject.AddComponent<BoneEffect>();
            comp.Refresh(ch);

            // 诊断：骨头从背包消失时抓一次现场
            int now = BoneEffect.ActiveTierOf(__instance);
            if (_lastTier >= 0 && now < 0 && ch.health != null && !ch.health.dead)
            {

                var inv = AccessTools.Field(typeof(Inventory), "item").GetValue(__instance) as ItemInventory;
                var sb = new System.Text.StringBuilder("[DestinyBone][diag] 骨头消失，当前背包: ");
                if (inv != null)
                    for (int i = 0; i < inv.items.Count; i++)
                        if (inv.items[i] != null) sb.Append(i + ":" + inv.items[i].name + " ");
                Debug.LogWarning(sb.ToString());
            }
            _lastTier = now;
        }

        private static int _droppedSeed = int.MinValue;

        /// <summary>只在 1-1（Castle 章第 0 关）开局掉一次，每局仅一次</summary>
        private static void TryDropAtStageStart(Character ch, ItemInventory inv)
        {
            try
            {
                if (HasBone(inv)) return;

                var lm = Singleton<Service>.Instance.levelManager;
                var ch2 = lm.currentChapter;
                if (ch2 == null) return;
                if ((int)ch2.type != (int)Level.Chapter.Type.Castle || ch2.stageIndex != 0) return;

                int seed = Data.GameData.Save.instance.randomSeed;
                if (seed == _droppedSeed) return;
                _droppedSeed = seed;

                DropBoneNear(ch, 0);
            }
            catch (Exception e) { Debug.LogError("[DestinyBone] 1-1 掉落判定失败: " + e); }
        }

        private static bool HasBone(ItemInventory inv)
        {
            if (inv == null) return false;
            foreach (var it in inv.items)
                if (it != null && IsBone(it.name)) return true;
            return false;
        }

        /// <summary>在角色脚下掉一件骨头（走正常的掉落→拾取流程）</summary>
        private static void DropBoneNear(Character ch, int tier)
        {
            try
            {
                var r = Plugin.Refs[tier];
                var req = r.LoadAsync();
                req.WaitForCompletion();
                if (req.asset == null) { Debug.LogError("[DestinyBone] 掉落物加载失败"); return; }

                var lm = Singleton<Service>.Instance.levelManager;
                var pos = ch.transform.position + new Vector3(1.2f, 0f, 0f);
                var item = lm.DropItem(req, pos);
                Debug.Log("[DestinyBone] 已掉出 " + Plugin.Tiers[tier].Name + " -> " + (item == null ? "null" : item.name));
            }
            catch (Exception e) { Debug.LogError("[DestinyBone] 掉落失败: " + e); }
        }

        private static void GiveTier(Character ch, int tier)
        {
            try
            {
                var inv = AccessTools.Field(typeof(Inventory), "item").GetValue(ch.playerComponents.inventory) as ItemInventory;
                if (inv == null) return;
                ItemPages.Grow(inv);

                var r = Plugin.Refs[tier];
                var req = r.LoadAsync();
                req.WaitForCompletion();
                if (req.asset == null) return;

                var item = r.GetItem(req.asset);
                if (inv.TryEquip(item)) Debug.LogWarning("[DestinyBone] 骨头丢失，已补回 " + Plugin.Tiers[tier].Name);
            }
            catch (Exception e) { Debug.LogError("[DestinyBone] 补回失败: " + e); }
        }

        private static void GiveStartBone(Character ch)
        {
            try
            {
                var inv = AccessTools.Field(typeof(Inventory), "item").GetValue(ch.playerComponents.inventory) as ItemInventory;
                if (inv == null) return;

                foreach (var it in inv.items)
                    if (it != null && IsBone(it.name)) return;

                ItemPages.Grow(inv);   // 先扩到 27 格再发放，避免落在 9 格版本上
                var r = Plugin.Refs[0];
                var req = r.LoadAsync();
                req.WaitForCompletion();
                if (req.asset == null) { Debug.LogError("[DestinyBone] 开局道具加载失败"); return; }

                var item = r.GetItem(req.asset);
                Debug.Log("[DestinyBone][diag] 开局发放前: count=" + inv.items.Count + " 空位=" + inv.items.Count(x => x == null));
                if (inv.TryEquip(item)) Debug.Log("[DestinyBone] 开局已发放 " + Plugin.Tiers[0].Name);
                else Debug.LogWarning("[DestinyBone] 背包已满，开局道具没发出去");
            }
            catch (Exception e) { Debug.LogError("[DestinyBone] 开局发放异常: " + e); }
        }

        private static bool IsBone(string name)
        {
            foreach (var t in Plugin.Tiers) if (t.Id == name) return true;
            return false;
        }
    }

    /// <summary>挂在角色身上的效果控制器</summary>
    public class BoneEffect : MonoBehaviour
    {
        private Character _owner;
        private Stat.Values _values;
        private int _tier = -2;
        private bool _hooked;
        private bool _reviveUsed;
        private double _ourShield;
        private static int _shieldLog;
        private readonly object _shieldKey = new object();

        private GaveDamageDelegate _gaveHandler;
        private Character.OnKilledDelegate _killHandler;
        private Character.OnGaveStatusDelegate _statusHandler;
        private Data.GameData.Currency.OnEarnDelegate _goldHandler;
        private Action _dieHandler;

        private readonly int[] _kills = new int[Plugin.Tiers.Length];
        private readonly int[] _gold = new int[Plugin.Tiers.Length];
        private readonly int[] _procs = new int[Plugin.Tiers.Length];

        public void Refresh(Character owner)
        {
            _owner = owner;
            if (!_hooked)
            {
                _killHandler = OnKilled;
                _owner.onKilled = (Character.OnKilledDelegate)Delegate.Combine(_owner.onKilled, _killHandler);
                _statusHandler = OnGaveStatus;
                _owner.onGaveStatus = (Character.OnGaveStatusDelegate)Delegate.Combine(_owner.onGaveStatus, _statusHandler);
                _goldHandler = OnEarnGold;
                Data.GameData.Currency.currencies[Data.GameData.Currency.Type.Gold].onEarn += _goldHandler;
                _gaveHandler = OnGaveDamage;
                _owner.onGaveDamage = (GaveDamageDelegate)Delegate.Combine(_owner.onGaveDamage, _gaveHandler);
                _dieHandler = OnLethalDamage;
                _owner.health.onDie += _dieHandler;
                _hooked = true;
            }

            int tier = ActiveTierOf(_owner.playerComponents.inventory);
            if (tier == _tier) return;

            if (_values != null) { try { _owner.stat.DetachValues(_values); } catch { } _values = null; }
            try { if (_ourShield > 0) { _owner.health.shield.Remove(_shieldKey); _ourShield = 0; } } catch { }

            _tier = tier;
            if (tier < 0) return;

            var t = Plugin.Tiers[tier];
            var list = new List<Stat.Value>();
            foreach (var f in typeof(Stat.Kind).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(f.GetValue(null) is Stat.Kind k)) continue;
                bool bad = f.Name == "TakingDamage" || f.Name == "TakingPhysicalDamage" || f.Name == "TakingMagicDamage";
                list.Add(new Stat.Value(Stat.Category.Percent, k, bad ? 1.0 - t.Pct : 1.0 + t.Pct));
            }
            _values = new Stat.Values(list.ToArray());
            _owner.stat.AttachValues(_values);

            Debug.Log("[DestinyBone] 生效档位: " + t.Name + " (" + (t.Pct * 100) + "%)");
        }

        internal static int ActiveTierOf(Inventory inv)
        {
            int best = -1;
            try
            {
                var itemInv = AccessTools.Field(typeof(Inventory), "item").GetValue(inv) as ItemInventory;
                if (itemInv == null) return -1;
                foreach (var it in itemInv.items)
                {
                    if (it == null) continue;
                    for (int i = 0; i < Plugin.Tiers.Length; i++)
                        if (it.name == Plugin.Tiers[i].Id && i > best) best = i;
                }
            }
            catch { }
            return best;
        }

        // ---------- 攻击：负面状态 + 吸血 ----------
        private void OnGaveDamage(ITarget target, in Damage originalDamage, in Damage tookDamage, double damageDealt)
        {
            if (_tier < 0) return;
            var t = Plugin.Tiers[_tier];
            var victim = target == null ? null : target.character;
            if (victim == null || victim == _owner) return;

            int chance = (int)Math.Round(t.Pct * 100);
            for (int k = 0; k <= (int)CharacterStatus.Kind.Unmoving; k++)
            {
                if (!MMMaths.PercentChance(chance)) continue;
                try { _owner.GiveStatus(victim, new CharacterStatus.ApplyInfo((CharacterStatus.Kind)k)); } catch { }
            }

            if (t.Lifesteal <= 0 || damageDealt <= 0) return;
            try
            {
                double want = damageDealt * t.Lifesteal;
                // Heal 的返回值是"超出最大生命的溢出量"，不是治疗量
                double overflow = _owner.health.Heal(want);
                if (overflow > 0 && t.ShieldCap > 0) AddShield(overflow, t.ShieldCap);
            }
            catch { }
        }

        private void AddShield(double amount, double capPct)
        {
            var h = _owner.health;
            double cap = h.maximumHealth * capPct;
            double room = cap - h.shield.amount;
            if (room <= 0) return;
            double add = Math.Round(Math.Min(amount, room), 2, MidpointRounding.AwayFromZero);
            if (add <= 0) return;
            h.shield.Add(_shieldKey, (float)add);
            _ourShield = Math.Round(_ourShield + add, 2);

            if ((_shieldLog++ % 20) == 0)
                Debug.Log("[DestinyBone][diag] 护盾 +" + add + " （当前 " + Math.Round(h.shield.amount, 2)
                          + " / 上限 " + Math.Round(cap, 2) + "）");
        }

        // ---------- 首次死亡满血复活 ----------
        private void OnLethalDamage()
        {
            if (_tier < 0) return;
            if (!Plugin.Tiers[_tier].Revive || _reviveUsed) return;
            _reviveUsed = true;
            try
            {
                _owner.health.FixedAmountHeal(_owner.health.maximumHealth);
                Debug.Log("[DestinyBone] 首次死亡，满血复活");
            }
            catch (Exception e) { Debug.LogError("[DestinyBone] 复活失败: " + e); }
        }

        // ---------- 进化计数 ----------
        private void OnKilled(ITarget target, ref Damage damage)
        {
            if (_tier < 0 || _tier >= Plugin.Tiers.Length - 1) return;
            _kills[_tier]++;
            CheckEvolve(_tier);
        }

        private void OnEarnGold(int amount)
        {
            if (_tier < 0 || _tier >= Plugin.Tiers.Length - 1 || amount <= 0) return;
            _gold[_tier] += amount;
            CheckEvolve(_tier);
        }

        private void OnGaveStatus(Character target, CharacterStatus.ApplyInfo info, bool result)
        {
            if (!result || target == null || target == _owner) return;
            if (_tier < 0 || _tier >= Plugin.Tiers.Length - 1) return;
            _procs[_tier]++;
            CheckEvolve(_tier);
        }

        private void CheckEvolve(int tier)
        {
            var r = Plugin.Tiers[tier].Req;
            if (r == null) return;
            if (_kills[tier] < r.Kills || _gold[tier] < r.Gold || _procs[tier] < r.Procs) return;
            Debug.Log("[DestinyBone] 条件达成: 击杀 " + _kills[tier] + " / 金币 " + _gold[tier] + " / 状态 " + _procs[tier]);
            _kills[tier] = 0;
            _gold[tier] = 0;
            _procs[tier] = 0;
            Evolve(tier);
        }

        private bool Evolve(int tier)
        {
            if (tier + 1 >= Plugin.Tiers.Length) return false;
            try
            {
                var cur = FindItem(Plugin.Tiers[tier].Id);
                if (cur == null) return false;

                var next = Plugin.Refs[tier + 1];
                var req = next.LoadAsync();
                req.WaitForCompletion();

                if (cur.state == Gear.State.Equipped)
                {
                    var newItem = Singleton<Service>.Instance.levelManager.DropItem(req, Vector3.zero);
                    cur.ChangeOnInventory(newItem);
                    Debug.Log("[DestinyBone] 进化: " + Plugin.Tiers[tier].Name + " -> " + Plugin.Tiers[tier + 1].Name);
                    return true;
                }
            }
            catch (Exception e) { Debug.LogError("[DestinyBone] 进化异常: " + e); }
            return false;
        }

        private Item FindItem(string id)
        {
            try
            {
                var inv = AccessTools.Field(typeof(Inventory), "item").GetValue(_owner.playerComponents.inventory) as ItemInventory;
                if (inv == null) return null;
                foreach (var it in inv.items) if (it != null && it.name == id) return it;
            }
            catch { }
            return null;
        }

        private void OnDestroy()
        {
            try
            {
                if (_owner == null) return;
                if (_killHandler != null) _owner.onKilled = (Character.OnKilledDelegate)Delegate.Remove(_owner.onKilled, _killHandler);
                if (_statusHandler != null) _owner.onGaveStatus = (Character.OnGaveStatusDelegate)Delegate.Remove(_owner.onGaveStatus, _statusHandler);
                if (_goldHandler != null) Data.GameData.Currency.currencies[Data.GameData.Currency.Type.Gold].onEarn -= _goldHandler;
                if (_gaveHandler != null) _owner.onGaveDamage = (GaveDamageDelegate)Delegate.Remove(_owner.onGaveDamage, _gaveHandler);
                if (_dieHandler != null) _owner.health.onDie -= _dieHandler;
                if (_values != null) _owner.stat.DetachValues(_values);
            }
            catch { }
        }
    }
}
