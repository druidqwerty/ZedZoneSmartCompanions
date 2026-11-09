# Smart Companions 1.3.1 — F8 interface hotfix

## English

- Fixed blank companion cards, base-container lists and settings. The shipped Unity IL2CPP build strips `GUI.BeginScrollView`/`EndScrollView`; those wrappers compiled but threw at runtime. Replaced them with native clipping groups and independent wheel/drag/button scroll controls.
- Fixed missing Cyrillic and Chinese glyphs. The public dynamic-font constructors were also stripped. The retained native font creation call now supplies a persistent font validated for Latin, Cyrillic and Chinese characters. Unsupported emoji controls use text.
- F8 now captures native player input rather than relying on `Event.Use()`. Chest/HUD controls block world mouse actions under the pointer. Closing clicks stay captured until release.
- Guarded the actual Human controller attack overrides, so player clicks cannot trigger firearm, melee or held-melee attacks while the panel owns input. Companion attacks remain unaffected.
- Isolated and restored ambient GUI scale/colors/enabled state. Added scrolling to the settings column, no-wrap card headings and scene-safe player access.
- GUI failures preserve their stack trace and rate-limit repeated warnings. Plugin, assembly, manifests and config-summary version now agree.
- Existing saves and settings are retained. Install the new DLL with the game closed. No new chatter lines or gameplay-expansion features in this hotfix.

[Verification details and limits](verification-1.3.1.md) · [Steam feedback reviewed](feedback-1.3.1.md) · [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312)

© 2026 druidqwerty. MIT License. Free and open source; AI-assisted code, direction and testing by druid.

---

## Русский

- Исправлены пустые карточки напарников, списки ящиков и настройки. В игровой сборке Unity IL2CPP вырезаны `GUI.BeginScrollView`/`EndScrollView`: код собирался, но падал при отрисовке. Вместо них используются доступные нативные группы отсечения и прокрутка колесом, кнопками и перетаскиванием.
- Исправлены квадраты вместо кириллицы и китайских символов. Публичные конструкторы динамического шрифта тоже были вырезаны. Шрифт создаётся через сохранённый нативный вызов с проверкой латиницы, кириллицы и китайских знаков. Неподдерживаемые эмодзи заменены текстом.
- F8 перехватывает настоящее управление игрока, а не только IMGUI-события. Панели ящика и HUD блокируют игровые действия мыши под указателем. Закрывающий клик удерживается до отпускания кнопки.
- Защищены реальные методы атаки Human-контроллера: стрельба, ближний бой и удерживаемый удар игрока блокируются, пока панель принимает ввод. Атаки напарников не блокируются.
- Интерфейс изолирует и восстанавливает масштаб, цвета и состояние GUI. Добавлены прокрутка настроек, однострочные заголовки карточек и безопасный доступ к игроку при смене сцены.
- Ошибки GUI записываются со стеком и ограничением повторов. Версии плагина, DLL, манифестов и сводки конфига синхронизированы.
- Сохранения и настройки остаются на месте. Для установки новой DLL закройте игру. Новых реплик и дополнительных игровых механик в этом исправлении нет.

[Проверки и ограничения](verification-1.3.1.md) · [Прочитанные отзывы Steam](feedback-1.3.1.md) · [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3836613312)

© 2026 druidqwerty. Лицензия MIT. Бесплатный открытый мод; код с помощью ИИ, идея и тестирование — druid.
