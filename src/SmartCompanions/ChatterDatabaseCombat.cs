using System;

namespace ZedZone.SmartCompanions
{
    public static partial class ChatterDatabase
    {
        private static string[] GetCombatLinesRu(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.CombatSpotZombie:
                    return new[]
                    {
                        "Контакт! Ходячий по курсу, беру на мушку!",
                        "Вижу заражённого! Не подпускай его близко!",
                        "Мертвец впереди! Работаем аккуратно.",
                        "Ещё один гнилой ковыляет к нам. Сейчас успокою!",
                        "Цель обнаружена! Прикрываю тебя!"
                    };

                case ChatterTopic.CombatSpotHorde:
                    return new[]
                    {
                        "Твою мать, да их тут целая толпа! Держи дистанцию, не давай окружить!",
                        "Орда! Не стой на месте, откатываемся и отстреливаем по одному!",
                        "Слишком много целей! Контролируй фланги, я держу передних!",
                        "Волна мертвецов! Следи за перезарядкой и береги выносливость!"
                    };

                case ChatterTopic.CombatSpotBandit:
                    return new[]
                    {
                        "Внимание, вооружённый выживший! Ищи укрытие, он может стрелять!",
                        "Мародёры! Это не тупые зомби — двигайся зигзагом и бей на поражение!",
                        "Вражеский стрелок на горизонте! Подавляю огнём, обходи с фланга!",
                        "Живой противник! Береги голову, работаем по тактике!"
                    };

                case ChatterTopic.CombatSpotBoss:
                    return new[]
                    {
                        "Охренеть, что это за мутант?! Не подходи к нему вплотную, работаем издалека!",
                        "Крупная тварь! Сфокусируй весь огонь на ней и следи за её рывками!",
                        "Босс в секторе! Держим максимальную дистанцию, сейчас будет жарко!"
                    };

                case ChatterTopic.CombatKiting:
                    return new[]
                    {
                        "Слишком близко! Разрываю дистанцию и веду огонь на отходе!",
                        "Откатываюсь назад! Не даю им зажать меня в угол!",
                        "Меняю позицию! Держу тварей на прицеле в движении!",
                        "Отхожу на пару шагов для выстрела — прикрой манёвр!"
                    };

                case ChatterTopic.CombatClearFireLine:
                    return new[]
                    {
                        "Ухожу с твоей линии огня! Стреляй свободно!",
                        "Освобождаю сектор обстрела, не задень меня!",
                        "Смещаюсь вбок — сектор чист, открывай огонь!"
                    };

                case ChatterTopic.CombatSwitchMelee:
                    return new[]
                    {
                        "Вплотную подошли! Перехожу на холодное оружие!",
                        "Нет времени перезаряжать — рублю в ближнем бою!",
                        "Достаю клинок! Сейчас поговорим коротко!"
                    };

                case ChatterTopic.CombatSwitchRanged:
                    return new[]
                    {
                        "Дистанция есть — снова беру огнестрел!",
                        "Пушка заряжена, перехожу на стрельбу!",
                        "Отогнал их! Снова работаю из ствола!"
                    };

                case ChatterTopic.CombatKillEnemy:
                    return new[]
                    {
                        "Готов! Минус один, ищем следующую цель.",
                        "Лежать и не вставать! Чистая работа.",
                        "Уложил! Кто следующий?",
                        "Цель уничтожена! Сектор чист.",
                        "Точно в яблочко! Этот больше никого не укусит."
                    };

                case ChatterTopic.CombatSavePlayer:
                    return new[]
                    {
                        "Отвали от командира, гниль! Я прикрываю твою спину!",
                        "Вовремя я успел! Этот гад чуть тебя со спины не цапнул!",
                        "Снял его с тебя! Держись рядом со мной!"
                    };

                case ChatterTopic.CombatLowAmmo:
                    return new[]
                    {
                        "Чёрт, патроны на исходе! Перехожу на экономию или ближний бой!",
                        "Магазины почти пустые! Нам срочно нужен боекомплект!",
                        "Свинец заканчивается! Долго стрелять не смогу!"
                    };

                case ChatterTopic.CombatWounded:
                    return new[]
                    {
                        "Агх, меня зацепило! Нужна передышка на перевязку!",
                        "Чёрт, кровь идёт... Прикрой меня на пару секунд!",
                        "Ранен, но ещё в строю! Не списывай меня со счетов!"
                    };

                case ChatterTopic.FieldMedicAid:
                    return new[]
                    {
                        "Ты истекаешь кровью! Стой смирно, экстренно перевязываю тебя из своих запасов!",
                        "Полевой медик на месте! Вколол стимулятор и наложил повязку — живи, командир!",
                        "Не умирать! Перевязал твои раны своей аптечкой, держись!"
                    };

                case ChatterTopic.FieldAmmoShare:
                    return new[]
                    {
                        "Слышу пустой щелчок! Лови боекомплект из моего запаса — стреляй!",
                        "Делюсь патронами! Перезаряжайся скорее, я держу сектор!",
                        "Передал тебе запасные патроны! Не останавливай огонь!"
                    };

                case ChatterTopic.CombatFlankWarning:
                    return new[]
                    {
                        "Осторожно, командир! Враги заходят тебе со спины — обернись!",
                        "Угроза с тыла! Перехватываю тех, кто подкрался сзади!"
                    };

                case ChatterTopic.CombatWeaponJammed:
                    return new[]
                    {
                        "Оружие заклинило! Прикрой на пару секунд, устраняю задержку!",
                        "Поймал клин затвора! Сейчас быстро прочищу ствол!"
                    };

                default:
                    return new[] { "Я на связи, командир!" };
            }
        }

