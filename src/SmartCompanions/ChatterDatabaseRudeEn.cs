using System;

namespace ZedZone.SmartCompanions
{
    public static partial class ChatterDatabase
    {
        public static string[] GetRudeLinesEn(ChatterTopic topic)
        {
            switch (topic)
            {
                case ChatterTopic.PlayerEatSteakMeat:
                    return new[]
                    {
                        "Must taste pretty fucking good, huh?! Eating a whole steak by yourself while I'm starving here!",
                        "Look at this rich bastard! Toss me a piece of that meat, don't be a greedy prick!",
                        "Holy shit, a real steak in the quarantine zone?! My stomach is tying itself in knots just smelling it!",
                        "Yeah, chew louder, asshole! Half the fucking undead on the block can smell that steak!",
                        "Damn, that smells fucking amazing... Save me a bite or I'll chew my rifle stock off!"
                    };
                case ChatterTopic.PlayerEatRawOrBad:
                    return new[]
                    {
                        "Are you out of your fucking mind eating that raw?! If you shit yourself mid-fight, I'm not carrying you!",
                        "Ugh, what the fuck, man?! At least roast that garbage over a fire first!",
                        "Jesus Christ, you eat like a feral zombie! Stay upwind from me!"
                    };
                case ChatterTopic.PlayerEatCanned:
                    return new[]
                    {
                        "Fucking canned slop again... Tastes like salted cardboard, but it beats starving.",
                        "Don't drop that tin can on the pavement, dumbass, it echoes across the whole street!",
                        "Good old canned mystery meat! Eat up before it rusts."
                    };
                case ChatterTopic.PlayerEatFish:
                    return new[]
                    {
                        "Fresh fish! Don't choke on a fucking bone while we're on patrol!",
                        "Damn, pan-fried fish in a dead zone! Save me a fin, you lucky bastard!"
                    };
                case ChatterTopic.PlayerEatCookedMeal:
                    return new[]
                    {
                        "Now that's a proper fucking meal! Beats eating cold dog food out of a tin.",
                        "Smells damn good, boss! Fill your gut before the next wave hits."
                    };
                case ChatterTopic.PlayerEatSnackOrFruit:
                    return new[]
                    {
                        "Pecking at berries like a bird? We need real meat and ammo, dammit!",
                        "Sugar rush time, huh? Whatever keeps you moving!"
                    };
                case ChatterTopic.PlayerDrinkWaterSoda:
                    return new[]
                    {
                        "Don't chug the whole fucking canteen by yourself! My mouth is dry as sandpaper!",
                        "Old-world soda?! Leave a sip for your squad, you thirsty bastard!"
                    };
                case ChatterTopic.PlayerDrinkAlcohol:
                    return new[]
                    {
                        "Cracking open a cold beer without pouring me one?! Selfish bastard!",
                        "Drink up, boss — nobody can survive this shithole completely sober anyway!"
                    };
                case ChatterTopic.PlayerHealBandage:
                    return new[]
                    {
                        "They tore you up good, huh?! Wrap that fucking bandage tight, I'm covering your ass!",
                        "Stop bleeding all over the floor! Patch yourself up fast, I've got the perimeter!"
                    };
                case ChatterTopic.PlayerReloadAmmo:
                    return new[]
                    {
                        "Pack those mags full, dammit! An empty gun out here is a death sentence!",
                        "Load 'em up! Let's feed these bastards a full diet of lead!"
                    };
                case ChatterTopic.PlayerRepairOrCraft:
                    return new[]
                    {
                        "Fix that piece of shit properly so it doesn't jam in the middle of a horde!",
                        "Stop banging that hammer so loud or you'll wake every walker on the island!"
                    };
                case ChatterTopic.PlayerCookingBBQ:
                    return new[]
                    {
                        "Damn, that barbecue smell is gonna bring every zombie and raider for a mile! Hurry up and flip it!",
                        "Don't burn our fucking dinner, chef, or you're next on the grill!"
                    };
                case ChatterTopic.PlayerLockpickBreakDoor:
                    return new[]
                    {
                        "Just kick the fucking door down already! Why are you poking it like a cheap burglar?!",
                        "Keep your gun up — ten bucks says there's a pack of rotters behind that lock!"
                    };
                case ChatterTopic.PlayerShareFoodThanks:
                    return new[]
                    {
                        "Hell yeah, thanks for sharing the grub, boss! Now I'm fed, pissed off, and ready to rip heads off!",
                        "Respect, brother! Nothing like a full belly before we paint the walls with zombies!",
                        "Fucking A, thanks for the ration! I've got your back to the bitter end!"
                    };
                case ChatterTopic.CombatSpotZombie:
                    return new[]
                    {
                        "Look at this ugly rotting fucker! Putting a hole in its skull right now!",
                        "Walker ahead! Sit the fuck down, corpse!",
                        "Another dead bastard shambling over — lighting 'em up!"
                    };
                case ChatterTopic.CombatSpotHorde:
                    return new[]
                    {
                        "Holy shit, look how many of these fuckers crawled out! Back up and mow 'em down!",
                        "Whole fucking horde incoming! Don't stand still or we're zombie chow!",
                        "Too many rotters! Kite back and keep the lead flying!"
                    };
                case ChatterTopic.CombatSpotBandit:
                    return new[]
                    {
                        "Armed raiders, dammit! These assholes have guns — strafe and blow their heads off!",
                        "Hostile scavengers ahead! Watch the gunfire and flank those bastards!"
                    };
                case ChatterTopic.CombatSpotBoss:
                    return new[]
                    {
                        "What the fuck is that giant monstrosity?! Keep your distance and dump every mag into it!",
                        "Huge mutant freak! Don't let that bastard get in melee range!"
                    };
                case ChatterTopic.CombatKiting:
                    return new[]
                    {
                        "Too fucking close! Backing up and shooting on the move!",
                        "Get off me, you rotting pricks! Kiting back to open up range!"
                    };
                case ChatterTopic.CombatClearFireLine:
                    return new[]
                    {
                        "Watch where you're pointing that fucking gun! Stepping out of your line of fire — don't shoot my ass!",
                        "Clearing your aim corridor, boss! Shoot the zombies, not me, dammit!"
                    };
                case ChatterTopic.CombatSwitchMelee:
                    return new[]
                    {
                        "Right in my face! Screw reloading — blades out, let's chop 'em up!",
                        "Point-blank! Switching to melee to bash some skulls in!"
                    };
                case ChatterTopic.CombatSwitchRanged:
                    return new[]
                    {
                        "Got breathing room — pulling the gun back out to light 'em up!",
                        "Range is clear! Back to firearms!"
                    };
                case ChatterTopic.CombatKillEnemy:
                    return new[]
                    {
                        "Eat lead, you rotting piece of shit! Who's next?!",
                        "Stay the fuck down, maggot! Clean kill!",
                        "Blew that bastard away! One less freak in the zone!"
                    };
                case ChatterTopic.CombatSavePlayer:
                    return new[]
                    {
                        "Get your rotting teeth off the boss, asshole! Saved your six — watch your back!",
                        "Peeled that bastard right off your neck! You owe me a steak for that one!"
                    };
                case ChatterTopic.CombatLowAmmo:
                    return new[]
                    {
                        "Shit, I'm almost out of fucking ammo! Mags are running dry!",
                        "Running on fumes here — barely any bullets left!"
                    };
                case ChatterTopic.CombatWounded:
                    return new[]
                    {
                        "Fuck, they got me! Bleeding like a stuck pig — cover me for a second!",
                        "Dammit, that hurt! You bastards are gonna pay for that scratch!"
                    };
                case ChatterTopic.CombatFlankWarning:
                    return new[]
                    {
                        "Wake the fuck up, boss — hostiles sneaking up right behind your back! Turn around!",
                        "Watch your six, dammit! Flankers closing in from behind!"
                    };
                case ChatterTopic.CombatWeaponJammed:
                    return new[]
                    {
                        "Piece of shit gun jammed right in the middle of the fight! Clearing the bolt, cover me!",
                        "Fucking malfunction! Give me two seconds to rack this piece of junk!"
                    };
                case ChatterTopic.FieldMedicAid:
                    return new[]
                    {
                        "Hold still, dammit, you're leaking blood everywhere! Patched you up with my own kit!",
                        "Don't you dare die on me, boss! Slapped a field dressing on your wounds — stay in the fight!"
                    };
                case ChatterTopic.FieldAmmoShare:
                    return new[]
                    {
                        "Heard your gun click dry! Catch a pack of rounds from my stash and keep shooting, dammit!",
                        "Tossing you spare ammo! Reload fast and don't stand there with an empty piece!"
                    };
                case ChatterTopic.SortStart:
                    return new[]
                    {
                        "Here I go sorting your messy fucking boxes again! Found yourself an unpaid warehouse mule, huh?!",
                        "Jesus, what a pigsty in these chests! Alright, stand back, I'm organizing the whole damn base!"
                    };
                case ChatterTopic.SortPickUp:
                    return new[]
                    {
                        "Grabbed your pile of junk from the drop-off box, hauling it to the storage chests...",
                        "My back is killing me from all your hoarding! Sorting it into categories now!"
                    };
                case ChatterTopic.SortDepositWeapons:
                    return new[]
                    {
                        "Guns, parts, and ammo stacked in the armory chest! Ready for war, dammit!",
                        "Locked and loaded — all weapons and mags sorted into their crate!"
                    };
                case ChatterTopic.SortDepositFood:
                    return new[]
                    {
                        "Stashed the food and drinks in the pantry chests — and I didn't even eat half of it on the way!",
                        "Grub and cans sorted! At least we won't starve this week."
                    };
                case ChatterTopic.SortDepositMeds:
                    return new[]
                    {
                        "Bandages, pills, and medkits packed in the medical crate! Don't mix up the painkillers!",
                        "Medical stash organized and stacked neat!"
                    };
                case ChatterTopic.SortDepositMaterials:
                    return new[]
                    {
                        "Dumped all your scrap metal, tools, and crafting junk into the workshop chests, you packrat!",
                        "Materials and spare parts sorted tight!"
                    };
                case ChatterTopic.SortFinished:
                    return new[]
                    {
                        "There, sorted every fucking chest on the base! You owe me a cold beer and a break!",
                        "Base storage is spotless, boss! Every bullet and bean can is in its category box!"
                    };
                case ChatterTopic.LootFoundGood:
                    return new[]
                    {
                        "Hell yeah, look at this sweet loot just lying here! Mine now!",
                        "Bagged some good supplies off the ground — waste not, want not!"
                    };
                case ChatterTopic.LootOverweight:
                    return new[]
                    {
                        "Why the fuck did we haul all this heavy trash?! My spine is about to snap — only taking ammo, meds, and food now!",
                        "I'm not a fucking pack mule! Pack is nearly full, filtering for high-priority supplies only!"
                    };
                case ChatterTopic.BaseAutoDropoff:
                    return new[]
                    {
                        "Back at base! Dumped all our raid loot into the drop-off chest and sorted the storage!",
                        "Finally home! Unloaded my pack into the base chests — my shoulders can breathe again!"
                    };
                case ChatterTopic.AmbientRain:
                    return new[]
                    {
                        "Fucking rain again... Soaked to the bone and freezing my ass off!",
                        "Great weather if you're a damn frog! Let's find a roof soon."
                    };
                case ChatterTopic.AmbientBloodRain:
                    return new[]
                    {
                        "Holy fuck, blood rain! The infected are gonna go completely berserk — keep your finger on the trigger!",
                        "The sky is bleeding, dammit! Watch every corner!"
                    };
                case ChatterTopic.AmbientNight:
                    return new[]
                    {
                        "Pitch fucking black out here! Flip your flashlight on and watch for ambushes!",
                        "Night shift in hell... Stay close, the nastiest freaks come out in the dark."
                    };
                case ChatterTopic.AmbientMorning:
                    return new[]
                    {
                        "Sun's finally up, thank fuck! Survived another goddamn night!",
                        "Daylight! At least now I can see the ugly bastards before shooting 'em!"
                    };
                case ChatterTopic.AmbientHungry:
                    return new[]
                    {
                        "My stomach is eating itself, dammit! Toss me some food before I pass out!",
                        "Starving like a stray dog here, boss... We need grub!"
                    };
                case ChatterTopic.AmbientTired:
                    return new[]
                    {
                        "I'm dead on my fucking feet... We need a bed or a campfire break soon!",
                        "Eyes are barely staying open, boss. One more mile and I'll pass out standing."
                    };
                case ChatterTopic.AmbientBaseIdle:
                    return new[]
                    {
                        "Quiet at the base for once — nobody chewing on my ass, I can actually breathe.",
                        "Not a bad little fortress we built in the middle of this shithole, huh?"
                    };
                case ChatterTopic.BanterQuestion:
                    return new[]
                    {
                        "Hey, why are you dragging your feet like a half-dead mule, dumbass?",
                        "Keep your eyes open, idiot, or a walker's gonna take a chunk out of your neck!"
                    };
                case ChatterTopic.BanterReply:
                    return new[]
                    {
                        "Screw you, I'm hauling two crates of your ammo and canned stew on my back!",
                        "Worry about your own ass, smartass! My sector is clean!"
                    };
                case ChatterTopic.WorldNpcEncounter:
                    return new[]
                    {
                        "What the fuck are you staring at?! Keep walking while you still have legs!",
                        "More drifters... Keep your hands off my stash and we won't have a problem!"
                    };
                default:
                    return new[] { "Locked and loaded, boss!" };
            }
        }
    }
}
