using System.Collections.Generic;
using UnityEngine;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.Model
{
    /// <summary>
    /// Complete, verbatim database for Critters of Calamity - Core Rulebook: Eight Faction Edition:
    /// - 40 Relic Deck cards (Part 16)
    /// - 50 Resource Deck cards (20 Stone, 20 Wood, 10 Gold - Part 2)
    /// - 8-card Forage Deck (3 Plague + 5 Rummage; used cards go to a discard pile, reshuffled when the deck runs out)
    /// - 8-card Traded Goods Deck (Part 17 Faction 7)
    /// - The 8 Core Factions (Part 17):
    ///   1. Curly Tail's Warband (4/10)
    ///   2. Horde of the Millions (2/10)
    ///   3. Platypus Papacy (2/10)
    ///   4. Trouble Making Tanukis (4/10)
    ///   5. The Necro-mouser (6/10)
    ///   6. Firestarting Squirrels (6/10)
    ///   7. The Capybara Caravan (5/10)
    ///   8. The Briared Flower (7/10)
    /// </summary>
    public static class RulebookDatabase
    {
        /// <summary>How many of each building a faction owns (Part 17 materials lists).</summary>
        public static int BuildingSupply(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.WarParty: return 5;
                case BuildingType.Burrow: return 3;
                case BuildingType.TheCitadel: return 1;
                case BuildingType.Shrine: return 3;
                case BuildingType.Bunker: return 3;
                case BuildingType.Wallow: return 5;
                case BuildingType.PricklyThicket: return 4;
                case BuildingType.SummoningCircle: return 3;
            }
            return 0;
        }

        /// <summary>Tokens each faction owns (Part 17 materials lists). Catapults are shared: any player may craft one.</summary>
        public static List<KeyValuePair<TokenType, int>> TokenSupply(FactionId faction)
        {
            var list = new List<KeyValuePair<TokenType, int>>();
            switch (faction)
            {
                case FactionId.CurlyTail: list.Add(new KeyValuePair<TokenType, int>(TokenType.SupplyLine, 8)); break;
                case FactionId.HordeOfTheMillions:
                    list.Add(new KeyValuePair<TokenType, int>(TokenType.Gluttony, 3));
                    list.Add(new KeyValuePair<TokenType, int>(TokenType.Catapult, 2));
                    break;
                case FactionId.PlatypusPapacy: list.Add(new KeyValuePair<TokenType, int>(TokenType.HolyRelic, 2)); break;
                case FactionId.TroubleMakingTanukis: list.Add(new KeyValuePair<TokenType, int>(TokenType.Coin, 20)); break;
                case FactionId.FirestartingSquirrels: list.Add(new KeyValuePair<TokenType, int>(TokenType.TarPit, 3)); break;
                case FactionId.GoatCabal:
                    list.Add(new KeyValuePair<TokenType, int>(TokenType.InitiationRite, 2));
                    list.Add(new KeyValuePair<TokenType, int>(TokenType.Effigy, 3));
                    break;
            }
            return list;
        }

        /// <summary>Unique faction tiles each faction owns (Part 17 materials lists).</summary>
        public static List<KeyValuePair<string, int>> FactionTileSupply(FactionId faction)
        {
            var list = new List<KeyValuePair<string, int>>();
            switch (faction)
            {
                case FactionId.TroubleMakingTanukis: list.Add(new KeyValuePair<string, int>("Fallen Leaves", 13)); break;
                case FactionId.Necromouser: list.Add(new KeyValuePair<string, int>("Miasma", 6)); break;
                case FactionId.FirestartingSquirrels: list.Add(new KeyValuePair<string, int>("Fire", 6)); break;
                case FactionId.CapybaraCaravan: list.Add(new KeyValuePair<string, int>("Stream", 6)); break;
            }
            return list;
        }

        public static List<ResourceType> BuildStandard50ResourceDeck()
        {
            var deck = new List<ResourceType>(50);
            for (int i = 0; i < 20; i++) deck.Add(ResourceType.Stone);
            for (int i = 0; i < 20; i++) deck.Add(ResourceType.Wood);
            for (int i = 0; i < 10; i++) deck.Add(ResourceType.Gold);
            return deck;
        }

        /// <summary>The Goat Cabal's 14-card Grimoire (Faction 9). Milled one at a time; see CrittersGameEngine.Goat.cs.</summary>
        public static List<FactionSubDeckCardDef> BuildGoatGrimoire14()
        {
            var texts = new[]
            {
                "gr_purged_1|Gain 1 Purged",
                "gr_trade_purged_auth|Lose 1 Purged, gain 1 Authority",
                "gr_auth_for_purged|Lose 2 Authority, gain 2 Purged",
                "gr_hand_for_auth|Discard 1 card from your hand, gain 2 Authority",
                "gr_draw_for_auth|Draw 1 card, lose 2 Authority",
                "gr_res_for_purged|Discard 2 resources from your resource pile, gain 2 Purged",
                "gr_gold_2|Gain 2 Gold",
                "gr_gold_for_auth|Lose 1 Gold, gain 2 Authority",
                "gr_draw2_discard1|Draw 2 cards from the relic deck, then discard 1",
                "gr_gold_1|Gain 1 Gold",
                "gr_auth_for_gold|Lose 2 Authority, gain 1 Gold",
                "gr_auth_3|Gain 3 Authority",
                "gr_auth_gold_purged|Gain 1 Authority, 1 Gold, 1 Purged",
                "gr_gold_3_cost|Gain 3 Gold, lose 1 Authority, 1 Purged"
            };
            var list = new List<FactionSubDeckCardDef>();
            foreach (var t in texts)
            {
                var parts = t.Split('|');
                list.Add(new FactionSubDeckCardDef { Id = parts[0], DeckName = "Grimoire", Title = "Grimoire", Copies = 1, EffectText = "When milled: " + parts[1] });
            }
            return list;
        }

        public static List<FactionSubDeckCardDef> BuildMillionsForageDeck8()
        {
            return new List<FactionSubDeckCardDef>
            {
                new FactionSubDeckCardDef { Id = "plague_authority", DeckName = "Forage Deck", Title = "Plague: Authority Drain", Copies = 1, EffectText = "You lose 1 Authority and the Horde player gains 1 Authority." },
                new FactionSubDeckCardDef { Id = "plague_infest", DeckName = "Forage Deck", Title = "Plague: Rummaging Swarm", Copies = 1, EffectText = "Place 1 Millions unit from the Horde player's supply on a tile with one of your pieces, then discard 1 resource from your Resource Pile." },
                new FactionSubDeckCardDef { Id = "plague_sickness", DeckName = "Forage Deck", Title = "Plague: Sickness", Copies = 1, EffectText = "One of your units on a tile with a Millions unit loses 1 HP. Then the Horde player summons 1 Millions unit on any tile with an enemy unit." },
                new FactionSubDeckCardDef { Id = "rummage_stone_1", DeckName = "Forage Deck", Title = "Rummage: 2 Stone", Copies = 1, EffectText = "The Horde player gains 2 Stone from the resource deck." },
                new FactionSubDeckCardDef { Id = "rummage_stone_2", DeckName = "Forage Deck", Title = "Rummage: 2 Stone", Copies = 1, EffectText = "The Horde player gains 2 Stone from the resource deck." },
                new FactionSubDeckCardDef { Id = "rummage_wood_1", DeckName = "Forage Deck", Title = "Rummage: 2 Wood", Copies = 1, EffectText = "The Horde player gains 2 Wood from the resource deck." },
                new FactionSubDeckCardDef { Id = "rummage_wood_2", DeckName = "Forage Deck", Title = "Rummage: 2 Wood", Copies = 1, EffectText = "The Horde player gains 2 Wood from the resource deck." },
                new FactionSubDeckCardDef { Id = "rummage_gold", DeckName = "Forage Deck", Title = "Rummage: 1 Gold", Copies = 1, EffectText = "The Horde player gains 1 Gold from the resource deck." }
            };
        }

        public static List<FactionSubDeckCardDef> BuildCapybaraTradedGoodsDeck8()
        {
            return new List<FactionSubDeckCardDef>
            {
                new FactionSubDeckCardDef { Id = "tg_rusty_shovel", DeckName = "Traded Goods", Title = "Rusty Shovel", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. The next time you use the looting action, you can draw 3 cards from the resource deck and end the looting action, then discard this card." },
                new FactionSubDeckCardDef { Id = "tg_clay_edict", DeckName = "Traded Goods", Title = "Equipment - Clay Edict", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. During your main phase, equip this card to 1 of your units; it gains +1 tile movement for this turn, then discard this card." },
                new FactionSubDeckCardDef { Id = "tg_prayer_beads", DeckName = "Traded Goods", Title = "Prayer Beads", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. During your excavation phase, draw the top 2 cards from the relic deck, then discard this card." },
                new FactionSubDeckCardDef { Id = "tg_silver_spoon", DeckName = "Traded Goods", Title = "Silver Spoon", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. During your excavation phase, add 2 relic cards from the discard pile to your hand; then both you and the Capybara player gain 1 Authority." },
                new FactionSubDeckCardDef { Id = "tg_copper_figurine", DeckName = "Traded Goods", Title = "Copper Figurine", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. After a die has been rolled, discard this card to re-roll 1 die (can be used during the battle dice roll phase)." },
                new FactionSubDeckCardDef { Id = "tg_broken_pickaxe", DeckName = "Traded Goods", Title = "Broken Pickaxe", Copies = 1, Cost = new ResourceCost(any: 1), EffectText = "Cost: 1 Resource. Add 1 relic card from the discard pile to your hand, then discard this card." },
                new FactionSubDeckCardDef { Id = "tg_horse_hair_brush", DeckName = "Traded Goods", Title = "Horse Hair Brush", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. During your main phase, you can discard this card. Your next unearth, assemble, or disassemble action costs 1 less resource of your choosing." },
                new FactionSubDeckCardDef { Id = "tg_salve", DeckName = "Traded Goods", Title = "Equipment - Salve", Copies = 1, Cost = new ResourceCost(any: 2), EffectText = "Cost: 2 Resources. During your main phase, equip this card to a unit; it gains +1 maximum HP and heals 1HP, then discard this card." }
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
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Gain 2 Authority."
                },
                new RelicCardDef
                {
                    Id = "damaged_anankian_chariot", Name = "Damaged Anankian Chariot", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(stone: 1),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Recruit 1 basic unit at each of your recruitment buildings. Those units cannot move this turn, but they can loot and battle."
                },
                new RelicCardDef
                {
                    Id = "war_horn", Name = "War Horn", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 2),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepOnTableWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Effect: When you are the Defender, at the start of the Attack and Defense Points phase (after the dice are rolled), discard to place 1 basic unit from supply on the battle tile. It participates in the battle."
                },
                new RelicCardDef
                {
                    Id = "iron_halberd", Name = "Iron Halberd", CopiesInDeck = 2,
                    Cost = new ResourceCost(stone: 1),
                    DisassembleGain = new ResourceCost(stone: 1),
                    KeepOnTableWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Effect: When you are the Attacker, at the start of Attack and Defense Points phase, discard to add 2 to your dice roll."
                },
                new RelicCardDef
                {
                    Id = "brass_shield", Name = "Brass Shield", CopiesInDeck = 2,
                    Cost = new ResourceCost(stone: 1, gold: 1),
                    DisassembleGain = new ResourceCost(stone: 1),
                    KeepOnTableWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Effect: In any battle you are part of (win, lose or tie), during Damage Calculation, discard to reduce the damage your units take in this battle by 2 (min 0)."
                },
                new RelicCardDef
                {
                    Id = "statue_of_the_goddess", Name = "Statue of the Goddess", CopiesInDeck = 2,
                    Cost = new ResourceCost(stone: 2, gold: 1),
                    DisassembleGain = new ResourceCost(gold: 1),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Choose 1 tile with your units. Those units gain +1 Attack in their next battle. Until that battle, they can only move together."
                },
                new RelicCardDef
                {
                    Id = "overgrown_wall", Name = "Overgrown Wall", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(stone: 1),
                    KeepOnTableWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Effect: When you are the Defender, at the start of the Attack and Defense Points phase (after the dice are rolled), discard this card. The Attacker subtracts 2 from their dice roll."
                },
                new RelicCardDef
                {
                    Id = "abandoned_road", Name = "Rally", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, gold: 1),
                    DisassembleGain = new ResourceCost(gold: 1),
                    KeepOnTableWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Effect: During your Main phase, discard this card to take 1 extra Battle action."
                },
                new RelicCardDef
                {
                    Id = "bejeweled_chalice", Name = "Bejeweled Chalice", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(stone: 1),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Gain 1 Gold and draw 1 relic card into your Hand."
                },
                new RelicCardDef
                {
                    Id = "political_treatise", Name = "Political Treatise", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepOnTableWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Effect: When you are the Defender, at the start of Attack and Defense Points phase, discard to add 2 to your dice roll."
                },
                new RelicCardDef
                {
                    Id = "marble_aqueduct", Name = "Marble Aqueduct", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Choose one: (A) discard 1 card of your choice from Hand to draw 2 resources; or (B) return any 2 burned resource cards to your Resource Pile."
                },
                new RelicCardDef
                {
                    Id = "looted_vault", Name = "Looted Vault", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    DisassembleChoiceWoodOrStone = true,
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Draw 2 relic cards into your Hand. (Disassemble: 1 Wood or 1 Stone)"
                },
                new RelicCardDef
                {
                    Id = "underground_passage", Name = "Underground Passage", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1, stone: 1),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Choose another player. Take 1 random resource card from their Resource Pile (if they have 5 or more, take 2 instead)."
                },
                new RelicCardDef
                {
                    Id = "hallow_ground", Name = "Hallowed Ground", CopiesInDeck = 2,
                    Cost = new ResourceCost(gold: 1),
                    DisassembleGain = new ResourceCost(gold: 1),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Remove 1 basic unit from the board."
                },
                new RelicCardDef
                {
                    Id = "ceremonial_bow_and_arrow", Name = "Ceremonial Bow and Arrow", CopiesInDeck = 2,
                    Cost = new ResourceCost(wood: 1, stone: 1),
                    DisassembleGain = new ResourceCost(wood: 1),
                    KeepOnTableWhenCrafted = true, HasCraftedEffect = true,
                    EffectText = "Effect: During your turn, discard this card to choose 1 relic card on another player's Table. That player discards it."
                },
                new RelicCardDef
                {
                    Id = "dilapidated_bridge", Name = "Dilapidated Bridge", CopiesInDeck = 3,
                    Cost = new ResourceCost(wood: 1),
                    DisassembleGain = new ResourceCost(wood: 2),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Gain 2 Wood."
                },
                new RelicCardDef
                {
                    Id = "abandoned_well", Name = "Abandoned Well", CopiesInDeck = 3,
                    Cost = new ResourceCost(stone: 1),
                    DisassembleGain = new ResourceCost(stone: 2),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Gain 2 Stone."
                },
                new RelicCardDef
                {
                    Id = "forest_clearing", Name = "Forest Clearing", CopiesInDeck = 4,
                    Cost = new ResourceCost(any: 1),
                    DisassembleGain = new ResourceCost(wood: 1, stone: 1),
                    KeepOnTableWhenCrafted = false, HasCraftedEffect = true,
                    EffectText = "When Crafted: Gain 1 Stone and 1 Wood."
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
                // Faction 1: Curly Tail's Warband (Part 17 Faction 1)
                new FactionDef
                {
                    Id = FactionId.CurlyTail,
                    Name = "Curly Tail's Warband",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 6,
                    ShortSummary = "Snowballing early game faction led by a ferocious boar. Race to forge golden armor and secure supply lines to quickly cross the map.",
                    HowToScoreAuthority = "Forge Golden Arms and Armor (2 Authority each, the first time each is crafted). Momentum Level 2: 1 Authority in your End phase if you won a battle that turn. Remove enemy buildings, tokens and special units.",
                    LoreDescription = "Curly Tail, a boar warlord, stumbled upon the old political treatises of the Ananke and admired their indomitable warrior spirit. His loyal Warband pillages to forge golden arms from plundered wealth, stretching supply lines across the Continent like a closing fist.",
                    SetupInstructions = "Place Curly Tail, 4 Warband units, and 1 War Party on a forest or ruin tile at least 4 tiles away from every other player's pieces. Other players cannot set up within 4 tiles of Curly Tail. Set all three meters to Level 0.",
                    PrimaryColor = Palette.FromHex("#4A4D52"),
                    SecondaryColor = Palette.FromHex("#F5C445"),
                    PrimaryBuilding = BuildingType.WarParty,
                    PrimaryBuildingCost = new ResourceCost(wood: 1, stone: 1),
                    BuildingRules = "War Party: Craft (1 Wood, 1 Stone) on a tile with a Warband unit. Recruit: Place 1 Warband unit at each War Party.",
                    SpeciesTraits = new List<string>
                    {
                        "Spoils of War: The first time you win a battle each turn, after the battle gain 1 Wood or 1 Stone (your choice) from the resource deck.",
                        "End of Main Phase Meters: (1) Raise Thirst for Glory by 1 -> (2) Pay current Hunger level effect -> (3) Raise Hunger by 1.",
                        "Momentum Meter (Attacker battles only): Attack win -> +1 Momentum. Attack loss -> Momentum and Hunger drop to 0, -2 Thirst for Glory."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Removing a Supply Line Token: 1 Authority.",
                        "Removing a War Party: 1 Authority.",
                        "Removing Curly Tail (Strategic Retreat): 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Curly Tail's Warband Faction Board", "20 Warband Unit Pieces & Card", "1 Curly Tail Special Unit Piece & Card",
                        "5 War Party Buildings", "8 Supply Line Tokens", "3 Meter Boards (Thirst for Glory, Hunger, Momentum)",
                        "5 Golden Equipment Cards (Golden Sword, Golden War Axe, Golden Gauntlet, Golden Chest Plate, Tyrant's Crown)"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "warband", Name = "Warband", Faction = FactionId.CurlyTail, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 20, SummoningCondition = "Recruit at War Parties", AbilitiesText = "Supply Line (free action, up to 2x/Main phase): Discard 1 Wood -> place a Supply Line Token on a forest tile with Curly Tail or a Warband unit, adjacent to a War Party or Supply Line Token (linked network movement). Forge (free craft, 1x/Excavation): Pay Gold to craft & equip 1 Golden Equipment to Curly Tail." },
                        new UnitDef { Id = "curly_tail", Name = "Curly Tail, the Tyrannical", Faction = FactionId.CurlyTail, IsSpecialUnit = true, BaseAttack = 3, BaseDefense = 1, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Starts on board; Strategic Retreat places him at 1 HP on a War Party with 1 Warband unit (returns 2 equipped items to supply).", AbilitiesText = "Pillager: Free Pillage (draw 1 resource; +1 relic at Momentum Lv1+) after winning a battle he took part in. Taunt (Glory Lv3+): Pull enemy within 2 tiles 1 tile closer; if on Curly Tail's tile, start a free battle." }
                    },
                    Equipment = new List<EquipmentDef>
                    {
                        new EquipmentDef { Id = "golden_sword", Name = "Golden Sword", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 1), BonusAttack = 1, AuthorityOnCraft = 2, EffectText = "When Crafted: Gain 2 Authority (first craft only). Effect: Curly Tail gains +1 Attack." },
                        new EquipmentDef { Id = "golden_war_axe", Name = "Golden War Axe", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 2), AuthorityOnCraft = 2, EffectText = "When Crafted: Gain 2 Authority. Effect: Once per turn, take 1 free Battle action that includes Curly Tail. Curly Tail cannot be moved by enemy effects." },
                        new EquipmentDef { Id = "golden_gauntlet", Name = "Golden Gauntlet", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 2), AuthorityOnCraft = 2, EffectText = "When Crafted: Gain 2 Authority. Effect: When Curly Tail wins a battle, choose 1 enemy special unit to take 2 HP of the damage dealt (ignoring 1 HP limit)." },
                        new EquipmentDef { Id = "golden_chestplate", Name = "Golden Chest Plate", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 2), BonusDefense = 2, AuthorityOnCraft = 2, EffectText = "When Crafted: Gain 2 Authority. Effect: Curly Tail gains +2 Defense." },
                        new EquipmentDef { Id = "tyrants_crown", Name = "Tyrant's Crown", Faction = FactionId.CurlyTail, TargetUnitId = "curly_tail", MaxPerUnit = 1, SupplyCount = 1, Cost = new ResourceCost(gold: 2), BonusHP = 2, AuthorityOnCraft = 2, EffectText = "When Crafted: Gain 2 Authority. Effect: Curly Tail gains +2 maximum HP and heals 2 HP when equipped." }
                    }
                },

                // Faction 2: Horde of the Millions (Part 17 Faction 2)
                new FactionDef
                {
                    Id = FactionId.HordeOfTheMillions,
                    Name = "Horde of the Millions",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 2,
                    ShortSummary = "Mid game faction made up of an endless swarm of rabbits that wins by spreading plague and being impossible to remove.",
                    HowToScoreAuthority = "Burrows: 1 Authority for every 2 Burrows on the board, each End phase. On the Search for Food: 1 Authority for every 3 Millions on a tile with enemy pieces (max 2 per turn). Plague cards from the Forage deck. Remove enemy buildings, tokens and special units.",
                    LoreDescription = "The Millions are a horde of hungry rabbits that do not seek to conquer the Continent so much as fill it. Burrow by burrow they multiply, and while rummaging for food they accidentally spread sickness.",
                    SetupInstructions = "Place 2 Burrows on 2 separate forest tiles with no enemy units. Then place 2 Millions units on each of those tiles.",
                    PrimaryColor = Palette.FromHex("#D99B66"),
                    SecondaryColor = Palette.FromHex("#F5E6D3"),
                    PrimaryBuilding = BuildingType.Burrow,
                    PrimaryBuildingCost = new ResourceCost(wood: 1),
                    BuildingRules = "Burrow: Craft (1 Wood) on a tile with a Millions unit, no enemy buildings, and < 3 enemy units. Start of End Phase: +1 Authority per 2 Burrows. End Phase Upkeep: Discard 1 Wood or 1 Stone per 2 Burrows (or remove 2 Burrows per unpaid Wood).",
                    SpeciesTraits = new List<string>
                    {
                        "Out of Hibernation: During your End phase, at each Burrow tile with fewer than 3 Millions units, place 1 Millions unit.",
                        "Prolific Creatures: At the start of the next player's Main phase, place 1 Millions unit on every tile with >= 1 Millions and >= 1 enemy piece (unless already >= 3 Millions).",
                        "On the Search for Food: During your End phase, gain 1 Authority per 3 Millions on each tile with >= 1 enemy piece (max 2 Authority/turn).",
                        "Oblivious to Danger: When the Horde loses a battle, it does not deal mandatory loser damage."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Removing a Burrow, Gluttony Token, or Catapult Token: 1 Authority.",
                        "Using your own Catapult (non-Horde players): 1 Authority per use.",
                        "Removing the King of the Millions: 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Horde of the Millions Faction Board", "30 Millions Unit Pieces & Card", "1 King of the Millions Special Unit Piece & Card",
                        "2 Catapult Tokens", "3 Gluttony Tokens", "3 Burrow Buildings", "8-Card Forage Deck (3 Plague, 5 Rummage)"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "millions", Name = "Millions", Faction = FactionId.HordeOfTheMillions, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 30, SummoningCondition = "Recruit at Burrows", AbilitiesText = "Forage: Each other player's End phase, if sharing a tile with a Millions, they draw 1 Forage card, apply it, and shuffle it back. Other People's Gluttony (free craft, Excavation): Discard 1 Gold -> place Gluttony Token on a Millions tile." },
                        new UnitDef { Id = "king_of_the_millions", Name = "King of the Millions", Faction = FactionId.HordeOfTheMillions, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 4, SupplyCount = 1, SummoningCondition = "Discard 1 Gold, 1 Stone, and 1 Wood -> Place King on a tile with a Burrow (can be summoned again after being removed).", AbilitiesText = "Spread the Fun: When summoned, every other player draws 1 Forage card. Multiply: Once per Main phase, place 1 Millions unit on a forest tile with an enemy unit of your choosing." }
                    },
                    SubDeckCards = BuildMillionsForageDeck8()
                },

                // Faction 3: Platypus Papacy (Part 17 Faction 3)
                new FactionDef
                {
                    Id = FactionId.PlatypusPapacy,
                    Name = "Platypus Papacy",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 2,
                    ShortSummary = "Mid to late game faction that martyrs Pilgrims to fuel a Crusade that only grows stronger with time.",
                    HowToScoreAuthority = "Martyrdom: 1 Authority per Pilgrim sacrificed. Holy Relic Tokens: 1 Authority when crafted. Alms: 1 Authority each time the Platy-Pope levels up. Level 5: turn 5 Faith into 1 Authority. Remove enemy buildings, tokens and special units (1 extra during a Crusade).",
                    LoreDescription = "The Papacy holds the platypus to be the divinely inspired creation of a divine being. Turning away from the heresy of the Ananke, the Platypus Pilgrims carry their gospel into hostile lands and die for it gladly.",
                    SetupInstructions = "Place the Citadel, 3 Pilgrims, and the Platy-Pope on a forest or stream tile with no enemy units. Set the Faith Meter and the Platy-Pope Level Meter to 0. During setup, other factions cannot set up within 2 tiles of the Citadel.",
                    PrimaryColor = Palette.FromHex("#C49A6C"),
                    SecondaryColor = Palette.FromHex("#F5E6A8"),
                    PrimaryBuilding = BuildingType.TheCitadel,
                    PrimaryBuildingCost = new ResourceCost(wood: 2, stone: 2),
                    BuildingRules = "The Citadel (1 piece, forest or stream tile): Recruits Pilgrims + Crusaders based on Platy-Pope Level (Lv0: 1 Pilgrim; Lv1+: +1 Crusader; Lv3+: +2 more Crusaders). If not on board, rebuild in Excavation for 2 Stone + 2 Wood and place 1 Pilgrim + 1 Crusader.",
                    SpeciesTraits = new List<string>
                    {
                        "Prophetic Vision: At the start of your Draw phase, before drawing, look at top 3 Relic cards: add 1 to Hand and put 2 on bottom of deck.",
                        "Alms: Once per Main phase while Platy-Pope is on board and Faith == 5 -> Raise Platy-Pope Level by 1, gain 1 Authority, reset Faith to 0.",
                        "Indulgences: During your Main phase, discard 1 Gold to gain 2 Faith.",
                        "Peaceful Travelers (Pilgrims): When a side made up only of Pilgrims loses a battle, it does not deal the 1 HP of mandatory loser damage.",
                        "Crucifixion (Platy-Pope Level 4+): During your Main phase, remove 1 of your Pilgrims from the board to gain 1 Faith.",
                        "Platy-Pope Return: Start of Excavation phase, if Platy-Pope is off board and Citadel is on board, place Platy-Pope at Citadel at full HP (Level resets to 1, Faith to 0).",
                        "Crusade: When the Platy-Pope reaches Level 1, and each time it levels up after that, enter a Crusade for your next 2 turns (overlapping Crusades add together). During a Crusade, Crusaders gain +1 Attack, removing an enemy building, token or special unit gives 1 extra Authority, and winning a battle gives 1 Faith. At Level 4, Crusaders removing enemy units score Authority (and enemies removing Crusaders score too)."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Removing 2 or more Crusaders in one battle: 1 Authority (plus 1 per Crusader removed during a Crusade at Lv4+).",
                        "Removing a Holy Relic Token or The Citadel: 1 Authority.",
                        "Removing the Platy-Pope: 2 Authority (and Papacy loses 1 Authority)."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Platypus Papacy Faction Board", "6 Pilgrim Unit Pieces & Card", "13 Crusader Unit Pieces & Card",
                        "1 Platy-Pope Special Unit Piece & Card", "2 Holy Relic Tokens", "1 Citadel Building", "1 Faith Meter & Platy-Pope Level Meter Board"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "pilgrim", Name = "Pilgrim", Faction = FactionId.PlatypusPapacy, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 6, SummoningCondition = "Recruit at The Citadel", AbilitiesText = "Martyrdom: When a Pilgrim ends a Move action on a tile with enemy units, remove that Pilgrim to gain 1 Faith and 1 Authority! Peaceful Travelers: when a side made up only of Pilgrims loses a battle, it does not deal the 1 HP of mandatory loser damage." },
                        new UnitDef { Id = "crusader", Name = "Crusader", Faction = FactionId.PlatypusPapacy, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 1, BaseMaxHP = 1, SupplyCount = 13, SummoningCondition = "Recruit at The Citadel (Platy-Pope Lv1+)", AbilitiesText = "Holy Relic Token: Once per Main phase while a Crusader is on board, discard 1W+1S+1G to place 1 Holy Relic Token on the Citadel's tile and immediately gain 1 Authority (+1 Faith each End phase)." },
                        new UnitDef { Id = "platy_pope", Name = "The Platy-Pope", Faction = FactionId.PlatypusPapacy, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 3, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "Starts on board; returns at Citadel at start of Excavation phase if removed.", AbilitiesText = "Unyielding: Cannot move or be moved. Crusade (2 turns on each level-up): Crusaders +1 Attack, +1 extra Authority on removing enemy building/token/special unit, +1 Faith on battle win. Lv4: Crucifixion (remove 1 Pilgrim -> +1 Faith). Lv4: Crucifixion (remove 1 Pilgrim -> +1 Faith). Lv5: Spend 5 Faith -> +1 Authority." }
                    }
                },

                // Faction 4: Trouble Making Tanukis (Part 17 Faction 4)
                new FactionDef
                {
                    Id = FactionId.TroubleMakingTanukis,
                    Name = "Trouble Making Tanukis",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 4,
                    ShortSummary = "Mid game faction that scampers around the board collecting coins while hiding pranks and traps beneath fallen leaves.",
                    HowToScoreAuthority = "Deliver Coin Tokens to your Shrines (2 Authority each). Remove enemy buildings, tokens and special units.",
                    LoreDescription = "Inspired by the religious works of the Ananke, the Tanukis scamper across the Continent gathering the Ananke's scattered coins and dropping them off at shrines while leaving traps for travelers.",
                    SetupInstructions = "1. Place 3 Tanuki units and 1 Shrine on a forest tile with no enemy units. 2. After ALL other players finish setup, place 12 Coin Tokens on 12 different forest tiles with no enemy units (each at least 2 tiles away from every other Coin Token).",
                    PrimaryColor = Palette.FromHex("#A8792C"),
                    SecondaryColor = Palette.FromHex("#F2C94C"),
                    PrimaryBuilding = BuildingType.Shrine,
                    PrimaryBuildingCost = new ResourceCost(wood: 2),
                    BuildingRules = "Shrine: Craft (2 Wood) on a forest or Fallen Leaves tile with a Tanuki or Band. Scoring Coins: Whenever a Coin Token is moved or placed onto a tile with a Shrine, return the Coin Token to supply and gain 2 Authority!",
                    SpeciesTraits = new List<string>
                    {
                        "The Coin Hunt (compulsory): A Coin Token on the same tile as your Tanukis/Band is held and always moves with them; they can carry up to 2 Coins (a tile still holds at most 2 tokens).",
                        "Leaf Runner: your Move actions can go 1 extra tile when they end on a Fallen Leaves tile.",
                        "Quick Feet: When >= 1 HP damage is dealt to your Tanukis on a tile with >= 2 Tanukis, after 0 HP units are removed, move remaining units to adjacent tiles with no enemy units and place 1 Fallen Leaves tile on one forest tile a unit moved to.",
                        "Rustling in the Trees: Once during your Draw phase, place 1 Fallen Leaves tile on a forest tile with or adjacent to a Shrine (with no enemy units)."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Removing a flipped Fallen Leaves tile (isolated): the remover gains 1 Wood, no Authority (returns to forest tile, leaf shuffled back into stack).",
                        "Removing a Shrine: 1 Authority.",
                        "Removing A Band of Troublemakers: 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Trouble Making Tanukis Faction Board", "18 Tanuki Unit Pieces & Card", "1 A Band of Troublemakers Special Unit Piece & Card",
                        "3 Shrine Buildings", "20 Coin Tokens", "8 Prank Leaf Fallen Leaves Tiles", "5 Trap Leaf Fallen Leaves Tiles"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "tanuki", Name = "Tanuki", Faction = FactionId.TroubleMakingTanukis, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 1, BaseMaxHP = 1, SupplyCount = 18, SummoningCondition = "Recruit at Shrines", AbilitiesText = "Running Amok: Once during your Main phase, place 1 Fallen Leaves tile from the shuffled 13-tile stack onto a forest tile with a Tanuki unit. Any player can Loot a Fallen Leaves tile for 1 Wood." },
                        new UnitDef { Id = "band_of_troublemakers", Name = "A Band of Troublemakers", Faction = FactionId.TroubleMakingTanukis, IsSpecialUnit = true, BaseAttack = 2, BaseDefense = 2, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "During Main phase, if 3+ Tanukis are on the same tile, remove 3 Tanukis and discard 1 Gold to place the Band. You can only have one Band at a time.", AbilitiesText = "Belly Drums (Draw phase): Place a Fallen Leaves tile on up to 2 forest tiles with a Coin Token and no enemy units. No Honor Amongst Troublemakers: On 0 HP, return to supply and place 1 Tanuki on up to 3 adjacent forest/Fallen Leaves tiles (remover gains 2 Authority)." }
                    }
                },

                // Faction 5: The Necro-mouser (Part 17 Faction 5)
                new FactionDef
                {
                    Id = FactionId.Necromouser,
                    Name = "The Necro-mouser",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 6,
                    ShortSummary = "Late game faction that harvests the dead from every battle, hoards resources, and spends fallen units to summon powerful undead horrors.",
                    HowToScoreAuthority = "Undead Shackles: 1 Authority per 2 Dead in the Cauldron each End phase (max 2). Summoning special units: The Bloat 1, The Vanguard 3, The Brute 4. Ring of Favor explosions: 2 more. Remove enemy buildings, tokens and special units.",
                    LoreDescription = "The incarnate of death came to the Continent long before the other factions did. Where armies clash he seeds his creeping miasma, and the fallen of every banner rise again in his service.",
                    SetupInstructions = "At the very end of setup (after any Tanuki Coin Tokens), place 3 Miasma Tiles facedown on any tiles, ignoring other factions' setup restrictions (not directly adjacent or on the same tile). Start with 3 Skeleton Units on the Cauldron ('Dead' units).",
                    PrimaryColor = Palette.FromHex("#7B46B8"),
                    SecondaryColor = Palette.FromHex("#D6C9E8"),
                    PrimaryBuilding = BuildingType.None,
                    PrimaryBuildingCost = new ResourceCost(),
                    BuildingRules = "The Necromouser has no building and cannot use the recruitment main phase action or the comeback mechanic.",
                    SpeciesTraits = new List<string>
                    {
                        "Hoarder: Keep up to 8 resource cards in your resource pile during your end phase instead of 6.",
                        "Plague: When you spend Gold on equipment, place 1 Miasma Tile directly adjacent to an existing Miasma Tile.",
                        "Undead Shackles: Once during your end phase, gain 1 Authority per 2 Dead in the Cauldron (cap 2 Authority per turn).",
                        "Cauldron: When a unit is removed on a Miasma Tile or an adjacent tile, place 1 removed unit from each faction onto the Cauldron as 'Dead'.",
                        "Arise: Twice per turn, spend 1 Dead from the Cauldron to summon 1 Skeleton Unit on a Miasma Tile."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Removing a Necromouser special unit: Authority equal to its summoning cost (1/3/4) + free loot 1 equipment material (adds 1 Skeleton to Cauldron).",
                        "Removing an isolated Miasma Tile: Attacker gains 1 Authority, Necromouser adds 1 Skeleton from supply to Cauldron."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Necromouser Faction Board", "1 Necromouser Special Unit Piece & Card", "3 Bloat Special Unit Pieces & Card",
                        "3 Vanguard Special Unit Pieces & Card", "3 Brute Special Unit Pieces & Card", "18 Skeleton Unit Pieces & Card",
                        "6 Miasma Tiles", "1 Cauldron Card", "6 Sword, 3 Helmet, 3 Shield, 3 Boots, 3 Locket, 3 Ring Equipment Cards"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "skeleton", Name = "Skeleton", Faction = FactionId.Necromouser, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 18, SummoningCondition = "Arise trait or Miasma awakening", AbilitiesText = "Spread the Bad News: Once during your main phase, remove 1 Skeleton Unit from the board (does not go to Cauldron) to place a Miasma Tile on its tile." },
                        new UnitDef { Id = "necromouser", Name = "The Necromouser", Faction = FactionId.Necromouser, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 1, SummoningCondition = "First time a unit is removed on/adjacent to a Miasma Tile: summon the Necromouser + 1 Skeleton on that tile (Miasma Tiles are not flipped).", AbilitiesText = "Necromancy: Summon 1 special unit/turn. Cowardice: Cannot be attacked while friendly units share his tile; when isolated and removed, lose 3 Authority, move 2 tiles away, place 3 adjacent Miasma Tiles." },
                        new UnitDef { Id = "the_bloat", Name = "The Bloat", Faction = FactionId.Necromouser, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 2, BaseMaxHP = 1, SupplyCount = 3, SummoningCondition = "Remove 1 Dead from Cauldron -> Gain 1 Authority, summon on Necromouser's tile.", AbilitiesText = "Necrosis (Draw phase): Explode to deal 2HP damage to all enemy units on tile (bypasses 1HP cap). If removed before exploding, remover gains 1 Authority + 1 resource card." },
                        new UnitDef { Id = "the_vanguard", Name = "The Vanguard", Faction = FactionId.Necromouser, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 3, SummoningCondition = "Remove 3 Dead from Cauldron -> Gain 3 Authority, summon on Necromouser's tile.", AbilitiesText = "Devotion: If Necromouser is defending on an adjacent tile, Vanguard and all friendly units on Vanguard's tile join the defending battle." },
                        new UnitDef { Id = "the_brute", Name = "The Brute", Faction = FactionId.Necromouser, IsSpecialUnit = true, BaseAttack = 2, BaseDefense = 0, BaseMaxHP = 2, SupplyCount = 3, SummoningCondition = "Remove 4 Dead from Cauldron -> Gain 4 Authority, summon on Necromouser's tile.", AbilitiesText = "Can equip up to 2 Anankian Swords (+1 ATK each) and 1 Leather Boots (+1 Move)." }
                    },
                    Equipment = new List<EquipmentDef>
                    {
                        new EquipmentDef { Id = "locket_of_memory", Name = "Locket of Memory", Faction = FactionId.Necromouser, TargetUnitId = "the_bloat", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(stone: 2), EffectText = "Bloat explosion gains a 1-tile radius." },
                        new EquipmentDef { Id = "ring_of_favor", Name = "Ring of Favor", Faction = FactionId.Necromouser, TargetUnitId = "the_bloat", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(stone: 2), EffectText = "When Bloat explodes, gain 2 more Authority and place a Miasma Tile on that tile." },
                        new EquipmentDef { Id = "anankian_helmet", Name = "Anankian Helmet", Faction = FactionId.Necromouser, TargetUnitId = "the_vanguard", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(wood: 1, stone: 1), BonusDefense = 2, EffectText = "+2 Defense to equipped Vanguard." },
                        new EquipmentDef { Id = "anankian_shield", Name = "Anankian Shield", Faction = FactionId.Necromouser, TargetUnitId = "the_vanguard", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(wood: 1, stone: 1, gold: 1), BonusDefense = 1, EffectText = "+1 Defense to equipped Vanguard and any other friendly units on its tile." },
                        new EquipmentDef { Id = "anankian_sword", Name = "Anankian Sword", Faction = FactionId.Necromouser, TargetUnitId = "the_brute", MaxPerUnit = 2, SupplyCount = 6, Cost = new ResourceCost(wood: 1, stone: 1, gold: 1), BonusAttack = 1, EffectText = "+1 Attack to equipped Brute (up to 2 Swords per Brute)." },
                        new EquipmentDef { Id = "leather_boots", Name = "Leather Boots", Faction = FactionId.Necromouser, TargetUnitId = "the_brute", MaxPerUnit = 1, SupplyCount = 3, Cost = new ResourceCost(wood: 2), BonusMove = 1, EffectText = "+1 tile movement per movement action to equipped Brute." }
                    }
                },

                // Faction 6: Firestarting Squirrels (Part 17 Faction 6)
                new FactionDef
                {
                    Id = FactionId.FirestartingSquirrels,
                    Name = "Firestarting Squirrels",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 6,
                    ShortSummary = "Aggressive early game faction that burns down the forest and profits from the chaos, forcing enemies off tiles with fire and disrupting their resources.",
                    HowToScoreAuthority = "Flames of War: 1 Authority per 2 HP of Fire Tile damage and per 2 enemy units moved by fire (max 4 per End phase). Forest Fire: 1 per Fire Tile next to enemy pieces (max 2). Burning Gold (Disruption, Burn the Deck): 1 each. Remove enemy buildings, tokens and special units.",
                    LoreDescription = "The Squirrels use Acorn Firestarters to create Fire Tiles that damage enemy units, drive enemies out, and burn the Continent's resources.",
                    SetupInstructions = "Place 1 Squirrel Unit each on 3 adjacent forest tiles without any enemy pieces. Then place 1 Bunker on one of those forest tiles. Place 2 Charcoal resource cards in your resource pile and 1 crafted Acorn Firestarter card faceup on your table.",
                    PrimaryColor = Palette.FromHex("#D62E1C"),
                    SecondaryColor = Palette.FromHex("#3B322C"),
                    PrimaryBuilding = BuildingType.Bunker,
                    PrimaryBuildingCost = new ResourceCost(stone: 2),
                    BuildingRules = "Bunker: Craft (2 Stone) on a tile with a Squirrel unit. Squirrel units on a tile with a Bunker cannot take damage from or be moved by a Fire Tile.",
                    SpeciesTraits = new List<string>
                    {
                        "Unquenchable Spirit: Once per Main phase, a Squirrel on a Fire Tile can use a Main Phase action to loot the Fire Tile for 1 Charcoal card. Only the Squirrels can loot Fire Tiles, and only for Charcoal.",
                        "Flames of War: End of Fire Tile resolutions -> +1 Authority per 2HP damage dealt to enemies, +1 Authority per 2 enemies moved by Fire (max 4/turn).",
                        "Forest Fire: During End phase -> +1 Authority for every Fire Tile adjacent to another faction's pieces (cap 2).",
                        "Ignite: During End phase, after all Fire Tiles resolve -> Burn 1 card from Hand to relocate 1 Fire Tile to a forest tile adjacent to an enemy unit (or a stream or ruin tile with a Tar Pit Token).",
                        "Disruption: Once per Main phase -> Burn 2 cards of your choice in your Resource Pile to burn 1 Excavation card and 1 card from each player's Resource Pile (+1 Authority if Gold or Gold-disassemble relic burned)."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Removing a Bunker or Tar Pit Token: 1 Authority.",
                        "Removing a Fire Tile (during Main phase with unit on it, discard 2 Stone): 1 Authority.",
                        "Removing the Pyromaniac: 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Firestarting Squirrels Faction Board", "15 Squirrel Unit Pieces & Card", "1 Pyromaniac Special Unit Piece & Card",
                        "6 Fire Tiles", "3 Tar Pit Tokens", "3 Bunker Buildings", "6 Charcoal Resource Cards", "3 Acorn Firestarter Cards"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "squirrel", Name = "Squirrel", Faction = FactionId.FirestartingSquirrels, IsSpecialUnit = false, BaseAttack = 1, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 15, SummoningCondition = "Recruit at Bunkers", AbilitiesText = "Tar Pit Token (Excavation): Discard 2 Charcoal -> place Tar Pit on/adjacent to Squirrel (+1HP Fire damage bypassing cap; costs enemies 2 actions to leave). Acorn Firestarter (Excavation phase, uses your Craft action): Discard 1 Charcoal + 1 Wood to craft; it sits with your crafted cards; discard 1 crafted Firestarter to place a Fire Tile on a Squirrel's forest tile." },
                        new UnitDef { Id = "pyromaniac", Name = "Pyromaniac", Faction = FactionId.FirestartingSquirrels, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 2, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "Once/game (Main phase): Burn 1 Wood, 1 Stone, 1 Gold, 1 Charcoal -> Summon on a Fire Tile.", AbilitiesText = "Affinity: Immune to Fire Tiles. Flameweaver: Burn 2 hand cards -> move 1 Fire Tile to adjacent forest tile. Pyromancy: Burn 1W+1G -> place Fire Tile on Pyromaniac's tile. Oil Spill: Burn 1 Charcoal -> place Tar Pit adjacent to Fire/Tar Pit." }
                    }
                },

                // Faction 7: The Capybara Caravan (Part 17 Faction 7)
                new FactionDef
                {
                    Id = FactionId.CapybaraCaravan,
                    Name = "The Capybara Caravan",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 5,
                    ShortSummary = "Peaceful economic faction that expands the stream network and profits from enemy movements through hospitality and trade.",
                    HowToScoreAuthority = "Customary Greeting: 1 Authority per player with a Guest unit. Traded Goods: 1 Authority each time someone buys from your Trader. Hospitality: 1 Authority when a Guest draws a relic. Remove enemy buildings, tokens and special units.",
                    LoreDescription = "At the bottom of the stream are remnants of the Ananke, and the capybaras dredge that memory up one relic at a time and sell those relics using their merchants.",
                    SetupInstructions = "Special Setup (before any other player sets up): Place all 6 Stream Tiles one at a time onto forest tiles, each adjacent to another Stream Tile. Regular Setup: Place 3 Capybara units, the Capybara Trader, and 1 Wallow on a stream tile.",
                    PrimaryColor = Palette.FromHex("#68B5D6"),
                    SecondaryColor = Palette.FromHex("#5BC0BE"),
                    PrimaryBuilding = BuildingType.Wallow,
                    PrimaryBuildingCost = new ResourceCost(wood: 1, stone: 1),
                    BuildingRules = "Wallow: Craft (1 Wood, 1 Stone) on a stream tile with a Capybara unit, or on a forest tile adjacent to a stream tile with a Capybara unit.",
                    SpeciesTraits = new List<string>
                    {
                        "Watering Hole: An enemy unit becomes a Guest when it enters a tile with a Wallow and a Capybara unit, when a Wallow is built on its tile, when it is placed after buying from the Capybara Trader, or while it is on the Riverkeeper's tile. Each player with a Guest draws 1 resource card in their End phase. A Guest stops being one when it leaves its tile, attacks the Capybara Trader or the Riverkeeper, or destroys a Wallow.",
                        "Customary Greeting: Once during your End phase, gain 1 Authority for each player who has a Guest unit."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Buying a Traded Good Card from the Capybara Trader: 1 Authority to Buyer and 1 Authority to Capybara player.",
                        "Removing the Capybara Trader or a Wallow: 1 Authority.",
                        "Removing the Riverkeeper: 2 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Capybara Caravan Faction Board", "5 Wallows", "Traded Goods Deck (8 cards)",
                        "1 Riverkeeper Special Unit Piece & Card", "1 Capybara Trader Special Unit Piece & Card",
                        "20 Capybara Unit Pieces & Card", "6 Stream Tiles"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "capybara", Name = "Capybara", Faction = FactionId.CapybaraCaravan, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 2, SupplyCount = 20, SummoningCondition = "Recruit at Wallows", AbilitiesText = "Forage: Once per Main phase on a stream tile, draw 1 relic deck card. Semiaquatic: For 1 Move action, move across 2 stream tiles." },
                        new UnitDef { Id = "capybara_trader", Name = "Capybara Trader", Faction = FactionId.CapybaraCaravan, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Starts on board; if off board during Excavation phase, discard 2 relic cards from Hand to place at a Wallow.", AbilitiesText = "Traveling Caravan: Players with a unit on the Trader's tile can buy from the 3-card Traded Goods Stall (+1 Auth to buyer & Capybara, buyer places 1 basic unit as Guest at a Wallow). Peddling: 1 free Move action each turn (up to 2 land or 3 stream tiles)." },
                        new UnitDef { Id = "riverkeeper", Name = "The Riverkeeper", Faction = FactionId.CapybaraCaravan, IsSpecialUnit = true, BaseAttack = 2, BaseDefense = 4, BaseMaxHP = 3, SupplyCount = 1, SummoningCondition = "During Main phase with 2+ Wallows on board: Discard 1 Stone + 2 Gold -> Summon at a Wallow.", AbilitiesText = "Don't Wallow: all other Capybara Caravan units within 2 tiles of the Riverkeeper gain +1 Attack. Hospitality: Once per Main phase, a Guest on its tile draws 1 relic -> Capybara gains 1 Authority. Rejuvenate: During your Main phase, discard 2 Gold to heal the Riverkeeper 1 HP (repeatable)." }
                    },
                    SubDeckCards = BuildCapybaraTradedGoodsDeck8()
                },

                // Faction 8: The Briared Flower (Part 17 Faction 8)
                new FactionDef
                {
                    Id = FactionId.BriaredFlower,
                    Name = "The Briared Flower",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 7,
                    ShortSummary = "Explosive special unit faction that scores Authority by hurling thorns across the board and scattering seeds in its wake.",
                    HowToScoreAuthority = "Thistle Barrage: 1 Authority per 2 HP of damage. Furious Lashing: 1 Authority if it removes the unit. Bursting Into Bloom: 1 Authority if it removes an enemy unit. Pollination: 1 Authority with Thorned Seeds on 4+ tiles (2 with 7+). Remove enemy buildings, tokens and special units.",
                    LoreDescription = "Born with an instinctual hatred of other creatures, the Blooming Briar hurls its spines across the Continent so that its seedlings might take root, and when stripped bare, thrashes out one last fury.",
                    SetupInstructions = "Place the Blooming Briar with 6 Thorns, 2 Thorned Seeds, and 1 Prickly Thicket on a forest tile with no enemy pieces within 4 tiles. Other factions cannot place their pieces within 4 tiles of any Briared Flower pieces during setup.",
                    PrimaryColor = Palette.FromHex("#C2295A"),
                    SecondaryColor = Palette.FromHex("#5E8C31"),
                    PrimaryBuilding = BuildingType.PricklyThicket,
                    PrimaryBuildingCost = new ResourceCost(wood: 2),
                    BuildingRules = "Prickly Thicket: Craft (2 Wood) on a forest tile with a Thorned Seed or Blooming Briar. Effect: Once per Main phase, if Blooming Briar is on a Prickly Thicket tile, remove the Prickly Thicket to give Blooming Briar +1 Thorn.",
                    SpeciesTraits = new List<string>
                    {
                        "In Half Bloom: During Main phase, remove 2 resources from Resource Pile and 3 Thorned Seeds from the board to place 1 Blooming Briar with 2 Thorns at a Prickly Thicket.",
                        "Branching Out: Thistle Barrage gains +1 tile range while the Blooming Briar is on one of your Prickly Thickets (3 tiles -> 4 tiles).",
                        "Rooted In Place: During your turn, the Blooming Briar can only move 1 tile for the entire turn. It cannot move or be moved onto stream tiles.",
                        "Pollination: During your End phase, gain 1 Authority if you have Thorned Seeds on 4+ different tiles (gain 2 Authority if on 7+ different tiles)."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Removing a Prickly Thicket: 1 Authority.",
                        "Removing the Blooming Briar: 2 Authority (triggers Propagation: 1HP damage + places 1 Thorned Seed on adjacent forest tile)."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Briared Flower Faction Board", "1 Thorn Meter",
                        "1 Blooming Briar Special Unit Piece & Card", "20 Thorned Seed Unit Pieces & Card",
                        "4 Prickly Thicket Buildings", "6 Thorn Tokens (markers for the Blooming Briar's Thorned Body)"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "thorned_seed", Name = "Thorned Seed", Faction = FactionId.BriaredFlower, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 20, SummoningCondition = "Recruit at Prickly Thickets or spawned via Thistle Barrage / Propagation", AbilitiesText = "Bursting Into Bloom: Once during your Main phase, remove 1 Thorned Seed from the board to deal 1HP damage to 1 enemy unit on its tile, then gain 1 Authority if you removed an enemy unit." },
                        new UnitDef { Id = "blooming_briar", Name = "Blooming Briar", Faction = FactionId.BriaredFlower, IsSpecialUnit = true, BaseAttack = 6, BaseDefense = 0, BaseMaxHP = 1, SupplyCount = 1, SummoningCondition = "Starts on board with 6 Thorns; respawns via In Half Bloom.", AbilitiesText = "With 1-6 Thorns: Thistle Barrage (up to 2x/Main phase, 3 tile range, 4 on a Prickly Thicket, 1 Thorn + 1 Res of your choice per unit -> 1HP each & spawn Seed; +1 Auth per 2HP dealt; or 1 Thorn to remove isolated enemy buildings/tokens within 3 tiles), Thorn Armor (1 Thorn blocks 1HP), Sun Eater (2 Seeds + 3 Res -> +2 Thorns). At 0 Thorns (Frenzied Bloom): Withered Blossom (+3 ATK, 2HP bypass), Furious Lashing (1HP to 1 enemy unit within 2 tiles; +1 Auth if it is removed), Propagation." }
                    }
                },

                // Faction 9: The Goat Cabal (Part 17 Faction 9)
                new FactionDef
                {
                    Id = FactionId.GoatCabal,
                    Name = "The Goat Cabal",
                    Tier = FactionDifficultyTier.RecommendedForNewPlayers,
                    DifficultyRatingOutOf10 = 9,
                    ShortSummary = "Occult faction that sacrifices its own flock, treasure and Authority to summon demons that reshape the Continent, so long as it keeps paying.",
                    HowToScoreAuthority = "Starts with 5 Authority. Grimoire cards, The Midian's A Gift, and The Lord of War's Greed. Remove enemy buildings, tokens and special units.",
                    LoreDescription = "When the Ananke vanished, an older force whispered in the shadows. The Cabal traded their souls for its favor, and now they trade everything else.",
                    SetupInstructions = "Place 4 Goat units and a Summoning Circle on a forest or ruin tile 2 tiles away from all enemy pieces. Place 1 Goat on the Purgatory card. Add 1 Gold to your resource pile and start with 5 Authority. Other factions cannot place pieces within 2 tiles of a Goat unit during setup.",
                    PrimaryColor = Palette.FromHex("#6B5788"),
                    SecondaryColor = Palette.FromHex("#D2462F"),
                    PrimaryBuilding = BuildingType.SummoningCircle,
                    PrimaryBuildingCost = new ResourceCost(wood: 2, stone: 2),
                    BuildingRules = "Summoning Circle: Once during your Excavation phase, on a tile with 5 or more Goat units, Purge 1 of those Goats and discard 2 Stone and 2 Wood to craft a Summoning Circle there. Recruit summons 1 Goat at each Summoning Circle.",
                    SpeciesTraits = new List<string>
                    {
                        "Purgatory: Purged Goats go onto the Purgatory card, an off-board spot only you use.",
                        "Secrets of the Ancients: In your Draw phase, after you draw, mill the top Grimoire card and apply it. If you can't pay its cost, the whole card fizzles.",
                        "Kleptomancy: Keep up to 10 Gold and 6 other resources at the end of your turn.",
                        "Suspicion: Enemies can only move onto a Summoning Circle tile with at least as many units as there are Goats there.",
                        "Sparagmos: Once per Main phase, Purge 1 Goat from a Summoning Circle tile."
                    },
                    EnemyAuthorityScoring = new List<string>
                    {
                        "Removing a demon: 2 Authority.",
                        "Removing a Summoning Circle, Initiation Rite Token, or Effigy Token: 1 Authority."
                    },
                    MaterialsManifest = new List<string>
                    {
                        "1 Goat Cabal Faction Board", "1 How to Use Demon Special Unit Cards Board", "1 Purgatory Card",
                        "20 Goat Unit Pieces", "5 Demon Special Unit Pieces & Cards (Faust, The Midian, Serpentine Sovereign, The Lord of War, The Horned Beast)",
                        "2 Initiation Rite Tokens", "3 Effigy Tokens", "3 Summoning Circle Buildings", "14 Card Grimoire Deck"
                    },
                    Units = new List<UnitDef>
                    {
                        new UnitDef { Id = "goat", Name = "Goat", Faction = FactionId.GoatCabal, IsSpecialUnit = false, BaseAttack = 0, BaseDefense = 1, BaseMaxHP = 1, SupplyCount = 20, SummoningCondition = "Recruit at Summoning Circles", AbilitiesText = "Invocation: Purge 1 Goat and discard 1 Wood, 1 Stone, 1 Gold to place an Initiation Rite Token on a Summoning Circle. Canto Inferno: once per Main phase, remove an Initiation Rite Token and pay a demon's cost to summon it there. Effigy: once per Excavation phase, return 1 Purged to supply to place an Effigy Token on a Summoning Circle." },
                        new UnitDef { Id = "faust", Name = "Faust", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 1, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Canto Inferno: 2 Purged to supply, 1 Wood and 1 Stone.", AbilitiesText = "In the Pursuit of Knowledge: when summoned, mill 2 Grimoire cards. Bargain: once per turn, Purge 1 Goat on Faust's tile to mill 1 Grimoire card. Binding: 2 Purged on Purgatory in your End phase. Unbound: lose 3 Gold." },
                        new UnitDef { Id = "the_midian", Name = "The Midian", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 0, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Canto Inferno: 2 Purged to supply and 2 Gold.", AbilitiesText = "A Gift: when summoned, gain 1 Authority per Summoning Circle. All Turns to Gold: once per turn, gain 2 Gold; another player draws 1 resource card. Binding: lose 2 Authority in your End phase. Unbound: roll a die (1: lose 2 random resources, 2-4: lose 2 Authority, 5-6: gain 2 Gold)." },
                        new UnitDef { Id = "serpentine_sovereign", Name = "Serpentine Sovereign", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 1, BaseDefense = 2, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Canto Inferno: 3 Purged to supply.", AbilitiesText = "Royal Decree: when summoned, mark a tile up to 4 tiles away; at the start of your next Draw phase everything on it is removed. Binding: lose 2 Authority during your turn. Unbound: lose 2 Gold or 3 resources (else return 2 Purged to supply)." },
                        new UnitDef { Id = "lord_of_war", Name = "The Lord of War", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 3, BaseDefense = 1, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Canto Inferno: 3 Purged to supply.", AbilitiesText = "Fear: when summoned, all non-Goat units on its tile move to an adjacent tile. Onslaught: 3 free battles per Main phase. Greed: in each of its battles roll a die (even: +1 Authority, odd: lose 1 Gold). Binding: no enemy pieces on its tile in your End phase. Unbound: 1 HP damage to two of your units." },
                        new UnitDef { Id = "horned_beast", Name = "The Horned Beast", Faction = FactionId.GoatCabal, IsSpecialUnit = true, BaseAttack = 2, BaseDefense = 3, BaseMaxHP = 2, SupplyCount = 1, SummoningCondition = "Canto Inferno: 5 Purged to supply.", AbilitiesText = "Subservience: when summoned, choose a restriction on every other player for their next two turns. Binding: each End phase discard 2 Gold, 1 random Hand card, lose 1 Authority and return 1 Purged, and have 3 Summoning Circles on the board. Unbound: lose 1 Main phase action next turn." }
                    },
                    SubDeckCards = BuildGoatGrimoire14()
                }
            };
        }
    }
}
