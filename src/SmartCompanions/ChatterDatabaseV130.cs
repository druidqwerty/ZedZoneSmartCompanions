using System;

namespace ZedZone.SmartCompanions
{
    public static partial class ChatterDatabase
    {
        public static bool TryGetV130Lines(ChatterTopic topic, ModLanguage lang, ProfanityMode mode, out string[] result)
        {
            if (lang == ModLanguage.Auto)
                lang = CompanionConfigStore.EffectiveLanguage;

            string[] rude = lang switch
            {
                ModLanguage.Russian => GetV130RudeRu(topic),
                ModLanguage.Chinese => GetV130RudeZh(topic),
                _ => GetV130RudeEn(topic)
            };

            string[] clean = lang switch
            {
                ModLanguage.Russian => GetV130CleanRu(topic),
                ModLanguage.Chinese => GetV130CleanZh(topic),
                _ => GetV130CleanEn(topic)
            };

            if (rude == null && clean == null)
            {
                result = null;
                return false;
            }

            if (mode == ProfanityMode.Clean)
            {
                result = clean ?? rude;
                return result != null;
            }

            if (mode == ProfanityMode.HardcoreRude)
            {
                result = (rude != null && rude.Length > 0) ? rude : clean;
                return result != null;
            }

            if (rude == null || rude.Length == 0) { result = clean; return result != null; }
            if (clean == null || clean.Length == 0) { result = rude; return result != null; }

            result = new string[rude.Length + clean.Length];
            Array.Copy(rude, 0, result, 0, rude.Length);
            Array.Copy(clean, 0, result, rude.Length, clean.Length);
            return true;
        }

        public static string TryGetArchetypeBonusLine(CompanionArchetype arch, ChatterTopic topic, ModLanguage lang, ProfanityMode mode)
        {
            if (lang == ModLanguage.Auto) lang = CompanionConfigStore.EffectiveLanguage;
            bool rude = mode != ProfanityMode.Clean;

            if (topic == ChatterTopic.AmbientBaseIdle || topic == ChatterTopic.CampfireStory)
            {
                return arch switch
                {
                    CompanionArchetype.Veteran => CompanionConfigStore.L(lang,
                        rude ? "В старом гарнизоне учили: пока тихо — чисти ствол и береги сухой паёк, блядь." : "Старая армейская привычка: в затишье проверь сектора обстрела и запасные магазины.",
                        rude ? "Old garrison rule: when it's quiet, clean your rifle and count every damn round." : "Old garrison habit: use every quiet minute to check firing lanes and spare mags.",
                        rude ? "老兵规矩：趁着消停赶紧擦枪数子弹，别他妈等尸潮来了抓瞎！" : "军营老习惯：趁着安静检查射界和备用弹匣。"),
                    CompanionArchetype.StreetPunk => CompanionConfigStore.L(lang,
                        rude ? "Скукота смертная, бля! Щас бы тачку угнать да по району с ветерком прокатиться!" : "Тишина какая-то подозрительная... Пошли уже потрясём соседний квартал на хабар!",
                        rude ? "Dead boring here! Let's hotwire a ride and go smash some skulls on the block!" : "Too quiet around here... Let's go shake down the next block for loot!",
                        rude ? "闷死老子了！走，搞辆车去隔壁街区干一票大的！" : "太安静了……咱们去隔壁街区搜刮点好东西吧！"),
                    CompanionArchetype.ParanoidPrepper => CompanionConfigStore.L(lang,
                        rude ? "Тихо! Слышал шорох за стенкой?! Запирай засовы, сука, за нами следят!" : "Ты слышал этот скрип? Нужно пересчитать консервы и проверить замки на всех дверях...",
                        rude ? "Shh! Did you hear that scrape outside?! Lock the damn bolts, something's watching us!" : "Did you hear that creak? We should recount our rations and double-check the door locks...",
                        rude ? "嘘！听见墙外动静没？！赶紧把门反锁，他妈的有东西在盯着咱们！" : "你听见刚才的响声了吗？得再去检查一遍门锁和罐头库存……"),
                    CompanionArchetype.CombatMedic => CompanionConfigStore.L(lang,
                        rude ? "Руки перед жратвой мой, блядь, мне только дизентерии в отряде не хватало!" : "Пока мы на базе, покажи старые ссадины — сменю повязки, чтобы зараза не пошла.",
                        rude ? "Wash your damn hands before eating, I'm not treating dysentery in this hellhole!" : "While we're resting at base, let me check those scratches and change your dressings.",
                        rude ? "吃饭前先把脏手洗干净，老子可不想在这鬼地方给你们治痢疾！" : "趁着在基地休息，让我检查一下你的旧伤口、换个干净绷带。"),
                    CompanionArchetype.JollyScavenger => CompanionConfigStore.L(lang,
                        rude ? "Хе-хе, глянь сколько добра натащили! Ещё пара рейдов — и откроем свой ларёк, бля!" : "Обожаю возвращаться с полными рюкзаками! Главное — всё аккуратно по ящикам разложить.",
                        rude ? "Heh, look at all this sweet loot! Two more raids and we'll open our own black market!" : "Nothing beats coming back with full packs! Just gotta sort it all into the chests.",
                        rude ? "嘿嘿，瞧瞧咱们攒的这堆宝贝！再干两票就能开黑市发财了！" : "满载而归的感觉真棒！把战利品归类放好看着就舒坦。"),
                    _ => null
                };
            }
            return null;
        }

