using System.Collections.Generic;
using System.Linq;
using CrittersOfCalamity.Model;
using UnityEngine;

namespace CrittersOfCalamity.Engine
{
    /// <summary>
    /// A decision the game is waiting on: pick one of the buttons, click one of the highlighted tiles, or skip.
    /// While a choice is pending nothing else can happen.
    /// </summary>
    public class PendingChoiceState
    {
        public string Kind;
        public int PlayerIndex;
        public string Title;
        public string Prompt;
        public List<string> Options = new List<string>();
        public List<bool> OptionEnabled = new List<bool>();
        public List<HexCoord> Tiles = new List<HexCoord>();
        public bool CanSkip;
        public string SkipLabel = "Skip";

        // working data for the choice
        public HexCoord Coord;
        public int Number;
        public List<RelicCardDef> Cards = new List<RelicCardDef>();
        public List<string> UnitIds = new List<string>();
    }

    public partial class CrittersGameEngine
    {
        public PendingChoiceState PendingChoice;
        public bool IsChoicePending => PendingChoice != null;

        /// <summary>Choice answers: option >= 0 picks a button; ChooseTile with a tile; ChooseSkip to skip.</summary>
        public const int ChooseTile = -1;
        public const int ChooseSkip = -2;

        private bool _endPhaseIgniteAsked;

        /// <summary>Choices waiting their turn (one opened while a battle or another choice was in progress).</summary>
        public List<PendingChoiceState> QueuedChoices = new List<PendingChoiceState>();

        private PendingChoiceState NewChoice(string kind, int seat, string title, string prompt)
        {
            var c = new PendingChoiceState { Kind = kind, PlayerIndex = seat, Title = title, Prompt = prompt };
            if (PendingChoice != null || PendingBattle != null) QueuedChoices.Add(c);
            else PendingChoice = c;
            return c;
        }

        private void OpenNextQueuedChoice()
        {
            if (PendingChoice != null || PendingBattle != null || QueuedChoices.Count == 0) return;
            PendingChoice = QueuedChoices[0];
            QueuedChoices.RemoveAt(0);
        }

        // Necro-mouser Miasma: each removal event puts at most 1 unit per faction in the Cauldron
        private readonly HashSet<int> _cauldronCountedThisEvent = new HashSet<int>();

        /// <summary>Starts a new "removal event" (one battle, one ability, one command) for the Cauldron rule.</summary>
        public void BeginRemovalEvent() => _cauldronCountedThisEvent.Clear();

        private static void AddOption(PendingChoiceState c, string label, bool enabled = true)
        {
            c.Options.Add(label);
            c.OptionEnabled.Add(enabled);
        }

        public bool CanResolveChoice(int seat, int option, HexCoord tile, out string reason)
        {
            reason = null;
            var c = PendingChoice;
            if (c == null) { reason = "There's nothing to decide right now."; return false; }
            if (seat != c.PlayerIndex) { reason = $"Waiting on {Players[c.PlayerIndex].DisplayName} to decide."; return false; }
            if (option == ChooseSkip) { if (c.CanSkip) return true; reason = "You have to choose."; return false; }
            if (option == ChooseTile) { if (c.Tiles.Contains(tile)) return true; reason = "That tile isn't one of the choices."; return false; }
            if (option < 0 || option >= c.Options.Count) { reason = "That isn't one of the choices."; return false; }
            if (!c.OptionEnabled[option]) { reason = "That choice isn't available."; return false; }
            return true;
        }

