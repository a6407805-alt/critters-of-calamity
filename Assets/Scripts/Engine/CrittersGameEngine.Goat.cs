using System;
using System.Collections.Generic;
using System.Linq;
using CrittersOfCalamity.Model;
using UnityEngine;

namespace CrittersOfCalamity.Engine
{
    /// <summary>
    /// Part 17 Faction 9: The Goat Cabal. Purgatory, the Grimoire, Kleptomancy, Suspicion, the Goat abilities, and the
    /// five demons with their binding requirements and unbound penalties.
    ///
    /// Demon states: Unsummoned (card face down, never summoned), Bound (piece on the board), Unbound (it slipped its
    /// binding: the piece leaves the board and it can be rebound by paying only its Purged cost), Removed (an enemy or
    /// effect removed it from the board: it can't be summoned again).
    /// </summary>
    public partial class CrittersGameEngine
    {
        public const int DemonUnsummoned = 0, DemonBound = 1, DemonUnbound = 2, DemonRemoved = 3;
        public static readonly string[] DemonIds = { "faust", "the_midian", "serpentine_sovereign", "lord_of_war", "horned_beast" };

        public int DemonState(PlayerState p, string id) => p.DemonStates.TryGetValue(id, out var s) ? s : DemonUnsummoned;
        public static int DemonPurgedCost(string id) => id == "faust" || id == "the_midian" ? 2 : id == "horned_beast" ? 5 : 3;
        public static ResourceCost DemonResourceCost(string id) =>
            id == "faust" ? new ResourceCost(wood: 1, stone: 1) : id == "the_midian" ? new ResourceCost(gold: 2) : new ResourceCost();

        /// <summary>What summoning (or rebinding) this demon costs right now, or null if it can't be summoned.</summary>
        public string DemonCostLabel(PlayerState p, string id)
        {
            int st = DemonState(p, id);
            if (st == DemonBound || st == DemonRemoved) return null;
            var rc = DemonResourceCost(id);
            bool rebind = st == DemonUnbound;
            string res = rebind || (rc.Wood + rc.Stone + rc.Gold == 0) ? "" : $" + {rc}";
            return $"{DemonPurgedCost(id)} Purged{res}{(rebind ? " (rebind)" : "")}";
        }

        public bool CanAffordDemon(PlayerState p, string id)
        {
            int st = DemonState(p, id);
            if (st == DemonBound || st == DemonRemoved) return false;
            if (p.GoatPurged < DemonPurgedCost(id)) return false;
            return st == DemonUnbound || CanAfford(p, DemonResourceCost(id));
        }

        private string DemonName(PlayerState p, string id) => p.Faction.Units.First(u => u.Id == id).Name;

        private void LoseAuthority(PlayerState p, int n, string why)
        {
            if (n <= 0) return;
            int before = p.Authority;
            p.Authority = Mathf.Max(0, p.Authority - n);
            Log($"{p.DisplayName} lost {before - p.Authority} Authority ({why}) -> Total: {p.Authority}/{VictoryAuthorityThreshold}.");
        }

        // ---------------- Purgatory ----------------

        /// <summary>Purges one of the player's Goats from this tile onto the Purgatory card.</summary>
        private bool PurgeGoatFrom(HexTileState t, PlayerState p)
        {
            var g = t.Units.FirstOrDefault(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == "goat");
            if (g == null) return false;
            t.Units.Remove(g);
            p.GoatPurged++;
            return true;
        }

        /// <summary>"Gain Purged": Goats from the supply go onto the Purgatory card.</summary>
        private void GainPurged(PlayerState p, int n)
        {
            int k = Mathf.Min(n, UnitSupplyLeft(p, "goat"));
            p.GoatPurged += k;
        }

        /// <summary>"Remove Purged and place them into your supply".</summary>
        private void ReturnPurged(PlayerState p, int n) => p.GoatPurged = Mathf.Max(0, p.GoatPurged - n);

        // ---------------- Grimoire (Secrets of the Ancients) ----------------

        private void SetupGrimoire(PlayerState p)
        {
            p.Grimoire = RulebookDatabase.BuildGoatGrimoire14();
            Shuffle(p.Grimoire);
            p.GrimoireDiscard.Clear();
        }