        private static string[] GetV130RudeRu(ChatterTopic topic) => topic switch
        {
            ChatterTopic.CampfireStory => new[]
            {
                "Грей клешни у костра, братва... В такую холодину без огня можно и бубенцы отморозить, блядь!",
                "Подбросил дровишек в топку! Сидим как короли посреди пиздеца, только шашлыка и пиваса не хватает!",
                "Помню, до карантина мы так же у костра бухали... А теперь за забором жмурики шастают, сука.",
                "Огонь горит заебись! Отогревай кости, командир, завтра снова в мясорубку лезть!",
                "Слышь, у костра хоть человеком себя чувствуешь, а не дичью загнанной, блядь."
            },
            ChatterTopic.ChoreGardening => new[]
            {
                "Полил твою ботву в горшках, агроном хуев! Смотри, какой урожай прёт, с голоду не сдохнем!",
                "Я чё, нанимался в колхозники, блядь?! Ладно, полил и удобрил грядки — жратва своя нужна!",
                "Всыпал удобрений в горшки, сука — теперь помидоры с кулак вырастут, зуб даю!",
                "Огород полит и ухожен! Хоть свежей зелени пожрём вместо ржавых консервов, блядь!"
            },
            ChatterTopic.ChoreWeaponMaint => new[]
            {
                "Почистил затвор и смазал стволы! Теперь ни одна железка в замесе не клинанёт, блядь!",
                "Подлатал нашу снарягу и протёр пушки от кровищи! Оружие любит ласку и чистку, сука!",
                "Подтянул ремни на броне и вычистил нагар из ствола — к следующей бойне готовы на все сто!",
                "Проверил бойки и подправил кромку на клинках! С такой снарягой всех тварей на ремни пустим!"
            },
            ChatterTopic.ChoreVehicleCheck => new[]
            {
                "Осмотрел нашу ласточку и выгрузил хабар из багажника по ящикам! Тачка — зверь, блядь!",
                "Подтянул гайки на кузове и перекинул трофеи из багажника на склад! Можем гнать хоть на край зоны!",
                "Багажник пустой, весь лут уже на базе по полочкам! Заодно движок и колёса проверил, командир!",
                "Вытряхнул всё добро из тачки в ящики и подстучал вмятины на бампере от зомбаков!"
            },
            ChatterTopic.CombatPlayerDownedRescue => new[]
            {
                "Вставай, сука, не вздумай подыхать на моём дежурстве! Вколол адреналин и замотал раны — ЖИВИ, БЛЯДЬ!",
                "Командир упал?! Хуй вам, твари, я его не отдам! Держись, братан, поднимаю на ноги!",
                "Нихуя себе тебя сложило! Стой-стой, зажал артерию и всадил стимулятор — дыши, родной!",
                "Отползай за меня, блядь! Перевязал и откачал тебя в последнюю секунду, не пугай так больше!"
            },
            ChatterTopic.CombatFocusTarget => new[]
            {
                "Все стволы по этой жирной мрази! Фокусим главную цель, рвём её в клочья, блядь!",
                "Валим сначала эту тварь все вместе! Свинцом её накачивай, не давай подойти!",
                "Концентрируем огонь по приоритетной мишени! Сейчас мы ей башку открутим нахуй!"
            },
            ChatterTopic.CombatStealthLightsOff => new[]
            {
                "Гашу фонарь, блядь! Работаем втемную, чтоб ни одна падла нас раньше времени не спалила!",
                "Вырубил свет — идём тихо как тени, режем по-тихому без лишнего кипиша!",
                "Фонари на ноль! В режиме стелса светить мордой — только жмуров созывать!"
            },
            ChatterTopic.LevelUpRank => new[]
            {
                "Ебать, я матерею на глазах! Новый ветеранский ранг — теперь я этих тварей голыми руками рвать буду!",
                "Опыт не пропьёшь, сука! Поднял боевой ранг — чую, сил и здоровья прибавилось конкретно!",
                "Вот это закалка в боях! С каждым рейдом стреляю точнее и держу удар крепче, командир!",
                "Плюс один ранг выжившего! Теперь мы в этой зоне настоящая элита, блядь!"
            },
            ChatterTopic.PlayerDrivingRide => new[]
            {
                "Жми на газ до упора, шеф! Раскатаем эту гниль бампером по асфальту, блядь!",
                "Ебать ты Шумахер! Только в столб не въебись, а то пешком до базы пиздовать придётся!",
                "Хорошо идём! На тачке по карантину рассекать — в сто раз пизже, чем ноги стаптывать!",
                "Дави ходячих колёсами, не тормози, сука! Главное — следи за бензином и аккумулятором!"
            },
            ChatterTopic.SquadBattleCry => new[]
            {
                "ЗАРЯЖАЙ И ВПЕРЁД, БРАТВА! Всем полный кураж — раскатаем этих мразей в тонкий блин, блядь!",
                "ШТУРМ ПО КОМАНДЕ! Магазины полные, клины сняты — разносим сектор в щепки нахуй!",
                "НИКТО НЕ ОТСТУПАЕТ! Погнали рубить и крошить, покажем этим тварям, кто тут хозяин!",
                "БОЕВАЯ ГОТОВНОСТЬ СТО ПРОЦЕНТОВ! Прикрываем командира и выжигаем всё живое и мёртвое!"
            },
            _ => null
        };

