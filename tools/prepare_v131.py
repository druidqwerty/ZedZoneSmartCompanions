"""Prepare documentation/staging metadata only; no build, upload, or publication."""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
BANNER = "https://raw.githubusercontent.com/druidqwerty/ZedZoneSmartCompanions/a889f354afa76bbae9852cd8a1b46520c73ddce1/docs/banner.png"
LOADER = "https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258"
REPO = "https://github.com/druidqwerty/ZedZoneSmartCompanions"
WORKSHOP = "https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312"

updates = {
    "README.md": "## Fixed in v1.3.1\n\n- F8 companion cards, base containers and settings no longer go blank at the scrolling area.\n- Russian and Simplified Chinese labels use a glyph-checked font instead of missing-character squares; unsupported emoji buttons use readable text.\n- Clicking F8 or the mod's chest/HUD controls is isolated from world input, including the closing click.\n- GUI errors retain their full stack trace without flooding the log.\n- No save migration or reset required. See [verification and limitations](docs/verification-1.3.1.md).\n\n",
    "README_RU.md": "## Исправлено в v1.3.1\n\n- Карточки напарников, ящики базы и настройки F8 больше не исчезают при отрисовке прокрутки.\n- Для русского и китайского текста проверяется наличие символов в шрифте; неподдерживаемые эмодзи заменены читаемыми надписями.\n- Клики в F8 и по панелям мода отделены от игрового управления, включая закрывающий клик.\n- Ошибки интерфейса записываются со стеком вызовов, без спама в журнал.\n- Сброс или перенос сохранений не требуется. [Проверки и ограничения](docs/verification-1.3.1.md).\n\n",
    "README_CN.md": "## v1.3.1 修复\n\n- 修复 F8 队友卡片、基地储物箱与设置页面的空白内容。\n- 检查字体是否支持中文和俄文字形，避免方框；按钮中的不支持表情改为文字。\n- 点击 F8 窗口或模组面板时不再触发角色攻击，包括关闭窗口的一次点击。\n- 界面异常保留调用堆栈，不再刷屏。无需重置存档。\n- [测试记录与限制](docs/verification-1.3.1.md)。\n\n",
}
for name, section in updates.items():
    path = ROOT / name
    text = path.read_text(encoding="utf-8").replace("/tag/v1.3.0", "/tag/v1.3.1").replace("Release v1.3.0 ZIP", "Release v1.3.1 ZIP").replace("下载 v1.3.0", "下载 v1.3.1").replace("релиз v1.3.0", "релиз v1.3.1").replace("ZedZoneSmartCompanions-1.3.0.zip", "ZedZoneSmartCompanions-1.3.1.zip").replace("?id=3836614258", "?id=3834614258")
    # Install archive contains Mods/, so extract at the game root, not inside Mods/SmartCompanions.
    text = text.replace("into `<ZED ZONE>\\Mods\\SmartCompanions\\`", "into the `<ZED ZONE>` game folder")
    if section not in text:
        anchor = text.find("\n---\n")
        if anchor >= 0:
            anchor += len("\n---\n")
            text = text[:anchor] + "\n" + section + text[anchor:]
    path.write_text(text, encoding="utf-8")

