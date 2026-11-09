using System;

namespace ZedZone.SmartCompanions
{
    public static partial class ChatterDatabase
    {
        private static string[] GetRudeCombatLinesZh(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.CombatSpotZombie:
                    return new[]
                    {
                        "看这丑八怪烂肉！老子现在就给你脑袋开个天窗！",
                        "前面有行尸！给老子趴下，死鬼！",
                        "又晃过来一只烂货——看老子一枪崩了它！",
                        "发现死鬼！别让它凑过来恶心人，开火！"
                    };
                case ChatterTopic.CombatSpotHorde:
                    return new[]
                    {
                        "卧槽，哪钻出来这么一大群烂肉？！边退边打，别被包饺子了！",
                        "尸潮来了，妈的！别站桩当活靶子，拉开距离点名！",
                        "数量太多了！全他妈是丧尸，往后拉扯着清空弹匣！",
                        "操，捅了丧尸窝了！盯紧侧翼，给老子狠狠地扫！"
                    };
                case ChatterTopic.CombatSpotBandit:
                    return new[]
                    {
                        "他妈的是带枪的强盗！走位找掩体，别硬吃枪子儿！",
                        "前面有劫掠者杂碎！这帮狗东西会开枪，包抄干掉他们！",
                        "活人匪徒！爆他们的头，省得留活口抢咱们的物资！",
                        "小心对面火力！老子压制他们，你从侧面摸过去！"
                    };
                case ChatterTopic.CombatSpotBoss:
                    return new[]
                    {
                        "卧槽，那是什么巨型怪物？！拉开最大距离，把所有子弹全倾泻过去！",
                        "妈的，遇到变异大块头了！千万别让这狗娘养的贴脸！",
                        "大家伙出来了！全队集火那个怪物，注意躲它的冲撞！"
                    };
                case ChatterTopic.CombatKiting:
                    return new[]
                    {
                        "贴得太他妈近了！老子边退边打，休想摸到我！",
                        "滚远点，臭烂肉！后撤一步继续爆你狗头！",
                        "拉扯距离中！边走位边点射，溜死这帮蠢货！"
                    };
                case ChatterTopic.CombatClearFireLine:
                    return new[]
                    {
                        "枪口往哪指呢？！老子闪开你的射界了，别他妈打中自己人！",
                        "让开枪线了，老大！瞄准丧尸打，别崩着老子屁股！",
                        "侧移让出火线！尽管开火，别客气！"
                    };
                case ChatterTopic.CombatSwitchMelee:
                    return new[]
                    {
                        "贴到脸上了！去他妈的换弹，拔刀直接剁了它！",
                        "近身肉搏！老子用冷兵器把你脑壳劈成两半！",
                        "没工夫压子弹了——亮刀子，近战砍翻这群杂碎！"
                    };
                case ChatterTopic.CombatSwitchRanged:
                    return new[]
                    {
                        "距离拉开了——重新端枪，送他们上西天！",
                        "有射击空间了！切回枪械继续点名！",
                        "推开身位了，换步枪火力全开！"
                    };
                case ChatterTopic.CombatKillEnemy:
                    return new[]
                    {
                        "吃铅弹去吧，烂肉杂碎！下一个是谁？！",
                        "给老子老老实实躺地上！爆头，漂亮！",
                        "一枪送走！这鬼地方又少一个祸害！",
                        "干翻一个！谁还想来试试老子的准头？！"
                    };
                case ChatterTopic.CombatSavePlayer:
                    return new[]
                    {
                        "把你那臭嘴从老大脖子上挪开，杂碎！救了你一命，回头请老子吃牛排！",
                        "妈的，好险！刚才那死鬼差点咬穿你的后脖梗！",
                        "替你把身后的冷箭挡了！跟紧老子，别乱跑！"
                    };
                case ChatterTopic.CombatLowAmmo:
                    return new[]
                    {
                        "操，老子子弹快打光了！弹匣见底了，省着点打！",
                        "弹药告急，妈的！再没子弹老子只能抡枪托了！",
                        "兜里没几发子儿了！赶紧解决战斗搜刮点弹药！"
                    };
                case ChatterTopic.CombatWounded:
                    return new[]
                    {
                        "嘶——他妈的被抓了一爪子！流血了，掩护老子喘口气！",
                        "操，挂彩了！这帮狗东西，老子要你们血债血偿！",
                        "妈的，流血不止！替老子顶住两秒，包扎一下接着干！"
                    };
                case ChatterTopic.CombatFlankWarning:
                    return new[]
                    {
                        "醒醒，老大！有脏东西从你背后摸上来了，快回头！",
                        "小心后背，妈的！后方有老六偷袭，老子去截住它！",
                        "背后有人摸过来了！别光盯着前面，转头看六点钟方向！"
                    };
                case ChatterTopic.CombatWeaponJammed:
                    return new[]
                    {
                        "操，破枪偏偏这时候卡壳了！掩护我两秒，老子拉一下枪栓！",
                        "他妈的卡弹了！顶住两秒，老子马上排障！",
                        "该死的破铜烂铁卡住了！看老子一巴掌拍通枪机！"
                    };
                case ChatterTopic.FieldMedicAid:
                    return new[]
                    {
                        "别乱动，血都滋出来了！老子用自己的急救包给你扎上了，撑住！",
                        "你他妈可别死在这儿，老大！给你打了一针强心剂又缠了绷带，继续干！",
                        "战地急救搞定！命给你从阎王爷手里抢回来了！"
                    };
                case ChatterTopic.FieldAmmoShare:
                    return new[]
                    {
                        "听见你空仓挂机咔咔响了！接住老子的备用子弹，继续开火，妈的！",
                        "给你匀了一把子弹！快装上，别拿着空枪发呆！",
                        "弹药甩给你了，老大！压满弹匣接着轰！"
                    };
                default:
                    return new[] { "随时待命，老大！" };
            }
        }

