using System;
using System.Collections.Generic;

namespace ZedZone.SmartCompanions
{
    public enum NpcBiologicalGender
    {
        Unknown = 0,
        Male = 1,
        Female = 2
    }

    public readonly struct NpcChatterPersona : IEquatable<NpcChatterPersona>
    {
        public readonly NpcBiologicalGender Gender;
        public readonly bool HasNoveltyHeadgear;

        public bool IsFemale => Gender == NpcBiologicalGender.Female;
        public bool AvoidGruffMacho => IsFemale || HasNoveltyHeadgear;

        internal int CacheKey => ((int)Gender << 1) | (HasNoveltyHeadgear ? 1 : 0);

        public NpcChatterPersona(NpcBiologicalGender gender, bool hasNoveltyHeadgear = false)
        {
            Gender = gender;
            HasNoveltyHeadgear = hasNoveltyHeadgear;
        }

        public static readonly NpcChatterPersona Neutral = new NpcChatterPersona(NpcBiologicalGender.Unknown, false);
        public static readonly NpcChatterPersona Male = new NpcChatterPersona(NpcBiologicalGender.Male, false);
        public static readonly NpcChatterPersona Female = new NpcChatterPersona(NpcBiologicalGender.Female, false);
        public static readonly NpcChatterPersona AnimeHoodMale = new NpcChatterPersona(NpcBiologicalGender.Male, true);
        public static readonly NpcChatterPersona AnimeHoodFemale = new NpcChatterPersona(NpcBiologicalGender.Female, true);

        public bool Equals(NpcChatterPersona other) => Gender == other.Gender && HasNoveltyHeadgear == other.HasNoveltyHeadgear;
        public override bool Equals(object obj) => obj is NpcChatterPersona other && Equals(other);
        public override int GetHashCode() => CacheKey;
    }

    public static partial class ChatterDatabase
    {
        private readonly struct PersonaCacheSlot : IEquatable<PersonaCacheSlot>
        {
            public readonly ChatterTopic Topic;
            public readonly ModLanguage Lang;
            public readonly ProfanityMode Mode;
            public readonly int PersonaKey;

            public PersonaCacheSlot(ChatterTopic topic, ModLanguage lang, ProfanityMode mode, int personaKey)
            {
                Topic = topic;
                Lang = lang;
                Mode = mode;
                PersonaKey = personaKey;
            }

            public bool Equals(PersonaCacheSlot other)
                => Topic == other.Topic && Lang == other.Lang && Mode == other.Mode && PersonaKey == other.PersonaKey;

            public override bool Equals(object obj) => obj is PersonaCacheSlot other && Equals(other);

            public override int GetHashCode()
                => ((((int)Topic * 397) ^ (int)Lang) * 397 ^ (int)Mode) * 7 ^ PersonaKey;
        }

        private static readonly object PersonaCacheLock = new object();
        private static readonly Dictionary<PersonaCacheSlot, string[]> PersonaLineCache = new Dictionary<PersonaCacheSlot, string[]>();

        public static string[] GetLines(ChatterTopic topic)
        {
            return GetLines(topic, CompanionConfigStore.EffectiveLanguage, CompanionConfigStore.Profanity, NpcChatterPersona.Neutral);
        }

        public static string[] GetLines(ChatterTopic topic, bool ru)
        {
            return GetLines(topic, ru ? ModLanguage.Russian : ModLanguage.English, CompanionConfigStore.Profanity, NpcChatterPersona.Neutral);
        }

        public static string[] GetLines(ChatterTopic topic, bool ru, ProfanityMode mode)
        {
            return GetLines(topic, ru ? ModLanguage.Russian : ModLanguage.English, mode, NpcChatterPersona.Neutral);
        }

        public static string[] GetLines(ChatterTopic topic, ModLanguage lang, ProfanityMode mode)
        {
            return GetLines(topic, lang, mode, NpcChatterPersona.Neutral);
        }

