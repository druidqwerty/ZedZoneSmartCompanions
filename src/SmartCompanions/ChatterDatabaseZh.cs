using System;

namespace ZedZone.SmartCompanions
{
    public static partial class ChatterDatabase
    {
        public static string[] GetRudeLinesZh(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.PlayerEatSteakMeat:
                    return new[]
                    {
                        "好吃吗你他妈的？！自己一个人偷吃牛排，老子连肉味都没闻过！",
                        "卧槽，隔离区里还能吃上真牛排？！分老子一口，别当铁公鸡！",
                        "妈的，这烤肉味太香了，老子肚子都叫得像丧尸吼了！",
                        "你小子吃得倒爽！小心香味把半条街的死鬼全招过来！",
                        "看你大口嚼肉，老子羡慕得想啃枪托了，快留块边角料！"
                    };
                case ChatterTopic.PlayerEatRawOrBad:
                    return new[]
                    {
                        "你他妈疯了吧生吃这玩意儿？！一会儿拉肚子拉死老子可不背你！",
                        "卧槽，真恶心！好歹架火上烤一下再往嘴里塞啊！",
                        "妈的，比丧尸吃得还生猛！离老子远点，别吐我一身！",
                        "你这铁胃是钛合金做的吧？！吃生肉也不怕长寄生虫！"
                    };
                case ChatterTopic.PlayerEatCanned:
                    return new[]
                    {
                        "又是这该死的罐头……一股盐纸板味，不过总比饿死强。",
                        "空铁罐别乱扔地上，傻逼，叮当响得整条街都能听见！",
                        "老牌肉罐头，末日硬通货！赶紧吃，吃完继续干活。",
                        "妈的，这罐头比老子年纪都大，居然还没坏！"
                    };
                case ChatterTopic.PlayerEatFish:
                    return new[]
                    {
                        "鲜鱼不错啊！巡逻的时候别他妈被鱼刺卡住嗓子！",
                        "卧槽，香煎鱼排！给老子留根鱼尾巴，你个狗大户！",
                        "这鬼地方还能吃上河鲜，你小子真他妈会享受！"
                    };
                case ChatterTopic.PlayerEatCookedMeal:
                    return new[]
                    {
                        "这才叫他妈的人吃的饭！比啃冷罐头强一万倍！",
                        "闻着真香，老大！吃饱喝足，一会儿好去砍翻那帮死鬼！",
                        "卧槽，热乎的硬菜！老子闻着味儿口水都快把防毒面具淹了！"
                    };
                case ChatterTopic.PlayerEatSnackOrFruit:
                    return new[]
                    {
                        "像鸟一样啄点果子顶个屁用，老子想要大块烤肉和子弹！",
                        "搞点甜食补糖分？行吧，只要别掉队就行！",
                        "还有零嘴吃？！分老子半块，嘴巴淡出鸟来了！"
                    };
                case ChatterTopic.PlayerDrinkWaterSoda:
                    return new[]
                    {
                        "别他妈一个人把水壶吹干了！老子嗓子都快冒烟了！",
                        "卧槽，旧世界的肥宅快乐水？！给兄弟留一口，别独吞！",
                        "咕咚咕咚喝得真爽，老子嘴唇都干裂起皮了！"
                    };
                case ChatterTopic.PlayerDrinkAlcohol:
                    return new[]
                    {
                        "偷偷开冰啤酒不给老子倒一杯？！不够意思啊老大！",
                        "喝吧喝吧，在这鬼地方完全清醒着才容易发疯！",
                        "妈的，酒香飘过来了！别喝高了把老子当丧尸崩了就行！"
                    };
                case ChatterTopic.PlayerHealBandage:
                    return new[]
                    {
                        "被抓得不轻啊？！他妈的赶紧把绷带勒紧，老子替你架枪！",
                        "别往地上滴血了！快包扎好，周围交给我盯死！",
                        "操，伤口挺深啊！忍着点疼，千万别死在这儿！"
                    };
                case ChatterTopic.PlayerReloadAmmo:
                    return new[]
                    {
                        "趁现在把弹匣全压满，妈的，开打再装弹就是找死！",
                        "压满子弹！一会儿请那帮杂碎吃顿饱铅弹！",
                        "听着子弹咔哒咔哒进弹匣的声音，老子心里才踏实！"
                    };
                case ChatterTopic.PlayerRepairOrCraft:
                    return new[]
                    {
                        "把那破家伙修结实点，别他妈在尸潮里卡壳掉链子！",
                        "敲锤子小声点，蠢货，想把全岛的行尸都敲醒吗？！",
                        "手艺挺牛逼啊老大，顺便帮老子看看这把枪的撞针！"
                    };
                case ChatterTopic.PlayerCookingBBQ:
                    return new[]
                    {
                        "卧槽，这烧烤味能飘二里地！赶紧翻面，在丧尸来之前吃进肚里！",
                        "大厨，别他妈把晚饭烤焦了，老子口水都流一地了！",
                        "滋滋冒油啊！谁敢这时候来抢烤肉，老子一喷子轰碎他！"
                    };
                case ChatterTopic.PlayerLockpickBreakDoor:
                    return new[]
                    {
                        "直接一脚踹开不就完了吗？！磨磨唧唧撬什么锁！",
                        "枪举稳了，老子敢打赌门后面肯定关着几只饿鬼！",
                        "撬快点老大，老子在后面替你放风，别让人堵在门口！"
                    };
                case ChatterTopic.PlayerShareFoodThanks:
                    return new[]
                    {
                        "牛逼啊老大，还记着兄弟一口吃的！老子现在吃饱喝足，谁来干谁！",
                        "够义气！肚里有粮心里不慌，走，带老子去把尸群撕碎！",
                        "他妈的太香了！冲这口热饭，今天这条命归你了！",
                        "谢了兄弟！吃饱了力气翻倍，看老子一会儿怎么爆那些杂碎的头！"
                    };
                case ChatterTopic.SortStart:
                    return new[]
                    {
                        "又让老子给你收拾这堆烂摊子！真把老子当免费仓管骡子了？！",
                        "卧槽，这箱子乱得跟猪窝一样！闪开，老子把基地彻底理一遍！",
                        "行行行，老子去理箱子！枪和罐头都堆一块儿了，真有你的！"
                    };
                case ChatterTopic.SortPickUp:
                    return new[]
                    {
                        "把收货箱里的破烂全抱起来了，正往分类箱里搬……",
                        "你小子真能囤货，老子腰都快搬断了！按类别归位中！",
                        "让一让，别挡道！老子怀里抱满物资，正在往仓库分拣！"
                    };
                case ChatterTopic.SortDepositWeapons:
                    return new[]
                    {
                        "枪械、配件和弹匣全塞进军火箱了！随时可以开战，妈的！",
                        "武器弹药归位完毕！整整齐齐，看着就舒坦！",
                        "军火箱码满了！下次谁来抄家，直接开箱拿家伙轰他娘的！"
                    };
                case ChatterTopic.SortDepositFood:
                    return new[]
                    {
                        "吃的喝的全放进食品箱了——老子路上可一口都没偷吃！",
                        "肉食和罐头分门别类码好了！这周不用担心饿肚子了。",
                        "口粮和饮水全归位了！看着满满一箱子吃的，心里真他妈踏实！"
                    };
                case ChatterTopic.SortDepositMeds:
                    return new[]
                    {
                        "绷带、止痛药和急救包全码进医疗箱了，别他妈拿错药！",
                        "医疗物资整理完毕，救命的家伙都在老地方！",
                        "急救箱塞得整整齐齐！以后挂彩了直接来这箱子翻！"
                    };
                case ChatterTopic.SortDepositMaterials:
                    return new[]
                    {
                        "废铁、工具和制作材料全丢进工坊箱了，你个捡破烂之王！",
                        "材料和维修零件全部归位，一点格子都没浪费！",
                        "螺丝、木板和修车工具全码进工坊箱了，强迫症看了都说爽！"
                    };
                case ChatterTopic.SortFinished:
                    return new[]
                    {
                        "行了，全基地箱子都给你码得明明白白！欠老子一瓶冰啤酒！",
                        "仓库整理完毕，老大！每颗子弹、每罐肉都按标签放好了！",
                        "呼——总算把这堆山一样的物资全归好类了，老子歇会儿！"
                    };
                case ChatterTopic.LootFoundGood:
                    return new[]
                    {
                        "卧槽，地上有好货！不拿白不拿，归咱们小队了！",
                        "顺手摸到点好物资！塞进背包带走！",
                        "发财了老大，这儿有好东西，老子直接揣兜里了！"
                    };
                case ChatterTopic.LootOverweight:
                    return new[]
                    {
                        "他妈的背包装得快炸了！破铜烂铁别捡了，老子现在只收弹药、医疗和口粮！",
                        "老子又不是卡车！负重快满了，自动过滤垃圾，只拿救命物资！",
                        "背不动了操！再塞破烂老子腰都要折了，只捡子弹、药和吃的！"
                    };
                case ChatterTopic.BaseAutoDropoff:
                    return new[]
                    {
                        "回基地了！老子把搜刮来的战利品全倒进收货箱并按类别码好了！",
                        "自动卸货搞定！背包清空，肩膀终于他妈的活过来了！",
                        "到家第一件事：把战利品全卸进基地分类箱，一身轻松！"
                    };
                case ChatterTopic.AmbientRain:
                    return new[]
                    {
                        "操，又下大雨了……浑身湿透，冻得老子直哆嗦！",
                        "这鬼天气只适合青蛙！赶紧找个屋檐避一避吧。",
                        "妈的，靴子里全是雨水，走一步吱嘎响！"
                    };
                case ChatterTopic.AmbientBloodRain:
                    return new[]
                    {
                        "卧槽，下血雨了！外面的感染者肯定要发狂，手指别离开扳机！",
                        "天都在流血，他妈的邪门透了！盯紧四周！",
                        "血雨来了！这鬼天气丧尸鼻子灵得很，千万别大意！"
                    };
                case ChatterTopic.AmbientNight:
                    return new[]
                    {
                        "黑得伸手不见五指！打开手电筒，小心暗处扑出来的杂碎！",
                        "夜班开始了……跟紧点，晚上出来的全是狠角色。",
                        "妈的，这夜路阴森森的，随便哪个角落都可能蹦出只死鬼！"
                    };
                case ChatterTopic.AmbientMorning:
                    return new[]
                    {
                        "总算他妈的天亮了！又在鬼门关前混过一宿！",
                        "出太阳了！这下能看清那些丑八怪的脑袋再开枪了！",
                        "活着见到早上的太阳，在隔离区里就算中头奖了！"
                    };
                case ChatterTopic.AmbientHungry:
                    return new[]
                    {
                        "妈的，饿得前胸贴后背了！老大，赏口吃的吧，不然没力气抡刀了！",
                        "肚子叫得跟打雷一样……再不搞点吃的，老子看见丧尸都想啃两口！",
                        "饿得眼冒金星了，哪怕给块发硬的肉干也行啊！"
                    };
                case ChatterTopic.AmbientTired:
                    return new[]
                    {
                        "累得像条死狗……咱们得找张床或者生堆火歇会儿了！",
                        "眼皮直打架，老大。再走两里地老子站着都能睡着。",
                        "两条腿跟灌了铅一样沉，什么时候回基地睡一觉啊？"
                    };
                case ChatterTopic.AmbientBaseIdle:
                    return new[]
                    {
                        "基地里总算清静一会儿，没丧尸咬屁股的感觉真他妈爽。",
                        "咱们这小据点整得还挺像样，只要箱子理干净、门锁死就稳了。",
                        "难得消停片刻，擦擦枪管、清点下弹匣，随时准备下一仗。"
                    };
                case ChatterTopic.BanterQuestion:
                    return new[]
                    {
                        "喂，那边那个蠢货，走快点行不行，没吃饭啊？",
                        "眼睛放亮一点，傻逼，别让人从背后摸了脖子！",
                        "打赌吗？今天老子崩掉的丧尸绝对比你多一倍！"
                    };
                case ChatterTopic.BanterReply:
                    return new[]
                    {
                        "滚蛋，老子背着两箱子你的弹药和罐头呢！",
                        "管好你自己的屁股吧，老子这边的防区干净得很！",
                        "少吹牛逼了，刚才那只大家伙明明是老子一枪爆头的！"
                    };
                case ChatterTopic.WorldNpcEncounter:
                    return new[]
                    {
                        "看什么看？！不想挨枪子儿就离老子的地盘远点！",
                        "又是游荡的拾荒客……手别乱碰老子的物资，大家相安无事！",
                        "滚远点，陌生人！这片街区现在归老子罩着！"
                    };
                default:
                    return GetRudeCombatLinesZh(topic);
            }
        }