        public bool ResolveChoice(int seat, int option, HexCoord tile)
        {
            if (!CanResolveChoice(seat, option, tile, out var why)) { if (why != null) Log(why); return false; }
            var c = PendingChoice;
            PendingChoice = null;
            var p = Players[c.PlayerIndex];
            bool skip = option == ChooseSkip;

            if (ResolveGoatChoice(c, p, option, tile, skip)) { OpenNextQueuedChoice(); OnStateChanged?.Invoke(); return true; }

            switch (c.Kind)
            {
                case "prophetic_vision":
                    if (skip)
                    {
                        for (int i = c.Cards.Count - 1; i >= 0; i--) RelicDeck.Add(c.Cards[i]); // back on top, same order
                        Log($"{p.DisplayName} looked at the top of the relic deck and left it as it was.");
                    }
                    else
                    {
                        var keep = c.Cards[option];
                        p.RelicHand.Add(keep);
                        foreach (var other in c.Cards.Where(x => x != keep)) RelicDeck.Insert(0, other); // bottom of the deck
                        Log($"{p.DisplayName} used Prophetic Vision: kept 1 card and put {c.Cards.Count - 1} on the bottom of the relic deck.");
                    }
                    FinishDrawPhaseDraw(p);
                    break;

                case "martyrdom":
                {
                    var t = Board[c.Coord];
                    int n = skip ? 0 : c.Number - option;
                    for (int i = 0; i < n; i++)
                    {
                        var pil = t.Units.FirstOrDefault(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == "pilgrim");
                        if (pil == null) break;
                        t.Units.Remove(pil);
                        p.PlatypusFaith = Mathf.Min(5, p.PlatypusFaith + 1);
                        AwardAuthority(p.PlayerIndex, 1, "Martyrdom (+1 Faith)");
                    }
                    if (n == 0) Log($"{p.DisplayName}'s Pilgrims chose to live another day.");
                    break;
                }

                case "statue":
                    if (!skip && Board.TryGetValue(tile, out var st))
                    {
                        st.HasStatueOfGoddessBuff = true;
                        Log($"Statue of the Goddess: {p.DisplayName}'s units at {tile} gain +1 Attack in their next battle.");
                    }
                    break;

                case "aqueduct":
                    if (option == 0)
                    {
                        RelicDiscardPile.Add(p.RelicHand[0]);
                        p.RelicHand.RemoveAt(0);
                        p.ResourcePile.Add(DrawRandomResourceCard());
                        p.ResourcePile.Add(DrawRandomResourceCard());
                        Log($"Marble Aqueduct: {p.DisplayName} discarded a card to draw 2 resource cards.");
                    }
                    else
                    {
                        int back = 0;
                        foreach (var want in new[] { ResourceType.Gold, ResourceType.Stone, ResourceType.Wood, ResourceType.Charcoal })
                            while (back < 2 && BurnedResources.Contains(want)) { BurnedResources.Remove(want); p.ResourcePile.Add(want); back++; }
                        Log($"Marble Aqueduct: {p.DisplayName} took {back} burned resource card(s) back.");
                    }
                    break;

                case "passage":
                {
                    var others = Players.Where(o => o.PlayerIndex != p.PlayerIndex).ToList();
                    var target = others[Mathf.Clamp(option, 0, others.Count - 1)];
                    int take = target.ResourcePile.Count >= 5 ? 2 : 1;
                    int took = 0;
                    for (int i = 0; i < take && target.ResourcePile.Count > 0; i++)
                    {
                        int idx = _rng.Next(target.ResourcePile.Count);
                        p.ResourcePile.Add(target.ResourcePile[idx]);
                        target.ResourcePile.RemoveAt(idx);
                        took++;
                    }
                    Log($"Underground Passage: {p.DisplayName} took {took} random resource card(s) from {target.DisplayName}.");
                    break;
                }

                case "hallowed":
                {
                    var t = Board[tile];
                    var owners = t.Units.Where(u => !u.Definition.IsSpecialUnit).Select(u => u.OwnerPlayerIndex).Distinct().ToList();
                    if (owners.Count == 1) RemoveHallowed(p, t, owners[0]);
                    else
                    {
                        var next = NewChoice("hallowed_owner", p.PlayerIndex, "Hallowed Ground", $"Whose basic unit at {tile} is removed?");
                        next.Coord = tile;
                        foreach (var s in owners)
                            AddOption(next, $"{Players[s].DisplayName}'s {t.Units.First(u => u.OwnerPlayerIndex == s && !u.Definition.IsSpecialUnit).Definition.Name}");
                        next.UnitIds = owners.Select(s => s.ToString()).ToList();
                    }
                    break;
                }

                case "hallowed_owner":
                    RemoveHallowed(p, Board[c.Coord], int.Parse(c.UnitIds[option]));
                    break;

                case "plague":
                    if (!skip && MiasmaTilesLeft(p) > 0)
                    {
                        PlaceOverlayTile(tile, p.NecromouserAwakened ? FactionOverlayTileType.Miasma_Revealed : FactionOverlayTileType.Miasma_Hidden, p.PlayerIndex);
                        Log($"Plague: {p.DisplayName} spread a Miasma Tile to {tile}.");
                    }
                    break;

                case "ignite_card":
                    if (skip) { ContinueEndPhase(); break; }
                    c.Cards = new List<RelicCardDef> { p.RelicHand[option] };
                    {
                        var next = NewChoice("ignite_remove", p.PlayerIndex, "Ignite", "Click the Fire Tile to remove.");
                        next.Cards = c.Cards;
                        next.Tiles = Board.Values.Where(t => t.Overlay == FactionOverlayTileType.Fire && t.OverlayOwnerPlayerIndex == p.PlayerIndex).Select(t => t.Coord).ToList();
                        next.CanSkip = true; next.SkipLabel = "Don't Ignite";
                    }
                    break;

                case "ignite_remove":
                {
                    if (skip) { ContinueEndPhase(); break; }
                    var legal = IgnitePlacementTiles(p, tile);
                    if (legal.Count == 0) { Log("Ignite: no legal tile for the new Fire Tile, so nothing happened."); ContinueEndPhase(); break; }
                    var next = NewChoice("ignite_place", p.PlayerIndex, "Ignite", "Click the forest tile for the new Fire Tile (next to an enemy unit).");
                    next.Cards = c.Cards;
                    next.Coord = tile;
                    next.Tiles = legal;
                    break;
                }

                case "necro_loot":
                {
                    if (skip) break;
                    var r = (ResourceType)int.Parse(c.UnitIds[option]);
                    GainSpecificResourceFromDeck(p, r);
                    var necro = Players[c.Number];
                    necro.NecromouserCauldronDeadCount++;
                    Log($"{p.DisplayName} looted 1 {r} from the fallen unit's equipment; {necro.DisplayName} adds a Skeleton to the Cauldron.");
                    break;
                }

                case "trader_visit":
                    if (!skip && option >= 0 && option < TradedGoodsStall.Count && CanAfford(p, TradedGoodsStall[option].Cost))
                        BuyTradedGood(p, option);
                    else Log($"{p.DisplayName} didn't buy from the Capybara Trader.");
                    break;

                case "guest_wallow":
                {
                    var basic = p.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
                    if (basic == null) break;
                    var spawned = SpawnUnit(tile, basic.Id, p, 1);
                    foreach (var u in spawned) u.IsGuest = true;
                    Log(spawned.Count > 0 ? $"{p.DisplayName} placed a {basic.Name} as a guest at the Wallow {tile}." : $"{p.DisplayName} had no {basic.Name} to place as a guest.");
                    break;
                }

                case "ignite_place":
                {
                    var card = c.Cards[0];
                    if (p.RelicHand.Remove(card)) BurnedRelics.Add(card);
                    var old = Board[c.Coord];
                    old.Overlay = FactionOverlayTileType.None;
                    old.OverlayOwnerPlayerIndex = -1;
                    PlaceFireTile(Board[tile], p.PlayerIndex);
                    Log($"Ignite: {p.DisplayName} burned {card.Name} and moved a Fire Tile from {c.Coord} to {tile}.");
                    ContinueEndPhase();
                    break;
                }
            }

            OpenNextQueuedChoice();
            OnStateChanged?.Invoke();
            return true;
        }