        public void MillGrimoire(PlayerState p, int count)
        {
            for (int i = 0; i < count; i++) MillOneGrimoire(p);
        }

        private void MillOneGrimoire(PlayerState p)
        {
            if (p.Grimoire.Count == 0)
            {
                if (p.GrimoireDiscard.Count == 0) return;
                p.Grimoire.AddRange(p.GrimoireDiscard);
                p.GrimoireDiscard.Clear();
                Shuffle(p.Grimoire);
                Log($"{p.DisplayName} reshuffled the Grimoire.");
            }
            var card = p.Grimoire[p.Grimoire.Count - 1];
            p.Grimoire.RemoveAt(p.Grimoire.Count - 1);
            p.GrimoireDiscard.Add(card);
            bool paid = ApplyGrimoire(p, card.Id);
            p.LastGrimoireText = card.EffectText + (paid ? "" : " (fizzled)");
            Log($"{p.DisplayName} milled the Grimoire: {card.EffectText}{(paid ? "" : " -> couldn't pay, the card fizzled.")}");
        }

        /// <summary>Applies one Grimoire card. Returns false (doing nothing) when its cost can't be paid.</summary>
        private bool ApplyGrimoire(PlayerState p, string id)
        {
            switch (id)
            {
                case "gr_purged_1": GainPurged(p, 1); return true;
                case "gr_trade_purged_auth":
                    if (p.GoatPurged < 1) return false;
                    ReturnPurged(p, 1); AwardAuthority(p.PlayerIndex, 1, "Grimoire"); return true;
                case "gr_auth_for_purged":
                    if (p.Authority < 2) return false;
                    LoseAuthority(p, 2, "Grimoire"); GainPurged(p, 2); return true;
                case "gr_hand_for_auth":
                {
                    if (p.RelicHand.Count < 1) return false;
                    var c = NewChoice("grimoire_discard", p.PlayerIndex, "Grimoire", "Discard 1 card from your Hand, then gain 2 Authority.");
                    c.Number = 2;
                    c.Cards = p.RelicHand.ToList();
                    foreach (var card in c.Cards) AddOption(c, card.Name);
                    return true;
                }
                case "gr_draw_for_auth":
                {
                    if (p.Authority < 2) return false;
                    LoseAuthority(p, 2, "Grimoire");
                    var d = DrawRelicCard(); if (d != null) p.RelicHand.Add(d);
                    return true;
                }
                case "gr_res_for_purged":
                    if (p.ResourcePile.Count < 2) return false;
                    DiscardCheapest(p, 2); GainPurged(p, 2); return true;
                case "gr_gold_2": GainSpecificResourceFromDeck(p, ResourceType.Gold); GainSpecificResourceFromDeck(p, ResourceType.Gold); return true;
                case "gr_gold_for_auth":
                    if (Count(p, ResourceType.Gold) < 1) return false;
                    RemoveResources(p, ResourceType.Gold, 1); AwardAuthority(p.PlayerIndex, 2, "Grimoire"); return true;
                case "gr_draw2_discard1":
                {
                    var drawn = new List<RelicCardDef>();
                    for (int i = 0; i < 2; i++) { var d = DrawRelicCard(); if (d != null) { p.RelicHand.Add(d); drawn.Add(d); } }
                    if (drawn.Count == 0) return true;
                    var c = NewChoice("grimoire_discard", p.PlayerIndex, "Grimoire", "You drew 2 relic cards. Discard 1 of them.");
                    c.Number = 0;
                    c.Cards = drawn;
                    foreach (var card in drawn) AddOption(c, card.Name);
                    return true;
                }
                case "gr_gold_1": GainSpecificResourceFromDeck(p, ResourceType.Gold); return true;
                case "gr_auth_for_gold":
                    if (p.Authority < 2) return false;
                    LoseAuthority(p, 2, "Grimoire"); GainSpecificResourceFromDeck(p, ResourceType.Gold); return true;
                case "gr_auth_3": AwardAuthority(p.PlayerIndex, 3, "Grimoire"); return true;
                case "gr_auth_gold_purged":
                    AwardAuthority(p.PlayerIndex, 1, "Grimoire"); GainSpecificResourceFromDeck(p, ResourceType.Gold); GainPurged(p, 1); return true;
                case "gr_gold_3_cost":
                    if (p.Authority < 1 || p.GoatPurged < 1) return false;
                    for (int i = 0; i < 3; i++) GainSpecificResourceFromDeck(p, ResourceType.Gold);
                    LoseAuthority(p, 1, "Grimoire"); ReturnPurged(p, 1); return true;
            }
            return true;
        }

        // ---------------- Turn hooks ----------------

        /// <summary>Start of a Draw phase: the Serpentine Sovereign's Royal Decree, and the Horned Beast's tribute.</summary>
        private void GoatStartOfDrawPhase(PlayerState p)
        {
            if (p.Faction.Id == FactionId.GoatCabal && p.RoyalDecreeArmed)
            {
                p.RoyalDecreeArmed = false;
                if (OnBoard(p, "serpentine_sovereign") && DemonState(p, "serpentine_sovereign") == DemonBound && Board.TryGetValue(p.RoyalDecreeTile, out var t))
                    ResolveRoyalDecree(p, t);
                else Log("Royal Decree lapsed: the Serpentine Sovereign is no longer bound.");
            }

            // Subservience restriction 4: give the Goat player 1 Gold or 3 random resources
            if (p.SubservienceTurnsLeft > 0 && p.SubservienceRestriction == 4)
            {
                var goat = Players.FirstOrDefault(o => o.Faction != null && o.Faction.Id == FactionId.GoatCabal && o.PlayerIndex != p.PlayerIndex);
                if (goat != null)
                {
                    int gi = p.ResourcePile.IndexOf(ResourceType.Gold);
                    if (gi >= 0) { p.ResourcePile.RemoveAt(gi); goat.ResourcePile.Add(ResourceType.Gold); Log($"Subservience: {p.DisplayName} gave {goat.DisplayName} 1 Gold."); }
                    else
                    {
                        int n = 0;
                        for (; n < 3 && p.ResourcePile.Count > 0; n++)
                        {
                            int k = _rng.Next(p.ResourcePile.Count);
                            goat.ResourcePile.Add(p.ResourcePile[k]);
                            p.ResourcePile.RemoveAt(k);
                        }
                        Log($"Subservience: {p.DisplayName} gave {goat.DisplayName} {n} random resource card(s).");
                    }
                }
            }
        }

        /// <summary>Royal Decree: remove every unit, token, building and unique faction tile on the marked tile.</summary>
        private void ResolveRoyalDecree(PlayerState p, HexTileState t)
        {
            Log($"ROYAL DECREE: everything on {t.Coord} is removed!");
            foreach (var u in t.Units.ToList())
                if (t.Units.Contains(u)) RemoveUnitFromBoard(t, u, u.OwnerPlayerIndex == p.PlayerIndex ? -1 : p.PlayerIndex);
            if (t.Building != null)
            {
                if (t.Building.OwnerPlayerIndex != p.PlayerIndex) RemoveStructureForAuthority(t, null, true, p);
                else t.Building = null;
            }
            foreach (var tk in t.Tokens.ToList())
            {
                if (tk.OwnerPlayerIndex != p.PlayerIndex && tk.Type != TokenType.Coin) RemoveStructureForAuthority(t, tk, false, p);
                else t.Tokens.Remove(tk);
            }
            if (t.Overlay != FactionOverlayTileType.None)
            {
                if (IsLeaves(t) && t.OverlayOwnerPlayerIndex >= 0)
                    Players[t.OverlayOwnerPlayerIndex].FallenLeavesStack.Add(t.Overlay == FactionOverlayTileType.FallenLeaf_Trap);
                t.Overlay = FactionOverlayTileType.None;
                t.OverlayOwnerPlayerIndex = -1;
            }
        }

        /// <summary>Goat Cabal End phase: each bound demon's binding must be met, or it becomes unbound.</summary>
        private void ResolveDemonBindings(PlayerState p)
        {
            foreach (var id in DemonIds)
            {
                if (DemonState(p, id) != DemonBound) continue;
                var tile = TileOf(p, id);
                if (tile == null) continue;
                bool held = true;
                switch (id)
                {
                    case "faust":
                        held = p.GoatPurged >= 2;
                        break;
                    case "the_midian":
                    case "serpentine_sovereign":
                        held = p.Authority >= 2;
                        if (held) LoseAuthority(p, 2, $"binding {DemonName(p, id)}");
                        break;
                    case "lord_of_war":
                        held = !tile.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex) &&
                               !(tile.Building != null && tile.Building.OwnerPlayerIndex != p.PlayerIndex) &&
                               !tile.Tokens.Any(k => k.OwnerPlayerIndex != p.PlayerIndex);
                        break;
                    case "horned_beast":
                        held = Count(p, ResourceType.Gold) >= 2 && p.RelicHand.Count >= 1 && p.Authority >= 1 && p.GoatPurged >= 1 &&
                               Board.Values.Count(t => t.Building != null && t.Building.Type == BuildingType.SummoningCircle && t.Building.OwnerPlayerIndex == p.PlayerIndex) >= 3;
                        if (held)
                        {
                            RemoveResources(p, ResourceType.Gold, 2);
                            int k = _rng.Next(p.RelicHand.Count);
                            RelicDiscardPile.Add(p.RelicHand[k]); p.RelicHand.RemoveAt(k);
                            LoseAuthority(p, 1, "binding The Horned Beast");
                            ReturnPurged(p, 1);
                        }
                        break;
                }
                if (held) { Log($"{DemonName(p, id)} stays bound."); continue; }
                UnbindDemon(p, id, tile);
            }
        }