        public static string[] GetLines(ChatterTopic topic, ModLanguage lang, ProfanityMode mode, NpcChatterPersona persona)
        {
            if (lang == ModLanguage.Auto)
                lang = CompanionConfigStore.EffectiveLanguage;

            ProfanityMode effectiveMode = persona.HasNoveltyHeadgear ? ProfanityMode.Clean : mode;

            if (!persona.AvoidGruffMacho)
                return GetBaseLines(topic, lang, effectiveMode);

            var key = new PersonaCacheSlot(topic, lang, effectiveMode, persona.CacheKey);
            lock (PersonaCacheLock)
            {
                if (PersonaLineCache.TryGetValue(key, out var cached))
                    return cached;
            }

            string[] raw = GetBaseLines(topic, lang, effectiveMode);
            if (raw == null || raw.Length == 0)
                return raw;

            var adapted = new string[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                adapted[i] = AdaptLineForPersona(raw[i], lang, persona);

            lock (PersonaCacheLock)
            {
                PersonaLineCache[key] = adapted;
            }
            return adapted;
        }

        private static string[] GetBaseLines(ChatterTopic topic, ModLanguage lang, ProfanityMode mode)
        {
            if (TryGetV130Lines(topic, lang, mode, out var v130Lines))
                return v130Lines;

            string[] clean = lang switch
            {
                ModLanguage.Russian => GetLinesRu(topic),
                ModLanguage.Chinese => GetCleanLinesZh(topic),
                _ => GetLinesEn(topic)
            };

            if (mode == ProfanityMode.Clean)
                return clean;

            string[] rude = lang switch
            {
                ModLanguage.Russian => AppendExtraRudeRu(topic, GetRudeLinesRu(topic)),
                ModLanguage.Chinese => GetRudeLinesZh(topic),
                _ => AppendExtraRudeEn(topic, GetRudeLinesEn(topic))
            };

            if (mode == ProfanityMode.HardcoreRude)
                return (rude != null && rude.Length > 0) ? rude : clean;

            // Mixed mode: combine rude + clean lines
            if (rude == null || rude.Length == 0) return clean;
            if (clean == null || clean.Length == 0) return rude;
            var combined = new string[rude.Length + clean.Length];
            Array.Copy(rude, 0, combined, 0, rude.Length);
            Array.Copy(clean, 0, combined, rude.Length, clean.Length);
            return combined;
        }

        public static string AdaptLineForPersona(string line, ModLanguage lang, NpcChatterPersona persona)
        {
            if (string.IsNullOrEmpty(line) || !persona.AvoidGruffMacho)
                return line;

            if (lang == ModLanguage.Auto)
                lang = CompanionConfigStore.EffectiveLanguage;

            return lang switch
            {
                ModLanguage.Chinese => AdaptZhLineForPersona(line),
                ModLanguage.Russian => AdaptRuLineForPersona(line, persona),
                _ => AdaptEnLineForPersona(line, persona)
            };
        }

        private static string AdaptZhLineForPersona(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf("兄弟", StringComparison.Ordinal) >= 0)
            {
                s = s.Replace("谢了兄弟", "谢了队长")
                     .Replace("给兄弟", "给队友")
                     .Replace("记着兄弟", "记着队友")
                     .Replace("兄弟", "队友");
            }
            if (s.IndexOf("你小子", StringComparison.Ordinal) >= 0)
                s = s.Replace("你小子", "你这家伙");
            if (s.IndexOf("老子", StringComparison.Ordinal) >= 0)
            {
                s = s.Replace("老子的", "我的")
                     .Replace("老子", "我");
            }
            return s;
        }

        private static string AdaptEnLineForPersona(string s, NpcChatterPersona persona)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string brotherReplacement = persona.IsFemale ? "sister" : "partner";
            if (s.IndexOf("brother", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s = s.Replace("Respect, brother!", $"Respect, {brotherReplacement}!")
                     .Replace(", brother!", $", {brotherReplacement}!")
                     .Replace(", brother", $", {brotherReplacement}")
                     .Replace("brother", brotherReplacement);
            }
            if (persona.AvoidGruffMacho)
            {
                s = s.Replace("what the fuck, man?!", "what the fuck?!")
                     .Replace("Man, sitting by the fire", "Sitting by the fire");
            }
            return s;
        }

