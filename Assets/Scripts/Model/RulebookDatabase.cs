using System.Collections.Generic;
using UnityEngine;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.Model
{
    /// <summary>
    /// Complete, verbatim database of all 40 Relic Deck cards (19 types), 50 Resource Deck cards,
    /// all 3 Faction-Specific Decks (8-card Forage Deck, 14-card Grimoire Deck, 8-card Traded Goods Deck),
    /// 7 Carnivorous Queen Mutation Cards, and all 11 Factions from Eben's Critters of Calamity rulebook.
    /// </summary>
    public static class RulebookDatabase
    {
        public static List<ResourceType> BuildStandard50ResourceDeck()
        {
            var deck = new List<ResourceType>(50);
            for (int i = 0; i < 20; i++) deck.Add(ResourceType.Stone);
            for (int i = 0; i < 20; i++) deck.Add(ResourceType.Wood);
            for (int i = 0; i < 10; i++) deck.Add(ResourceType.Gold);
            return deck;
        }

        public static List<FactionSubDeckCardDef> BuildMillionsForageDeck8()
        {
            // Rulebook Lines 501-502: 8 total cards (3 Plague Cards + 5 Rummage Cards)
            return new List<FactionSubDeckCardDef>
            {
                new FactionSubDeckCardDef { Id = "plague_authority", DeckName = "Forage Deck", Title = "Plague: Authority Drain", Copies = 1, EffectText = "You lose 1 Authority and the Millions' player gains 1 Authority." },
                new FactionSubDeckCardDef { Id = "plague_infest", DeckName = "Forage Deck", Title = "Plague: Rummaging Swarm", Copies = 1, EffectText = "Place 1 Millions unit on a tile with one of your faction's pieces, then discard 1 resource card from your resource pile." },
                new FactionSubDeckCardDef { Id = "plague_sickness", DeckName = "Forage Deck", Title = "Plague: Sickness", Copies = 1, EffectText = "If you have a unit or special unit on a tile with a Millions, one of those units loses 1HP." },
                new FactionSubDeckCardDef { Id = "rummage_stone_1", DeckName = "Forage Deck", Title = "Rummage: 2 Stone", Copies = 1, EffectText = "The Millions' player gains 2 Stone cards from the resource deck." },
                new FactionSubDeckCardDef { Id = "rummage_stone_2", DeckName = "Forage Deck", Title = "Rummage: 2 Stone", Copies = 1, EffectText = "The Millions' player gains 2 Stone cards from the resource deck." },
                new FactionSubDeckCardDef { Id = "rummage_wood_1", DeckName = "Forage Deck", Title = "Rummage: 2 Wood", Copies = 1, EffectText = "The Millions' player gains 2 Wood cards from the resource deck." },
                new FactionSubDeckCardDef { Id = "rummage_wood_2", DeckName = "Forage Deck", Title = "Rummage: 2 Wood", Copies = 1, EffectText = "The Millions' player gains 2 Wood cards from the resource deck." },
                new FactionSubDeckCardDef { Id = "rummage_gold", DeckName = "Forage Deck", Title = "Rummage: 1 Gold", Copies = 1, EffectText = "The Millions' player gains 1 Gold card from the resource deck." }
            };
        }

        public static List<FactionSubDeckCardDef> BuildGoatGrimoireDeck14()
        {
            // Rulebook Lines 629-641: 14-card Grimoire Deck
            return new List<FactionSubDeckCardDef>
            {
                new FactionSubDeckCardDef { Id = "grim_1",  DeckName = "Grimoire", Title = "Whisper of the Flock", Copies = 1, EffectText = "When milled: Gain 1 Purged." },
                new FactionSubDeckCardDef { Id = "grim_2",  DeckName = "Grimoire", Title = "Soul Tithe", Copies = 1, EffectText = "When milled: Lose 1 Purged, gain 1 Authority." },
                new FactionSubDeckCardDef { Id = "grim_3",  DeckName = "Grimoire", Title = "Dark Martyrdom", Copies = 1, EffectText = "When milled: Lose 2 Authority, gain 2 Purged." },
                new FactionSubDeckCardDef { Id = "grim_4",  DeckName = "Grimoire", Title = "Burnt Offering", Copies = 1, EffectText = "When milled: Discard 1 card from your hand, gain 2 Authority." },
                new FactionSubDeckCardDef { Id = "grim_5",  DeckName = "Grimoire", Title = "Forbidden Lore", Copies = 1, EffectText = "When milled: Draw 1 card, lose 2 Authority." },
                new FactionSubDeckCardDef { Id = "grim_6",  DeckName = "Grimoire", Title = "Blood Harvest", Copies = 1, EffectText = "When milled: Discard 2 resources from your resource pile, gain 2 Purged." },
                new FactionSubDeckCardDef { Id = "grim_7",  DeckName = "Grimoire", Title = "Alchemical Greed", Copies = 1, EffectText = "When milled: Gain 2 Gold." },
                new FactionSubDeckCardDef { Id = "grim_8",  DeckName = "Grimoire", Title = "Golden Covenant", Copies = 1, EffectText = "When milled: Lose 1 Gold, gain 2 Authority." },
                new FactionSubDeckCardDef { Id = "grim_9",  DeckName = "Grimoire", Title = "Ancient Excavation", Copies = 1, EffectText = "When milled: Draw 2 cards from the relic deck, discard 1." },
                new FactionSubDeckCardDef { Id = "grim_10", DeckName = "Grimoire", Title = "Shadow Coin", Copies = 1, EffectText = "When milled: Gain 1 Gold." },
                new FactionSubDeckCardDef { Id = "grim_11", DeckName = "Grimoire", Title = "Bartered Crown", Copies = 1, EffectText = "When milled: Lose 2 Authority, gain 1 Gold." },
                new FactionSubDeckCardDef { Id = "grim_12", DeckName = "Grimoire", Title = "Abyssal Dominion", Copies = 1, EffectText = "When milled: Gain 3 Authority." },
                new FactionSubDeckCardDef { Id = "grim_13", DeckName = "Grimoire", Title = "Unholy Trinity", Copies = 1, EffectText = "When milled: Gain 1 Authority, 1 Gold, 1 Purged." },
                new FactionSubDeckCardDef { Id = "grim_14", DeckName = "Grimoire", Title = "Fool's Bargain", Copies = 1, EffectText = "When milled: Gain 3 Gold, lose 1 Authority and 1 Purged." }
            };
        }

        public static List<FactionSubDeckCardDef> BuildCapybaraTradedGoodsDeck8()
        {
            // Rulebook Lines 1385-1395: 8-card Traded Goods Deck
            return new List<FactionSubDeckCardDef>
            {
                new FactionSubDeckCardDef { Id = "tg_rusty_shovel", DeckName = "Traded Goods", Title = "Rusty Shovel", Copies = 1, Cost = new ResourceCost(any: 3), EffectText = "Cost: 3 Resources. Next time you use Loot, draw 3 cards from the resource deck and end the looting action." },
                new FactionSubDeckCardDef { Id = "tg_clay_edict", DeckName = "Traded Goods", Title = "Equipment - Clay Edict", Copies = 1, Cost = new ResourceCost(any: 3), EffectText = "Cost: 3 Resources. Equip to 1 of your Special Units; it gains +1 tile movement for the next 3 turns." },
                new FactionSubDeckCardDef { Id = "tg_prayer_beads", DeckName = "Traded Goods", Title = "Prayer Beads", Copies = 1, Cost = new ResourceCost(any: 3), EffectText = "Cost: 3 Resources. During your Excavation Phase, draw the top 2 cards from the relic deck." },
                new FactionSubDeckCardDef { Id = "tg_silver_spoon", DeckName = "Traded Goods", Title = "Silver Spoon", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. Add 2 relic cards from the discard pile to your hand, then both you and the Capybara player gain 1 Authority." },
                new FactionSubDeckCardDef { Id = "tg_copper_figurine", DeckName = "Traded Goods", Title = "Copper Figurine", Copies = 1, Cost = new ResourceCost(any: 3), EffectText = "Cost: 3 Resources. After a die has been rolled, discard to re-roll 1 die (can be used during battle dice roll phase)." },
                new FactionSubDeckCardDef { Id = "tg_broken_sickle", DeckName = "Traded Goods", Title = "Broken Sickle", Copies = 1, Cost = new ResourceCost(any: 1), EffectText = "Cost: 1 Resource. Add 1 relic card from the discard pile to your hand." },
                new FactionSubDeckCardDef { Id = "tg_horse_hair_brush", DeckName = "Traded Goods", Title = "Horse Hair Brush", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. Your next Unearth, Assemble, or Disassemble action costs 1 less resource." },
                new FactionSubDeckCardDef { Id = "tg_old_jug", DeckName = "Traded Goods", Title = "Old Jug", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. During your Main Phase, heal a Special Unit for 1HP." }
            };
        }

        public static List<EquipmentDef> BuildQueenMutations7()
        {
            // Rulebook Lines 1056-1079: 7 Mutation Equipment cards
            return new List<EquipmentDef>
            {
                new EquipmentDef { Id = "mut_serrated_claws", Name = "Serrated Claws", Faction = FactionId.CarnivorousQueen, TargetUnitId = "carnivorous_queen", MaxPerUnit = 1, SupplyCount = 1, BonusAttack = 2, EffectText = "When Queen kills an enemy unit, gain +2 Attack until End Phase, 1 free Move action, and +1 Authority." },
                new EquipmentDef { Id = "mut_membranous_wings", Name = "Membranous Wings", Faction = FactionId.CarnivorousQueen, TargetUnitId = "carnivorous_queen", MaxPerUnit = 1, SupplyCount = 1, BonusMove = 1, EffectText = "After Queen battles or Ensnares, spend 2 Rage to move up to 2 tiles and become Hidden for free." },
                new EquipmentDef { Id = "mut_mandibles", Name = "Mandibles", Faction = FactionId.CarnivorousQueen, TargetUnitId = "carnivorous_queen", MaxPerUnit = 1, SupplyCount = 1, EffectText = "Once per Main Phase, spend 2 Rage + 2 resources (or 1 Gold) to deal 1HP true damage to an enemy on Queen's tile." },
                new EquipmentDef { Id = "mut_abdomen", Name = "Abdomen", Faction = FactionId.CarnivorousQueen, TargetUnitId = "carnivorous_queen", MaxPerUnit = 1, SupplyCount = 1, EffectText = "Ensnare becomes a FREE action (no longer costs a Main Phase action; still costs 3 Rage, grants +1 Auth & +1 Feeding)." },
                new EquipmentDef { Id = "mut_thoracic_hinges", Name = "Thoracic Hinges", Faction = FactionId.CarnivorousQueen, TargetUnitId = "carnivorous_queen", MaxPerUnit = 1, SupplyCount = 1, EffectText = "Place 2 Egg Tokens by discarding 2 relics; enemies adjacent to Larvae/Chrysalis lose 1 Defense and need 2 Move actions to enter Chrysalis tile." },
                new EquipmentDef { Id = "mut_scaphism", Name = "Scaphism", Faction = FactionId.CarnivorousQueen, TargetUnitId = "carnivorous_queen", MaxPerUnit = 1, SupplyCount = 1, EffectText = "Spend 1 Main Phase action + 3 Rage to devour all Ensnared on the board: +1 Authority and heal 1HP per Ensnared removed." },
                new EquipmentDef { Id = "mut_carapace", Name = "Carapace", Faction = FactionId.CarnivorousQueen, TargetUnitId = "carnivorous_queen", MaxPerUnit = 1, SupplyCount = 1, BonusHP = 1, BonusDefense = 1, EffectText = "Queen gains +1 Max HP (heals 1 on draft) and prevents the first 1HP damage taken each turn." }
            };
        }

        public static List<RelicCardDef> GetAllRelicCardDefs()
        {
            return new List<RelicCardDef>
            {
                new RelicCardDef
                {
                    Id = "locked_chest", Name = "Locked Chest", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Gain 2 Authority."
                },
                new RelicCardDef
                {
                    Id = "damaged_anankian_chariot", Name = "Damaged Anakian Chariot", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 2, stone: 2),
                    DisassembleGain = new ResourceCost(stone: 2),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Recruit 1 of your units at each of your recruitment buildings. Those units cannot move this turn but they can loot and battle."
                },
                new RelicCardDef
                {
                    Id = "war_horn", Name = "War Horn", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 3),
                    DisassembleGain = new ResourceCost(wood: 2),
                    KeepFaceUpWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Keep faceup on the table. Discard in a defending battle at the end of Attack Declaration phase to recruit 1 of your non-special units before dice or defense points are calculated."
                },
                new RelicCardDef
                {
                    Id = "iron_halberd", Name = "Iron Halberd", CopiesInDeck = 2,
                    Cost = new ResourceCost(stone: 1),
                    DisassembleGain = new ResourceCost(stone: 1),
                    KeepFaceUpWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Keep faceup in front of you. At the start of the Attack and Defense point phase, discard this card to add +2 to your attack roll."
                },
                new RelicCardDef
                {
                    Id = "brass_shield", Name = "Brass Shield", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 2),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepFaceUpWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Keep faceup in front of you. Discard during the Damage Calculation phase to subtract 2HP damage from Winner's Damage Dealt."
                },
                new RelicCardDef
                {
                    Id = "statue_of_the_goddess", Name = "Statue of the Goddess", CopiesInDeck = 2,
                    Cost = new ResourceCost(stone: 2, gold: 1),
                    DisassembleGain = new ResourceCost(gold: 1),
                    KeepFaceUpWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Choose 1 tile, all your units on that tile gain +1 attack in their next battle and they must all move together."
                },
                new RelicCardDef
                {
                    Id = "overgrown_wall", Name = "Overgrown Wall", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, gold: 1),
                    DisassembleGain = new ResourceCost(gold: 1),
                    KeepFaceUpWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Keep faceup in front of you. Where you are defending in battle, discard at the start of the Dice Roll phase: -2 to the dice roll of the attacker."
                },
                new RelicCardDef
                {
                    Id = "abandoned_road", Name = "Abandoned Road", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, gold: 1),
                    DisassembleGain = new ResourceCost(gold: 1),
                    KeepFaceUpWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Keep faceup in front of you. Discard during the main phase and gain 1 free battle action."
                },
                new RelicCardDef
                {
                    Id = "bejeweled_chalice", Name = "Bejeweled Chalice", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(stone: 1),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "Gain 1 Gold and draw 1 card from the relic deck."
                },
                new RelicCardDef
                {
                    Id = "political_treatise", Name = "Political Treatise", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepFaceUpWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Keep faceup in front of you. At the start of the Attack and Defense point phase, discard this card to add +2 to your defense roll."
                },
                new RelicCardDef
                {
                    Id = "marble_aqueduct", Name = "Marble Aqueduct", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "Return up to 2 burned resources to the resource pile or you can discard 1 card from your hand to draw 2 cards from the resource deck."
                },
                new RelicCardDef
                {
                    Id = "looted_vault", Name = "Looted Vault", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    DisassembleChoiceWoodOrStone = true,
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "Draw 2 from the relic deck. (Disassemble: Gain 1 Wood or 1 Stone)"
                },
                new RelicCardDef
                {
                    Id = "underground_passage", Name = "Underground Passage", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1, stone: 1),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "Steal one random resource from a player's resource pile. If they have 5 or more steal 2."
                },
                new RelicCardDef
                {
                    Id = "hallow_ground", Name = "Hallow Ground", CopiesInDeck = 2,
                    Cost = new ResourceCost(gold: 1),
                    DisassembleGain = new ResourceCost(gold: 1),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "Target one non-special unit, remove it from the board and place it in the owner's supply."
                },
                new RelicCardDef
                {
                    Id = "ceremonial_bow_and_arrow", Name = "Ceremonial Bow and Arrow", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepFaceUpWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "You can discard this card to destroy one faceup relic deck card another player controls."
                },
                new RelicCardDef
                {
                    Id = "dilapidated_bridge", Name = "Dilapidated Bridge", CopiesInDeck = 3,
                    Cost = new ResourceCost(wood: 1),
                    DisassembleGain = new ResourceCost(wood: 2),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = false,
                    EffectText = "No Crafted Effect. Disassemble: Gain 2 Wood."
                },
                new RelicCardDef
                {
                    Id = "abandoned_well", Name = "Abandoned Well", CopiesInDeck = 3,
                    Cost = new ResourceCost(stone: 1),
                    DisassembleGain = new ResourceCost(stone: 2),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = false,
                    EffectText = "No Crafted Effect. Disassemble: Gain 2 Stone."
                },
                new RelicCardDef
                {
                    Id = "forest_clearing", Name = "Forest Clearing", CopiesInDeck = 4,
                    Cost = new ResourceCost(any: 1),
                    DisassembleGain = new ResourceCost(wood: 1, stone: 1),
                    KeepFaceUpWhenCrafted = false, HasCraftedEffect = false,
                    EffectText = "No Crafted Effect. Disassemble: Gain 1 Stone and 1 Wood."
                }
            };
        }

        public static List<RelicCardDef> BuildFull40RelicDeck()
        {
            var defs = GetAllRelicCardDefs();
            var deck = new List<RelicCardDef>(40);
            foreach (var d in defs)
            {
                for (int i = 0; i < d.CopiesInDeck; i++)
                    deck.Add(d);
            }
            return deck;
        }

        public static List<FactionDef> GetAllFactions()
        {
            return new List<FactionDef>
            {
                // Faction 1: The Necromouser
                new FactionDef
                {
                    Id = FactionId.Necromouser,
                    Name = "The Necromouser",
                    Tier = FactionDifficultyTier.Intermediate,
                    DifficultyRatingOutOf10 = 6,
                    ShortSummary = "Late game necromancer who profits from everyone else's war, summons an increasingly strong horde of the undead.",
                    LoreDescription = "The incarnate of death came to the Continent long before the other factions did, and the Necromouser has made a deal with death to make it his trade. Where armies clash he seeds his creeping miasma, and the fallen of every banner rise again in his service.",
                    SetupInstructions = "Place 3 Miasma Tiles facedown on the gameboard in any pattern (not directly adjacent or on the same tile). Start with 3 Skeleton Units on the Cauldron ('Dead' units).",
                    PrimaryColor = Palette.FromHex("#7B46B8"),
                    SecondaryColor = Palette.FromHex("#D6C9E8"),
                    PrimaryBuilding = BuildingType.None,
                    PrimaryBuildingCost = new ResourceCost(),
                    BuildingRules = "The Necromouser has no building and cannot use the recruitment main phase action or the comeback mechanic.",
                    SpeciesTraits = new List<string>
                    {
                        "Hoarder: Keep up to 8 resource cards in your resource pile during your end phase instead of 5.",
                        "Plague: When you spend Gold on equipment, place 1 Miasma Tile directly adjacent to an existing Miasma Tile.",
                        "Undead Shackles: Once during your end phase, gain 1 Authority per 2 Dead in the Cauldron (cap 2 Authority per turn).",
                        "Cauldron: Units removed on or adjacent to a Miasma Tile (except exiling) place 1 unit per faction onto the Cauldron as 'Dead'.",
                        "Arise: Once per turn, spend 1 Dead from the Cauldron to summon 1 Skeleton Unit on a Miasma Tile."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Kill a Necromouser Special Unit: Gain Authority equal to its summoning cost (1/3/4) + free loot 1 equipment material (adds 1 Skeleton to Cauldron).",
                        "Destroy an unguarded Miasma Tile: Attacker gains 1 Authority, Necromouser adds 1 Skeleton to Cauldron."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Necromouser Faction Board", "1 Necromouser Special Unit Piece & Card", "3 Bloat Special Unit Pieces & Card",
                        "3 Vanguard Special Unit Pieces & Card", "3 Brute Special Unit Pieces & Card", "18 Skeleton Unit Pieces & Card",
                        "6 Miasma Tiles", "1 Cauldron Card", "6 Sword, 3 Helmet, 3 Shield, 3 Boots, 3 Locket, 3 Ring Equipment Cards"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "skeleton", Name = "Skeleton", Faction = FactionId.Necromouser, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 18, SummoningCondition = "Arise trait or Miasma trigger", AbilitiesText = "Spread the Bad News: Remove 1 Skeleton from board to place a Miasma Tile on its tile." },
                        new UnitDef { Id = "necromouser", Name = "The Necromouser", Faction = FactionId.Necromouser, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 1, SummoningCondition = "First time a unit dies on/adjacent to Miasma: flip Miasma tiles and summon Necromouser + 1 Skeleton.", AbilitiesText = "Necromancy: Summon 1 Special Unit/turn (cap 4/game). Cowardice: Cannot be attacked if friendly units share tile; on death lose 3 Authority, relocate 2 tiles away, place 3 adjacent Miasma Tiles." },
                        new UnitDef { Id = "the_bloat", Name = "The Bloat", Faction = FactionId.Necromouser, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 2, BaseMaxHP = 1, SupplyCount = 3, SummoningCondition = "Spend 1 Dead from Cauldron -> Gain 1 Authority, summon on Necromouser's tile.", AbilitiesText = "Necrosis: During draw phase, explode to deal 2HP damage to all enemy units on tile (bypasses 1HP cap). If killed first, killer gains 1 Authority + 1 Resource." },
                        new UnitDef { Id = "the_vanguard", Name = "The Vanguard", Faction = FactionId.Necromouser, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 3, SummoningCondition = "Spend 3 Dead from Cauldron -> Gain 3 Authority, summon on Necromouser's tile.", AbilitiesText = "Devotion: If Necromouser is defending on an adjacent tile, Vanguard and all friendly units on Vanguard's tile join the defense." },
                        new UnitDef { Id = "the_brute", Name = "The Brute", Faction = FactionId.Necromouser, IsSpecialUnit = true, BaseAttack = 2, BaseDefense = 0, BaseMaxHP = 2, SupplyCount = 3, SummoningCondition = "Spend 4 Dead from Cauldron -> Gain 4 Authority, summon on Necromouser's tile.", AbilitiesText = "Can equip up to 2 Anankian Swords and 1 Leather Boots." }
                    },
                    Equipment = new List<EquipmentDef>
                    {
                        new EquipmentDef { Id = "locket_of_memory", Name = "Locket of Memory", Faction = FactionId.Necromouser, TargetUnitId = "the_bloat", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(stone: 2), EffectText = "Bloat explosion gains a 1-tile radius." },
                        new EquipmentDef { Id = "ring_of_favor", Name = "Ring of Favor", Faction = FactionId.Necromouser, TargetUnitId = "the_bloat", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(stone: 2), EffectText = "When Bloat explodes, gain +2 Authority and place a Miasma Tile." },
                        new EquipmentDef { Id = "anankian_helmet", Name = "Anankian Helmet", Faction = FactionId.Necromouser, TargetUnitId = "the_vanguard", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(wood: 1, stone: 1), BonusDefense = 2, EffectText = "+2 Defense to equipped Vanguard." },
                        new EquipmentDef { Id = "anankian_shield", Name = "Anankian Shield", Faction = FactionId.Necromouser, TargetUnitId = "the_vanguard", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(wood: 1, stone: 1, gold: 1), BonusDefense = 1, EffectText = "+1 Defense to equipped Vanguard and all friendly units on its tile." },
                        new EquipmentDef { Id = "anankian_sword", Name = "Anankian Sword", Faction = FactionId.Necromouser, TargetUnitId = "the_brute", MaxPerUnit = 2, SupplyCount = 6, Cost = new ResourceCost(wood: 1, stone: 1, gold: 1), BonusAttack = 1, EffectText = "+1 Attack to equipped Brute (max 2 per Brute)." },
                        new EquipmentDef { Id = "leather_boots", Name = "Leather Boots", Faction = FactionId.Necromouser, TargetUnitId = "the_brute", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(wood: 2), BonusMove = 1, EffectText = "+1 tile movement per movement action to equipped Brute." }
                    }
                },

                // Faction 2: Horde of the Millions
                new FactionDef
                {
                    Id = FactionId.HordeOfTheMillions,
                    Name = "Horde of the Millions",
                    Tier = FactionDifficultyTier.BeginnerFriendly,
                    DifficultyRatingOutOf10 = 2,
                    ShortSummary = "Mid game faction made up of an endless swarm of rabbits that wins by spreading the plague and being impossible to remove.",
                    LoreDescription = "The Millions is a horde of hungry rabbits that do not seek to conquer the Continent so much as fill it, burrow by burrow they multiply, rummaging for food they accidentally spread sickness.",
                    SetupInstructions = "Place 2 Burrows on 2 separate Forest tiles without enemy pieces. Place 2 Millions units on each of those tiles.",
                    PrimaryColor = Palette.FromHex("#D99B66"),
                    SecondaryColor = Palette.FromHex("#F5E6D3"),
                    PrimaryBuilding = BuildingType.Burrow,
                    PrimaryBuildingCost = new ResourceCost(wood: 1),
                    BuildingRules = "Burrow: Craft on a tile without enemy buildings or 3+ enemy units. Upkeep: Discard 1 Wood per 2 Burrows during End Phase. Start of End Phase: Gain 1 Authority per 2 Burrows.",
                    SpeciesTraits = new List<string>
                    {
                        "Out of Hibernation: End Phase -> Recruit 1 Millions at each Burrow with fewer than 3 Millions.",
                        "Prolific Creatures: Start of next player's Main Phase -> Place 1 Millions on any tile with enemy pieces + a Millions (if < 3 Millions).",
                        "On the Search for Food: End Phase -> Gain 1 Authority per 3 Millions on tiles with enemy pieces (cap 2/turn).",
                        "Oblivious to Danger: Millions units do not deal 1HP Mandatory Loser Damage in battle."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Destroy a Burrow: 1 Authority.",
                        "Kill King of the Millions: 2 Authority.",
                        "Use or Destroy a Catapult Token: 1 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Horde of the Millions Faction Board", "30 Millions Unit Pieces & Card", "1 King of the Millions Special Unit Piece & Card",
                        "2 Catapult Tokens", "3 Gluttony Tokens", "3 Burrow Buildings", "8-Card Forage Deck (3 Plague, 5 Rummage)"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "millions", Name = "Millions", Faction = FactionId.HordeOfTheMillions, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 30, SummoningCondition = "Recruit at Burrows", AbilitiesText = "Forage: Every player's End Phase on shared tile -> draw 1 Forage card. Other People's Gluttony: Free craft 1 Gluttony Token for 1 Gold." },
                        new UnitDef { Id = "king_of_the_millions", Name = "King of the Millions", Faction = FactionId.HordeOfTheMillions, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 4, SupplyCount = 1, SummoningCondition = "Once per game: Discard 1 Gold, 1 Stone, 1 Wood -> Summon at a Burrow.", AbilitiesText = "Spread the Fun: All other players draw & resolve 1 Forage card. King of the Rabbit Hill: Once per Main Phase, spawn 1 Millions on King's tile." }
                    },
                    SubDeckCards = BuildMillionsForageDeck8()
                },

                // Faction 3: The Goat Cabal
                new FactionDef
                {
                    Id = FactionId.GoatCabal,
                    Name = "The Goat Cabal",
                    Tier = FactionDifficultyTier.Expert,
                    DifficultyRatingOutOf10 = 8,
                    ShortSummary = "Late game faction that sacrifices everything to summon board-warping demons.",
                    LoreDescription = "When the Ananke vanished, an older force whispered in the shadows. The Cabal traded their souls for its favor, and now they trade everything else to drag demons up from the dark.",
                    SetupInstructions = "Place 4 Goat units and 1 Summoning Circle on a Forest or Ruin tile 2 tiles away from enemies. Place 1 Goat on Purgatory card. Start with 5 Authority.",
                    PrimaryColor = Palette.FromHex("#8C1D2E"),
                    SecondaryColor = Palette.FromHex("#E0B068"),
                    PrimaryBuilding = BuildingType.SummoningCircle,
                    PrimaryBuildingCost = new ResourceCost(wood: 2, stone: 2),
                    BuildingRules = "Summoning Circle: Requires 5+ Goats on tile; Purge 1 Goat to Purgatory + discard 2 Stone & 2 Wood.",
                    SpeciesTraits = new List<string>
                    {
                        "Purgatory: Purged Goat units go onto the Purgatory card to fuel Demon summons and bindings.",
                        "Secrets of the Ancients: Draw Phase -> Mill top card of the 14-card Grimoire Deck and apply its effect.",
                        "Kleptomancy: Keep up to 10 Gold + 5 other resources during End Phase.",
                        "Suspicion: Enemies cannot move onto a Summoning Circle tile unless bringing more units than Goats present.",
                        "Sparagmos: Once per Main Phase, Purge 1 Goat from a Summoning Circle tile to Purgatory."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Destroy a Demon Special Unit: 2 Authority (cannot be resummoned).",
                        "Destroy a Summoning Circle, Initiation Rite Token, or Effigy Token: 1 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Goat Cabal Faction Board", "1 Demon Rules Board", "1 Purgatory Card", "20 Goat Unit Pieces",
                        "5 Demon Special Unit Pieces & Cards (Faust, The Midian, Serpentine Sovereign, The Lord of War, The Horned Beast)",
                        "2 Initiation Rite Tokens", "3 Effigy Tokens", "3 Summoning Circle Buildings", "14-Card Grimoire Deck"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "goat", Name = "Goat", Faction = FactionId.GoatCabal, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 1, BaseMaxHP = 1, SupplyCount = 20, SummoningCondition = "Recruit at Summoning Circles", AbilitiesText = "Invocation: Purge 1 Goat + 1W/1S/1G -> Place Initiation Rite. Canto Inferno: Remove Initiation Rite + Demon cost -> Summon Demon. Effigy: Return 1 Purged -> Place Effigy Token." },
                        new UnitDef { Id = "faust", Name = "Faust the Wise", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 1, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "2 Purged + 1 Wood + 1 Stone", AbilitiesText = "On Summon: Mill 2 Grimoire cards. Bargain: Purge 1 Goat -> Mill 1 Grimoire. Binding: 2 Purged in Purgatory. Unbound: Lose 3 Gold." },
                        new UnitDef { Id = "the_midian", Name = "The Midian", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "2 Purged + 2 Gold", AbilitiesText = "A Gift: +1 Authority per Summoning Circle. All Turns to Gold: Gain 2 Gold, another player draws 1 Resource. Binding: Lose 2 Authority." },
                        new UnitDef { Id = "serpentine_sovereign", Name = "Serpentine Sovereign", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "3 Purged", AbilitiesText = "Royal Decree: Mark tile within 4 tiles; start of next Draw Phase destroy all pieces and overlay tiles on it. Binding: Lose 2 Authority during turn." },
                        new UnitDef { Id = "lord_of_war", Name = "The Lord of War", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 3, BaseDefense = 1, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "3 Purged", AbilitiesText = "Fear: Push non-Goats on summon. Onslaught: 3 free battles in Main Phase. Greed: Roll d6 on attack (even +1 Auth, odd -1 Gold)." },
                        new UnitDef { Id = "horned_beast", Name = "The Horned Beast", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 2, BaseDefense = 3, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "5 Purged", AbilitiesText = "Subservience: Impose global restriction on all opponents next turn. Binding: 2 Gold, 1 Card, 1 Auth, 1 Purged + 3 Summoning Circles." }
                    },
                    SubDeckCards = BuildGoatGrimoireDeck14()
                },

                // Faction 4: Curly Tail's Warband
                new FactionDef
                {
                    Id = FactionId.CurlyTail,
                    Name = "Curly Tail's Warband",
                    Tier = FactionDifficultyTier.BeginnerFriendly,
                    DifficultyRatingOutOf10 = 4,
                    ShortSummary = "A snowballing early game faction led by a ferocious boar. Race to forge golden armor and secure supply lines to quickly traverse the map.",
                    LoreDescription = "Curly Tail, a boar warlord, stumbled upon the old political treatises of the Ananke and admired their indomitable warrior spirit. Every victory feeds Curly Tail's hunger, and his hunger feeds his glory.",
                    SetupInstructions = "Place Curly Tail, 4 Warband units, and 1 War Party on a Forest or Ruin tile 2 tiles away from enemies. Set Thirst for Glory, Hunger, and Momentum meters to 0.",
                    PrimaryColor = Palette.FromHex("#C86428"),
                    SecondaryColor = Palette.FromHex("#F5C445"),
                    PrimaryBuilding = BuildingType.WarParty,
                    PrimaryBuildingCost = new ResourceCost(wood: 1, stone: 1),
                    BuildingRules = "War Party: Craft for 1 Wood & 1 Stone on a tile with a Warband unit.",
                    SpeciesTraits = new List<string>
                    {
                        "Spoils of War: First battle won each turn grants 1 Wood or 1 Stone from the resource deck.",
                        "Three Meters: Thirst for Glory (0-4), Hunger (0-4), and Momentum (0-4)."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Destroy a Supply Line Token or War Party Building: 1 Authority.",
                        "Force Curly Tail into Strategic Retreat (0 HP): 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Curly Tail's Warband Faction Board", "20 Warband Unit Pieces & Card", "1 Curly Tail Special Unit Piece & Card",
                        "5 War Party Buildings", "8 Supply Line Tokens", "3 Meter Boards (Thirst for Glory, Hunger, Momentum)",
                        "5 Golden Equipment Cards (Golden Sword, Golden Gauntlet, Golden War Axe, Golden Chestplate, Tyrant's Crown)"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "warband", Name = "Warband", Faction = FactionId.CurlyTail, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 1, BaseMaxHP = 1, SupplyCount = 20, SummoningCondition = "Recruit at War Parties", AbilitiesText = "Supply Line: Twice/Main Phase spend 1 Wood to place Supply Line Token. Forge: Free craft 1 Golden Equipment in Crafting Phase." },
                        new UnitDef { Id = "curly_tail", Name = "Curly Tail, The Tyrannical", Faction = FactionId.CurlyTail, IsSpecialUnit = true, BaseAttack = 3, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Starts on board; Strategic Retreat respawns at a War Party.", AbilitiesText = "Pillager: Free loot after winning a battle. Taunt (Glory Lv3): Pull enemy 1 tile closer and free battle." }
                    },
                    Equipment = new List<EquipmentDef>
                    {
                        new EquipmentDef { Id = "golden_sword", Name = "Golden Sword", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 1), BonusAttack = 1, AuthorityOnCraft = 2, EffectText = "+2 Authority on craft. Curly Tail gains +1 Attack." },
                        new EquipmentDef { Id = "golden_war_axe", Name = "Golden War Axe", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 2), AuthorityOnCraft = 2, EffectText = "+2 Authority on craft. Curly Tail gains 1 free battle action and cannot be moved by enemy effects." },
                        new EquipmentDef { Id = "golden_gauntlet", Name = "Golden Gauntlet", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 2), AuthorityOnCraft = 2, EffectText = "+2 Authority on craft. Curly Tail deals 2HP to a single enemy Special Unit on win (bypasses 1HP cap)." },
                        new EquipmentDef { Id = "golden_chestplate", Name = "Golden Chest Plate", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 2), BonusDefense = 2, AuthorityOnCraft = 2, EffectText = "+2 Authority on craft. Curly Tail gains +2 Defense." },
                        new EquipmentDef { Id = "tyrants_crown", Name = "Tyrant's Crown", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 2), BonusHP = 2, AuthorityOnCraft = 2, EffectText = "+2 Authority on craft. Curly Tail gains +2 HP." }
                    }
                },

                // Faction 5: Firestarting Squirrels
                new FactionDef
                {
                    Id = FactionId.FirestartingSquirrels,
                    Name = "Firestarting Squirrels",
                    Tier = FactionDifficultyTier.Intermediate,
                    DifficultyRatingOutOf10 = 5,
                    ShortSummary = "Early game faction made of Squirrels that set the board ablaze to deal damage, limit resources, and control enemy movements.",
                    LoreDescription = "The Squirrels use Acorn Firestarters to create Fire Tiles that damage enemy units, drive enemies out, and burn the Continent's resources.",
                    SetupInstructions = "Place 1 Squirrel Unit on 3 adjacent Forest tiles without enemies, and 1 Bunker on one of them. Start with 2 Charcoal resources and 1 crafted Acorn Firestarter.",
                    PrimaryColor = Palette.FromHex("#E35225"),
                    SecondaryColor = Palette.FromHex("#3B322C"),
                    PrimaryBuilding = BuildingType.Bunker,
                    PrimaryBuildingCost = new ResourceCost(stone: 2),
                    BuildingRules = "Bunker: Craft for 2 Stone on a tile with a Squirrel. Protects Squirrels on its tile from Fire Tile damage and movement.",
                    SpeciesTraits = new List<string>
                    {
                        "Unquenchable Spirit: Once per Main Phase, loot an adjacent Fire Tile for 1 Charcoal card.",
                        "Flames of War: End Phase -> +1 Authority per 2HP Fire damage dealt, +1 Authority per 2 enemies moved by Fire (cap 4/turn).",
                        "Forest Fire: End Phase -> +1 Authority per Fire Tile adjacent to enemy pieces (cap 2).",
                        "Ignite: End Phase -> Burn 1 hand card to relocate 1 Fire Tile to a Forest tile adjacent to an enemy within 3 tiles of a Squirrel.",
                        "Disruption: Once per Main Phase, burn 2 resources to burn 1 Excavation card and 1 resource from each opponent (+1 Authority if Gold burned)."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Destroy a Bunker, Fire Tile (costs 2 Stone on tile), or Tar Pit Token: 1 Authority.",
                        "Kill the Pyromaniac: 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Firestarting Squirrels Faction Board", "15 Squirrel Unit Pieces & Card", "1 Pyromaniac Special Unit Piece & Card",
                        "6 Fire Tiles", "3 Tar Pit Tokens", "3 Bunker Buildings", "6 Charcoal Resource Cards", "3 Acorn Firestarter Cards"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "squirrel", Name = "Squirrel", Faction = FactionId.FirestartingSquirrels, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 15, SummoningCondition = "Recruit at Bunkers", AbilitiesText = "Tar Pit Token: Spend 2 Charcoal in Crafting Phase. Acorn Firestarter: Spend 1 Charcoal + 1 Wood to craft; discard to place Fire Tile." },
                        new UnitDef { Id = "pyromaniac", Name = "Pyromaniac", Faction = FactionId.FirestartingSquirrels, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 2, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "Once/game: Burn 1 Wood, 1 Stone, 1 Gold, 1 Charcoal -> Summon on a Fire Tile.", AbilitiesText = "Affinity: Immune to Fire. Flameweaver: Burn 2 hand cards to move a Fire Tile. Pyromancy: Burn 1W+1G to place Fire Tile. Oil Spill: Burn 1 Charcoal for Tar Pit." }
                    }
                },

                // Faction 6: Platypus Papacy
                new FactionDef
                {
                    Id = FactionId.PlatypusPapacy,
                    Name = "Platypus Papacy",
                    Tier = FactionDifficultyTier.BeginnerFriendly,
                    DifficultyRatingOutOf10 = 2,
                    ShortSummary = "Mid-late game faction that uses the martyring of Pilgrims to fuel a crusade that only grows stronger with time.",
                    LoreDescription = "The Papacy holds the platypus as divinely inspired creations. Pilgrims carry the word into hostile lands and gladly martyr themselves to fuel the Platy-Pope's Crusade.",
                    SetupInstructions = "Place The Citadel, 3 Pilgrims, and The Platy-Pope on a tile with no enemy pieces. Set Faith Meter and Platy-Pope Level to 0.",
                    PrimaryColor = Palette.FromHex("#2E8B9A"),
                    SecondaryColor = Palette.FromHex("#F5E6A8"),
                    PrimaryBuilding = BuildingType.TheCitadel,
                    PrimaryBuildingCost = new ResourceCost(wood: 2, stone: 2),
                    BuildingRules = "The Citadel (Limit 1): Recruits 1 Pilgrim (plus Crusaders as Platy-Pope levels up). If destroyed, rebuild for 2 Stone + 2 Wood and spawn 1 Pilgrim + 1 Crusader.",
                    SpeciesTraits = new List<string>
                    {
                        "Prophetic Vision: Start of Draw Phase -> Look at top 3 Relic cards, put 1 in hand and 2 on bottom of deck.",
                        "Alms: Once per Main Phase at 5 Faith -> Increase Platy-Pope Level by 1, gain 1 Authority, reset Faith to 0.",
                        "Indulgences: Main Phase -> Discard 1 Gold to gain 2 Faith."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Kill 2 or more Crusaders in one battle: 1 Authority.",
                        "Kill The Platy-Pope: 2 Authority (Papacy loses 1 Authority).",
                        "Destroy a Holy Relic Token or The Citadel: 1 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Platypus Papacy Faction Board", "5 Pilgrim Unit Pieces & Card", "13 Crusader Unit Pieces & Card",
                        "1 Platy-Pope Special Unit Piece & Card", "2 Holy Relic Tokens", "1 Citadel Building", "1 Faith & Platy-Pope Level Meter Board (0-5)"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "pilgrim", Name = "Pilgrim", Faction = FactionId.PlatypusPapacy, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 5, SummoningCondition = "Recruit at The Citadel", AbilitiesText = "Martyrdom: When a Pilgrim moves onto a tile with enemy pieces, remove Pilgrim to gain +1 Faith." },
                        new UnitDef { Id = "crusader", Name = "Crusader", Faction = FactionId.PlatypusPapacy, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 1, BaseMaxHP = 1, SupplyCount = 13, SummoningCondition = "Recruit at The Citadel (Platy-Pope Lv1+)", AbilitiesText = "Holy Relic Token: Spend 1 Wood, 1 Stone, 1 Gold to place Holy Relic at Citadel (+1 Faith each End Phase). Scales with Platy-Pope Level (0-5)." },
                        new UnitDef { Id = "platy_pope", Name = "The Platy-Pope", Faction = FactionId.PlatypusPapacy, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 3, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "Starts on board (cannot be resummoned if killed).", AbilitiesText = "Unyielding: Cannot move or be moved. Crusade (2 turns on level-up): Crusaders +1 Attack, +1 bonus Authority on structures/specials, +1 Faith on battle win." }
                    }
                },

                // Faction 7: Carnivorous Queen
                new FactionDef
                {
                    Id = FactionId.CarnivorousQueen,
                    Name = "Carnivorous Queen",
                    Tier = FactionDifficultyTier.Intermediate,
                    DifficultyRatingOutOf10 = 6,
                    ShortSummary = "Early-mid game faction, stalk the board as a lone monster that evolves by feeding. Hunt, ensnare, and devour your prey to mutate into an ever-deadlier predator.",
                    LoreDescription = "In the deep woods something feasts on the unsuspecting creatures of the Continent. Patiently stalking, the Queen evolves through 4 stages and drafts permanent mutations.",
                    SetupInstructions = "Place Carnivorous Queen (Stage 1: Lurker 2/1/3) and 1 Chrysalis on a Forest tile. On 3 other Forest tiles without enemies, place 1 Larvae and 1 Egg Token each. Rage=0, Feeding=0.",
                    PrimaryColor = Palette.FromHex("#2E6F40"),
                    SecondaryColor = Palette.FromHex("#A8E05F"),
                    PrimaryBuilding = BuildingType.Chrysalis,
                    PrimaryBuildingCost = new ResourceCost(wood: 2, stone: 1),
                    BuildingRules = "Chrysalis (0/2/2): Acts as both a building and a defending unit. Cannot move or attack, but defends in battle.",
                    SpeciesTraits = new List<string>
                    {
                        "Darwinism: Main Phase -> If Queen is dead, remove 1 Egg + 1 Larvae to respawn her at a Chrysalis at current Stage/Mutations with half HP (rounded up).",
                        "Watching From The Dark: End Phase -> If Queen is Hidden, gain 1 Authority.",
                        "Nesting: Main Phase -> Discard 1 Relic card from hand to place 1 Egg Token at a Chrysalis.",
                        "Swarm: Draw Phase -> Hatch any number of Egg Tokens into Larvae units.",
                        "Lack of Humanity: Cannot use the Assemble action."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Kill the Carnivorous Queen: 2 Authority.",
                        "Destroy an Egg Token or Chrysalis: 1 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Carnivorous Queen Faction Board", "1 Rage (0-15) & Feeding (0-7) Meter", "7-Card Equipment Mutation Deck",
                        "1 Carnivorous Queen Special Unit Piece & 4 Stage Cards", "8 Ensnared Unit Pieces & Card", "6 Larvae Unit Pieces & Card",
                        "8 Egg Tokens", "3 Chrysalis Building Pieces & Card"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "larvae", Name = "Larvae", Faction = FactionId.CarnivorousQueen, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 6, SummoningCondition = "Recruit at Chrysalis or hatch Egg Tokens via Swarm", AbilitiesText = "Grants +1 Rage when killed; beacon for Verdant Voracity." },
                        new UnitDef { Id = "ensnared", Name = "Ensnared", Faction = FactionId.CarnivorousQueen, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 8, SummoningCondition = "Created by Queen's Ensnare ability (exiles enemy unit)", AbilitiesText = "Cannot move, attack, or use abilities; can be attacked or devoured by Scaphism." },
                        new UnitDef { Id = "carnivorous_queen", Name = "Carnivorous Queen", Faction = FactionId.CarnivorousQueen, IsSpecialUnit = true, BaseAttack = 2, BaseDefense = 1, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "Starts at Stage 1 (2/1/3) -> Stage 2 (3/1/3 @ 2 Feed) -> Stage 3 (4/2/4 @ 4 Feed) -> Stage 4 (5/2/5 @ 7 Feed).", AbilitiesText = "Hidden Amongst the Trees (2 Rage), Ensnare (1 Action + 3 Rage -> +1 Auth & +1 Feed), Verdant Voracity (3 Rage teleport), Law of Nature (2 Rage devour)." }
                    },
                    Equipment = BuildQueenMutations7()
                },

                // Faction 8: The Glacier Kingdom
                new FactionDef
                {
                    Id = FactionId.GlacierKingdom,
                    Name = "The Glacier Kingdom",
                    Tier = FactionDifficultyTier.Intermediate,
                    DifficultyRatingOutOf10 = 5,
                    ShortSummary = "Mid game defensive faction made of Penguins. Spread over to strengthen your units, wall off enemies, and push your enemies away.",
                    LoreDescription = "Hailing from the far North, the Penguins freeze the forests into a fortress of ice and drive avalanches through enemies that approach.",
                    SetupInstructions = "Place 1 Glacier Tile on a tile with no enemy units, then place 3 Penguin units and 1 Igloo on that Glacier Tile.",
                    PrimaryColor = Palette.FromHex("#3A9AD9"),
                    SecondaryColor = Palette.FromHex("#E6F7FF"),
                    PrimaryBuilding = BuildingType.Igloo,
                    SecondaryBuilding = BuildingType.FrozenHarbor,
                    PrimaryBuildingCost = new ResourceCost(wood: 1, stone: 1),
                    BuildingRules = "Igloo (1W+1S on Glacier Tile). Frozen Harbor (2W on board-edge or river-adjacent Glacier Tile): +1 Excavation action, loots 1 Gold, recruits 1 Penguin.",
                    SpeciesTraits = new List<string>
                    {
                        "Ice Age: Draw Phase -> Place 1 Glacier Tile adjacent to an existing Glacier Tile (pushes enemies off).",
                        "Cold Shoulder: End Phase -> Gain 1 Authority if an enemy was moved by a Glacier piece or is Frozen.",
                        "Avalanche: Draw Phase -> Choose Snowfall (place Lv1 token), Buildup (upgrade Lv1->2->3), or Cascade (remove Lv3 to push all units within 2 tiles)."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Destroy an Iceberg Wall, Igloo, or Frozen Harbor: 1 Authority.",
                        "Kill The Emperor Penguin: 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Glacier Kingdom Faction Board", "1 Blizzard Meter", "18 Penguin Unit Pieces & Card",
                        "1 Emperor Penguin Special Unit Piece & Card", "20 Glacier Tiles", "4 Igloo Buildings",
                        "1 Frozen Harbor Building", "20 Iceberg Wall Pieces (0/0/2)", "9 Snowfall Tokens (3x Lv1, 3x Lv2, 3x Lv3)"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "penguin", Name = "Penguin", Faction = FactionId.GlacierKingdom, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 2, BaseMaxHP = 1, SupplyCount = 18, SummoningCondition = "Recruit at Igloos and Frozen Harbor", AbilitiesText = "Slip and Slide (move 2 Glacier tiles + push enemy), Huddle (+1 total Defense when 3+ defend), Ice Fishing (loot Glacier tile), Iceberg Wall (2 Stone -> 0/0/2 hex-edge wall)." },
                        new UnitDef { Id = "emperor_penguin", Name = "The Emperor Penguin", Faction = FactionId.GlacierKingdom, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 3, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Once/game: Discard 2 Gold -> Summon on Glacier Tile with Igloo/Frozen Harbor.", AbilitiesText = "Frozen World: Ice Age places +1 Glacier Tile. Blizzard: Spend 1 Gold -> +1 Authority and freeze isolated enemies across the board for 1 round." }
                    }
                },

                // Faction 9: Trouble-Making Tanukis
                new FactionDef
                {
                    Id = FactionId.TroubleMakingTanukis,
                    Name = "Trouble-Making Tanukis",
                    Tier = FactionDifficultyTier.BeginnerFriendly,
                    DifficultyRatingOutOf10 = 4,
                    ShortSummary = "Mid game faction that scampers around the board collecting coins while hiding pranks and crippling traps beneath fallen leaves.",
                    LoreDescription = "Inspired by Anankian texts, the Tanukis scamper the Continent gathering scattered coins to deposit at shrines while paving the woods with prank and trap leaves.",
                    SetupInstructions = "Place 12 Coin Tokens on Forest tiles around the map (each at least 2 tiles apart). Place 3 Tanuki units and 1 Shrine on an empty Forest tile.",
                    PrimaryColor = Palette.FromHex("#B5652A"),
                    SecondaryColor = Palette.FromHex("#F2C94C"),
                    PrimaryBuilding = BuildingType.Shrine,
                    PrimaryBuildingCost = new ResourceCost(wood: 2),
                    BuildingRules = "Shrine: Craft for 2 Wood on a Forest tile with a Tanuki. Delivering a Coin Token to a Shrine scores +2 Authority!",
                    SpeciesTraits = new List<string>
                    {
                        "The Coin Hunt: When a Tanuki or Band moves off a tile with a Coin Token, carry the Coin with them (delivering to a Shrine gives +2 Authority).",
                        "Quick Feet: When Tanukis on a tile with 2+ Tanukis take damage in battle, surviving Tanukis may scatter to empty adjacent tiles and drop 1 Fallen Leaves Tile.",
                        "Rustling in the Trees: Draw Phase -> Place 1 Fallen Leaves Tile on or adjacent to a Shrine without enemies."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Destroy a flipped Fallen Leaves Tile or a Shrine: 1 Authority.",
                        "Reduce A Band of Troublemakers to 0 HP: 2 Authority (splits into 3 Tanukis)."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Troublemaking Tanuki Faction Board", "18 Tanuki Unit Pieces & Card", "1 A Band of Troublemakers Special Unit Piece & Card",
                        "3 Shrine Buildings", "20 Coin Tokens", "8 Prank Fallen Leaves Tiles", "5 Trap Fallen Leaves Tiles"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "tanuki", Name = "Tanuki", Faction = FactionId.TroubleMakingTanukis, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 1, BaseMaxHP = 1, SupplyCount = 18, SummoningCondition = "Recruit at Shrines", AbilitiesText = "Running Amok: Once per Main Phase, place 1 Fallen Leaves Tile (Trap or Prank) on a tile with a Tanuki." },
                        new UnitDef { Id = "band_of_troublemakers", Name = "A Band of Troublemakers", Faction = FactionId.TroubleMakingTanukis, IsSpecialUnit = true, BaseAttack = 2, BaseDefense = 2, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "Remove 3 Tanukis on same tile + 1 Gold -> Summon Band.", AbilitiesText = "Belly Drums: Draw Phase -> Place Fallen Leaves on up to 2 Coin tiles. No Honor Amongst Troublemakers: On 0 HP, split into 3 Tanukis on adjacent Forest tiles." }
                    }
                },

                // Faction 10: The Capybara Caravan
                new FactionDef
                {
                    Id = FactionId.CapybaraCaravan,
                    Name = "The Capybara Caravan",
                    Tier = FactionDifficultyTier.Intermediate,
                    DifficultyRatingOutOf10 = 5,
                    ShortSummary = "Mid-late game faction of peaceful river merchants who win through hospitality and trade.",
                    LoreDescription = "The Capybaras dredge Anankian relics from the riverbeds and welcome weary travelers as Guests to their Wallows, scoring Authority through peaceful commerce.",
                    SetupInstructions = "Special Setup (before all others): Place 8 connected River Tiles onto Forest tiles. Regular Setup: Place 3 Capybaras, Capybara Trader, and 1 Wallow on a River Tile.",
                    PrimaryColor = Palette.FromHex("#9C6B3B"),
                    SecondaryColor = Palette.FromHex("#5BC0BE"),
                    PrimaryBuilding = BuildingType.Wallow,
                    PrimaryBuildingCost = new ResourceCost(wood: 1, stone: 1),
                    BuildingRules = "Wallow: Craft for 1 Wood & 1 Stone on a River tile with a Capybara (or Forest tile adjacent to a River with a Capybara).",
                    SpeciesTraits = new List<string>
                    {
                        "Watering Hole: Peaceful enemy units at Wallows/Riverkeeper or Traded Goods buyers become Guests (draw +1 Resource at End Phase).",
                        "Selfless Nature: Capybara units on the same tile absorb 1HP battle damage for Guests.",
                        "Customary Greeting: End Phase -> Gain 1 Authority for each player who controls a Guest unit (cap 2/turn).",
                        "Exchange Rate: Once per Main Phase, trade 3 identical resources for 2 identical resources of any type from the deck."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Buy a Traded Goods card from the Capybara Trader: Buyer gains 1 Authority (and Capybara gains 1 Authority).",
                        "Kill Capybara Trader or destroy a Wallow: 1 Authority.",
                        "Kill The Riverkeeper: 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Capybara Caravan Faction Board", "5 Wallow Buildings", "8-Card Traded Goods Deck",
                        "1 Riverkeeper Special Unit Piece & Card", "1 Capybara Trader Special Unit Piece & Card",
                        "20 Capybara Unit Pieces & Card", "8 River Tiles"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "capybara", Name = "Capybara", Faction = FactionId.CapybaraCaravan, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 1, BaseMaxHP = 2, SupplyCount = 20, SummoningCondition = "Recruit at Wallows", AbilitiesText = "Forage: Once/Main Phase on a River Tile, gain 1 Gold or 1 Stone. Semiaquatic: Move 2 River Tiles for 1 Move action." },
                        new UnitDef { Id = "capybara_trader", Name = "Capybara Trader", Faction = FactionId.CapybaraCaravan, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Starts on board; if removed, discard 2 Relic cards in Excavation Phase to respawn at a Wallow.", AbilitiesText = "Traveling Caravan: Opponents on/adjacent can buy from the 3-card Traded Goods Stall (+1 Auth to both). Peddling: 1 free move/turn (2 land or 3 river tiles)." },
                        new UnitDef { Id = "riverkeeper", Name = "The Riverkeeper", Faction = FactionId.CapybaraCaravan, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 4, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "Requires 2+ Wallows: Spend 2 resources + 1 Gold -> Summon at a Wallow.", AbilitiesText = "Hospitality: Guest on tile draws 1 Relic -> Capybara gains +1 Authority & recovers 1 discarded Relic. Immovable. Commerce: Guests anywhere can buy Traded Goods." }
                    },
                    SubDeckCards = BuildCapybaraTradedGoodsDeck8()
                },

                // Faction 11: The Briared Flower
                new FactionDef
                {
                    Id = FactionId.BriaredFlower,
                    Name = "The Briared Flower",
                    Tier = FactionDifficultyTier.BeginnerFriendly,
                    DifficultyRatingOutOf10 = 4,
                    ShortSummary = "Early game glass-cannon faction whose flower special unit hurls thorns across the board, shedding its own armor with every strike until it lashes out in a final fury.",
                    LoreDescription = "Born with an instinctual hatred of other creatures, the Blooming Briar hurls its fiery spines across the Continent to protect its scattered seedlings, entering Frenzied Bloom when stripped bare.",
                    SetupInstructions = "Place Blooming Briar (with 6 Thorns), 2 Thorned Seeds, and 1 Prickly Thicket on a Forest tile at least 4 tiles away from enemies.",
                    PrimaryColor = Palette.FromHex("#C2295A"),
                    SecondaryColor = Palette.FromHex("#5E8C31"),
                    PrimaryBuilding = BuildingType.PricklyThicket,
                    PrimaryBuildingCost = new ResourceCost(wood: 2),
                    BuildingRules = "Prickly Thicket: Craft for 2 Wood on a Forest tile with a Thorned Seed or Blooming Briar. Can be consumed by Blooming Briar for +1 Thorn.",
                    SpeciesTraits = new List<string>
                    {
                        "In Half Bloom: Main Phase -> If Blooming Briar is off-board, spend 2 resources + 3 Thorned Seeds to respawn her with 2 Thorns at a Prickly Thicket.",
                        "Branching Out: Thistle Barrage gains +1 tile range per Thorned Seed on Blooming Briar's tile (cap +2 range -> 6 tiles).",
                        "Rooted In Place: Blooming Briar can only move 1 tile per turn.",
                        "Pollination: End Phase -> Gain 1 Authority if Thorned Seeds occupy 4+ different tiles (2 Authority if 7+ tiles).",
                        "Instinctual Hatred: Cannot trade with other players."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Destroy a Prickly Thicket: 1 Authority.",
                        "Kill the Blooming Briar: 2 Authority (triggers Propagation: 1HP damage + spawns 1 Thorned Seed)."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Briared Flower Faction Board", "1 Thorn Meter Board (0-6 Thorns)",
                        "1 Blooming Briar Special Unit Piece & Card", "18 Thorned Seed Unit Pieces & Card",
                        "4 Prickly Thicket Building Pieces"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "thorned_seed", Name = "Thorned Seed", Faction = FactionId.BriaredFlower, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 18, SummoningCondition = "Recruit at Prickly Thickets or spawned via Thistle Barrage", AbilitiesText = "Bursting Into Bloom: Remove up to 3 Thorned Seeds to deal 1HP damage on their tiles (+1 Authority if an enemy unit dies)." },
                        new UnitDef { Id = "blooming_briar", Name = "Blooming Briar", Faction = FactionId.BriaredFlower, IsSpecialUnit = true, BaseAttack = 6, BaseDefense = 1, BaseMaxHP = 1, SupplyCount = 1, SummoningCondition = "Starts on board with 6 Thorns; respawns via In Half Bloom.", AbilitiesText = "With 1-6 Thorns: Thistle Barrage (ranged 4-6 tile snipe, +1 Auth per 2HP dealt), Thorn Armor (spend 1 Thorn to block 1HP), Sun Eater. At 0 Thorns (Frenzied Bloom): Withered Blossom (+3 Attack, 2HP single-unit bypass), Furious Lashing (1HP AoE within 3 tiles), Propagation." }
                    }
                }
            };
        }
    }
}