        /// <summary>The demon slips its binding: its penalty resolves, then it leaves the board and its card flips face down.</summary>
        private void UnbindDemon(PlayerState p, string id, HexTileState tile)
        {
            var demon = tile.Units.First(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == id);
            tile.Units.Remove(demon);
            p.DemonStates[id] = DemonUnbound;
            Log($"{DemonName(p, id)} became UNBOUND and left the board.");
            switch (id)
            {
                case "faust":
                {
                    int g = Mathf.Min(3, Count(p, ResourceType.Gold));
                    RemoveResources(p, ResourceType.Gold, g);
                    Log($"Unbound Faust: {p.DisplayName} lost {g} Gold.");
                    break;
                }
                case "the_midian":
                {
                    int roll = RollD6();
                    if (roll == 1)
                    {
                        int n = 0;
                        for (; n < 2 && p.ResourcePile.Count > 0; n++) ReturnResourceToDeck(p.ResourcePile, _rng.Next(p.ResourcePile.Count));
                        Log($"Unbound Midian rolled 1: {p.DisplayName} lost {n} random resource card(s).");
                    }
                    else if (roll <= 4) { Log($"Unbound Midian rolled {roll}."); LoseAuthority(p, 2, "Unbound Midian"); }
                    else
                    {
                        GainSpecificResourceFromDeck(p, ResourceType.Gold); GainSpecificResourceFromDeck(p, ResourceType.Gold);
                        Log($"Unbound Midian rolled {roll}: {p.DisplayName} gained 2 Gold.");
                    }
                    break;
                }
                case "serpentine_sovereign":
                {
                    p.RoyalDecreeArmed = false;
                    int nonGold = p.ResourcePile.Count(r => r != ResourceType.Gold);
                    if (nonGold >= 3) { DiscardCheapest(p, 3); Log($"Unbound Serpentine Sovereign: {p.DisplayName} lost 3 resource cards."); }
                    else if (Count(p, ResourceType.Gold) >= 2) { RemoveResources(p, ResourceType.Gold, 2); Log($"Unbound Serpentine Sovereign: {p.DisplayName} lost 2 Gold."); }
                    else if (p.ResourcePile.Count >= 3) { DiscardCheapest(p, 3); Log($"Unbound Serpentine Sovereign: {p.DisplayName} lost 3 resource cards."); }
                    else { ReturnPurged(p, 2); Log($"Unbound Serpentine Sovereign: {p.DisplayName} couldn't pay and returned 2 Purged to supply."); }
                    break;
                }
                case "lord_of_war":
                {
                    // 1 HP to two of your units (Goats first)
                    var mine = Board.Values.SelectMany(t => t.Units.Where(u => u.OwnerPlayerIndex == p.PlayerIndex).Select(u => new { t, u }))
                        .OrderBy(x => x.u.Definition.IsSpecialUnit ? 1 : 0).Take(2).ToList();
                    foreach (var x in mine)
                    {
                        x.u.CurrentHP -= 1;
                        if (x.u.CurrentHP <= 0) RemoveUnitFromBoard(x.t, x.u, -1);
                    }
                    Log($"Unbound Lord of War: {mine.Count} of {p.DisplayName}'s units took 1 HP damage.");
                    break;
                }
                case "horned_beast":
                    p.LoseMainActionNextTurn = true;
                    Log($"Unbound Horned Beast: {p.DisplayName} loses 1 Main phase action next turn.");
                    break;
            }
        }