        private static readonly (string From, string To)[] RuFemaleReplacements = new[]
        {
            ("За пацанов и чтоб хуй стоял и патроны были! Вздрогнем, блядь!", "За наш отряд, боевой фарт и полные магазины! Вздрогнем, блядь!"),
            ("Ебать мой хуй, сколько их тут вывалило!", "Охуеть, сколько их тут вывалило!"),
            ("С пустым стволом тут только хуй сосать остаётся!", "С пустым стволом тут даже отбиться нечем!"),
            ("В такую холодину без огня можно и бубенцы отморозить, блядь!", "В такую холодину без огня до костей промёрзнуть можно, блядь!"),
            ("Грей клешни у костра, братва...", "Грей руки у костра, отряд..."),
            ("ЗАРЯЖАЙ И ВПЕРЁД, БРАТВА!", "ЗАРЯЖАЙ И ВПЕРЁД, ОТРЯД!"),
            ("Пацаны не местные нарисовались", "Чужаки не местные нарисовались"),
            ("Вскрываем хату, пацаны!", "Вскрываем хату, ребята!"),
            ("нормальный пацан,", "свой человек,"),
            ("по-пацански", "по-нашему"),
            ("по-братски", "по-дружески"),
            ("братуха", "командир"),
            ("братан", "командир"),
            ("а братве даже косточки", "а своим даже косточки"),
            ("дыши, родной!", "дыши, командир!"),
            ("Я тебе чё, ишак вьючный,", "Я тебе чё, ломовая лошадь,"),
            ("нанимался в колхозники", "нанималась в фермеры"),
            ("выбраться отсюда живым.", "выбраться отсюда живой."),
            ("я первый в очереди", "я первая в очереди"),
            ("я ж тебя сам на угли посажу", "я ж тебя сама на угли посажу"),
            ("сытый и пиздец какой злой", "сытая и пиздец какая злая"),
            ("давиться должен!", "давиться должна!"),
            ("Устал я как собака", "Устала я как собака"),
            ("Устал как собака", "Устала как собака"),
            ("Готов! Минус один", "Готова! Минус один"),
            ("готов к любым испытаниям!", "готова к любым испытаниям!"),
            ("Ранен, но ещё в строю!", "Ранена, но ещё в строю!"),
            ("Ранен, сука!", "Ранена, сука!"),
            ("стал выносливее и крепче!", "стала выносливее и крепче!"),
            ("Тихо спиздил и ушёл — называется нашёл!", "Тихо спиздила и ушла — называется нашла!"),
            ("Глянь чё нашёл,", "Глянь чё нашла,"),
            ("Я заебался в доску", "Я заебалась в доску"),
            ("Заебался я сегодня", "Заебалась я сегодня"),
            ("я не предупреждал!", "я не предупреждала!"),
            ("я вот стейк никогда не пробовал", "я вот стейк никогда не пробовала"),
            ("сухпайков не видел.", "сухпайков не видела."),
            ("я часто жарил такие стейки", "я часто жарила такие стейки"),
            ("от кусочка лосося не отказался...", "от кусочка лосося не отказалась..."),
            ("Сто лет не держал в руках", "Сто лет не держала в руках"),
            ("химозу сладкую не пил!", "химозу сладкую не пила!"),
            ("я уже ложку приготовил.", "я уже ложку приготовила."),
            ("я ствол навёл!", "я ствол навела!"),
            ("я за сегодня уложил больше", "я за сегодня уложила больше"),
            ("я сегодня больше этих гнилых пидоров уложил", "я сегодня больше этих гнилых пидоров уложила"),
            ("я успел!", "я успела!"),
            ("я последний раз такой кусок мяса видел", "я последний раз такой кусок мяса видела"),
            ("угли жрать я не подписывался!", "угли жрать я не подписывалась!"),
            ("С линии огня ушёл,", "С линии огня ушла,"),
            ("Ушёл вбок", "Ушла вбок"),
            ("клин поймал!", "клин поймала!"),
            ("Клин поймал на ровном месте", "Клин поймала на ровном месте"),
            ("Поймал клин затвора!", "Поймала клин затвора!"),
            ("движок и колёса проверил, командир!", "движок и колёса проверила, командир!"),
            ("проверил состояние кузова!", "проверила состояние кузова!"),
            ("привёл экипировку отряда в порядок и восстановил часть", "привела экипировку отряда в порядок и восстановила часть"),
            ("Принял!", "Приняла!"),
            ("Навёл порядок", "Навела порядок"),
            ("Забрал несортированный", "Забрала несортированный"),
            ("Забрал твоё барахло", "Забрала твоё барахло"),
            ("Разложил пушки", "Разложила пушки"),
            ("разложил по полочкам", "разложила по полочкам"),
            ("разложил твоё барахло", "разложила твоё барахло"),
            ("разложил по складу", "разложила по складу"),
            ("разложил по категориям", "разложила по категориям"),
            ("разложил ровненько", "разложила ровненько"),
            ("убрал на хранение", "убрала на хранение"),
            ("убрал на склад", "убрала на склад"),
            ("Сложил стройматериалы", "Сложила стройматериалы"),
            ("сложил в", "сложила в"),
            ("сложил отдельно", "сложила отдельно"),
            ("перенёс в профильный ящик", "перенесла в профильный ящик"),
            ("Перенёс все трофеи", "Перенесла все трофеи"),
            ("Подобрал припасы!", "Подобрала припасы!"),
            ("Выгрузил собранные", "Выгрузила собранные"),
            ("Выгрузил трофеи", "Выгрузила трофеи"),
            ("Сгрузил всю добычу", "Сгрузила всю добычу"),
            ("Скинул весь собранный", "Скинула весь собранный"),
            ("уложил как в аптеке", "уложила как в аптеке"),
            ("уложил в продуктовый", "уложила в продуктовый"),
            ("уложил в рабочие", "уложила в рабочие"),
            ("Уложил! Кто следующий?", "Уложила! Кто следующий?"),
            ("закинул в оружейку", "закинула в оружейку"),
            ("закинул в медицинский", "закинула в медицинский"),
            ("закинул в топку", "закинула в топку"),
            ("закинул сладкого", "закинула сладкого"),
            ("Закинул хавки", "Закинула хавки"),
            ("распихал по продуктовым", "распихала по продуктовым"),
            ("распихал по мастерским", "распихала по мастерским"),
            ("Закончил ебаться", "Закончила ебаться"),
            ("Отогнал их!", "Отогнала их!"),
            ("Отогнал тварей!", "Отогнала тварей!"),
            ("Вколол стимулятор и наложил", "Вколола стимулятор и наложила"),
            ("Вколол стимулятор и перевязал", "Вколола стимулятор и перевязала"),
            ("Вколол адреналин и замотал", "Вколола адреналин и замотала"),
            ("Перевязал твои раны", "Перевязала твои раны"),
            ("Перевязал критические раны", "Перевязала критические раны"),
            ("Перевязал и откачал", "Перевязала и откачала"),
            ("Перевязал тебя на бегу", "Перевязала тебя на бегу"),
            ("Передал тебе запасные", "Передала тебе запасные"),
            ("Пожрал от пуза", "Поела от пуза"),
            ("Отпрыгнул в сторону", "Отпрыгнула в сторону"),
            ("Сектор освободил,", "Сектор освободила,"),
            ("Вынес нахуй!", "Вынесла нахуй!"),
            ("Прикрыл тебя", "Прикрыла тебя"),
            ("замотал своей аптечкой!", "замотала своей аптечкой!"),
            ("Заштопал твои дырки", "Заштопала твои дырки"),
            ("Отсыпал тебе свинца", "Отсыпала тебе свинца"),
            ("накатил пивка", "накатила пивка"),
            ("Разорвал дистанцию!", "Разорвала дистанцию!"),
            ("Пришил гада", "Пришила гада"),
            ("Залепил твои раны", "Залепила твои раны"),
            ("Подкинул тебе боекомплекта", "Подкинула тебе боекомплекта"),
            ("Сгрёб весь этот бардак", "Сгребла весь этот бардак"),
            ("Подбросил дровишек", "Подбросила дровишек"),
            ("Подбросил дров", "Подбросила дров"),
            ("Полил твою ботву", "Полила твою ботву"),
            ("полил и удобрил", "полила и удобрила"),
            ("Всыпал удобрений", "Всыпала удобрений"),
            ("Почистил затвор и смазал", "Почистила затвор и смазала"),
            ("Почистил и смазал наше оружие, подтянул", "Почистила и смазала наше оружие, подтянула"),
            ("Подлатал нашу снарягу и протёр", "Подлатала нашу снарягу и протёрла"),
            ("Подтянул ремни на броне и вычистил", "Подтянула ремни на броне и вычистила"),
            ("Подтянул гайки на кузове и перекинул", "Подтянула гайки на кузове и перекинула"),
            ("Проверил бойки и подправил", "Проверила бойки и подправила"),
            ("Проверил наши грядки: полил растения и добавил", "Проверила наши грядки: полила растения и добавила"),
            ("Осмотрел нашу ласточку и выгрузил", "Осмотрела нашу ласточку и выгрузила"),
            ("Осмотрел автомобиль и разгрузил", "Осмотрела автомобиль и разгрузила"),
            ("Вытряхнул всё добро из тачки в ящики и подстучал", "Вытряхнула всё добро из тачки в ящики и подстучала"),
            ("зажал артерию и всадил стимулятор", "зажала артерию и всадила стимулятор"),
            ("Остановил кровотечение и поднял тебя", "Остановила кровотечение и подняла тебя"),
            ("Вырубил свет", "Вырубила свет"),
            ("Погасил свет", "Погасила свет"),
            ("Поднял боевой ранг", "Подняла боевой ранг"),
            ("Повысил свой ветеранский ранг", "Повысила свой ветеранский ранг"),
            ("Провёл полевое ТО", "Провела полевое ТО"),
        };