        private static string[] GetV130CleanRu(ChatterTopic topic) => topic switch
        {
            ChatterTopic.CampfireStory => new[]
            {
                "Подбросил дров в костёр! Подойди погрейся, командир, у огня сразу силы возвращаются.",
                "Как же хорошо посидеть у живого огня после тяжёлого выхода в зону...",
                "Костёр горит ровно и тепло! Можно перевести дух, просушить одежду и обсудить план на завтра.",
                "У огня даже мрачные мысли отступают. Главное — поддерживать пламя до рассвета."
            },
            ChatterTopic.ChoreGardening => new[]
            {
                "Проверил наши грядки: полил растения и добавил удобрений. Скоро соберём свежий урожай!",
                "Ухаживаю за посевами на базе — своя свежая зелень и овощи в карантине на вес золота!",
                "Грядки политы и ухожены! Растут отлично, продовольственный запас будет пополнен."
            },
            ChatterTopic.ChoreWeaponMaint => new[]
            {
                "Почистил и смазал наше оружие, подтянул крепления на броне. Снаряжение в отличном состоянии!",
                "Провёл полевое ТО стволов и клинков — в следующем бою осечек не будет!",
                "Пока тихо, привёл экипировку отряда в порядок и восстановил часть износа оружия."
            },
            ChatterTopic.ChoreVehicleCheck => new[]
            {
                "Осмотрел автомобиль и разгрузил багажник прямо по ящикам склада! Машина готова к выезду.",
                "Перенёс все трофеи из багажника машины на склад базы и проверил состояние кузова!",
                "Автомобиль обслужен, грузовой отсек разгружен и рассортирован по категориям базы!"
            },
            ChatterTopic.CombatPlayerDownedRescue => new[]
            {
                "Держись, командир, не смей погибать! Вколол стимулятор и перевязал раны — вставай в строй!",
                "Экстренная реанимация! Остановил кровотечение и поднял тебя на ноги, отходим за мою спину!",
                "Я с тобой! Перевязал критические раны, держись за меня и дыши глубже!"
            },
            ChatterTopic.CombatFocusTarget => new[]
            {
                "Весь отряд — огонь по главной цели! Сфокусируем выстрелы на самом опасном противнике!",
                "Координирую огонь по приоритетной мишени! Дожимаем её все вместе!",
                "Берём главную угрозу в перекрёстный прицел — открываем сосредоточенный огонь!"
            },
            ChatterTopic.CombatStealthLightsOff => new[]
            {
                "Выключаю фонарь для маскировки! В тихом режиме двигаемся без лишнего света.",
                "Погасил свет, чтобы не привлекать заражённых. Работаем скрытно!",
                "Светомаскировка включена — держим тихий ход."
            },
            ChatterTopic.LevelUpRank => new[]
            {
                "Отличная боевая практика! Повысил свой ветеранский ранг — стал выносливее и крепче!",
                "Новый ранг мастерства! С каждым боем наш отряд становится всё сильнее, командир!",
                "Повышение в строю! Опыт рейдов даёт о себе знать — готов к любым испытаниям!"
            },
            ChatterTopic.PlayerDrivingRide => new[]
            {
                "Отличная машина! На колёсах пересекать опасные сектора куда быстрее и надёжнее.",
                "Слежу за дорогой из окна! Главное — объезжать крупные завалы и беречь подвеску.",
                "С ветерком едем! Если на пути попадутся одиночные ходячие — бампер выдержит!"
            },
            ChatterTopic.SquadBattleCry => new[]
            {
                "Отряд, к бою! Оружие перезаряжено, держим строй и атакуем в полную силу!",
                "Боевая координация! Сняли осечки, восстановили дыхание — работаем слаженно по целям!",
                "Вперёд, команда! Прикрываем командира и зачищаем сектор!"
            },
            _ => null
        };