        /// <summary>"When summoned" effects (they trigger on every summon, rebinding included).</summary>
        private void OnDemonSummoned(PlayerState p, string id, HexTileState t)
        {
            switch (id)
            {
                case "faust":
                    Log("In the Pursuit of Knowledge: Faust mills 2 Grimoire cards.");
                    MillGrimoire(p, 2);
                    break;
                case "the_midian":
                {
                    int circles = Board.Values.Count(x => x.Building != null && x.Building.Type == BuildingType.SummoningCircle);
                    if (circles > 0) AwardAuthority(p.PlayerIndex, circles, "A Gift (The Midian)");
                    break;
                }
                case "serpentine_sovereign":
                {
                    var c = NewChoice("royal_decree", p.PlayerIndex, "Royal Decree", "Click a tile up to 4 tiles from the Serpentine Sovereign. At the start of your next Draw phase, everything still on it is removed.");
                    c.Tiles = Board.Values.Where(x => HexCoord.Distance(x.Coord, t.Coord) <= 4).Select(x => x.Coord).ToList();
                    break;
                }
                case "lord_of_war":
                {
                    if (!t.Units.Any(u => u.OwnerPlayerIndex != p.PlayerIndex)) break;
                    var c = NewChoice("lord_fear", p.PlayerIndex, "Fear", "Click an adjacent tile. Every non-Goat unit on the Lord of War's tile moves there.");
                    c.Coord = t.Coord;
                    c.Tiles = Neighbors(t).Select(x => x.Coord).ToList();
                    if (c.Tiles.Count == 0) { if (PendingChoice == c) PendingChoice = null; else QueuedChoices.Remove(c); }
                    break;
                }
                case "horned_beast":
                {
                    var c = NewChoice("subservience", p.PlayerIndex, "Subservience", "Choose a restriction for every other player during their next two turns.");
                    AddOption(c, "1: Movements cost 1 more action");
                    AddOption(c, "2: No unearth, assemble, disassemble, or trading relic cards");
                    AddOption(c, "3: Attackers roll 1 die instead of 2");
                    AddOption(c, "4: In their Draw phase they give you 1 Gold or 3 random resources");
                    break;
                }
            }
        }