pages = {
    "workshop.txt": ("ZED ZONE: Smart Companions 1.3.1", [
        "[h1]Smart Companions 1.3.1[/h1]",
        "Smarter recruited companions: tactical combat, base sorting and live chatter in English, Russian and Simplified Chinese.",
        f"[url=https://boosty.to/lrgpp][img]{BANNER}[/img][/url]",
        "Enjoy the mod? Support development on Boosty.",
        "[h2]Fixed in 1.3.1[/h2]",
        "[list][*]F8 no longer shows blank companion, chest or settings panels.[*]Readable Russian and Chinese labels instead of missing-character squares.[*]Clicking the mod's panels no longer makes your character attack the base.[/list]",
        "[h2]Your squad and base[/h2]",
        "6 combat doctrines, companion personalities and veteran ranks, field aid, camp chores, vehicle unloading, 12-category chest sorting and RU/EN/ZH chatter with 18+, Mixed or Clean modes. All settings change in-game.",
        "[h2]Install and controls[/h2]",
        f"Required third-party dependency: [url={LOADER}]ZED ZONE Code Mod Loader[/url] (not a mod by druidqwerty). Subscribe and run its install.cmd once, then launch through Steam.",
        "F8: squad/base/settings. F7: sort base. F6: share rations. F9: battle cry. No save reset required. Code mods are local and do not auto-sync between co-op players.",
        f"[url={REPO}]Source, release downloads and detailed checks[/url]",
        "© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.",
    ]),
    "workshop_russian.txt": ("ZED ZONE: Smart Companions 1.3.1 — Умные напарники", [
        "[h1]Умные напарники 1.3.1[/h1]",
        "Тактический бой, сортировка базы и живые реплики нанятых выживших на русском, английском и китайском.",
        f"[url=https://boosty.to/lrgpp][img]{BANNER}[/img][/url]",
        "Нравится мод? Поддержать разработку можно на Boosty.",
        "[h2]Исправлено в 1.3.1[/h2]",
        "[list][*]Карточки напарников, ящики и настройки F8 больше не пропадают.[*]Русские и китайские надписи отображаются без квадратов.[*]Клики по панелям мода больше не заставляют персонажа бить и ломать базу.[/list]",
        "[h2]Отряд и база[/h2]",
        "6 боевых доктрин, характеры и ветеранские ранги, полевая помощь, быт лагеря, разгрузка машин, сортировка по 12 категориям и живые реплики RU/EN/ZH. Режим речи: Мат 18+, Умеренный или Без мата. Настройки меняются в игре.",
        "[h2]Установка и управление[/h2]",
        f"Обязательная сторонняя зависимость: [url={LOADER}]ZED ZONE Code Mod Loader[/url] (это не мод druidqwerty). Подпишитесь, один раз запустите install.cmd и запускайте игру через Steam.",
        "F8 — отряд/база/настройки; F7 — сортировка; F6 — паёк; F9 — боевой клич. Сброс сохранений не нужен. Кодовые моды работают локально и не синхронизируются автоматически в кооперативе.",
        f"[url={REPO}]Исходники, архив релиза и подробные проверки[/url]",
        "© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.",
    ]),
    "workshop_schinese.txt": ("ZED ZONE: Smart Companions 1.3.1 — 智能队友", [
        "[h1]智能队友 1.3.1[/h1]",
        "招募队友的战术战斗、基地物资分类与实时互动台词，支持简体中文、俄语和英语。",
        f"[url=https://boosty.to/lrgpp][img]{BANNER}[/img][/url]",
        "喜欢这个模组吗？欢迎在 Boosty 支持开发。",
        "[h2]1.3.1 修复[/h2]",
        "[list][*]修复 F8 队友、储物箱与设置页面空白。[*]修复中文与俄文字体显示方框。[*]点击模组面板不再触发角色攻击，避免破坏基地。[/list]",
        "6 种战术、队友性格与老兵等级、战地急救、营地内务、汽车卸货、12 类储物箱整理和三语台词。可选末日粗口18+、混合或纯净模式，设置即时生效。",
        f"[h2]安装与操作[/h2]需要第三方依赖 [url={LOADER}]ZED ZONE Code Mod Loader[/url]（并非 druidqwerty 制作的模组）：订阅后运行一次 install.cmd，通过 Steam 启动游戏。",
        "F8：队友/基地/设置；F7：整理；F6：分享口粮；F9：战吼。无需重置存档。代码模组在本地运行，不会自动同步到联机队友。",
        f"[url={REPO}]源码、下载与详细测试记录[/url]",
        "© 2026 druidqwerty。MIT 许可证。免费开源；代码由 AI 辅助，创意与测试由 druid 完成。",
    ]),
}
for name, (title, lines) in pages.items():
    header = "version=1\nid=3836613312\n" if name == "workshop.txt" else ""
    (ROOT / "workshop" / name).write_text(header + f"title={title}\nvisibility=public\n" + "\n".join("description=" + line for line in lines) + "\n", encoding="utf-8")

meta = {"name": "ZED ZONE Smart Companions", "author": "Modsi", "version": "1.3.1", "enabled": True}
(ROOT / "workshop/Contents/SmartCompanions/mod.json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")
print("Prepared 1.3.1 READMEs, 3 Workshop locales and staging manifest (not published).")