        private static string[] GetV130RudeEn(ChatterTopic topic) => topic switch
        {
            ChatterTopic.CampfireStory => new[]
            {
                "Stoked the campfire! Get your frozen ass over here before your fingers fall off!",
                "Nothing beats warming your bones by the fire after wading through rotting freaks all day!",
                "Fire's blazing hot! All we're missing is a crate of cold beer and some grilled steak!",
                "Man, sitting by the fire almost makes you forget the whole damn world ended outside."
            },
            ChatterTopic.ChoreGardening => new[]
            {
                "Watered and fertilized your damn planters! Didn't think I'd become a post-apocalyptic farmer!",
                "Tended the base crops! Fresh veggies beat eating rusty dog-food cans any day!",
                "Poured water and compost on the pots — those crops are growing fat and healthy, boss!"
            },
            ChatterTopic.ChoreWeaponMaint => new[]
            {
                "Cleaned the bolts, oiled the barrels, and patched our armor straps! No damn jams on my watch!",
                "Scraped the zombie gore off our blades and tuned up the guns — gear's sharp and ready!",
                "Field-stripped our weapons and fixed up the wear and tear. Ready to rock and roll!"
            },
            ChatterTopic.ChoreVehicleCheck => new[]
            {
                "Unloaded the vehicle trunk straight into base storage and checked the bodywork! Ride's ready!",
                "Hauled all the loot out of the car trunk into our sorted chests and tightened the bolts!",
                "Trunk's empty and sorted, boss! Checked the tires and engine while I was at it!"
            },
            ChatterTopic.CombatPlayerDownedRescue => new[]
            {
                "GET UP, BOSS! You're not dying on my fucking watch! Slammed an adrenaline shot in you — breathe!",
                "Leader's down?! Hell no! Patched your arteries and hauled you back on your feet — stay behind me!",
                "Hold on! Clamped the bleeding and hit you with emergency meds — get back in the fight!"
            },
            ChatterTopic.CombatFocusTarget => new[]
            {
                "ALL GUNS ON THAT UGLY BASTARD! Focus fire on the primary threat and shred it!",
                "Light up the big target together! Pour every round into it right now!",
                "Crossfire on the main threat! Tear that freak apart!"
            },
            ChatterTopic.CombatStealthLightsOff => new[]
            {
                "Killing my flashlight! Moving dark and quiet so we don't wake the whole damn block!",
                "Lights off! Stealth mode active — let's carve 'em up in the dark!",
                "Doused the beam! No point shining a beacon for every freak in the street!"
            },
            ChatterTopic.LevelUpRank => new[]
            {
                "Hell yeah, leveled up my veteran rank! Feeling tougher, faster, and meaner than ever!",
                "Promoted in the field! All this bloodshed made me a hardened survivor, boss!",
                "New combat rank unlocked! Bring on the hordes, I'm just getting warmed up!"
            },
            ChatterTopic.PlayerDrivingRide => new[]
            {
                "Floor it, boss! Paint the bumper red with any rotter standing in the road!",
                "Hell of a ride! Way better than wearing out our boots — just don't wrap us around a pole!",
                "Cruising through the quarantine zone in style! Watch the fuel gauge and ram 'em!"
            },
            ChatterTopic.SquadBattleCry => new[]
            {
                "LOCK AND LOAD, SQUAD! Full adrenaline surge — let's wipe every bastard off the street!",
                "ASSAULT FORMATION! Guns unjammed, mags topped off — tear through 'em!",
                "NO MERCY! Rally on the boss and blow this whole sector to hell!"
            },
            _ => null
        };

