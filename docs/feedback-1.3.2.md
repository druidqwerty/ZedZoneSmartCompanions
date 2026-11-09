# Public Steam feedback for 1.3.2

Retrieved anonymously: 2026-11-09T19:09:33.741688+00:00

## Summary

- Public item feed still contains seven comments: Chinese missing-glyph complaint, independent gathering-trip request, praise, earlier translation request and loader-discoverability complaint. No new public combat-reaction complaint appears in this feed; current owner message supplies that report.
- Both standard discussions remain pinned with zero replies. No public replies or other mutations were made.
- Current owner screenshot shows the native NPC command context menu, not the F8 IMGUI window. Previous F8 fixture checks did not cover this native menu.

## Independent code audit (not runtime reproduction)

- `AutoLootSystem.cs:32`: even a specific-agent/passive sweep is centered on the player, not the named NPC; remote companions can collect around the player outside their own nearby radius.
- `BaseSortingSystem.cs:531`: destination `TryAddItem` exposes `partiallyAdded` but the transfer ignores it. Partial-stack native behavior requires a fixture test to determine exact duplication/loss cases; do not count this as reproduced before that test.
- `AutoLootSystem.cs:216`: failed additions fall through to another inventory without reconciling potential partial merges; same verification requirement.
- `CompanionModels.cs:266`: experience is saved in the rank-up branch only, with no controller shutdown/save hook, so ordinary between-rank progress is not durably written unless another action saves configuration.
- `SmartChatterSystem.cs:186`: manual ration sharing never consumes a food item and has no cooldown; repeated F6 replenishes nutrition and energy for free. This is an implementation fact; changing its intended balance needs an explicit decision by the implementation owner.

## Comments

Source: https://steamcommunity.com/comment/PublishedFile_Public/render/76561198068633311/3836613312/?start=0&count=100

total_count=7

 乄十年，一个轮回-/ 

 3 hours ago 

 为什么我游戏里mod的字体全部错误？全是显示“口” 

 乄十年，一个轮回-/ 

 3 hours ago 

 能指令队友独自外出收集物资吗？游戏本身的队友指令感觉没效果，我想让队友去采集物质但还是呆呆的跟着我 

 star fox 

 4 hours ago 

 喜欢 

 Иван Тугаринский 

 5 hours ago 

 Will translate. No problem :) 

 君雅 

 5 hours ago 

 Thank you for creating this mod. As it does not have a Chinese version, I translated it using AI for personal use only. I will not share or publish it on any community. Hope you don’t mind. I really appreciate your work! 

 君雅 

 7 hours ago 

 I failed to find the necessary runtime file and cannot open it within the community. 

 Иван Тугаринский 

 10 hours ago 

 Test version. Please submit all bugs. 

## Discussions

Source: https://steamcommunity.com/forum/40676593/PublishedFile/render/3836613312/?start=0&count=30

total_count=2

 0 
 
 9 hours ago 

 PINNED: 
 Ideas & Suggestions / Идеи и предложения 
 
 Иван Тугаринский 

 0 
 
 9 hours ago 

 PINNED: 
 Error Logs & Bug Reports / Логи ошибок и баги 
 
 Иван Тугаринский 