        private static string AdaptRuLineForPersona(string s, NpcChatterPersona persona)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (persona.IsFemale)
            {
                for (int i = 0; i < RuFemaleReplacements.Length; i++)
                {
                    var pair = RuFemaleReplacements[i];
                    if (s.IndexOf(pair.From, StringComparison.Ordinal) >= 0)
                        s = s.Replace(pair.From, pair.To);
                }
            }
            else if (persona.HasNoveltyHeadgear)
            {
                // Clean mode is already selected for novelty headgear; guard custom lines just in case
                s = s.Replace("братан", "командир")
                     .Replace("братуха", "командир")
                     .Replace("БРАТВА", "ОТРЯД")
                     .Replace("братва", "отряд")
                     .Replace("пацаны", "ребята");
            }
            return s;
        }

        private static string[] GetLinesRu(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.PlayerEatSteakMeat:
                    return new[]
                    {
                        "Вкусно тебе наверное? Да? Я вот стейк никогда не пробовал...",
                        "М-м-м, жареное мяско! От одного запаха слюнки текут... Оставишь кусочек?",
                        "Настоящий стейк в карантинной зоне? Да ты живёшь как король старого мира!",
                        "Боже, как же пахнет прожаренным мясом... Я уже сто лет ничего кроме сухпайков не видел.",
                        "Ешь-ешь, командир, тебе силы нужны. Но выглядит этот стейк просто божественно...",
                        "Слышишь, как у меня в животе заурчало? Это всё твой сочный кусок мяса виноват!",
                        "Вот это пир посреди апокалипсиса! Главное, чтобы зомби на запах шашлыка не сбежались.",
                        "До эпидемии я часто жарил такие стейки на выходных... Эх, были же времена.",
                        "Прожарка что надо! Если вдруг не доешь — я первый в очереди, договорились?",
                        "Ты так аппетитно жуёшь это мясо, что я сейчас свой приклад от зависти сгрызу!"
                    };

                case ChatterTopic.PlayerEatRawOrBad:
                    return new[]
                    {
                        "Эй, ты серьёзно ешь ЭТО сырым?! У тебя желудок из титана или жить надоело?",
                        "Фу-у, меня сейчас вывернет... Хотя бы на костре поджарил для приличия!",
                        "После такой диеты нам точно придётся искать таблетки от живота. Не говори потом, что я не предупреждал!",
                        "Смотреть страшно, как ты это жуёшь... Надеюсь, ты знаешь, что делаешь.",
                        "Брр, пахнет сомнительно! Если начнётся горячка — я тебя на руках не дотащу!"
                    };

                case ChatterTopic.PlayerEatCanned:
                    return new[]
                    {
                        "Опять консервы? Зато надёжно — не портятся даже после конца света.",
                        "Тушёнка — лучшая валюта карантинной зоны. Приятного аппетита!",
                        "Консервный нож не забудь убрать. Эх, сейчас бы эту тушёнку да с горячей картошечкой...",
                        "Старый добрый СПУМ! На вкус как бумага с солью, но калории даёт исправно.",
                        "Главное банку на землю не бросай, чтобы не греметь на всю улицу."
                    };

                case ChatterTopic.PlayerEatFish:
                    return new[]
                    {
                        "Свежая рыбка! Не зря мы с удочкой у воды торчали.",
                        "Ого, рыбный деликатес! Смотри косточкой не подавись посреди боя.",
                        "Пахнет жареной рыбой... Настоящий ресторан на берегу мёртвого острова!",
                        "В рыбе много фосфора — в темноте светиться не начнёшь, но стрелять будешь метче!",
                        "Улов что надо. Вкусно тебе наверное? Я бы тоже от кусочка лосося не отказался..."
                    };

                case ChatterTopic.PlayerEatCookedMeal:
                    return new[]
                    {
                        "Горячая домашняя еда! В наши дни это дороже золота.",
                        "Пахнет как настоящая кухня! Сразу настроение поднимается, когда видишь нормальную еду.",
                        "Подкрепись как следует, впереди ещё куча работы.",
                        "Вот это кулинария! Сразу видно человека, который умеет выживать со вкусом.",
                        "Вкусно пахнет! На сытый желудок и зомби бить веселее."
                    };

                case ChatterTopic.PlayerEatSnackOrFruit:
                    return new[]
                    {
                        "Витамины в карантинной зоне никогда не лишние!",
                        "Сладеньким балуешься? Правильно, глюкоза для мозгов нужна.",
                        "Ягоды и фрукты — хоть какая-то радость среди бетона и ржавчины.",
                        "Ого, перекус на ходу! Быстро и сердито."
                    };

                case ChatterTopic.PlayerDrinkWaterSoda:
                    return new[]
                    {
                        "Ого, газировка из старого мира! В ней ещё остались пузырьки?",
                        "Пей-пей, обезвоживание тут убивает быстрее мертвецов.",
                        "Холодная водичка... Как же хорошо! Главное — сырую из лужи не хлебай.",
                        "Сделай пару глотков и за меня. Во рту совсем пересохло от этой пыли.",
                        "Сладкая шипучка! Сто лет не держал в руках банку Колы."
                    };

                case ChatterTopic.PlayerDrinkAlcohol:
                    return new[]
                    {
                        "Опа, пенное пошло! Только сильно не увлекайся, нам ещё стрелять ровно надо.",
                        "За выживших! Эх, холодного лагера бы сейчас кружечку...",
                        "Нервы лечишь? В этом аду без пары глотков и правда крыша поедет.",
                        "Ну, за то, чтобы патроны не кончались и двери держались!"
                    };

                case ChatterTopic.PlayerHealBandage:
                    return new[]
                    {
                        "Сильно зацепило? Держись, я прикрою периметр, пока ты перевязываешься!",
                        "Затягивай бинт потуже, кровь в нашей ситуации терять нельзя.",
                        "Вот зараза, всё-таки достали тебя... Зашивайся спокойно, я смотрю по сторонам.",
                        "Не дёргайся, дай лекарству подействовать. Я никого к тебе не подпущу!",
                        "Шрамы украшают выжившего, но лучше бы ты берег себя, командир."
                    };

                case ChatterTopic.PlayerReloadAmmo:
                    return new[]
                    {
                        "Правильно, набивай магазины заранее — в бою на это времени не будет.",
                        "Прикрываю, пока ты снаряжаешь обоймы! Люблю звук щёлкающих патронов.",
                        "Полный магазин — залог спокойного сна. Проверь заодно и затвор!",
                        "Заряжай-заряжай, свинца на этих тварей жалеть не будем."
                    };

                case ChatterTopic.PlayerRepairOrCraft:
                    return new[]
                    {
                        "Золотые у тебя руки! В карантине без хорошего инструмента никуда.",
                        "Чини на совесть — будет обидно, если пушка заклинит посреди орды.",
                        "Люблю смотреть, как старый хлам превращается в рабочую снарягу.",
                        "Мастеришь что-то полезное? Я пока покараулю округу."
                    };

                case ChatterTopic.PlayerCookingBBQ:
                    return new[]
                    {
                        "Ух, какой аромат от костра пошёл! Сейчас либо все выжившие, либо все зомби сбегутся!",
                        "Жарь до румяной корочки! От одного вида огня на душе теплее становится.",
                        "Шеф-повар за работой! Смотри не сожги наш ужин, я уже ложку приготовил.",
                        "Запах жареного на костре... Прямо как в походе до всей этой катастрофы."
                    };

                case ChatterTopic.PlayerLockpickBreakDoor:
                    return new[]
                    {
                        "Тихонько... или выносим с ноги? В любом случае я держу ствол наготове!",
                        "Интересно, что там за замком спрятали. Надеюсь, не толпу голодных жильцов!",
                        "Давай-давай, открывай! Я страхую спину на случай засады.",
                        "Взлом с проникновением? В наши дни это называется «плановый сбор ресурсов»."
                    };

                case ChatterTopic.SortStart:
                    return new[]
                    {
                        "Принял! Навожу порядок на складе — сейчас всё разложим по полочкам.",
                        "Запускаю ревизию ящиков! Люблю, когда патроны к патронам, а тушёнка к тушёнке.",
                        "Есть рассортировать базу! Иду разбирать хабар по категориям.",
                        "Наконец-то займёмся складом! А то в ящиках уже чёрт ногу сломит."
                    };

                case ChatterTopic.SortPickUp:
                    return new[]
                    {
                        "Забрал несортированный хабар, несу по профильным ящикам...",
                        "Так-так, что тут у нас? Сейчас разнесём всё по своим местам.",
                        "Взял партию вещей на сортировку. Не мешайся под ногами, склад работает!",
                        "Навёл порядок в приёмнике, распределяю запасы по контейнерам базы."
                    };

                case ChatterTopic.SortDepositWeapons:
                    return new[]
                    {
                        "Оружие, обвесы и боекомплект уложены в оружейный ящик! Арсенал в порядке.",
                        "Стволы и патроны на месте — если нагрянут гости, сразу найдём чем встретить.",
                        "Разложил пушки и магазины по ячейкам. Красота, как на военном складе!"
                    };

                case ChatterTopic.SortDepositFood:
                    return new[]
                    {
                        "Провизия, консервы и вода разложены по продуктовым ящикам!",
                        "Еду убрал на хранение — свежее отдельно, консервы отдельно. С голоду не пропадём!",
                        "Кухонные запасы пополнены! Сразу видно — запасливая у нас база."
                    };

                case ChatterTopic.SortDepositMeds:
                    return new[]
                    {
                        "Бинты, аптечки и препараты сложил в медицинский отсек. Теперь в бою долго искать не придётся!",
                        "Медпункт укомплектован: перевязка и таблетки лежат аккуратными стопками.",
                        "Всю медицину перенёс в профильный ящик. Главное — не болеть!"
                    };

                case ChatterTopic.SortDepositMaterials:
                    return new[]
                    {
                        "Материалы, инструменты и детали разложены по мастерским ящикам!",
                        "Сложил стройматериалы и запчасти в контейнер. Для крафта всё под рукой!",
                        "Ресурсы и снаряжение упакованы плотно, ни одной лишней клетки не пропало."
                    };

                case ChatterTopic.SortFinished:
                    return new[]
                    {
                        "Сортировка базы завершена! Все вещи разложены строго по категориям ящиков.",
                        "Готово, командир! На складе идеальный порядок — хоть сейчас инспекцию проводи.",
                        "Уф, всё разложил по полочкам! Теперь каждый патрон и каждая банка на своём месте.",
                        "Работа на складе окончена! Все контейнеры уплотнены и рассортированы."
                    };

                case ChatterTopic.LootFoundGood:
                    return new[]
                    {
                        "Опа, неплохой трофей! Забираю в копилку отряда.",
                        "Смотри-ка, полезная вещица валяется. В хозяйстве всё пригодится!",
                        "Подобрал припасы! Не пропадать же добру посреди улицы.",
                        "Есть улов! Ещё немного хабара в наш рюкзак."
                    };

                case ChatterTopic.PlayerShareFoodThanks:
                    return new[]
                    {
                        "Ого, поделился пайком! Спасибо, командир, теперь сил хоть отбавляй!",
                        "От души за горячую еду! На сытый желудок и прицел ровнее, и шаг быстрее!",
                        "Спасибо за угощение! Теперь мы в полной боевой готовности!"
                    };

                case ChatterTopic.LootOverweight:
                    return new[]
                    {
                        "Рюкзак почти под завязку! Беру только патроны, медицину и провизию!",
                        "Перегруз даёт о себе знать — тяжёлый хлам пропускаю, собираю только самое важное!"
                    };

                case ChatterTopic.BaseAutoDropoff:
                    return new[]
                    {
                        "Вернулись на базу! Выгрузил собранные в рейде трофеи в приёмный ящик и разложил по складу!",
                        "Авто-разгрузка завершена: все припасы из рюкзака распределены по ящикам базы!"
                    };

                case ChatterTopic.WorldNpcEncounter:
                    return new[]
                    {
                        "Держи дистанцию, путник. В карантинной зоне каждый сам за себя.",
                        "Осторожнее на улицах, за соседним кварталом видели толпу заражённых.",
                        "Ищешь припасы? Главное — в чужие тайники не лезь."
                    };

                case ChatterTopic.AmbientRain:
                    return new[]
                    {
                        "Опять ливень зарядил... Промокнем до нитки, если крышу над головой не найдём.",
                        "Дождь смывает следы, но одежда тяжелеет и холод пробирает до костей.",
                        "Слышишь, как капли стучат? Зато в такую погоду мертвецы хуже слышат наши шаги."
                    };

                case ChatterTopic.AmbientBloodRain:
                    return new[]
                    {
                        "Кровавый дождь... Воздух будто железным стал. Держи ухо востро, твари сейчас взбесятся!",
                        "Небо красное как кровь... Не нравится мне это, совсем не нравится!",
                        "Осторожнее! В кровавый ливень заражённые чуют нас за версту!"
                    };

                case ChatterTopic.AmbientNight:
                    return new[]
                    {
                        "Темнеет быстро... Проверь батарейки в фонаре и держи палец у спуска.",
                        "Ночью в карантинной зоне каждый куст кажется ходячим трупом. Не отходи далеко!",
                        "Ночная смена началась. Смотри в оба, сейчас самые опасные твари вылезут."
                    };

                case ChatterTopic.AmbientMorning:
                    return new[]
                    {
                        "Рассвет! Ещё одну ночь пережили — уже маленькая победа.",
                        "Солнце встаёт. Наконец-то видно, куда стрелять!",
                        "Доброе утро, если в этом месте вообще бывает доброе утро. Выдвигаемся?"
                    };

                case ChatterTopic.AmbientHungry:
                    return new[]
                    {
                        "В животе уже марш играет... Нам бы перекусить при случае, командир.",
                        "Эх, сейчас бы банку тушёнки или кусок мяса... Совсем силы от голода падают.",
                        "Жрать охота так, что даже консервы с кукурузой кажутся мечтой..."
                    };

                case ChatterTopic.AmbientTired:
                    return new[]
                    {
                        "Ноги уже гудят от беготни... Нам бы передохнуть у костра пару часов.",
                        "Глаза слипаются. После следующего рейда точно надо будет отоспаться на базе.",
                        "Устал я как собака, но пока держусь в строю!"
                    };

                case ChatterTopic.AmbientBaseIdle:
                    return new[]
                    {
                        "На базе тихо и спокойно. Даже не верится, что за забором творится ад.",
                        "Хорошее мы место обустроили. Главное — запасы вовремя сортировать и двери запирать.",
                        "Пока затишье — можно почистить оружие и проверить ящики со снарягой.",
                        "Знаешь, с тобой в команде у меня впервые появилась надежда выбраться отсюда живым."
                    };

                case ChatterTopic.BanterQuestion:
                    return new[]
                    {
                        "Эй, напарник, как думаешь — за пределами карантина ещё осталась нормальная жизнь?",
                        "Слушай, а что ты сделаешь первым делом, когда мы выберемся из этой зоны?",
                        "Как у тебя с патронами? Не спишь там на ходу?",
                        "Спорим, я за сегодня уложил больше ходячих, чем ты?"
                    };

                case ChatterTopic.BanterReply:
                    return new[]
                    {
                        "Сначала доживём до завтра, а там я закажу себе огромный стейк и высплюсь на неделю!",
                        "Боекомплект в норме, прицел чистый. Ты главное за своим сектором следи!",
                        "Считай-считай, всё равно самый жирный зомби на моём счету!",
                        "Прорвёмся! С таким командиром нам никакая орда не страшна."
                    };

                default:
                    return GetCombatLinesRu(topic);
            }
        }
    }
}