        private static string[] GetCleanCombatLinesZh(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.CombatSpotZombie:
                    return new[]
                    {
                        "发现目标！前方有感染者，正在瞄准射击！",
                        "看到行尸了！保持距离，交给我处理！"
                    };
                case ChatterTopic.CombatSpotHorde:
                    return new[]
                    {
                        "前方出现大群感染者！边退边打，千万别被包围！",
                        "目标数量太多！注意控制距离，交替掩护后撤！"
                    };
                case ChatterTopic.CombatSpotBandit:
                    return new[]
                    {
                        "注意，前方有武装敌对幸存者！寻找掩体，小心对方射击！",
                        "发现掠夺者！横向走位规避弹道，优先集火威胁目标！"
                    };
                case ChatterTopic.CombatSpotBoss:
                    return new[]
                    {
                        "发现巨型变异体！拉开最大安全距离，集中全部火力！",
                        "高威胁头目出现！注意躲避冲锋，保持远程输出！"
                    };
                case ChatterTopic.CombatKiting:
                    return new[]
                    {
                        "敌人靠得太近了！正在后撤步拉开射击距离！",
                        "保持机动走位，边退边维持火力压制！"
                    };
                case ChatterTopic.CombatClearFireLine:
                    return new[]
                    {
                        "正在让开你的射击火线！队长，请自由开火！",
                        "已侧移避开你的瞄准走廊，放心射击！"
                    };
                case ChatterTopic.CombatSwitchMelee:
                    return new[]
                    {
                        "敌人已贴身！切换近战武器迎敌！",
                        "来不及换弹了——拔出近战武器解决它！"
                    };
                case ChatterTopic.CombatSwitchRanged:
                    return new[]
                    {
                        "距离已拉开，切回远程枪械继续射击！",
                        "射界清晰，重新使用枪械火力支援！"
                    };
                case ChatterTopic.CombatKillEnemy:
                    return new[]
                    {
                        "目标已清除！继续搜索下一个威胁。",
                        "干净利落解决一个！防区暂时安全。",
                        "精准命中！又少了一个威胁。"
                    };
                case ChatterTopic.CombatSavePlayer:
                    return new[]
                    {
                        "离队长远点！我已经替你解围了，注意身后！",
                        "及时拦下了偷袭你的敌人！请待在我身边！"
                    };
                case ChatterTopic.CombatLowAmmo:
                    return new[]
                    {
                        "糟糕，我的弹药快耗尽了！准备切换近战或节省子弹！",
                        "备用弹匣见底了，我们需要尽快补充弹药！"
                    };
                case ChatterTopic.CombatWounded:
                    return new[]
                    {
                        "我受伤了！掩护我几秒钟，需要紧急包扎！",
                        "挂彩了，但我还能坚持战斗！"
                    };
                case ChatterTopic.CombatFlankWarning:
                    return new[]
                    {
                        "小心身后，队长！有敌人正从你背后包抄过来，快转身！",
                        "后方六点钟方向发现偷袭者！我正在过去拦截！"
                    };
                case ChatterTopic.CombatWeaponJammed:
                    return new[]
                    {
                        "枪械卡壳了！掩护我两秒钟，正在快速排除故障！",
                        "出现供弹故障，马上拉栓清理枪膛！"
                    };
                case ChatterTopic.FieldMedicAid:
                    return new[]
                    {
                        "你流血过多！别动，我用急救包替你完成了战地紧急包扎！",
                        "战地医护支援已到位！伤口已经处理好了，坚持住队长！"
                    };
                case ChatterTopic.FieldAmmoShare:
                    return new[]
                    {
                        "听见你空仓了！接住我分给你的备用弹药，快装填继续射击！",
                        "已向你递交紧急弹药补给！快换弹匣，我来掩护！"
                    };
                default:
                    return new[] { "收到，队长！随时保持警戒。" };
            }
        }
    }
}
