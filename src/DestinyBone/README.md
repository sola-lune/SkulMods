# Destiny Bone — 骨头进化链（独立 mod）

Skul: The Hero Slayer 1.9.1 / BepInEx 5.4.21 / 不依赖任何其它 mod。

## 四档设计

| 档 | 名称 | 稀有度 | 全属性 | 攻击上状态 | 吸血 | 护盾上限 | 刻印 | 复活 |
|---|---|---|---|---|---|---|---|---|
| 0 | 人魂之骨 | 普通 | +10% / 受伤 -10% | 每种 10% | — | — | — | — |
| 1 | 地魄之骨 | 稀有 | +15% / 受伤 -15% | 每种 15% | 5% | 10% | — | — |
| 2 | 天命之骨 | 独特 | +20% / 受伤 -20% | 每种 20% | 5% | 20% | +1 | — |
| 3 | 神话之骨 | 传说 | +25% / 受伤 -25% | 每种 25% | 10% | 25% | +2 | 首次死亡满血复活 |

- 四档全部是 **骨头 ×2** 刻印。
- **攻击上状态是全档常驻**，概率等于该档的属性幅度（10/15/20/25），6 种状态各自独立 roll。
- 吸血从**稀有档**开始，**溢出治疗自动转为护盾**，护盾上限按最大生命的 10%/20%/25% 计（地魄 10%、天命 20%、神话 25%）。
- 刻印加成、复活只在高档出现，**不叠加**（同时持有多档时只按最高档生效）。

## 获取方式

- 四档全部 `obtainable = false` → **不进任何掉落池 / 商店 / 事件**（游戏池子取件逻辑是 `_items[rarity].Where(i => i.available)`）。
- **开局自动发放人魂之骨**：新一局检测到背包里没有骨头时，`LoadAsync().WaitForCompletion()` → `GetItem()` → `inv.TryEquip()`。
- 保留 `_items` 注册，只是为了存档按名字反查引用（否则读档会丢装备）。

## 进化条件

| 阶段 | 条件 |
|---|---|
| 人魂 → 地魄 | 击杀 50 |
| 地魄 → 天命 | 击杀 100 + 获得 500 金币 |
| 天命 → 神话 | 击杀 200 + 获得 2000 金币 + 触发 50 次负面状态 |

与（AND）关系，全部达成才进化；每次进化后计数清零，只统计当前档位期间的数据。

## 效果实现

| 效果 | 实现 |
|---|---|
| 全属性有利方向 N% | 46 条 `Stat.Kind` 各挂一条 `Category.Percent`：受伤类 ×(1-N%)，其余 ×(1+N%) |
| 攻击上状态 | `Character.onGaveDamage` 中对 6 种 `CharacterStatus.Kind` 逐个 roll |
| 吸血 | 同一次 `onGaveDamage` 里按 `damageDealt × Lifesteal` 调 `health.Heal()` |
| 溢出转护盾 | `healed = Heal(want)`，`overflow = want - healed` → `health.shield.Add(key, min(overflow, cap - shield.amount))` |
| 刻印 +1/+2 | `Inventory.UpdateSynergy` 前后给每个已有刻印 `bonusCount ±N`；空刻印不会被激活 |
| 首次死亡复活 | 订阅 `health.onDie`（在 `dead = true` 之前触发），`FixedAmountHeal(maximumHealth)`，只触发一次 |
| 进化 | `TryGetItemReferenceByName` → `LoadAsync().WaitForCompletion()` → `DropItem` → `cur.ChangeOnInventory(new)` 原地替换 |
| 掉落屏蔽 | `GearReference.obtainable = false` |

## 改数值

全部集中在 `Plugin.cs` 顶部的 `Tiers` 数组：

```csharp
new BoneTier { Id = "Custom-BoneEarth", Name = "地魄之骨", RarityValue = 1, Pct = 0.15,
               Lifesteal = 0.05, ShieldCap = 0.20,
               Req = new EvoReq { Kills = 100, Gold = 500 }, ... },
```

- `Pct` —— 属性幅度，同时决定攻击上状态概率
- `Lifesteal` / `ShieldCap` —— 吸血比例 / 护盾上限（占最大生命）
- `InscriptionBonus` —— 全刻印加成
- `Revive` —— 是否首次死亡满血复活
- `RarityValue` —— 0 普通 / 1 稀有 / 2 独特 / 3 传说
- `Req` —— `new EvoReq { Kills, Gold, Procs }`，填 0 表示不要求

改完 `dotnet build`，把 `bin/Debug/DestinyBone.dll` 覆盖到 `BepInEx/plugins/DestinyBone/`。

## 图标

从 VOTC 的 `Sprites.BoneOfRandomness.{Icon,Thumbnail,AbilityIcon}.png` 提取，内嵌为 `DestinyBone.Sprites.*`（32 PPU，Point 过滤）。四档共用一张图，靠稀有度颜色区分。
贴图版权属 VOTC 作者，仅自用。

## 已知限制