        /// <summary>Choices opened by the Goat Cabal. Returns true when it handled the kind.</summary>
        private bool ResolveGoatChoice(PendingChoiceState c, PlayerState p, int option, HexCoord tile, bool skip)
        {
            switch (c.Kind)
            {
                case "grimoire_discard":
                {
                    var card = c.Cards[Mathf.Clamp(option, 0, c.Cards.Count - 1)];
                    if (p.RelicHand.Remove(card)) RelicDiscardPile.Add(card);
                    Log($"{p.DisplayName} discarded {card.Name} (Grimoire).");
                    if (c.Number > 0) AwardAuthority(p.PlayerIndex, c.Number, "Grimoire");
                    if (CurrentPhase == TurnPhase.DrawPhase && p.PlayerIndex == CurrentTurnPlayerIndex && QueuedChoices.Count == 0 && !HasDrawPhaseOptions(p))
                        EnterExcavationPhase(p);
                    return true;
                }
                case "royal_decree":
                    p.RoyalDecreeArmed = true;
                    p.RoyalDecreeTile = tile;
                    Log($"Royal Decree: {tile} is marked. Everything still on it at the start of {p.DisplayName}'s next Draw phase will be removed.");
                    return true;
                case "lord_fear":
                {
                    var from = Board[c.Coord];
                    var to = Board[tile];
                    foreach (var grp in from.Units.Where(u => u.OwnerPlayerIndex != p.PlayerIndex && CanBeMovedByEnemyEffect(u)).GroupBy(u => u.OwnerPlayerIndex).ToList())
                    {
                        int room = Mathf.Max(0, MaxUnitsPerFactionPerTile - to.Units.Count(u => u.OwnerPlayerIndex == grp.Key));
                        var moving = grp.Take(room).ToList();
                        if (moving.Count > 0) RelocateUnits(from, to, moving);
                    }
                    Log($"Fear: the Lord of War drove the other units from {c.Coord} to {tile}.");
                    return true;
                }
                case "subservience":
                {
                    int r = option + 1;
                    foreach (var o in Players.Where(o => o.PlayerIndex != p.PlayerIndex))
                    {
                        o.SubservienceRestriction = r;
                        o.SubservienceTurnsLeft = 2;
                    }
                    Log($"Subservience: every other player suffers restriction {r} for their next two turns.");
                    return true;
                }
            }
            return false;
        }

