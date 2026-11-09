using System;

namespace ZedZone.SmartCompanions
{
    public static partial class ChatterDatabase
    {
        public static string[] AppendExtraRudeRu(ChatterTopic topic, string[] baseRude)
        {
            var extra = GetExtraRudeRu(topic);
            if (extra == null || extra.Length == 0) return baseRude;
            if (baseRude == null || baseRude.Length == 0) return extra;
            var res = new string[baseRude.Length + extra.Length];
            Array.Copy(baseRude, 0, res, 0, baseRude.Length);
            Array.Copy(extra, 0, res, baseRude.Length, extra.Length);
            return res;
        }

        public static string[] AppendExtraRudeEn(ChatterTopic topic, string[] baseRude)
        {
            var extra = GetExtraRudeEn(topic);
            if (extra == null || extra.Length == 0) return baseRude;
            if (baseRude == null || baseRude.Length == 0) return extra;
            var res = new string[baseRude.Length + extra.Length];
            Array.Copy(baseRude, 0, res, 0, baseRude.Length);
            Array.Copy(extra, 0, res, baseRude.Length, extra.Length);
            return res;
        }

        private static string[] GetExtraRudeRu(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.PlayerEatSteakMeat:
                    return new[]
                    {
                        "Сука, я последний раз такой кусок мяса видел только по телевизору до эпидемии, а ты его молча точишь!",
                        "Чтоб тебе этот стейк поперёк горла не встал, буржуй хренов! Ладно, шучу, но кусок-то отломи, блядь!",
                        "Ебать аромат пошёл! У меня щас желудок сам себя переварит от зависти, сука!"
                    };
                case ChatterTopic.PlayerEatRawOrBad:
                    return new[]
                    {
                        "Ты реально конченый, блядь — сырую падаль в рот тянуть! Потом не ной, когда кишки наизнанку вывернет!",
                        "Фу нахуй, отойди от меня подальше со своей сырой требухой, людоед хуев!"
                    };
                case ChatterTopic.PlayerEatCanned:
                    return new[]
                    {
                        "Опять эта жестяная хуетень! Зато срок годности сто лет — переживёт и нас, и зомбаков, блядь.",
                        "Шкрябаешь ложкой по банке так, будто там чёрная икра, а не дешёвая тушёнка из жил и сои, сука!"
                    };
                case ChatterTopic.PlayerEatFish:
                    return new[]
                    {
                        "Рыбный день устроил, блядь?! Смотри плавником не подавись, рыбак ебаный!",
                        "Нихуя ты гурман — карасей да лососей посреди карантина уплетает в обе щёки!"
                    };
                case ChatterTopic.PlayerEatCookedMeal:
                    return new[]
                    {
                        "Охуеть, горячая похлёбка! В нашем гадюшнике за миску такого супа убить могут, блядь!",
                        "Жри пока горячее, сука, на сытое пузо и прицел не трясётся!"
                    };
                case ChatterTopic.PlayerEatSnackOrFruit:
                    return new[]
                    {
                        "Пряники да ягодки точишь, сладкоежка хуев? Нам бы кабана завалить, а не траву жевать!",
                        "Ну хоть какая-то радость в этом ёбаном морге — закинул сладкого и вроде жить можно."
                    };
                case ChatterTopic.PlayerDrinkWaterSoda:
                    return new[]
                    {
                        "Булькаешь своей фляжкой на всю округу, блядь! Оставь пару глотков, сушняк дикий!",
                        "Сладкая водичка из прошлой жизни... Пей-пей, сука, пока почки не отвалились!"
                    };
                case ChatterTopic.PlayerDrinkAlcohol:
                    return new[]
                    {
                        "Бухаешь в одно жало на дежурстве?! Ну ты и наглая рожа, блядь, плесни хоть на донышко!",
                        "Во, накатил пивка — и сразу зомбаки не такими страшными кажутся, да, сука?!"
                    };
                case ChatterTopic.PlayerHealBandage:
                    return new[]
                    {
                        "Давай-давай, заматывай свои сопли и дырки, блядь! Нам ещё полгорода зачищать!",
                        "Опять подставился под раздачу, герой хуев?! Зашивайся быстрее, я держу сектор!"
                    };
                case ChatterTopic.PlayerReloadAmmo:
                    return new[]
                    {
                        "Пихай патроны плотнее, сука! В следующем дворе этих тварей как говна за баней!",
                        "Правильно, блядь, пустой рожок в бою — прямой билет к праотцам!"
                    };
                case ChatterTopic.PlayerRepairOrCraft:
                    return new[]
                    {
                        "Подкрути гайки как следует, блядь, чтоб в самый ответственный момент не разлетелось к хуям!",
                        "Очумелые ручки в деле, сука! Главное — сделай так, чтоб стреляло и рубило!"
                    };
                case ChatterTopic.PlayerCookingBBQ:
                    return new[]
                    {
                        "Костёр дымит на пол-острова, блядь! Зато мясо прожарится как надо, хуй с ними с зомбаками!",
                        "Переворачивай вовремя, шашлычник хренов, угли жрать я не подписывался!"
                    };
                case ChatterTopic.PlayerLockpickBreakDoor:
                    return new[]
                    {
                        "Ломай этот ёбаный замок быстрее, у меня пальцы чешутся хабар проверить!",
                        "Щас дверь с петель слетит — держи ствол наготове, мало ли какая блядь внутри притаилась!"
                    };
                case ChatterTopic.PlayerShareFoodThanks:
                    return new[]
                    {
                        "Вот это по-пацански, блядь! Накормил отряд — теперь мы любую орду на ремни пустим!",
                        "Красава, командир, не зажал пайку! Сразу сил прибавилось, щас будем рвать и метать, сука!"
                    };
                case ChatterTopic.CombatSpotZombie:
                    return new[]
                    {
                        "Глянь на эту гнилую рожу, блядь! Щас сделаю ему проветривание черепушки!",
                        "Ещё один клиент нарисовался! Стой смирно, уёбок, щас прилетит маслина!",
                        "Ползёт мертвечина ебаная! Не подпускай на укус!"
                    };
                case ChatterTopic.CombatSpotHorde:
                    return new[]
                    {
                        "Ёбаный рот этого карантина, да их тут десятки! Откатываемся и жжём весь боекомплект нахуй!",
                        "Толпа гнилых прёт со всех щелей, сука! Держи строй и не дай зажать нас в коробку!"
                    };
                case ChatterTopic.CombatSpotBandit:
                    return new[]
                    {
                        "Быдло со стволами по курсу! Гаси этих крыс первыми, пока они по нам не отработали, блядь!",
                        "Мародёрская падаль! Щас мы им покажем, чей это район, сука!"
                    };
                case ChatterTopic.CombatSpotBoss:
                    return new[]
                    {
                        "Нихуя себе годзилла карантинная вылезла! Не подходи близко, расстреливаем из всех калибров, блядь!",
                        "Здоровая хуйня прёт прямо на нас! Разрывай дистанцию и бей по уязвимым местам, сука!"
                    };
                case ChatterTopic.CombatKiting:
                    return new[]
                    {
                        "Хер вам, а не свежее мясо, ублюдки! Отхожу назад и нашпиговываю свинцом!",
                        "Ноги в руки и палим на отходе, блядь! Не давай им сократить дистанцию!"
                    };
                case ChatterTopic.CombatClearFireLine:
                    return new[]
                    {
                        "Блядь, чуть затылок мне не продул! Ушёл вбок — хуярь давай по мертвякам, а не по мне!",
                        "Смещаюсь с прицела, сука! Контролируй ствол, командир!"
                    };
                case ChatterTopic.CombatSwitchMelee:
                    return new[]
                    {
                        "Зажали в упор, бляди! Ладно, щас я вам устрою мясную лавку холодным оружием!",
                        "Ствол за спину, беру топор — щас полетят гнилые бошки нахуй!"
                    };
                case ChatterTopic.CombatSwitchRanged:
                    return new[]
                    {
                        "Разорвал дистанцию! Достаю волыну обратно, щас добавим огоньку, сука!",
                        "Снова на огнестреле, блядь! Ловите маслины!"
                    };
                case ChatterTopic.CombatKillEnemy:
                    return new[]
                    {
                        "Земля стекловатой, уёбок гнилой! Следующий в очередь!",
                        "Башка вдребезги, блядь! Вот это я понимаю — точный выстрел!",
                        "Лежать, сука! Тут вам не прогулочный парк!"
                    };
                case ChatterTopic.CombatSavePlayer:
                    return new[]
                    {
                        "Опять я твою жопу вытаскиваю в последний момент, командир! С тебя ящик тушёнки, блядь!",
                        "Пришил гада прямо у тебя за спиной! Не зевай, сука, второй раз могу и не успеть!"
                    };
                case ChatterTopic.CombatLowAmmo:
                    return new[]
                    {
                        "Сука, последний рожок добиваю! Если не найдём патронов — будем камнями кидаться, блядь!",
                        "Маслины почти кончились нахуй! Экономлю каждый выстрел!"
                    };
                case ChatterTopic.CombatWounded:
                    return new[]
                    {
                        "Блядь, шкуру прокусили, пидорасы! Замотаюсь — и всем пизда!",
                        "Сука, кровь хлещет, но ствол из рук не выпущу!"
                    };
                case ChatterTopic.CombatFlankWarning:
                    return new[]
                    {
                        "Командир, сзади крысы поджимают, блядь! Разворачивайся бегом!",
                        "Тыл горит, сука! За спиной у тебя мертвяки, беру их на себя!"
                    };
                case ChatterTopic.CombatWeaponJammed:
                    return new[]
                    {
                        "Гильзу перекосило, ёбаный рот! Ща выбью затвор, придержи их пару секунд!",
                        "Клин поймал на ровном месте, сука! Чищу патронник!"
                    };
                case ChatterTopic.FieldMedicAid:
                    return new[]
                    {
                        "Залепил твои раны последней аптечкой, блядь! Только попробуй сдохнуть после этого!",
                        "Перевязал тебя на бегу, сука! Держись на ногах, нам ещё выбираться отсюда!"
                    };
                case ChatterTopic.FieldAmmoShare:
                    return new[]
                    {
                        "На, держи заначку патронов, стрелок хуев! Перезаряжайся и гаси их!",
                        "Подкинул тебе боекомплекта из своего рюкзака, блядь! Не стой с пустым стволом!"
                    };
                case ChatterTopic.SortStart:
                    return new[]
                    {
                        "Ща-ща, блядь, разложу твои железки, только под руку не пизди!",
                        "Опять ревизия на складе, сука! Ладно, щас всё будет разложено по полочкам как в лучших домах!"
                    };
                case ChatterTopic.SortPickUp:
                    return new[]
                    {
                        "Сгрёб весь этот бардак в охапку, несу по профильным ящикам, блядь!",
                        "Чего тут только не навалено, сука... Сортирую по категориям!"
                    };
                case ChatterTopic.SortDepositWeapons:
                    return new[]
                    {
                        "Всю оружейку и боезапас разложил ровненько — хоть сейчас оборону держи, блядь!",
                        "Пушки к пушкам, патроны к патронам. Красота, сука!"
                    };
                case ChatterTopic.SortDepositFood:
                    return new[]
                    {
                        "Пайки и консервы уложил в продуктовый отсек, блядь. Запасы на чёрный день готовы!",
                        "Вся жратва и вода расфасованы по ящикам, командир!"
                    };
                case ChatterTopic.SortDepositMeds:
                    return new[]
                    {
                        "Аптечки и бинты сложил в медпункт, сука. Теперь в темноте на ощупь найдём!",
                        "Медицина на месте, блядь!"
                    };
                case ChatterTopic.SortDepositMaterials:
                    return new[]
                    {
                        "Весь стройбат, хлам и инструменты уложил в рабочие ящики, сука!",
                        "Материалы рассортированы под чистую, блядь!"
                    };
                case ChatterTopic.SortFinished:
                    return new[]
                    {
                        "Принимай работу, начальник хуев! Все ящики базы отсортированы и утрамбованы!",
                        "Порядок на базе наведён на сто процентов, блядь! Пойду перекурю!"
                    };
                case ChatterTopic.LootFoundGood:
                    return new[]
                    {
                        "Опа, халявный лут! В карантине всё что плохо лежит — автоматически наше, блядь!",
                        "Ништяк в кармане, сука! Двигаем дальше!"
                    };
                case ChatterTopic.LootOverweight:
                    return new[]
                    {
                        "Сука, я уже еле ноги переставляю от твоего хабара! Беру только патроны, аптечки и жратву!",
                        "Рюкзак забит под горло, блядь! Всякий тяжёлый металлолом оставляю на земле!"
                    };
                case ChatterTopic.BaseAutoDropoff:
                    return new[]
                    {
                        "Сгрузил всю добычу из рюкзака прямо в ящики базы, блядь! Сразу дышать легче стало!",
                        "Хабар дома, сука! Всё разложил по категориям автоматически!"
                    };
                case ChatterTopic.AmbientRain:
                    return new[]
                    {
                        "Дождина ебашит без остановки, сука... Вся снаряга насквозь промокла!",
                        "Холодный ливень, блядь — как бы воспаление лёгких не схватить в этой дыре."
                    };
                case ChatterTopic.AmbientBloodRain:
                    return new[]
                    {
                        "Кровавый ливень хуярит с неба, блядь! Заражённые щас попрут со всех щелей, готовься!",
                        "Ебать жуть какая — всё вокруг в красном тумане! Держи палец на курке, сука!"
                    };
                case ChatterTopic.AmbientNight:
                    return new[]
                    {
                        "Ночь опустилась, блядь, хоть глаз выколи! Свети фонарём и не шуми лишний раз!",
                        "В такую темень только за приключениями на свою жопу ходить, сука!"
                    };
                case ChatterTopic.AmbientMorning:
                    return new[]
                    {
                        "Светает, блядь! Живы-целы — и то хлеб в этом дурдоме!",
                        "Рассвет встретили на ногах, сука! Значит, ещё повоюем!"
                    };
                case ChatterTopic.AmbientHungry:
                    return new[]
                    {
                        "У меня кишки уже урчат громче, чем генератор, блядь! Давай пожрём чего-нибудь!",
                        "Голодуха лютая, сука... Сейчас бы хоть сухарь с тушёнкой заточить!"
                    };
                case ChatterTopic.AmbientTired:
                    return new[]
                    {
                        "Ноги ватные, башка гудит, блядь... Нам срочно нужен привал, командир!",
                        "Заебался я сегодня бегать по этим развалинам, сука. Поспать бы!"
                    };
                case ChatterTopic.AmbientBaseIdle:
                    return new[]
                    {
                        "На базе тишь да гладь, блядь. Хоть пять минут без этой беготни и стрельбы.",
                        "Сидим ровно, припасы в ящиках, периметр под прицелом. Живём, сука!"
                    };
                case ChatterTopic.BanterQuestion:
                    return new[]
                    {
                        "Слышь, ты чё так шумно топаешь, слон ебаный? Всех зомбаков с округи соберёшь!",
                        "Эй, напарник, у тебя ебальник такой кислый, будто ты сырого мяса нажрался!"
                    };
                case ChatterTopic.BanterReply:
                    return new[]
                    {
                        "Сам ты слон, блядь! На себя посмотри — гремишь рюкзаком как консервная банка!",
                        "На свой ебальник в зеркало глянь, шутник хуев! Главное, что стреляю метко!"
                    };
                case ChatterTopic.WorldNpcEncounter:
                    return new[]
                    {
                        "Проходи мимо и не задерживайся, блядь! Тут чужакам не рады!",
                        "Стволом не маши перед носом, сука, и разойдёмся мирно!"
                    };
                default:
                    return Array.Empty<string>();
            }
        }