        private static string[] GetLinesEn(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.PlayerEatSteakMeat:
                    return new[]
                    {
                        "Must taste pretty good, huh? Yeah? I've actually never tried a real steak...",
                        "Mmm, roasted meat! Just the smell makes my mouth water... Save me a bite?",
                        "A real steak in the quarantine zone? You're living like an old-world king!",
                        "Eat up, boss, you need your strength. Though that steak smells heavenly...",
                        "Did you hear my stomach growl? That juicy piece of meat is to blame!"
                    };
                case ChatterTopic.PlayerEatRawOrBad:
                    return new[]
                    {
                        "Wait, are you eating THAT raw?! Do you have a titanium stomach or a death wish?",
                        "Ugh, at least roast it over a fire first! Don't blame me if your stomach rebels."
                    };
                case ChatterTopic.PlayerEatCanned:
                    return new[]
                    {
                        "Canned food again? At least it survives the apocalypse.",
                        "Canned stew — the best currency in the zone. Enjoy your meal!"
                    };
                case ChatterTopic.PlayerEatFish:
                    return new[]
                    {
                        "Fresh fish! All that time with the fishing rod paid off.",
                        "Smells like pan-fried fish... A real restaurant on a dead island!"
                    };
                case ChatterTopic.PlayerDrinkWaterSoda:
                    return new[]
                    {
                        "Old-world soda! Does it still have any fizz left?",
                        "Stay hydrated, boss. Thirst kills faster than the infected out here."
                    };
                case ChatterTopic.PlayerDrinkAlcohol:
                    return new[]
                    {
                        "Cracking open a cold one? Just keep your aim steady!",
                        "Cheers to the survivors! Here's to never running out of ammo."
                    };
                case ChatterTopic.PlayerHealBandage:
                    return new[]
                    {
                        "Did they get you bad? Patch up, I'm watching the perimeter!",
                        "Wrap that bandage tight. I've got your back while you heal."
                    };
                case ChatterTopic.PlayerReloadAmmo:
                    return new[]
                    {
                        "Smart move packing magazines now — won't have time mid-fight.",
                        "Covering you while you load up! Nothing beats a full mag."
                    };
                case ChatterTopic.PlayerRepairOrCraft:
                    return new[]
                    {
                        "Good hands! Keeping our gear maintained is how we stay alive.",
                        "Fix it up solid — last thing we need is a jam during a horde."
                    };
                case ChatterTopic.PlayerCookingBBQ:
                    return new[]
                    {
                        "That campfire smell is incredible! Every survivor and zombie for a mile can smell it!",
                        "Chef's on duty! Don't burn dinner, I already have my plate ready."
                    };
                case ChatterTopic.SortStart:
                    return new[]
                    {
                        "On it! Organizing the base storage — putting everything into its category chest.",
                        "Starting base inventory sort! Ammo with ammo, food with food."
                    };
                case ChatterTopic.SortFinished:
                    return new[]
                    {
                        "Base sorting complete! Every item is neatly packed in its category chest.",
                        "All done, boss! The storage chests are sorted and compacted."
                    };
                case ChatterTopic.CombatSpotZombie:
                    return new[] { "Contact! Infected ahead, taking the shot!", "Spotted a walker! Keeping it off you!" };
                case ChatterTopic.CombatSpotHorde:
                    return new[] { "Whole horde incoming! Keep moving, don't let them surround us!", "Too many targets! Kite back and pick them off!" };
                case ChatterTopic.CombatSpotBandit:
                    return new[] { "Hostile human ahead! Watch for gunfire and flank them!", "Armed raider! Strafe and take cover!" };
                case ChatterTopic.CombatSpotBoss:
                    return new[] { "Huge mutant spotted! Keep maximum distance and focus fire!", "Boss threat ahead! Watch its charge attacks!" };
                case ChatterTopic.CombatKiting:
                    return new[] { "Too close! Backing up while firing!", "Kiting back to keep firing distance!" };
                case ChatterTopic.CombatClearFireLine:
                    return new[] { "Clearing your line of fire! Shoot freely!", "Stepping out of your aim corridor!" };
                case ChatterTopic.CombatSwitchMelee:
                    return new[] { "Point-blank! Switching to melee weapon!", "No time to reload — blades out!" };
                case ChatterTopic.CombatSwitchRanged:
                    return new[] { "Distance is clear — switching back to firearm!", "Gun ready, engaging at range!" };
                case ChatterTopic.CombatKillEnemy:
                    return new[] { "Target down! Scanning for next threat.", "Dropped 'em! Clean shot.", "One less threat in the zone!" };
                case ChatterTopic.CombatSavePlayer:
                    return new[] { "Get off the boss! I've got your six!", "Saved your back! Stay close!" };
                case ChatterTopic.FieldMedicAid:
                    return new[] { "You're bleeding out! Hold still, applying emergency field dressing!", "Field medic here! Patched you up from my kit!" };
                case ChatterTopic.FieldAmmoShare:
                    return new[] { "Heard you click empty! Catch some spare ammo from my pack!", "Sharing ammo! Reload and keep firing!" };
                case ChatterTopic.PlayerShareFoodThanks:
                    return new[] { "Thanks for sharing the ration, boss! Feeling energized and ready for action!", "Appreciate the meal! Full stamina and sharp aim!" };
                case ChatterTopic.CombatFlankWarning:
                    return new[] { "Watch your six, boss! Hostiles flanking from behind!", "Threat behind you! Intercepting flankers!" };
                case ChatterTopic.CombatWeaponJammed:
                    return new[] { "Weapon jammed! Cover me for two seconds while I clear it!", "Clearing a weapon malfunction — hold the line!" };
                case ChatterTopic.LootOverweight:
                    return new[] { "Backpack is nearly full! Filtering for ammo, medicine, and food only!", "Overweight limit reached — taking only high-priority supplies!" };
                case ChatterTopic.BaseAutoDropoff:
                    return new[] { "Back at base! Automatically unloaded raid loot into the drop-off chest and sorted storage!", "Auto-dropoff complete: raid supplies sorted into base chests!" };
                case ChatterTopic.WorldNpcEncounter:
                    return new[] { "Keep your distance, stranger. Everyone's on edge in the quarantine zone.", "Watch the streets ahead, spotted walkers a block away." };
                default:
                    return new[] { "Standing by, boss!", "Copy that, watching our sector." };
            }
        }
    }
}
