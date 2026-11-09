import os
import subprocess
import urllib.parse
import urllib.request
import json

repo = r"C:\Users\druid\.openclaw\workspace-modder\projects\zedzone-smart-companions"
sha = "a889f354afa76bbae9852cd8a1b46520c73ddce1"
old_url = "https://raw.githubusercontent.com/druidqwerty/ALifeAirwaves/d2357132d52c903c5dadddb80fb66f64a32aff35/docs/banner.png"
new_url = f"https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/{sha}/docs/banner.png"

for rel in ["workshop/workshop.txt", "workshop/workshop_russian.txt"]:
    p = os.path.join(repo, rel)
    txt = open(p, encoding="utf-8").read().replace(old_url, new_url)
    open(p, "w", encoding="utf-8").write(txt)

for rel in ["README.md", "README_RU.md"]:
    p = os.path.join(repo, rel)
    txt = open(p, encoding="utf-8").read().replace('src="docs/banner.png"', f'src="{new_url}"')
    open(p, "w", encoding="utf-8").write(txt)

subprocess.run(["git", "add", "-A"], cwd=repo)
subprocess.run(["git", "commit", "-m", "docs: use immutable banner URL and Workshop ID 3836613312"], cwd=repo)
subprocess.run(["git", "push", "origin", "main"], cwd=repo, check=True)

notes = """## ZED ZONE: Smart Companions v1.0.0 (Initial Release)

### English
- **Smarter Combat AI vs. Zombies & Hostile NPCs (`SmartCombatAI`):** dynamic threat prioritization (protecting the player within `3.6 m`, prioritizing hostile human shooters and giant mutants, finishing off wounded targets), tactical backpedal kiting while firing, lateral strafing against armed raiders, Anti-Friendly-Fire corridor (stepping out of the player's aim line), and automatic CQB melee/ranged weapon switching.
- **Base Container Sorting & 12-Category Chest UI (`BaseSortingSystem` + `SmartCompanionsUI`):** docked category bar when opening any container (`LootInventoryPanel`) with 12 item categories, Drop-off Box mode, 1-click presets, auto-detection of categories from existing chest contents, and physical/instant companion sorting orders (`F7`).
- **44 Context Chatter Topics — 171 RU & 92 EN Lines (`ChatterDatabase` + `SmartChatterSystem`):** overhead speech bubble reactions when the player eats steak/meat, canned food, fish, or raw meat, drinks soda or lager, bandages wounds, reloads magazines, repairs gear/vehicles, cooks on a campfire, picks locks, fights hordes, or sorts the base, plus weather/time callouts and two-companion banter.
- **6 Tactical Doctrines & Field Support (`F8` Hub + Native Radial Menu):** *Balanced*, *Assault*, *Bodyguard*, *Sniper/Kite*, *Stealth Melee*, and *Passive* doctrines, **Field Medic** (emergency player dressing below 48% HP), **Field Ammo Supplier** (emergency ammo transfer when empty in combat), and **Sweep Nearby Loot**.
- **Required Loader:** [ZED ZONE Code Mod Loader (BepInEx IL2CPP)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258).
- **Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312

© 2026 druidqwerty. Released under the MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

### Русский
- **Умный боевой ИИ против зомби и вражеских NPC (`SmartCombatAI`):** динамическая оценка угроз (защита спины игрока в радиусе `3.6 м`, приоритет вооружённых мародёров и боссов, добивание подранков), стрельба на отходе (кайтинг), стрейф против стрелков, автоматический выход с линии огня игрока (Анти-Френдли-Фаер) и умная смена холодного/огнестрельного оружия.
- **Сортировка базы и интерфейс 12 категорий для ящиков (`BaseSortingSystem` + `SmartCompanionsUI`):** панель категорий при открытии любого ящика в игре, режим «Ящик-приёмник (Сброс хабара)», быстрые пресеты, авто-разметка ящиков по их содержимому и приказ напарникам рассортировать склад (`F7`).
- **44 контекстные темы живых реплик — 171 фраза на русском и 92 на английском (`ChatterDatabase` + `SmartChatterSystem`):** живые комментарии над головой NPC, когда игрок ест стейк или жареное мясо (*«Вкусно тебе наверное? Да? Я вот стейк никогда не пробовал...»*), консервы, рыбу или сырое мясо, пьёт газировку или лагер, перевязывается, снаряжает магазины, чинит вещи, жарит на костре, взламывает замки, сражается с ордой или сортирует склад, а также диалоги между двумя напарниками.
- **6 тактических доктрин и полевая поддержка (`F8` + родное меню приказов NPC):** доктрины *Умный баланс*, *Штурм*, *Телохранитель*, *Снайпер/Кайт*, *Тихий клинок*, *Не стрелять*, **Полевой медик** (экстренная перевязка игрока при HP < 48%), **Полевой снабженец** (подача патронов при пустом оружии в бою) и приказ **«Собрать трофеи вокруг»**.
- **Требуемый загрузчик:** [ZED ZONE Code Mod Loader (BepInEx IL2CPP)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258).
- **Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
"""

notes_file = os.path.join(repo, "dist", "RELEASE_NOTES_1.0.0.md")
open(notes_file, "w", encoding="utf-8").write(notes)
zip_file = os.path.join(repo, "dist", "ZedZoneSmartCompanions-1.0.0.zip")

r = subprocess.run(
    ["gh", "release", "create", "v1.0.0", zip_file, "--title", "ZED ZONE: Smart Companions v1.0.0", "--notes-file", notes_file],
    cwd=repo,
    capture_output=True,
    text=True,
)
print("GitHub Release:", r.stdout.strip(), r.stderr.strip())