        private static string[] GetV130CleanEn(ChatterTopic topic) => topic switch
        {
            ChatterTopic.CampfireStory => new[]
            {
                "Added fuel to the campfire! Come warm up by the flames and recover your strength.",
                "The fire's burning bright. Good to take a breather and dry off our gear at camp.",
                "Sitting by a warm fire makes the quarantine zone feel a little more livable."
            },
            ChatterTopic.ChoreGardening => new[]
            {
                "Watered and fertilized the base planters! Our crops are growing right on schedule.",
                "Tended the garden pots around base — fresh harvest will be ready before long!",
                "Checked the soil and watered our plants. Sustainable food supply is looking good!"
            },
            ChatterTopic.ChoreWeaponMaint => new[]
            {
                "Cleaned and oiled our weapons and reinforced armor straps during the downtime.",
                "Completed field maintenance on our firearms and melee gear — durability restored!",
                "Checked firing pins and cleaned barrels so nothing malfunctions in the next fight."
            },
            ChatterTopic.ChoreVehicleCheck => new[]
            {
                "Unloaded the vehicle trunk into our categorized base chests and inspected the chassis!",
                "Transferred all cargo from the vehicle into base storage and checked the tires and battery.",
                "Vehicle cargo unloaded and sorted! The ride is inspected and ready for the next run."
            },
            ChatterTopic.CombatPlayerDownedRescue => new[]
            {
                "Hang in there, commander! Administered emergency aid and got you back on your feet!",
                "Don't give up! Stopped your critical bleeding and revived you — fall back behind me!",
                "Emergency rescue complete! I've got you covered, catch your breath!"
            },
            ChatterTopic.CombatFocusTarget => new[]
            {
                "Squad, focus fire on the primary target! Bring that threat down together!",
                "Concentrating all fire on the high-priority hostile! Keep shooting!",
                "Coordinated strike on the main target — engage now!"
            },
            ChatterTopic.CombatStealthLightsOff => new[]
            {
                "Turning off my flashlight for stealth! Moving quietly in low-visibility mode.",
                "Light discipline active — doused the beam so we stay unseen.",
                "Flashlight off for silent approach."
            },
            ChatterTopic.LevelUpRank => new[]
            {
                "Combat rank increased! Field experience has boosted my stamina and resilience!",
                "Promoted to a higher survivor rank! Ready to serve the squad even better, commander!",
                "Leveled up! Every raid makes our team sharper and stronger."
            },
            ChatterTopic.PlayerDrivingRide => new[]
            {
                "Good to be rolling on wheels! Keep an eye on the road and I'll watch the flanks.",
                "Smooth driving, commander! Much faster and safer than crossing the zone on foot.",
                "Scanning the street from the vehicle — clear ahead!"
            },
            ChatterTopic.SquadBattleCry => new[]
            {
                "Squad rally! Weapons checked, stamina restored — engaging at full combat readiness!",
                "Coordinated assault order received! Clearing malfunctions and focusing fire!",
                "All units ready! Covering the commander and advancing together!"
            },
            _ => null
        };