1. **没有测试键**（F8/F9/F10 已移除），验证只能靠实际游玩。
2. **计数器只在内存里**，退出游戏清零。
3. **复活是"截断 onDie"实现**，如果同局还有别的复活来源（重组装置等），触发顺序不保证。
4. 四档共用一张图标。
5. 掉落权重无需调整（已不可掉落）。

## 源码

`D:\Game\Learning\Skul\_src\DestinyBone\`
- `Plugin.cs` —— 全部逻辑
- `Resources/*.png` —— 内嵌图标
- `进化机制与链路.md` —— VOTC 三种进化机制与各道具触发条件拆解

---

# 道具栏分页（3 页 × 9 = 27 格）

## 实现方式

| 层 | 做法 |
|---|---|
| 容量 | `ItemInventory.Awake` 后置把 `items` 列表补到 27 个 `null`（`TryEquip` / 刻印统计 / 属性结算自动按 27 件跑） |
| 存档 | 钩 `Data.GameData+Save.Initialize` 后置，把 `items` / `itemStacks` / `itemKeywords1` / `itemKeywords2` 四个 `*DataArray` 用反射重建为 27 长度。底层键名是 `Save/items/{i}`，老存档 0~8 的值仍在，9~26 读空值 |
| 翻页 | **整体轮转 `items` 列表** 9 位，然后调 `UI.Inventory.Panel.UpdateGearInfo()` 重绘。顺序对功能无影响（属性在装备时已挂上） |
| 按键 | `Q` 上一页 / `E` 下一页，仅在装备栏打开时生效 |
| 页码指示 | `PageOverlay`：取 9 个 `GearElement` 的屏幕包围盒，在下方居中画 3 个棱形，当前页实心（不透明 1.0）、其它页空心（0.5 透明度） |

## 为什么用"轮转"而不是"加格子"

暂停菜单的 9 个格子在预制体里是固定数组（`GearElement[] _items`），每个格子的 `onSelected` 闭包绑死了 `items[index]`。克隆新格子要重建导航、动画、套装特效，改动面太大。轮转列表相当于"把第 N 页挪到最前面"，UI 代码一行不用改，选中/丢弃/图标全部自然正确。

## 已知限制

1. **页码指示**：装备栏道具格下方画 3 个棱形，当前页实心、其它页空心（程序化生成 64px 贴图 + 1.5px 抗锯齿，走 IMGUI 覆盖层，不动游戏 UI 层级）。
2. **每次翻页会改变背包实际顺序**，`Trim()` 压缩空位后当前页内容可能错位，需要重新按 Q/E 对齐。
3. 满包时的**拾取替换界面**只有 9 格，显示的是当前页。
4. 第 10+ 件能否在**读档后恢复**取决于游戏的 Load 循环是否按 `instance.items.length`（存档侧已改 27），这一点待实测确认。

---

# 启动报错 "SteamAPI_Init() failed.. Quitting game" 的原因与修复

## 现象

启动后黑屏一行字：`Initialization failed : [SteamPlatform] SteamAPI_Init() failed.. Quitting game.. 1`

## 真正原因（不是版本问题）

日志里真正先炸的是这一串：

```
NullReferenceException
  Platforms.PlatformManager.<InitializeDataGetterAndSetter>b__33_0
  Data.IntData..ctor
  Data.GameData.Initialize()
  Services.Service.Awake()
  GameObject:AddComponent()
  Singletons.Singleton`1:get_Instance()
  DamageInsight.Plugin:Update()      ← SkulInsight QoL
```

SkulInsight 的 `Update()` 在**平台还没初始化**的时候就访问 `Singleton<Service>.Instance`，这个属性会**主动 new 一个 GameObject 并挂 Service**，于是 `Service.Awake()` → `GameData.Initialize()` 在平台未就绪时执行 → 空引用 → 平台数据读写失效 → 后面 Steam 层判定初始化失败，直接退出。

是个**时序竞态**：启动耗时变化（多了 BepInEx 插件、图标加载、存档扩容）就会把窗口拉开，所以表现为"有时能进、有时报错"。

## 修复

`SkulInsightCompat`（见 `ItemPages.cs`）：运行时给 `DamageInsight.Plugin.Update` 加一个 Harmony 前缀，`Service` 没实例化前直接跳过它的 Update：

```csharp
public static bool Prefix() => Services.Service.Instantiated;   // 不主动创建
```

必须**运行时补**：BepInEx 按字母序加载插件，Destiny Bone 先于 DamageInsight，`Awake` 阶段还看不到它的程序集，只能等第一帧再 patch。

## 验证

```
[Info : Unity Log] [DestinyBone] 已给 SkulInsight 打上防早初始化补丁
[Info : Unity Log] [SteamPlatform] Steam connected : Noob
NullReferenceException 计数: 0
```

## 注意

- 这个补丁在 **Destiny Bone** 里。如果哪天把 Destiny Bone 关掉又留着 SkulInsight，报错会复现。
- 也可以选择只留一个：禁用 `SkulInsight_QoL` 文件夹同样能进游戏，但会失去图鉴/战斗日志。
