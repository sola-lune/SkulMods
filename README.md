# Skul Mods

自制的《Skul: The Hero Slayer》（小骨：英雄杀手）mod 合集，基于 **BepInEx 5** + **Harmony**。

游戏版本：**1.9.1**（Unity 2020.3.34f1，Mono x64）

---

## 包含的 mod

### 1. DestinyBone —— 四档骨头进化链

新增一条完整的可进化道具链，四档全部是「骨头 ×2」刻印，开局在 1-1 掉落第一档，靠条件逐级进化。

| 档 | 名称 | 稀有度 | 全属性 | 特殊效果 | 进化条件 |
|---|---|---|---|---|---|
| 0 | 人魂之骨 | 普通 | +10% / 受伤 -10% | — | 击杀 50 |
| 1 | 地魄之骨 | 稀有 | +15% / 受伤 -15% | 吸血 5%、护盾上限 10% | 击杀 100 + 获得 500 金币 |
| 2 | 天命之骨 | 独特 | +20% / 受伤 -20% | 吸血 5%、护盾上限 20%、刻印 +1 | 击杀 200 + 获得 2000 金币 + 触发 50 次负面状态 |
| 3 | 神话之骨 | 传说 | +25% / 受伤 -25% | 吸血 10%、护盾上限 25%、刻印 +2、首次死亡满血复活 | 终点 |

- **属性方向**：受伤类 ×0.75（受伤 -25%），其余 43 项统一 ×1.25（含冷却加速）
- **负面状态**：攻击时 6 种状态（眩晕 / 冰冻 / 燃烧 / 创伤 / 中毒 / 定身）**各自独立** roll，概率等于当前档位幅度
- **吸血**：溢出治疗转护盾，超过上限的部分丢弃
- **获取**：四档 `obtainable = false`，不进任何掉落池 / 商店 / 事件；只在 1-1 脚下掉一次
- **进化条件**：三条计数为 AND 关系，进化后清零

### 2. DevMenuZhFix —— DevMenuTurbo 补充汉化

`lostone/DevMenuTurbo_ZH` 只汉化了主面板的 24 个控件，其余四个面板全是原文。本插件补齐：

- 主面板：章节 1~6 按钮、「通关次数」滑条、`Map/Seed`、`version`、复活计数（原为韩文）
- 装备清单：头骨 / 道具 / 精华 / 升级、两个开关、搜索框
- 数据控制面板：12 个按钮 + 2 个滑条（原作整个面板没翻）
- 日志面板：复制 / 清除

**依赖**：必须先装 `DevMenuTurbo`，本插件只做文本补充。

---

## 安装

1. 游戏先装好 **BepInEx 5.4.21+**（[BepInExPack_Skul](https://thunderstore.io/c/skul-the-hero-slayer/p/BepInEx/BepInExPack_Skul/)）
2. 到 [Releases](https://github.com/sola-lune/SkulMods/releases) 下载对应 DLL
3. 放进 `Skul/BepInEx/plugins/<ModName>/` 目录

```
BepInEx/
└── plugins/
    ├── DestinyBone/DestinyBone.dll
    └── DevMenuZhFix/DevMenuZhFix.dll
```

---

## 编译

需要 .NET SDK（6.0+）与 `Microsoft.NETFramework.ReferenceAssemblies`（已写在 csproj 里）。

```powershell
# 默认游戏路径 D:\Game\Learning\Skul
.\build.ps1

# 指定其它游戏目录
.\build.ps1 -SkulDir "E:\SteamLibrary\steamapps\common\Skul"
```

或单独构建：

```bash
dotnet build src/DestinyBone -c Release -p:SkulDir="你的游戏目录"
```

csproj 里的 `SkulDir` 用于引用游戏自身的程序集（`Assembly-CSharp.dll`、`UnityEngine.*.dll`），**仓库不含任何游戏文件**。

---

## 目录结构

```
Skul/
├── src/
│   ├── DestinyBone/          # 骨头进化链
│   │   ├── Plugin.cs         # 道具定义、注入、效果、进化
│   │   ├── ItemPages.cs      # 道具栏 3 页翻页 + 棱形页码指示
│   │   ├── Resources/        # 图标（见下方版权说明）
│   │   ├── README.md         # 实现细节与已知限制
│   │   └── 进化机制与链路.md  # VOTC 进化机制的完整拆解
│   └── DevMenuZhFix/         # DevMenuTurbo 补充汉化
├── docs/                     # 使用文档
├── build.ps1
└── README.md
```

---

## 版权与致谢

- **代码**：本项目代码为原创，可自由使用、修改、分发。
- **DestinyBone 图标**（`src/DestinyBone/Resources/*.png`）：提取自 **VoiceOfTheCommunity** mod（作者 damku1214），仅作个人自用。**如需公开发布或商用，请替换为自制素材。** 若原作者提出异议，会立即移除。
- **DevMenuZhFix** 是对 [DevMenuTurbo](https://thunderstore.io/c/skul-the-hero-slayer/p/MrBacanudo/DevMenuTurbo/)（作者 MrBacanudo）与 [其汉化版](https://thunderstore.io/c/skul-the-hero-slayer/p/lostone/DevMenuTurbo_ZH/)（作者 lostone）的补充，本体不含它们的任何代码。
- 游戏本体与相关资源版权归 **SouthPAW Games** 所有。本项目不包含任何游戏文件。