        private static string[] GetV130RudeZh(ChatterTopic topic) => topic switch
        {
            ChatterTopic.CampfireStory => new[]
            {
                "往篝火里添了把柴！赶紧过来烤烤火，别他妈把爪子冻僵了！",
                "卧槽，跟死鬼拼了一整天，还是围着火堆烤火最爽，就差两瓶冰啤酒了！",
                "火烧得旺旺的！老大，快过来暖和暖和，明天还得继续去砍丧尸！",
                "坐在火堆旁吹牛逼，才感觉自己他妈的还是个活人！"
            },
            ChatterTopic.ChoreGardening => new[]
            {
                "把基地的菜盆全浇水施肥了一遍！没想到老子在末日还得当菜农，等着收菜吧！",
                "伺候完那几盆庄稼了！总比天天啃生锈的烂罐头强，这波血赚！",
                "浇好水施好肥了，老大！瞧这长势，过两天就有新鲜蔬菜塞牙缝了！"
            },
            ChatterTopic.ChoreWeaponMaint => new[]
            {
                "趁着消停把枪管擦干净、上好油，护具也加固了一遍！保证开打绝不卡壳！",
                "把刀上的烂肉刮干净磨快了，枪械耐久也修补好了，随时能大干一场！",
                "全队武器保养完毕！家伙什顺手，爆起头来才他妈痛快！"
            },
            ChatterTopic.ChoreVehicleCheck => new[]
            {
                "把车后备箱里的物资全卸下来按类别塞进基地箱子了，顺便检修了一下车身！",
                "后备箱清空归位完毕，老大！轮胎、电瓶和引擎老子也顺手检查过了，随时能飙车！",
                "车上的战利品全搬进仓库分好类了，车况良好，咱们下次出门接着撞死鬼！"
            },
            ChatterTopic.CombatPlayerDownedRescue => new[]
            {
                "给老子撑住，老大！有我在你他妈别想死！一针强心剂扎进去了，快起来！",
                "队长倒了？！去你妈的丧尸，休想碰他！血止住了，赶紧退到老子身后！",
                "卧槽差点交代在这儿！紧急包扎拉起来了，喘口气，这帮杂碎交给我顶着！"
            },
            ChatterTopic.CombatFocusTarget => new[]
            {
                "全队集火那个最大的杂碎！所有枪口对准头号目标，把它轰成渣！",
                "先干掉最猛的那个家伙！火力全开，别让它靠近老大！",
                "集中火力猛攻头号威胁！一起开火把它撕碎！"
            },
            ChatterTopic.CombatStealthLightsOff => new[]
            {
                "手电筒关了！摸黑悄悄摸过去，别他妈开灯把整条街的死鬼全招来！",
                "熄灯潜行！夜里拿刀悄悄抹脖子才叫专业！",
                "关闭光源隐蔽行动，谁也别暴露位置！"
            },
            ChatterTopic.LevelUpRank => new[]
            {
                "牛逼！老子晋升老兵军衔了，感觉浑身是劲，血条和耐力都暴涨了一截！",
                "杀出来的实战经验！升阶成功，现在这帮丧尸在老子眼里就是送菜的！",
                "军衔晋升！跟着老大混越打越猛，咱们小队现在就是禁区传奇！"
            },
            ChatterTopic.PlayerDrivingRide => new[]
            {
                "油门踩到底，老大！前面挡路的烂肉直接用保险杠撞飞！",
                "卧槽，秋名山车神啊！飙归飙，别他妈撞电线杆上就行！",
                "坐车兜风比两条腿跑爽多了！注意盯着油表和电瓶，咱们一路碾过去！"
            },
            ChatterTopic.SquadBattleCry => new[]
            {
                "全员上膛，跟老子冲！肾上腺素拉满，把这群杂碎全轰上天！",
                "集火突击！卡壳已排障、弹匣已压满——给老子杀穿这片街区！",
                "战吼响应！全队死保老大，把眼前能喘气的怪物全剁碎！"
            },
            _ => null
        };