        private void RemoveHallowed(PlayerState p, HexTileState t, int ownerSeat)
        {
            var victim = t.Units.FirstOrDefault(u => u.OwnerPlayerIndex == ownerSeat && !u.Definition.IsSpecialUnit);
            if (victim == null) return;
            RemoveUnitFromBoard(t, victim, p.PlayerIndex);
            Log($"Hallowed Ground removed {Players[ownerSeat].DisplayName}'s {victim.Definition.Name} at {t.Coord}.");
        }

        // ------------------------------------------------------------------
        // Choice openers
        // ------------------------------------------------------------------

        /// <summary>Part 16 "When Crafted" effects that need a choice. Returns true when a choice was opened.</summary>
        private bool OfferCraftedRelicChoice(PlayerState p, RelicCardDef card)
        {
            switch (card.Id)
            {
                case "statue_of_the_goddess":
                {
                    var tiles = Board.Values.Where(t => t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex)).Select(t => t.Coord).ToList();
                    if (tiles.Count == 0) return false;
                    var c = NewChoice("statue", p.PlayerIndex, "Statue of the Goddess", "Click a tile with your units. They gain +1 Attack in their next battle.");
                    c.Tiles = tiles;
                    return true;
                }
                case "marble_aqueduct":
                {
                    var c = NewChoice("aqueduct", p.PlayerIndex, "Marble Aqueduct", "Choose one effect.");
                    AddOption(c, "A: discard 1 card from your Hand to draw 2 resource cards", p.RelicHand.Count > 0);
                    AddOption(c, $"B: return 2 burned resource cards to your Resource Pile ({BurnedResources.Count} burned)", BurnedResources.Count > 0);
                    if (!c.OptionEnabled.Any(e => e)) { PendingChoice = null; Log("Marble Aqueduct had no effect (empty Hand, nothing burned)."); return false; }
                    return true;
                }
                case "underground_passage":
                {
                    var c = NewChoice("passage", p.PlayerIndex, "Underground Passage", "Choose another player. Take 1 random resource card from them (2 if they have 5 or more).");
                    foreach (var o in Players.Where(o => o.PlayerIndex != p.PlayerIndex))
                        AddOption(c, $"{o.DisplayName} ({o.ResourcePile.Count} resource cards)");
                    return true;
                }
                case "hallow_ground":
                {
                    var tiles = Board.Values.Where(t => t.Units.Any(u => !u.Definition.IsSpecialUnit)).Select(t => t.Coord).ToList();
                    if (tiles.Count == 0) return false;
                    var c = NewChoice("hallowed", p.PlayerIndex, "Hallowed Ground", "Click a tile to remove 1 basic unit from it.");
                    c.Tiles = tiles;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Prophetic Vision (Papacy, start of the Draw phase): look at the top 3 relic cards, keep 1, bottom the rest.</summary>
        private bool OfferPropheticVision(PlayerState p)
        {
            if (p.Faction.Id != FactionId.PlatypusPapacy) return false;
            var cards = new List<RelicCardDef>();
            for (int i = 0; i < 3; i++) { var d = DrawRelicCard(); if (d != null) cards.Add(d); }
            if (cards.Count == 0) return false;
            var c = NewChoice("prophetic_vision", p.PlayerIndex, "Prophetic Vision",
                "The top cards of the relic deck. Add 1 to your Hand; the others go to the bottom. (Then you draw as normal.)");
            c.Cards = cards;
            foreach (var card in cards) AddOption(c, $"Keep {card.Name}");
            c.CanSkip = true;
            c.SkipLabel = "Don't take one (leave them on top)";
            return true;
        }

        /// <summary>Martyrdom: Pilgrims that end a Move action on a tile with enemy units may be removed for 1 Faith and 1 Authority each.</summary>
        private void OfferMartyrdom(PlayerState p, HexTileState t)
        {
            int n = t.Units.Count(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == "pilgrim");
            if (n == 0) return;
            var c = NewChoice("martyrdom", p.PlayerIndex, "Martyrdom",
                $"{n} Pilgrim(s) reached enemy units at {t.Coord}. Each one you remove gives 1 Faith and 1 Authority.");
            c.Coord = t.Coord;
            c.Number = n;
            for (int k = n; k >= 1; k--) AddOption(c, $"Martyr {k} Pilgrim{(k > 1 ? "s" : "")}");
            c.CanSkip = true;
            c.SkipLabel = "Keep them";
        }

        /// <summary>Plague (Necro-mouser): after spending Gold on equipment, may place a Miasma Tile next to a Miasma Tile.</summary>
        private void OfferPlagueMiasma(PlayerState p)
        {
            if (MiasmaTilesLeft(p) <= 0) return;
            var tiles = Board.Values.Where(t => t.Overlay == FactionOverlayTileType.None && Neighbors(t).Any(IsMiasma)).Select(t => t.Coord).ToList();
            if (tiles.Count == 0) return;
            var c = NewChoice("plague", p.PlayerIndex, "Plague", "You spent Gold on equipment. Click a tile next to a Miasma Tile to spread Miasma there.");
            c.Tiles = tiles;
            c.CanSkip = true;
        }

        /// <summary>Ignite (Squirrels' End phase): legal tiles for the new Fire Tile after removing one.</summary>
        private List<HexCoord> IgnitePlacementTiles(PlayerState p, HexCoord removing)
        {
            // Ignite no longer needs a Squirrel nearby: any forest tile next to an enemy unit.
            return Board.Values.Where(t =>
                    (CanTakeForestOverlay(t) || t.Coord.Equals(removing)) && t.BaseTerrain == TileTerrainType.Forest &&
                    Neighbors(t).Any(n => HasEnemyUnits(n, p.PlayerIndex)))
                .Select(t => t.Coord).ToList();
        }

        /// <summary>Necro-mouser: loot 1 material used by the removed special unit's equipment (Necro adds a Skeleton to the Cauldron).</summary>
        private void OfferNecroEquipmentLoot(PlayerState looter, PlayerState necro, UnitInstance fallen)
        {
            var mats = new List<ResourceType>();
            foreach (var e in fallen.EquippedItems)
            {
                if (e.Cost.Wood > 0 && !mats.Contains(ResourceType.Wood)) mats.Add(ResourceType.Wood);
                if (e.Cost.Stone > 0 && !mats.Contains(ResourceType.Stone)) mats.Add(ResourceType.Stone);
                if (e.Cost.Gold > 0 && !mats.Contains(ResourceType.Gold)) mats.Add(ResourceType.Gold);
            }
            if (mats.Count == 0) return;
            var c = NewChoice("necro_loot", looter.PlayerIndex, "Loot the equipment",
                $"You removed {fallen.Definition.Name}. Take 1 of the materials from its equipment? ({necro.DisplayName} then adds a Skeleton to the Cauldron.)");
            c.Number = necro.PlayerIndex;
            foreach (var r in mats) { AddOption(c, $"Take 1 {r}"); c.UnitIds.Add(((int)r).ToString()); }
            c.CanSkip = true;
            c.SkipLabel = "Don't loot";
        }

        /// <summary>Traveling Caravan: the buyer places 1 basic unit from their supply on a Wallow of their choice as a guest.</summary>
        private void OfferGuestPlacement(PlayerState buyer, PlayerState capy)
        {
            var basic = buyer.Faction.Units.FirstOrDefault(u => !u.IsSpecialUnit);
            if (basic == null || UnitSupplyLeft(buyer, basic.Id) <= 0) return;
            var wallows = Board.Values.Where(t => t.Building != null && t.Building.Type == BuildingType.Wallow &&
                                                  t.Building.OwnerPlayerIndex == capy.PlayerIndex && CanAddUnitToTile(t, buyer.PlayerIndex))
                                      .Select(t => t.Coord).ToList();
            if (wallows.Count == 0) return;
            var c = NewChoice("guest_wallow", buyer.PlayerIndex, "Visit a Wallow",
                $"Place 1 {basic.Name} from your supply on a Wallow as a guest. Click a green Wallow.");
            c.Tiles = wallows;
        }

        /// <summary>A relic card's cost, with the Horse Hair Brush discount (1 less, Gold first) once the brush has been used.</summary>
        public ResourceCost RelicCostFor(PlayerState p, RelicCardDef card)
        {
            var c = new ResourceCost(card.Cost.Wood, card.Cost.Stone, card.Cost.Gold, card.Cost.AnyResource, card.Cost.Charcoal);
            if (!p.HorseHairBrushReady) return c;
            if (c.Gold > 0) c.Gold--;
            else if (c.Stone > 0) c.Stone--;
            else if (c.Wood > 0) c.Wood--;
            else if (c.AnyResource > 0) c.AnyResource--;
            return c;
        }

        private void UseHorseHairBrushIfDiscounted(PlayerState p, RelicCardDef card)
        {
            if (!p.HorseHairBrushReady) return;
            var full = card.Cost;
            if (full.Wood + full.Stone + full.Gold + full.AnyResource + full.Charcoal == 0) return;
            p.HorseHairBrushReady = false;
            Log($"{p.DisplayName}'s Horse Hair Brush discount: {card.Name} cost 1 less.");
        }

        private bool OfferIgnite(PlayerState p)
        {
            if (p.Faction.Id != FactionId.FirestartingSquirrels || p.RelicHand.Count == 0) return false;
            var fires = Board.Values.Where(t => t.Overlay == FactionOverlayTileType.Fire && t.OverlayOwnerPlayerIndex == p.PlayerIndex).ToList();
            if (!fires.Any(f => IgnitePlacementTiles(p, f.Coord).Any(x => !x.Equals(f.Coord)))) return false;
            var c = NewChoice("ignite_card", p.PlayerIndex, "Ignite",
                "End phase: burn 1 card from your Hand to move a Fire Tile onto a forest tile next to an enemy unit. Pick the card to burn.");
            foreach (var card in p.RelicHand) AddOption(c, $"Burn {card.Name}");
            c.CanSkip = true;
            c.SkipLabel = "Don't Ignite";
            return true;
        }
    }
}
