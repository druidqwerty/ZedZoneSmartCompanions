> UNRELEASED 1.3.2 candidate: native menu checks passed, ranged-combat acceptance FAILED. See docs/verification-1.3.2.md. Published/installed stable version remains 1.3.1.

# ZED ZONE: Smart Companions — 智能队友、基地分类整理与 980+ 实时互动语录

<p align="center"><a href="https://boosty.to/lrgpp"><img src="https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png" alt="LrgPP mods on Boosty" width="640"></a></p>
<p align="center"><em>喜欢这个模组吗？欢迎在 <a href="https://boosty.to/lrgpp">Boosty</a> 上支持开发。</em></p>

<p align="center"><img src="docs/preview.png" alt="ZED ZONE: Smart Companions cover" width="420"></p>

<p align="center">
  <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312"><strong>Steam 创意工坊 (3836613312)</strong></a> ·
  <a href="https://github.com/druidqwerty/ZedZoneSmartCompanions/releases/tag/v1.3.2"><strong>下载 v1.3.2 发布包 (ZIP)</strong></a> ·
  <a href="README.md"><strong>English (README.md)</strong></a> ·
  <a href="README_RU.md"><strong>Русский (README_RU.md)</strong></a>
</p>

---

## v1.3.2 修复

- **游戏原生队友指令菜单：**修复空白分类和指令名称，保留原版指令。这与 1.3.1 的 F8 窗口修复不同。
- **威胁反应：**修复已存储的战斗与武器偏好指令，每 0.3 秒刷新威胁感知，仍受武器射程和原生视线检查限制；不会强制穿墙开火，也不会覆盖停止开火命令。
- **减少战斗干扰：**发现威胁时暂停物资整理；只有玩家瞄准时才避让射界；减少风筝移动对开火的打断。
- **拾取与背包：**以队友位置搜索物资；仅在目标背包能完整容纳时转移物品；经验会在晋级之间保存，合并频繁写入并在退出时完成最后一次保存。
- **口粮：**手动分享为每位获食队友消耗一个食物物品，每位队友分别有 30 秒冷却；自动分享使用玩家已经吃掉的食物。
- 无需重置存档，本次不增加台词。安装时关闭游戏一次，之后设置仍可即时调整。[测试记录与限制](docs/verification-1.3.2.md)。


## v1.3.1 修复

- 修复 F8 队友卡片、基地储物箱与设置页面的空白内容。
- 检查字体是否支持中文和俄文字形，避免方框；按钮中的不支持表情改为文字。
- 点击 F8 窗口或模组面板时不再触发角色攻击，包括关闭窗口的一次点击。
- 界面异常保留调用堆栈，不再刷屏。无需重置存档。
- [测试记录与限制](docs/verification-1.3.1.md)。


**ZED ZONE: Smart Companions（智能队友与基地物流中心）** 是一款基于 **BepInEx 6 (IL2CPP)** 的《ZED ZONE》代码模组，彻底升级你招募的幸存者队友：提供针对丧尸与人类匪徒的智能战术战斗 AI、带 **12 大物资分类** 的基地储物箱自动分拣系统、**60 个情景话题共 1,179 条三语实时气泡台词（其中包含 324 条中文台词：190 条末日粗口 18+ 与 134 条纯净台词）**、6 种战术学说、战地急救与弹药支援，以及 **语言自动识别与游戏内一键切换（🌐 自动 / 🇷🇺 Русский / 🇬🇧 English / 🇨🇳 简体中文）**。

## v1.3.0 更新亮点

- **完整简体中文支持 + 自动语言识别与手动切换 (`ModLanguage.Auto / Russian / English / Chinese`)：**
  - 默认处于 **🌐 自动 (`Auto`)** 模式：自动读取《ZED ZONE》游戏语言 (`GameController.instance.gameSettings.languageCode`) 或系统语言 (`Application.systemLanguage`)，当游戏设置为中文时自动切换为简体中文界面与中文气泡台词。
  - 在 **F8** 主窗口顶部标题栏与第 3 页设置面板中，均可随时点击 **🌐 Авто/Auto/自动 · 🇷🇺 Русский · 🇬🇧 English · 🇨🇳 简体中文** 立即切换语言，无需重启游戏（自动保存在 `BepInEx/config/SmartCompanions_Rules.txt` 与 `SmartCompanionsConfig.json` 中）。
  - 动态加载支持中日韩与西里尔字符的系统字体 (`Microsoft YaHei`, `Segoe UI`, `SimHei`, `Arial`)，确保中文 UI 与气泡清晰显示。
- **50 个情景话题全部汉化（263 条独特中文台词：159 条末日粗口 18+ / 104 条纯净版）：**
  - 包含吃独食牛排吐槽（*“好吃吗你他妈的？！自己一个人偷吃牛排，老子连肉味都没闻过！”* / *“一定很好吃吧？是吧？我以前还从来没尝过真正的牛排呢……”*）、生肉/罐头/鱼排/汽水/啤酒反应、包扎/装弹/修装备/烧烤/撬锁吐槽、遭遇尸潮/强盗/巨型变异体喊话、让开玩家枪线、枪械卡壳排障、背后偷袭雷达预警、战地急救与递弹药、分享口粮 (`F6`) 感谢、基地搬箱子发牢骚、背包超重吐槽、天气（大雨/血雨/昼夜）以及队友互怼与路人 NPC 台词。

## 核心功能

1. **智能战斗 AI (`SmartCombatAI`)：** 贴身保护玩家后背、人类枪手横向走位、巨型 Boss 保持距离、风筝后撤步射击、自动闪开玩家瞄准火线（防误伤走廊）、近身空仓自动拔刀、背后偷袭雷达预警、自动排障清卡壳。
2. **基地储物箱 12 分类自动整理 (`BaseSortingSystem`)：** 打开任意非玩家储物箱时屏幕上方自动显示 **12 分类栏**（近战武器、枪械与配件、弹药与弹匣、医疗与急救、生鲜与熟食、罐头与饮品、制作材料、工具与维修、服装与背包、载具与电力、农艺与宠物、爆炸物与杂项）与 **📥 收货中转箱** 开关。按 **F7** 命令队友将战利品按类别归位并自动紧凑排列 (`AutoSortMode.Dense`)；回基地脱战靠近箱子时还会自动卸货。
3. **分享口粮与「🔥 饱腹狂暴」增益 (`F6`)：** 按 **F6**（或在设置中开启进食自动分口粮）向小队分享食物，恢复队友饱食/饮水/精力并赋予 3 分钟 **「饱腹狂暴」** 战斗增益。
4. **6 种战术学说与原生右键菜单集成 (`F8` + `NPCCommandMenu`)：** 均衡战术、突击强攻、贴身护卫、狙击风筝、潜行近战、停止开火，全部集成至游戏原生队友环形菜单。

## 安装与快捷键

1. 订阅 **[ZED ZONE Code Mod Loader (BepInEx IL2CPP)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)**（创意工坊 ID `3834614258`），运行一次其中的 `install.cmd`。
2. 订阅本模组 **[ZED ZONE: Smart Companions](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312)**（或将 `ZedZoneSmartCompanions-1.3.2.zip` 解压至 `<ZED ZONE>\Mods\SmartCompanions\`）。
3. 快捷键：**`F8`** 打开三页智能队友与基地中心 · **`F7`** 命令整理基地储物箱 · **`F6`** 向小队分享口粮。

© 2026 druidqwerty. 基于 MIT 许可证开源发布。免费开源模组；AI 辅助代码编写，由 druid 设计与测试。