        private static string[] GetV130CleanZh(ChatterTopic topic) => topic switch
        {
            ChatterTopic.CampfireStory => new[]
            {
                "往篝火里加了柴火！快过来烤火暖和一下，恢复点体力和精神。",
                "营火烧得很旺，趁着安全在火边休整一下、烘烤湿衣服吧。",
                "围在温暖的篝火旁，感觉隔离区的寒意都散去不少。"
            },
            ChatterTopic.ChoreGardening => new[]
            {
                "已经帮基地的种植盆浇水并施了肥，农作物长势非常好！",
                "照料了一下营地的菜园，很快就能收获新鲜作物补充口粮了！",
                "基地植物已浇水养护完毕，后勤食物储备更加稳固！"
            },
            ChatterTopic.ChoreWeaponMaint => new[]
            {
                "趁着休整擦拭并保养了武器，加固了护具绑带，装备耐久已恢复！",
                "完成了小队枪械与近战武器的战地维护，下次战斗绝不掉链子。",
                "检查了撞针与刀刃，全队武器状态极佳！"
            },
            ChatterTopic.ChoreVehicleCheck => new[]
            {
                "已将载具后备箱的战利品全部卸下并按类别归入基地储物箱，同时检查了车况！",
                "车辆货舱卸载并整理完毕！轮胎、电瓶与车身状态均已巡检。",
                "后备箱物资已自动归位到仓库，车辆随时可以再次出发！"
            },
            ChatterTopic.CombatPlayerDownedRescue => new[]
            {
                "坚持住，队长！已为你注射急救药剂并止血包扎，快站起来退到我身后！",
                "绝不放弃任何队友！已完成战地紧急抢救，我来掩护你撤退！",
                "危急救援完成！伤口已经包扎好，深呼吸稳住！"
            },
            ChatterTopic.CombatFocusTarget => new[]
            {
                "全队注意，集中火力优先击杀高威胁目标！",
                "锁定头号威胁！所有队员协同射击，迅速解决它！",
                "交叉火力覆盖主目标，全力开火！"
            },
            ChatterTopic.CombatStealthLightsOff => new[]
            {
                "已关闭手电筒进入灯光管制，保持潜行隐蔽移动！",
                "熄灭光源避免惊动感染者，小队保持静默推进。",
                "已切换无光潜行状态！"
            },
            ChatterTopic.LevelUpRank => new[]
            {
                "实战历练积累完成，队友军衔成功晋升！最大生命值与体能恢复全面强化！",
                "晋升更高老兵等级！感谢队长的带领，我的战斗能力更强了！",
                "等级提升！经过多次突袭，我们小队的配合愈发精湛！"
            },
            ChatterTopic.PlayerDrivingRide => new[]
            {
                "有车代步效率高多了！你专心驾驶，我来观察道路两侧的敌情。",
                "车技很稳，队长！注意避开路障，保持车速通过危险区。",
                "正在车上警戒四周，前方道路畅通！"
            },
            ChatterTopic.SquadBattleCry => new[]
            {
                "小队战吼激励！全员武器已排障装填，体能充沛，准备协同突击！",
                "收到总攻指令！集中精神，掩护队长清理整片区域！",
                "全队进入最佳作战状态，立即发起突击！"
            },
            _ => null
        };
    }
}