        // ---------------- Subservience restrictions ----------------

        private bool HasRestriction(PlayerState p, int r) => p.SubservienceTurnsLeft > 0 && p.SubservienceRestriction == r;

        /// <summary>Restriction 2: no unearth, assemble, disassemble, or trading relic deck cards.</summary>
        public bool RelicActionsBlocked(PlayerState p) => HasRestriction(p, 2);

        // ---------------- Abilities ----------------

        private void AddGoat(List<AbilitySpec> L, PlayerState p, int me)
        {
            Func<HexTileState, bool> ownCircle = t => t.Building != null && t.Building.Type == BuildingType.SummoningCircle && t.Building.OwnerPlayerIndex == me;
            Func<string, bool> bound = id => DemonState(p, id) == DemonBound && OnBoard(p, id);

            L.Add(new AbilitySpec
            {
                Id = "sparagmos", Name = "Sparagmos", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Main phase: Purge 1 Goat from a tile with a Summoning Circle.",
                Prompt1 = "Click a Summoning Circle with a Goat.",
                Check = pl => Uses("sparagmos") > 0 ? "Already used this Main phase." : null,
                Legal1 = (pl, t) => ownCircle(t) && Has(t, pl, "goat"),
                Run = (pl, a, b, o) =>
                {
                    PurgeGoatFrom(Board[a], pl);
                    MarkUsed("sparagmos");
                    Log($"Sparagmos: {pl.DisplayName} purged a Goat at {a} ({pl.GoatPurged} Purged).");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "invocation", Name = "Invocation", Phase = TurnPhase.MainPhase, Target = AbilityTarget.TwoTiles,
                Summary = "Purge 1 Goat and discard 1 Wood, 1 Stone and 1 Gold to place an Initiation Rite Token on a Summoning Circle.",
                Prompt1 = "Click a tile with the Goat to purge.", Prompt2 = "Click a Summoning Circle for the Initiation Rite Token.",
                Check = pl => TokenSupplyLeft(pl, TokenType.InitiationRite) <= 0 ? "Both Initiation Rite Tokens are on the board." :
                              !CanAfford(pl, new ResourceCost(wood: 1, stone: 1, gold: 1)) ? "Needs 1 Wood, 1 Stone and 1 Gold." : null,
                Legal1 = (pl, t) => Has(t, pl, "goat") && Board.Values.Any(x => ownCircle(x) && CanPlaceTokenOn(x, TokenType.InitiationRite)),
                Legal2 = (pl, t1, t) => ownCircle(t) && CanPlaceTokenOn(t, TokenType.InitiationRite),
                Run = (pl, a, b, o) =>
                {
                    PurgeGoatFrom(Board[a], pl);
                    SpendResources(pl, new ResourceCost(wood: 1, stone: 1, gold: 1));
                    PlaceToken(b, TokenType.InitiationRite, pl);
                    Log($"Invocation: {pl.DisplayName} purged a Goat and placed an Initiation Rite Token at {b}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "canto_inferno", Name = "Canto Inferno", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Main phase: remove an Initiation Rite Token and pay a demon's summoning cost to summon it there (rebinding costs only the Purged).",
                Prompt1 = "Click your Initiation Rite Token.",
                Check = pl => Uses("canto_inferno") > 0 ? "Already used this Main phase." :
                              !DemonIds.Any(id => CanAffordDemon(pl, id)) ? "You can't pay any demon's summoning cost." : null,
                Legal1 = (pl, t) => t.Tokens.Any(k => k.Type == TokenType.InitiationRite && k.OwnerPlayerIndex == me),
                Choices = (pl, a, b) => DemonIds.Where(id => CanAffordDemon(pl, id)).Select(id => $"{DemonName(pl, id)} ({DemonCostLabel(pl, id)})").ToList(),
                Run = (pl, a, b, o) =>
                {
                    var ids = DemonIds.Where(d => CanAffordDemon(pl, d)).ToList();
                    if (ids.Count == 0) return false;
                    var demonId = ids[Mathf.Clamp(o, 0, ids.Count - 1)];
                    var t = Board[a];
                    t.Tokens.Remove(t.Tokens.First(k => k.Type == TokenType.InitiationRite && k.OwnerPlayerIndex == me));
                    bool rebind = DemonState(pl, demonId) == DemonUnbound;
                    ReturnPurged(pl, DemonPurgedCost(demonId));
                    if (!rebind) SpendResources(pl, DemonResourceCost(demonId));
                    SpawnUnit(a, demonId, pl, 1, ignoreStackingLimit: true);
                    pl.DemonStates[demonId] = DemonBound;
                    MarkUsed("canto_inferno");
                    Log($"Canto Inferno: {pl.DisplayName} {(rebind ? "rebound" : "summoned")} {DemonName(pl, demonId)} at {a}!");
                    OnDemonSummoned(pl, demonId, t);
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "effigy", Name = "Effigy", Phase = TurnPhase.ExcavationPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Excavation phase: return 1 Purged to your supply to place an Effigy Token on a Summoning Circle.",
                Prompt1 = "Click a Summoning Circle.",
                Check = pl => Uses("effigy") > 0 ? "Already used this Excavation phase." :
                              pl.GoatPurged < 1 ? "Needs 1 Purged." :
                              TokenSupplyLeft(pl, TokenType.Effigy) <= 0 ? "All 3 Effigy Tokens are on the board." : null,
                Legal1 = (pl, t) => ownCircle(t) && CanPlaceTokenOn(t, TokenType.Effigy),
                Run = (pl, a, b, o) =>
                {
                    ReturnPurged(pl, 1);
                    PlaceToken(a, TokenType.Effigy, pl);
                    MarkUsed("effigy");
                    Log($"Effigy: {pl.DisplayName} placed an Effigy Token at {a}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "effigy_offering", Name = "Effigy Offering", Phase = TurnPhase.DrawPhase, Target = AbilityTarget.None,
                Summary = "Draw phase, with an Effigy Token on the board: return 1 Purged to your supply to gain 1 Gold or lose 1 Authority.",
                Check = pl => Uses("effigy_offering") > 0 ? "Already used this Draw phase." :
                              TokensOnBoard(TokenType.Effigy, me) == 0 ? "No Effigy Token on the board." :
                              pl.GoatPurged < 1 ? "Needs 1 Purged." : null,
                Choices = (pl, a, b) => new List<string> { "Gain 1 Gold", "Lose 1 Authority" },
                Run = (pl, a, b, o) =>
                {
                    ReturnPurged(pl, 1);
                    if (o == 0) GainSpecificResourceFromDeck(pl, ResourceType.Gold); else LoseAuthority(pl, 1, "Effigy");
                    MarkUsed("effigy_offering");
                    Log($"Effigy Offering: {pl.DisplayName} {(o == 0 ? "gained 1 Gold" : "gave up 1 Authority")}.");
                    return true;
                }
            });

            if (bound("faust"))
                L.Add(new AbilitySpec
                {
                    Id = "bargain", Name = "Bargain (Faust)", Phase = TurnPhase.ExcavationPhase, Phase2 = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                    Summary = "Once per turn: Purge 1 Goat to mill the top Grimoire card and apply it.",
                    Prompt1 = "Click a tile with the Goat to purge.",
                    Check = pl => Uses("bargain") > 0 ? "Already used this turn." : null,
                    Legal1 = (pl, t) => Has(t, pl, "goat"),
                    Run = (pl, a, b, o) =>
                    {
                        PurgeGoatFrom(Board[a], pl);
                        MarkUsed("bargain");
                        MillGrimoire(pl, 1);
                        return true;
                    }
                });

            if (bound("the_midian"))
                L.Add(new AbilitySpec
                {
                    Id = "all_turns_to_gold", Name = "All Turns to Gold (The Midian)", Phase = TurnPhase.ExcavationPhase, Phase2 = TurnPhase.MainPhase,
                    Target = AbilityTarget.None,
                    Summary = "Once per turn: gain 2 Gold; another player of your choosing draws 1 resource card.",
                    Check = pl => Uses("all_turns_to_gold") > 0 ? "Already used this turn." : null,
                    Choices = (pl, a, b) => Players.Where(x => x.PlayerIndex != me).Select(x => $"{x.DisplayName} draws 1").ToList(),
                    Run = (pl, a, b, o) =>
                    {
                        var others = Players.Where(x => x.PlayerIndex != me).ToList();
                        var other = others[Mathf.Clamp(o, 0, others.Count - 1)];
                        GainSpecificResourceFromDeck(pl, ResourceType.Gold); GainSpecificResourceFromDeck(pl, ResourceType.Gold);
                        other.ResourcePile.Add(DrawRandomResourceCard());
                        MarkUsed("all_turns_to_gold");
                        Log($"All Turns to Gold: {pl.DisplayName} gained 2 Gold and {other.DisplayName} drew 1 resource card.");
                        return true;
                    }
                });

            if (bound("lord_of_war"))
                L.Add(new AbilitySpec
                {
                    Id = "onslaught", Name = "Onslaught (The Lord of War)", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                    Summary = "Up to 3 free battles per Main phase on the Lord of War's tile.",
                    Check = pl =>
                    {
                        if (Uses("onslaught") >= 3) return "All 3 free battles used this Main phase.";
                        var t = TileOf(pl, "lord_of_war");
                        return Players.Any(x => x.PlayerIndex != me && HasDefenderOn(t, x.PlayerIndex)) ? null : "No enemy to battle on its tile.";
                    },
                    Choices = (pl, a, b) =>
                    {
                        var t = TileOf(pl, "lord_of_war");
                        return Players.Where(x => x.PlayerIndex != me && HasDefenderOn(t, x.PlayerIndex)).Select(x => $"Attack {x.DisplayName}").ToList();
                    },
                    Run = (pl, a, b, o) =>
                    {
                        var t = TileOf(pl, "lord_of_war");
                        var foes = Players.Where(x => x.PlayerIndex != me && HasDefenderOn(t, x.PlayerIndex)).ToList();
                        if (foes.Count == 0) return false;
                        MarkUsed("onslaught");
                        TryBattleOnTile(t.Coord, foes[Mathf.Clamp(o, 0, foes.Count - 1)].PlayerIndex, consumesMainPhaseAction: false);
                        return true;
                    }
                });
        }

        private static bool HasDefenderOn(HexTileState t, int seat) =>
            t != null && (t.Units.Any(u => u.OwnerPlayerIndex == seat) || (t.Building != null && t.Building.OwnerPlayerIndex == seat) ||
                          t.Tokens.Any(k => k.OwnerPlayerIndex == seat && k.Type != TokenType.Coin));
    }
}