        public static string[] GetCleanLinesZh(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.PlayerEatSteakMeat:
                    return new[]
                    {
                        "一定很好吃吧？是吧？我以前还从来没尝过真正的牛排呢……",
                        "唔，烤肉的香味！光是闻着就流口水了……能留一小口吗？",
                        "在隔离区还能吃上热腾腾的牛排？队长，你这伙食简直像旧世界的国王！",
                        "快吃吧队长，你需要补充体力。不过这块牛排闻起来实在太香了……",
                        "听见我肚子咕咕叫了吗？全怪你手里这块多汁的烤肉！"
                    };
                case ChatterTopic.PlayerEatRawOrBad:
                    return new[]
                    {
                        "等等，你真的要生吃那个吗？！好歹在篝火上烤熟再吃啊！",
                        "看着都让人担心肠胃……要是吃坏了肚子可得赶紧找药。"
                    };
                case ChatterTopic.PlayerEatCanned:
                    return new[]
                    {
                        "又是罐头？虽然味道一般，但在末日里最耐放。",
                        "午餐肉罐头可是隔离区的硬通货，慢点吃！"
                    };
                case ChatterTopic.PlayerEatFish:
                    return new[]
                    {
                        "新鲜的鱼肉！看来咱们在水边钓鱼没白费功夫。",
                        "好香的煎鱼味……小心鱼刺，队长！"
                    };
                case ChatterTopic.PlayerEatCookedMeal:
                    return new[]
                    {
                        "热乎乎的熟食！在如今这世道比黄金还珍贵。",
                        "吃饱了才有力气战斗，闻起来真不错！"
                    };
                case ChatterTopic.PlayerEatSnackOrFruit:
                    return new[]
                    {
                        "补充点维生素和糖分也不错，保持头脑清醒！",
                        "边走边吃点小零嘴，省时又管用。"
                    };
                case ChatterTopic.PlayerDrinkWaterSoda:
                    return new[]
                    {
                        "旧世界的汽水！里面还有气泡吗？",
                        "多喝点水，队长，在这里脱水比感染者更危险。"
                    };
                case ChatterTopic.PlayerDrinkAlcohol:
                    return new[]
                    {
                        "喝点冰啤酒放松一下？不过小心别影响了准头！",
                        "敬幸存者一杯！祝咱们弹药充足、一路平安。"
                    };
                case ChatterTopic.PlayerHealBandage:
                    return new[]
                    {
                        "伤得重吗？快把绷带扎紧，我来替你警戒四周！",
                        "安心包扎伤口吧，队长，有我在没人能靠近你！"
                    };
                case ChatterTopic.PlayerReloadAmmo:
                    return new[]
                    {
                        "提前压满弹匣是对的，战斗打响可没工夫装子弹。",
                        "我替你掩护，把弹匣都装满吧！"
                    };
                case ChatterTopic.PlayerRepairOrCraft:
                    return new[]
                    {
                        "手艺不错！平时保养好装备，关键时刻才不会掉链子。",
                        "安心修理装备吧，我负责盯着周围。"
                    };
                case ChatterTopic.PlayerCookingBBQ:
                    return new[]
                    {
                        "篝火烤肉的味道太香了！希望别把附近的感染者吸引过来。",
                        "大厨上线了！我已经准备好碗筷等开饭啦。"
                    };
                case ChatterTopic.PlayerLockpickBreakDoor:
                    return new[]
                    {
                        "小心开门，我举枪替你盯着门后的动静！",
                        "不知道锁后面藏着什么物资，希望不是游荡的感染者。"
                    };
                case ChatterTopic.PlayerShareFoodThanks:
                    return new[]
                    {
                        "谢谢队长分我口粮！现在体力充沛，随时可以出发战斗！",
                        "太感谢了！吃饱喝足，准头和士气都拉满了！"
                    };
                case ChatterTopic.SortStart:
                    return new[]
                    {
                        "收到！开始整理基地仓库，把所有物资按箱子类别放好。",
                        "明白，正在清点收货箱并按分类规整基地容器！"
                    };
                case ChatterTopic.SortPickUp:
                    return new[]
                    {
                        "已取出待整理的战利品，正在送往对应的分类储物箱……",
                        "正在搬运物资归类上架，请稍候！"
                    };
                case ChatterTopic.SortDepositWeapons:
                    return new[]
                    {
                        "武器、枪械配件和弹药已整齐存入军火箱！",
                        "军火库整理完毕，武器和弹匣都已归位！"
                    };
                case ChatterTopic.SortDepositFood:
                    return new[]
                    {
                        "新鲜食材、罐头和饮用水已分类放入食品储物箱！",
                        "厨房与口粮储备已归整完毕！"
                    };
                case ChatterTopic.SortDepositMeds:
                    return new[]
                    {
                        "绷带、急救包与药品已存入医疗专用箱！",
                        "医疗物资整理完毕，急用时随时可取！"
                    };
                case ChatterTopic.SortDepositMaterials:
                    return new[]
                    {
                        "制作材料、工具与零件已归入工坊储物箱！",
                        "各类材料与维修工具已紧凑码放完毕！"
                    };
                case ChatterTopic.SortFinished:
                    return new[]
                    {
                        "基地仓库整理完毕！所有物品均已按类别归位并自动紧凑排列。",
                        "报告队长，储物箱全部分类整理好了，井井有条！"
                    };
                case ChatterTopic.LootFoundGood:
                    return new[]
                    {
                        "发现不错的战利品！已经收进小队背包。",
                        "捡到实用物资了，积少成多！"
                    };
                case ChatterTopic.LootOverweight:
                    return new[]
                    {
                        "背包快满了！现在只优先拾取弹药、医疗品和食物！",
                        "负重接近上限，已自动过滤杂物，只拿核心补给！"
                    };
                case ChatterTopic.BaseAutoDropoff:
                    return new[]
                    {
                        "回到基地了！已自动将背包里的战利品卸入储物箱并按类别排好！",
                        "自动卸货完成，搜刮来的物资都已归入基地仓库！"
                    };
                case ChatterTopic.AmbientRain:
                    return new[]
                    {
                        "又开始下雨了……注意保暖，雨声正好能掩护我们的脚步。",
                        "雨水把衣服打湿了，找个地方避避雨吧。"
                    };
                case ChatterTopic.AmbientBloodRain:
                    return new[]
                    {
                        "是血雨！空气都不对劲了，感染者会变得极具攻击性，千万小心！",
                        "天空变红了，血雨期间大家保持最高警戒！"
                    };
                case ChatterTopic.AmbientNight:
                    return new[]
                    {
                        "天黑了，检查手电筒电量，不要离开队伍太远。",
                        "夜晚视线受阻，注意听周围的脚步声。"
                    };
                case ChatterTopic.AmbientMorning:
                    return new[]
                    {
                        "黎明到了！咱们又平安度过了一夜。",
                        "太阳出来了，视野清晰多了，准备出发吧？"
                    };
                case ChatterTopic.AmbientHungry:
                    return new[]
                    {
                        "肚子有点饿了，队长，有空的话咱们吃点东西补充体力吧。",
                        "体力因为饥饿下降了，得找点口粮垫垫肚子。"
                    };
                case ChatterTopic.AmbientTired:
                    return new[]
                    {
                        "跑了一整天，双腿有些发沉，有机会的话回基地休息一下吧。",
                        "有点困倦了，下次行动前最好睡一觉恢复精力。"
                    };
                case ChatterTopic.AmbientBaseIdle:
                    return new[]
                    {
                        "基地里真安静，趁现在检查一下武器和储物箱吧。",
                        "咱们的据点越来越稳固了，物资分类也很清楚。"
                    };
                case ChatterTopic.BanterQuestion:
                    return new[]
                    {
                        "喂，搭档，你的弹药还够用吗？保持警惕！",
                        "你说隔离区外面现在还有正常运转的城市吗？"
                    };
                case ChatterTopic.BanterReply:
                    return new[]
                    {
                        "弹药充足，准星稳得很！你管好自己的防区就行。",
                        "只要咱们齐心协力活下去，总有一天能看到外面的世界！"
                    };
                case ChatterTopic.WorldNpcEncounter:
                    return new[]
                    {
                        "保持距离，陌生人。隔离区里大家都很警惕。",
                        "前面街区刚出现过感染者，走路小心点。"
                    };
                default:
                    return GetCleanCombatLinesZh(topic);
            }
        }
    }
}