        private static string[] GetExtraRudeEn(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.PlayerEatSteakMeat:
                    return new[]
                    {
                        "haven't seen a real piece of steak since the outbreak started, and you're wolfing it down solo!",
                        "That grilled meat smells so damn good my stomach is staging a mutiny!"
                    };
                case ChatterTopic.PlayerEatRawOrBad:
                    return new[]
                    {
                        "Eating raw meat like a wild animal?! Don't puke on my boots later, dumbass!"
                    };
                case ChatterTopic.PlayerEatCanned:
                    return new[]
                    {
                        "More tin-can mystery stew... At least it keeps us standing in this hellhole."
                    };
                case ChatterTopic.PlayerShareFoodThanks:
                    return new[]
                    {
                        "Now that's a real leader! Belly's full and I'm ready to tear through a whole horde!"
                    };
                case ChatterTopic.CombatSpotZombie:
                    return new[]
                    {
                        "Ugly walker shuffling in — about to ventilate its skull!",
                        "Got a live one—well, dead one! Dropping it!"
                    };
                case ChatterTopic.CombatSpotHorde:
                    return new[]
                    {
                        "Whole pack of rotters pouring in! Keep moving and dump every mag!"
                    };
                case ChatterTopic.CombatSpotBandit:
                    return new[]
                    {
                        "Looters with guns! Use cover and drop those pricks fast!"
                    };
                case ChatterTopic.CombatSpotBoss:
                    return new[]
                    {
                        "Look at the size of that mutant bastard! Stay back and focus fire!"
                    };
                case ChatterTopic.CombatKiting:
                    return new[]
                    {
                        "Keeping my distance and feeding 'em lead on the retreat!"
                    };
                case ChatterTopic.CombatClearFireLine:
                    return new[]
                    {
                        "Whoa, watch your muzzle! Stepped aside — light 'em up!"
                    };
                case ChatterTopic.CombatKillEnemy:
                    return new[]
                    {
                        "Headshot, motherfucker! Stay dead this time!",
                        "Another corpse on the pile! Next!"
                    };
                case ChatterTopic.CombatSavePlayer:
                    return new[]
                    {
                        "Saved your ass again, boss! Keep your eyes peeled!"
                    };
                case ChatterTopic.CombatFlankWarning:
                    return new[]
                    {
                        "Hostiles right behind you, boss! Spin around now!"
                    };
                case ChatterTopic.CombatWeaponJammed:
                    return new[]
                    {
                        "Casing jammed in the chamber! Racking the bolt, cover me!"
                    };
                case ChatterTopic.SortStart:
                    return new[]
                    {
                        "Time to play warehouse manager again! Sorting all this junk into the base chests!"
                    };
                case ChatterTopic.SortFinished:
                    return new[]
                    {
                        "All chests sorted and packed tight, boss! Time for a breather!"
                    };
                case ChatterTopic.LootOverweight:
                    return new[]
                    {
                        "My pack is bursting at the seams! Only grabbing ammo, meds, and grub now!"
                    };
                case ChatterTopic.BaseAutoDropoff:
                    return new[]
                    {
                        "Auto-stashed all our raid loot into the base chests! Feels good to unload!"
                    };
                case ChatterTopic.BanterQuestion:
                    return new[]
                    {
                        "Why are you stomping around like an elephant? You'll wake the whole block!"
                    };
                case ChatterTopic.BanterReply:
                    return new[]
                    {
                        "Shut up, your backpack rattles like a tin can factory!"
                    };
                case ChatterTopic.WorldNpcEncounter:
                    return new[]
                    {
                        "Keep moving, drifter — nobody trusts strangers in this zone!",
                        "Watch your step and keep your hands where I can see 'em, pal!"
                    };
                case ChatterTopic.PlayerEatFish:
                    return new[] { "Pan-fried fish in a quarantine zone?! Toss me a piece before I steal the pan!" };
                case ChatterTopic.PlayerEatCookedMeal:
                    return new[] { "Hot food at last! Smells ten times better than canned mystery meat!" };
                case ChatterTopic.PlayerEatSnackOrFruit:
                    return new[] { "Snacking on the move? Save some sugar for the rest of us!" };
                case ChatterTopic.PlayerDrinkWaterSoda:
                    return new[] { "Hey, leave a couple gulps in that bottle, my throat's parched!" };
                case ChatterTopic.PlayerDrinkAlcohol:
                    return new[] { "Drinking solo on watch?! Pass the bottle over here, boss!" };
                case ChatterTopic.PlayerHealBandage:
                    return new[] { "Patch those holes up quick — we've still got half the block to clear!" };
                case ChatterTopic.PlayerReloadAmmo:
                    return new[] { "Top off every mag, boss — next street's crawling with rotters!" };
                case ChatterTopic.PlayerRepairOrCraft:
                    return new[] { "Tighten those bolts good so it doesn't fall apart mid-fight!" };
                case ChatterTopic.PlayerCookingBBQ:
                    return new[] { "Smoke's rising over the whole block, but damn that meat smells worth it!" };
                case ChatterTopic.PlayerLockpickBreakDoor:
                    return new[] { "Pop that lock open already, let's see what loot's inside!" };
                case ChatterTopic.CombatSwitchMelee:
                    return new[] { "Too close for guns — pulling my blade to carve 'em up!" };
                case ChatterTopic.CombatSwitchRanged:
                    return new[] { "Got some distance — rifle's back up!" };
                case ChatterTopic.CombatLowAmmo:
                    return new[] { "Down to my last mag, dammit! Need ammo soon!" };
                case ChatterTopic.CombatWounded:
                    return new[] { "Took a nasty hit, dammit, but I'm still standing!" };
                case ChatterTopic.FieldMedicAid:
                    return new[] { "Slapped my last bandage on your wounds — don't waste it, boss!" };
                case ChatterTopic.FieldAmmoShare:
                    return new[] { "Here's a fresh pack of rounds from my bag — load up and fire!" };
                case ChatterTopic.SortPickUp:
                    return new[] { "Grabbed the unsorted haul, moving it to the category chests..." };
                case ChatterTopic.SortDepositWeapons:
                    return new[] { "Guns and ammo stacked neat in the armory crate!" };
                case ChatterTopic.SortDepositFood:
                    return new[] { "Rations and drinks stashed in the food chests!" };
                case ChatterTopic.SortDepositMeds:
                    return new[] { "Medkits and bandages locked into the medical box!" };
                case ChatterTopic.SortDepositMaterials:
                    return new[] { "Scrap, tools, and parts sorted into the workshop crates!" };
                case ChatterTopic.LootFoundGood:
                    return new[] { "Sweet find on the ground — stashing it in the squad pack!" };
                case ChatterTopic.AmbientRain:
                    return new[] { "Pouring like hell again... Gear's soaked through!" };
                case ChatterTopic.AmbientBloodRain:
                    return new[] { "Red sky and blood rain — the infected are going feral out there!" };
                case ChatterTopic.AmbientNight:
                    return new[] { "Dark as a tomb out here — keep your light steady!" };
                case ChatterTopic.AmbientMorning:
                    return new[] { "Made it to sunrise in one piece — that's a win in my book!" };
                case ChatterTopic.AmbientHungry:
                    return new[] { "Gut's rumbling louder than an engine — we need food soon!" };
                case ChatterTopic.AmbientTired:
                    return new[] { "Legs feel like lead, boss... Need a bunk or a campfire break!" };
                case ChatterTopic.AmbientBaseIdle:
                    return new[] { "Base perimeter's quiet — good time to check our chests and clean the guns." };
                default:
                    return Array.Empty<string>();
            }
        }
    }
}
