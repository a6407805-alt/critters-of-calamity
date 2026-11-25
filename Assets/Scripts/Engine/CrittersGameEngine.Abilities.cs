using System;
using System.Collections.Generic;
using System.Linq;
using CrittersOfCalamity.Model;
using UnityEngine;

namespace CrittersOfCalamity.Engine
{
    /// <summary>What an ability needs the player to click before it runs.</summary>
    public enum AbilityTarget
    {
        None,      // no tile
        Tile,      // one tile
        TwoTiles   // a tile, then a second tile (Catapult, Flameweaver)
    }

    /// <summary>One faction ability (or a general one such as the Catapult), as the Turn Guide shows it.</summary>
    public class FactionAbility
    {
        public string Id;
        public string Name;
        public string Summary;
        public TurnPhase Phase;
        public TurnPhase? Phase2;   // a second phase it can also be used in
        public AbilityTarget Target;
        public string Prompt1;
        public string Prompt2;
        public bool Available;
        public string Blocked;
        public bool IsGeneral;
    }

    /// <summary>
    /// Part 17 faction abilities, rebuilt rule by rule from the Eight Factions rulebook. Each ability knows its phase,
    /// its once-per-turn limits, its cost, the tiles it can target and any choice the player makes. The Turn Guide
    /// lists them; FactionAction commands run them.
    /// </summary>
    public partial class CrittersGameEngine
    {
        /// <summary>How many times each ability (or ability group) has been used this turn. Cleared in BeginTurn.</summary>
        public Dictionary<string, int> AbilityUses = new Dictionary<string, int>();

        /// <summary>Relic cards burned by Disruption (they leave the game).</summary>
        public List<RelicCardDef> BurnedRelics = new List<RelicCardDef>();

        private int Uses(string key) => AbilityUses.TryGetValue(key, out var n) ? n : 0;
        private void MarkUsed(string key, int n = 1) => AbilityUses[key] = Uses(key) + n;

        private class AbilitySpec
        {
            public string Id, Name, Summary, Prompt1, Prompt2;
            public TurnPhase Phase;
            public TurnPhase? Phase2;
            public AbilityTarget Target;
            public bool General;
            public Func<PlayerState, string> Check;                                   // null = usable now
            public Func<PlayerState, HexTileState, bool> Legal1;
            public Func<PlayerState, HexTileState, HexTileState, bool> Legal2;
            public Func<PlayerState, HexCoord, HexCoord, List<string>> Choices;        // optional
            public Func<PlayerState, HexCoord, HexCoord, int, bool> Run;
        }

        // ------------------------------------------------------------------
        // PUBLIC API
        // ------------------------------------------------------------------

        /// <summary>Every ability the player has, with whether it can be used right now and why not.</summary>
        public List<FactionAbility> GetFactionAbilities(PlayerState p)
        {
            var list = new List<FactionAbility>();
            if (p == null || p.Faction == null) return list;
            foreach (var s in BuildAbilitySpecs(p))
            {
                string blocked = AbilityBlockedReason(p, s);
                list.Add(new FactionAbility
                {
                    Id = s.Id, Name = s.Name, Summary = s.Summary, Phase = s.Phase, Phase2 = s.Phase2, Target = s.Target,
                    Prompt1 = s.Prompt1, Prompt2 = s.Prompt2, IsGeneral = s.General,
                    Available = blocked == null, Blocked = blocked
                });
            }
            return list;
        }

        /// <summary>Legal tiles for an ability: the first tile when first is null, otherwise the second.</summary>
        public List<HexCoord> AbilityTargets(PlayerState p, string id, HexCoord? first)
        {
            var s = FindSpec(p, id);
            var result = new List<HexCoord>();
            if (s == null || AbilityBlockedReason(p, s) != null) return result;
            if (first == null)
            {
                if (s.Legal1 == null) return result;
                foreach (var t in Board.Values) if (s.Legal1(p, t)) result.Add(t.Coord);
            }
            else
            {
                if (s.Legal2 == null || !Board.TryGetValue(first.Value, out var t1)) return result;
                foreach (var t in Board.Values) if (s.Legal2(p, t1, t)) result.Add(t.Coord);
            }
            return result;
        }

        /// <summary>The options the player picks from after choosing tiles (one entry or none means no choice is needed).</summary>
        public List<string> AbilityChoices(PlayerState p, string id, HexCoord t1, HexCoord t2)
        {
            var s = FindSpec(p, id);
            if (s == null || s.Choices == null) return new List<string>();
            return s.Choices(p, t1, t2) ?? new List<string>();
        }

        /// <summary>Runs an ability for the turn player. Returns false (and changes nothing) when it isn't legal.</summary>
        public bool UseFactionAbility(string id, HexCoord t1, HexCoord t2, int option)
        {
            if (PendingBattle != null || PendingChoice != null || WinnerPlayerIndex >= 0) return false;
            var p = Players[CurrentTurnPlayerIndex];
            var s = FindSpec(p, id);
            if (s == null) return false;
            string blocked = AbilityBlockedReason(p, s);
            if (blocked != null) { Log($"{s.Name}: {blocked}"); return false; }

            if (s.Target != AbilityTarget.None)
            {
                if (!Board.TryGetValue(t1, out var tile1) || !s.Legal1(p, tile1)) { Log($"{s.Name}: that tile isn't a legal target."); return false; }
                if (s.Target == AbilityTarget.TwoTiles &&
                    (!Board.TryGetValue(t2, out var tile2) || !s.Legal2(p, tile1, tile2))) { Log($"{s.Name}: that second tile isn't legal."); return false; }
            }
            if (s.Choices != null)
            {
                var opts = s.Choices(p, t1, t2);
                if (opts == null || opts.Count == 0) { Log($"{s.Name}: nothing to choose."); return false; }
                if (option < 0 || option >= opts.Count) option = 0;
            }

            bool ok = s.Run(p, t1, t2, option);
            if (ok) OnStateChanged?.Invoke();
            return ok;
        }

        /// <summary>Whether the player has anything to do in their Draw phase (otherwise it passes automatically).</summary>
        public bool HasDrawPhaseOptions(PlayerState p) =>
            BuildAbilitySpecs(p).Any(s => s.Phase == TurnPhase.DrawPhase && AbilityBlockedReason(p, s, ignorePhase: true) == null);

        // ------------------------------------------------------------------
        // INTERNALS
        // ------------------------------------------------------------------

        private AbilitySpec FindSpec(PlayerState p, string id) => BuildAbilitySpecs(p).FirstOrDefault(s => s.Id == id);

        private string AbilityBlockedReason(PlayerState p, AbilitySpec s, bool ignorePhase = false)
        {
            if (p.PlayerIndex != CurrentTurnPlayerIndex) return "Only on your turn.";
            if (!ignorePhase && CurrentPhase != s.Phase && s.Phase2 != CurrentPhase)
                return s.Phase2.HasValue ? $"Only in your {PhaseLabel(s.Phase)} or {PhaseLabel(s.Phase2.Value)}." : $"Only in your {PhaseLabel(s.Phase)}.";
            if (PendingBattle != null) return "Finish the battle first.";
            if (PendingChoice != null) return "Finish the current choice first.";
            string why = s.Check?.Invoke(p);
            if (why != null) return why;
            if (s.Target != AbilityTarget.None && s.Legal1 != null && !Board.Values.Any(t => s.Legal1(p, t)))
                return "No legal tile right now.";
            return null;
        }

        private static string PhaseLabel(TurnPhase ph)
        {
            switch (ph)
            {
                case TurnPhase.DrawPhase: return "Draw phase";
                case TurnPhase.ExcavationPhase: return "Excavation phase";
                case TurnPhase.MainPhase: return "Main phase";
                case TurnPhase.EndPhase: return "End phase";
            }
            return ph.ToString();
        }

        // ---- board helpers ----

        private IEnumerable<UnitInstance> AllUnits => Board.Values.SelectMany(t => t.Units);
        private bool OnBoard(PlayerState p, string unitId) => AllUnits.Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == unitId);
        private HexTileState TileOf(PlayerState p, string unitId) =>
            Board.Values.FirstOrDefault(t => t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == unitId));
        private static bool Has(HexTileState t, PlayerState p, string unitId) => t.Units.Any(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == unitId);
        private static bool HasEnemyUnits(HexTileState t, int seat) => t.Units.Any(u => u.OwnerPlayerIndex != seat);
        private IEnumerable<HexTileState> Neighbors(HexTileState t) =>
            t.Coord.GetNeighbors().Where(n => Board.ContainsKey(n)).Select(n => Board[n]);
        private int Count(PlayerState p, ResourceType r) => p.ResourcePile.Count(x => x == r);
        private int UnitSupplyLeft(PlayerState p, string unitId)
        {
            var def = p.Faction.Units.FirstOrDefault(u => u.Id == unitId);
            if (def == null) return 0;
            return Mathf.Max(0, def.SupplyCount - AllUnits.Count(u => u.OwnerPlayerIndex == p.PlayerIndex && u.Definition.Id == unitId));
        }
        private static bool IsMiasma(HexTileState t) => t.Overlay == FactionOverlayTileType.Miasma_Hidden || t.Overlay == FactionOverlayTileType.Miasma_Revealed;
        private static bool IsLeaves(HexTileState t) => t.Overlay == FactionOverlayTileType.FallenLeaf_Unflipped ||
                                                        t.Overlay == FactionOverlayTileType.FallenLeaf_Trap || t.Overlay == FactionOverlayTileType.FallenLeaf_Prank;

        /// <summary>Fire, Fallen Leaves: "can only replace a forest tile" and can't go on another unique faction tile (Part 4).</summary>
        private static bool CanTakeForestOverlay(HexTileState t) =>
            t.Overlay == FactionOverlayTileType.None && t.BaseTerrain == TileTerrainType.Forest;

        /// <summary>How many of a faction's unique tiles of a kind are on the board.</summary>
        private int OverlaysOnBoard(int ownerSeat, Func<HexTileState, bool> kind) =>
            Board.Values.Count(t => t.OverlayOwnerPlayerIndex == ownerSeat && kind(t));

        public int FireTilesLeft(PlayerState p) => 6 - OverlaysOnBoard(p.PlayerIndex, t => t.Overlay == FactionOverlayTileType.Fire);
        public int MiasmaTilesLeft(PlayerState p) => 6 - OverlaysOnBoard(p.PlayerIndex, IsMiasma);

        public int TokensOnBoard(TokenType type, int ownerSeat = -1) =>
            Board.Values.Sum(t => t.Tokens.Count(k => k.Type == type && (ownerSeat < 0 || k.OwnerPlayerIndex == ownerSeat)));

        /// <summary>Tokens left in a faction's supply (Catapults are shared: 2 in the whole game).</summary>
        public int TokenSupplyLeft(PlayerState p, TokenType type)
        {
            if (type == TokenType.Catapult) return Mathf.Max(0, 2 - TokensOnBoard(TokenType.Catapult));
            var entry = RulebookDatabase.TokenSupply(p.Faction.Id).FirstOrDefault(kv => kv.Key == type);
            return Mathf.Max(0, entry.Value - TokensOnBoard(type, p.PlayerIndex));
        }

        public int BuildingSupplyLeft(PlayerState p, BuildingType type) =>
            Mathf.Max(0, RulebookDatabase.BuildingSupply(type) -
                         Board.Values.Count(t => t.Building != null && t.Building.Type == type && t.Building.OwnerPlayerIndex == p.PlayerIndex));

        /// <summary>Part 4 token rules: 2 tokens per tile, not on streams (Coin and Holy Relic with the Citadel excepted), only Tar Pits on Fire.</summary>
        public bool CanPlaceTokenOn(HexTileState t, TokenType type)
        {
            if (t.Tokens.Count >= 2) return false;
            if (t.Overlay == FactionOverlayTileType.Fire && type != TokenType.TarPit) return false;
            if (t.EffectiveTerrain == TileTerrainType.Stream)
            {
                if (type == TokenType.Coin) return true;
                if (type == TokenType.HolyRelic && t.Building != null && t.Building.Type == BuildingType.TheCitadel) return true;
                return false;
            }
            return true;
        }

        /// <summary>Discards resources of the player's choosing, cheapest first (Charcoal, Wood, Stone, Gold).</summary>
        private List<ResourceType> DiscardCheapest(PlayerState p, int n, bool burn = false)
        {
            var order = new[] { ResourceType.Charcoal, ResourceType.Wood, ResourceType.Stone, ResourceType.Gold };
            var gone = new List<ResourceType>();
            for (int i = 0; i < n && p.ResourcePile.Count > 0; i++)
            {
                foreach (var r in order)
                {
                    int idx = p.ResourcePile.IndexOf(r);
                    if (idx < 0) continue;
                    gone.Add(r);
                    if (burn) { p.ResourcePile.RemoveAt(idx); BurnedResources.Add(r); }
                    else ReturnResourceToDeck(p.ResourcePile, idx);
                    break;
                }
            }
            return gone;
        }

        /// <summary>Applies "entering a Fire Tile" damage (Faction 6): 1 of the entering non-Squirrel units takes 1 HP.</summary>
        private void ApplyFireEntryDamage(HexTileState tile, List<UnitInstance> entered, int moverSeat)
        {
            if (tile.Overlay != FactionOverlayTileType.Fire) return;
            var victim = entered.FirstOrDefault(u => u.Faction != FactionId.FirestartingSquirrels && u.Definition.Id != "pyromaniac" && tile.Units.Contains(u));
            if (victim == null) return;
            victim.CurrentHP -= 1;
            Log($"{victim.Definition.Name} took 1 HP entering the Fire Tile at {tile.Coord}.");
            if (victim.CurrentHP <= 0) RemoveUnitFromBoard(tile, victim, tile.OverlayOwnerPlayerIndex);
        }

        /// <summary>Moves units by an effect (not a Move action), keeping guest status and Tanuki coins right.</summary>
        private void RelocateUnits(HexTileState from, HexTileState to, List<UnitInstance> units)
        {
            foreach (var u in units)
            {
                from.Units.Remove(u);
                to.Units.Add(u);
                u.IsGuest = BecomesGuestOnArrival(to, u.OwnerPlayerIndex);
            }
            if (units.Any(u => u.Definition.Id == "riverkeeper")) WelcomeGuestsAtRiverkeeper(to);
            var tanukiMover = units.FirstOrDefault(u => u.Faction == FactionId.TroubleMakingTanukis);
            if (tanukiMover != null && !from.Units.Any(u => u.OwnerPlayerIndex == tanukiMover.OwnerPlayerIndex))
                MoveAndScoreTanukiCoin(from, to, tanukiMover.OwnerPlayerIndex);
        }

        /// <summary>Whether an effect may move this unit (Platy-Pope, Trap Leaves, War Axe Curly Tail).</summary>
        private bool CanBeMovedByEnemyEffect(UnitInstance u)
        {
            if (u.Definition.Id == "platy_pope") return false;
            if (u.TrapLeafTurnsRemaining > 0 || (u.CannotMoveThisTurn && u.IsTrapped)) return false;
            if (u.Definition.Id == "curly_tail" && u.EquippedItems.Any(e => e.Id == "golden_war_axe")) return false;
            return true;
        }

        /// <summary>Removes an isolated building or token by an ability and scores it (Part 7 and Part 13).</summary>
        private void RemoveStructureForAuthority(HexTileState tile, TokenInstance token, bool building, PlayerState remover)
        {
            int crusade = remover.Faction.Id == FactionId.PlatypusPapacy && remover.CrusadeActive ? 1 : 0;
            if (building && tile.Building != null)
            {
                Log($"{remover.DisplayName} removed {Players[tile.Building.OwnerPlayerIndex].DisplayName}'s {tile.Building.Type} at {tile.Coord}.");
                if (tile.Building.Type == BuildingType.Wallow)
                    EndGuestStatus(tile.Units.Where(u => u.OwnerPlayerIndex == remover.PlayerIndex), "destroyed a Wallow");
                tile.Building = null;
                AwardAuthority(remover.PlayerIndex, 1 + crusade, "Removed an enemy building");
            }
            else if (token != null)
            {
                tile.Tokens.Remove(token);
                AwardAuthority(remover.PlayerIndex, 1 + crusade, $"Removed an enemy {token.Type} Token");
            }
        }

        /// <summary>Equips an equipment card to a unit (Curly Tail's Forge, Necro-mouser equipment).</summary>
        private void EquipUnit(PlayerState p, UnitInstance target, EquipmentDef eq)
        {
            SpendResources(p, eq.Cost);
            target.EquippedItems.Add(eq);
            if (eq.BonusHP > 0)
            {
                target.MaxHP += eq.BonusHP;
                target.CurrentHP = Mathf.Min(target.MaxHP, target.CurrentHP + eq.BonusHP); // Tyrant's Crown heals 2
            }
            if (eq.AuthorityOnCraft > 0 && !p.CraftedGoldenItemIds.Contains(eq.Id))
            {
                p.CraftedGoldenItemIds.Add(eq.Id);
                AwardAuthority(p.PlayerIndex, eq.AuthorityOnCraft, $"Forged {eq.Name}");
            }
            Log($"{p.DisplayName} crafted {eq.Name} and equipped it to {target.Definition.Name}.");
        }

        private int EquippedOnBoard(PlayerState p, string eqId) =>
            AllUnits.Where(u => u.OwnerPlayerIndex == p.PlayerIndex).Sum(u => u.EquippedItems.Count(e => e.Id == eqId));

        private bool CanTakeEquipment(UnitInstance u, EquipmentDef eq)
        {
            if (u.Definition.Id != eq.TargetUnitId) return false;
            if (u.EquippedItems.Count(e => e.Id == eq.Id) >= eq.MaxPerUnit) return false;
            // Vanguard: only one Helmet or Shield per turn
            if (u.Definition.Id == "the_vanguard" && Uses("vanguard_equip:" + u.InstanceId) > 0) return false;
            return true;
        }

        // Thistle Barrage scores 1 Authority for every 2 HP it deals in a turn
        private void ScoreThistleDamage(PlayerState p, int newHits)
        {
            int before = Uses("barrage_hp") / 2;
            MarkUsed("barrage_hp", newHits);
            int gain = Uses("barrage_hp") / 2 - before;
            if (gain > 0) AwardAuthority(p.PlayerIndex, gain, "Thistle Barrage (every 2 HP dealt)");
        }

        private static readonly string[] ResName = { "Wood", "Stone", "Gold", "Charcoal" };

        // ------------------------------------------------------------------
        // THE ABILITIES
        // ------------------------------------------------------------------

        private List<AbilitySpec> BuildAbilitySpecs(PlayerState p)
        {
            var L = new List<AbilitySpec>();
            int me = p.PlayerIndex;
            switch (p.Faction.Id)
            {
                case FactionId.CurlyTail: AddCurlyTail(L, p, me); break;
                case FactionId.HordeOfTheMillions: AddHorde(L, p, me); break;
                case FactionId.PlatypusPapacy: AddPapacy(L, p, me); break;
                case FactionId.TroubleMakingTanukis: AddTanukis(L, p, me); break;
                case FactionId.Necromouser: AddNecromouser(L, p, me); break;
                case FactionId.FirestartingSquirrels: AddSquirrels(L, p, me); break;
                case FactionId.CapybaraCaravan: AddCapybara(L, p, me); break;
                case FactionId.BriaredFlower: AddBriar(L, p, me); break;
            }
            AddGeneral(L, p, me);
            AddTradedGoods(L, p, me);
            return L;
        }

        // ---------------- Curly Tail's Warband ----------------
        private void AddCurlyTail(List<AbilitySpec> L, PlayerState p, int me)
        {
            L.Add(new AbilitySpec
            {
                Id = "supply_line", Name = "Supply Line", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Free, up to twice per Main phase: discard 1 Wood to place a Supply Line Token within 2 tiles of your War Party, Supply Line Token or Warband unit (not on a stream).",
                Prompt1 = "Click a tile within 2 of your War Parties, Supply Lines or Warband units.",
                Check = pl => Uses("supply_line") >= 2 ? "Already used twice this Main phase." :
                              Count(pl, ResourceType.Wood) < 1 ? "Needs 1 Wood." :
                              TokenSupplyLeft(pl, TokenType.SupplyLine) <= 0 ? "No Supply Line Tokens left in your supply." : null,
                Legal1 = (pl, t) => CanPlaceTokenOn(t, TokenType.SupplyLine) && !t.Tokens.Any(k => k.Type == TokenType.SupplyLine && k.OwnerPlayerIndex == me) &&
                                    Board.Values.Any(o => HexCoord.Distance(o.Coord, t.Coord) <= 2 &&
                                        ((o.Building != null && o.Building.Type == BuildingType.WarParty && o.Building.OwnerPlayerIndex == me) ||
                                         o.Tokens.Any(k => k.Type == TokenType.SupplyLine && k.OwnerPlayerIndex == me) ||
                                         Has(o, pl, "warband"))),
                Run = (pl, a, b, o) =>
                {
                    RemoveResources(pl, ResourceType.Wood, 1);
                    PlaceToken(a, TokenType.SupplyLine, pl);
                    MarkUsed("supply_line");
                    Log($"{pl.DisplayName} placed a Supply Line Token at {a}.");
                    return true;
                }
            });

            foreach (var eq in p.Faction.Equipment)
            {
                var e = eq;
                L.Add(new AbilitySpec
                {
                    Id = "forge:" + e.Id, Name = "Forge " + e.Name, Phase = TurnPhase.ExcavationPhase, Target = AbilityTarget.None,
                    Summary = $"Free craft, once per Excavation phase while Curly Tail is on the board: pay {e.Cost} and equip it to Curly Tail. {e.EffectText}",
                    Check = pl =>
                    {
                        if (Uses("forge") > 0) return "You already forged this Excavation phase.";
                        var ct = AllUnits.FirstOrDefault(u => u.OwnerPlayerIndex == me && u.Definition.Id == "curly_tail");
                        if (ct == null) return "Curly Tail isn't on the board.";
                        if (ct.EquippedItems.Any(x => x.Id == e.Id)) return "Curly Tail already has one.";
                        return CanAfford(pl, e.Cost) ? null : $"Costs {e.Cost}.";
                    },
                    Run = (pl, a, b, o) =>
                    {
                        var ct = AllUnits.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "curly_tail");
                        EquipUnit(pl, ct, e);
                        MarkUsed("forge");
                        return true;
                    }
                });
            }

            L.Add(new AbilitySpec
            {
                Id = "taunt", Name = "Taunt", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Thirst for Glory 3+, once per Main phase: move 1 enemy unit within 2 tiles of Curly Tail 1 tile closer. If it lands on his tile, start a free battle against its faction.",
                Prompt1 = "Click a tile within 2 of Curly Tail that has an enemy unit.",
                Check = pl => pl.CurlyTailThirstForGlory < 3 ? "Needs Thirst for Glory level 3." :
                              Uses("taunt") > 0 ? "Already used this Main phase." :
                              !OnBoard(pl, "curly_tail") ? "Curly Tail isn't on the board." : null,
                Legal1 = (pl, t) =>
                {
                    var ct = TileOf(pl, "curly_tail");
                    if (ct == null) return false;
                    int d = HexCoord.Distance(ct.Coord, t.Coord);
                    return d >= 1 && d <= 2 && t.Units.Any(u => u.OwnerPlayerIndex != me && CanBeMovedByEnemyEffect(u));
                },
                Choices = (pl, a, b) => Board.TryGetValue(a, out var t)
                    ? t.Units.Where(u => u.OwnerPlayerIndex != me && CanBeMovedByEnemyEffect(u))
                             .Select(u => $"{u.Definition.Name} ({Players[u.OwnerPlayerIndex].DisplayName}, {u.CurrentHP} HP)").ToList()
                    : new List<string>(),
                Run = (pl, a, b, o) =>
                {
                    var from = Board[a];
                    var ct = TileOf(pl, "curly_tail");
                    var targets = from.Units.Where(u => u.OwnerPlayerIndex != me && CanBeMovedByEnemyEffect(u)).ToList();
                    var victim = targets[Mathf.Clamp(o, 0, targets.Count - 1)];
                    HexTileState dest = HexCoord.Distance(a, ct.Coord) == 1 ? ct
                        : Neighbors(from).FirstOrDefault(n => HexCoord.Distance(n.Coord, ct.Coord) == 1 && CanAddUnitToTile(n, victim.OwnerPlayerIndex));
                    if (dest == null || !CanAddUnitToTile(dest, victim.OwnerPlayerIndex)) { Log("Taunt: no room to pull that unit closer."); return false; }
                    MarkUsed("taunt");
                    RelocateUnits(from, dest, new List<UnitInstance> { victim });
                    ApplyFireEntryDamage(dest, new List<UnitInstance> { victim }, me);
                    Log($"Curly Tail taunted {victim.Definition.Name} to {dest.Coord}.");
                    if (dest == ct && dest.Units.Contains(victim))
                        TryBattleOnTile(ct.Coord, victim.OwnerPlayerIndex, consumesMainPhaseAction: false);
                    return true;
                }
            });
        }

        // ---------------- Horde of the Millions ----------------
        private void AddHorde(List<AbilitySpec> L, PlayerState p, int me)
        {
            L.Add(new AbilitySpec
            {
                Id = "gluttony", Name = "Other People's Gluttony", Phase = TurnPhase.ExcavationPhase, Target = AbilityTarget.Tile,
                Summary = "Free craft: discard 1 Gold to place a Gluttony Token on a tile with a Millions unit. Enemy units there can't use a Move action to leave while a Millions unit is on it.",
                Prompt1 = "Click a tile with your Millions units.",
                Check = pl => Count(pl, ResourceType.Gold) < 1 ? "Needs 1 Gold." :
                              TokenSupplyLeft(pl, TokenType.Gluttony) <= 0 ? "No Gluttony Tokens left in your supply." : null,
                Legal1 = (pl, t) => Has(t, pl, "millions") && CanPlaceTokenOn(t, TokenType.Gluttony),
                Run = (pl, a, b, o) =>
                {
                    RemoveResources(pl, ResourceType.Gold, 1);
                    PlaceToken(a, TokenType.Gluttony, pl);
                    Log($"{pl.DisplayName} placed a Gluttony Token at {a}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "summon_king", Name = "Summon King of the Millions", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per game: discard 1 Gold, 1 Stone and 1 Wood to place the King on a tile with a Burrow. Spread the Fun: every other player draws 1 Forage card.",
                Prompt1 = "Click one of your Burrow tiles.",
                Check = pl => pl.KingOfTheMillionsSummonedOnce ? "The King can only be summoned once per game." :
                              !CanAfford(pl, new ResourceCost(wood: 1, stone: 1, gold: 1)) ? "Needs 1 Gold, 1 Stone and 1 Wood." : null,
                Legal1 = (pl, t) => t.Building != null && t.Building.Type == BuildingType.Burrow && t.Building.OwnerPlayerIndex == me && CanAddUnitToTile(t, me),
                Run = (pl, a, b, o) =>
                {
                    SpendResources(pl, new ResourceCost(wood: 1, stone: 1, gold: 1));
                    pl.KingOfTheMillionsSummonedOnce = true;
                    SpawnUnit(a, "king_of_the_millions", pl, 1);
                    Log($"{pl.DisplayName} summoned the King of the Millions at {a}. Spread the Fun!");
                    foreach (var op in Players.Where(x => x.PlayerIndex != me)) DrawAndResolveForageCard(op);
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "rabbit_hill", Name = "King of the Rabbit Hill", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Once per Main phase: place 1 Millions unit on the King's tile.",
                Check = pl => Uses("rabbit_hill") > 0 ? "Already used this Main phase." :
                              !OnBoard(pl, "king_of_the_millions") ? "The King isn't on the board." :
                              UnitSupplyLeft(pl, "millions") <= 0 ? "No Millions units left in your supply." :
                              !CanAddUnitToTile(TileOf(pl, "king_of_the_millions"), me) ? "The King's tile is full (6 units)." : null,
                Run = (pl, a, b, o) =>
                {
                    var k = TileOf(pl, "king_of_the_millions");
                    SpawnUnit(k.Coord, "millions", pl, 1);
                    MarkUsed("rabbit_hill");
                    Log($"King of the Rabbit Hill: 1 Millions unit joined the King at {k.Coord}.");
                    return true;
                }
            });
        }

        // ---------------- Platypus Papacy ----------------
        private void AddPapacy(List<AbilitySpec> L, PlayerState p, int me)
        {
            L.Add(new AbilitySpec
            {
                Id = "alms", Name = "Alms", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Once per Main phase, with the Platy-Pope on the board and 5 Faith: raise the Platy-Pope 1 level, gain 1 Authority, reset Faith to 0 and enter a Crusade for your next 2 turns.",
                Check = pl => Uses("alms") > 0 ? "Already used this Main phase." :
                              !OnBoard(pl, "platy_pope") ? "The Platy-Pope isn't on the board." :
                              pl.PlatypusFaith < 5 ? $"Needs 5 Faith (you have {pl.PlatypusFaith})." :
                              pl.PlatyPopeLevel >= 5 ? "The Platy-Pope is already level 5." : null,
                Run = (pl, a, b, o) =>
                {
                    MarkUsed("alms");
                    pl.PlatypusFaith = 0;
                    pl.PlatyPopeLevel = Mathf.Min(5, pl.PlatyPopeLevel + 1);
                    pl.CrusadeTurnsRemaining += 2; // Crusades stack
                    AwardAuthority(me, 1, $"Alms (Platy-Pope now level {pl.PlatyPopeLevel})");
                    Log($"{pl.DisplayName} enters a Crusade for their next 2 turns ({pl.CrusadeTurnsRemaining} Crusade turns banked).");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "indulgences", Name = "Indulgences", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Discard 1 Gold to gain 2 Faith (Faith above 5 is lost).",
                Check = pl => Count(pl, ResourceType.Gold) < 1 ? "Needs 1 Gold." : pl.PlatypusFaith >= 5 ? "Your Faith is already 5." : null,
                Run = (pl, a, b, o) =>
                {
                    RemoveResources(pl, ResourceType.Gold, 1);
                    pl.PlatypusFaith = Mathf.Min(5, pl.PlatypusFaith + 2);
                    Log($"{pl.DisplayName} sold Indulgences: Faith {pl.PlatypusFaith}/5.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "holy_relic", Name = "Holy Relic Token", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Once per Main phase with a Crusader on the board: discard 1 Wood, 1 Stone and 1 Gold to place a Holy Relic Token on the Citadel's tile and gain 1 Authority. Each gives 1 Faith in your End phase.",
                Check = pl =>
                {
                    if (Uses("holy_relic") > 0) return "Already used this Main phase.";
                    if (!OnBoard(pl, "crusader")) return "Needs a Crusader on the board.";
                    var cit = Board.Values.FirstOrDefault(t => t.Building != null && t.Building.Type == BuildingType.TheCitadel && t.Building.OwnerPlayerIndex == me);
                    if (cit == null) return "The Citadel isn't on the board.";
                    if (!CanPlaceTokenOn(cit, TokenType.HolyRelic)) return "The Citadel's tile already has 2 tokens.";
                    if (TokenSupplyLeft(pl, TokenType.HolyRelic) <= 0) return "Both Holy Relic Tokens are on the board.";
                    return CanAfford(pl, new ResourceCost(wood: 1, stone: 1, gold: 1)) ? null : "Needs 1 Wood, 1 Stone and 1 Gold.";
                },
                Run = (pl, a, b, o) =>
                {
                    var cit = Board.Values.First(t => t.Building != null && t.Building.Type == BuildingType.TheCitadel && t.Building.OwnerPlayerIndex == me);
                    SpendResources(pl, new ResourceCost(wood: 1, stone: 1, gold: 1));
                    PlaceToken(cit.Coord, TokenType.HolyRelic, pl);
                    MarkUsed("holy_relic");
                    AwardAuthority(me, 1, "Enshrined a Holy Relic Token");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "cleanse", Name = "Cleanse Crusaders (Level 2)", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Platy-Pope level 2+, once per turn: discard 1 Gold to remove all debuffs (Trap Leaves, Gluttony) from your Crusaders on one tile.",
                Prompt1 = "Click a tile with your Crusaders.",
                Check = pl => pl.PlatyPopeLevel < 2 ? "Needs Platy-Pope level 2." : Uses("cleanse") > 0 ? "Already used this turn." :
                              Count(pl, ResourceType.Gold) < 1 ? "Needs 1 Gold." : null,
                Legal1 = (pl, t) => Has(t, pl, "crusader"),
                Run = (pl, a, b, o) =>
                {
                    RemoveResources(pl, ResourceType.Gold, 1);
                    foreach (var u in Board[a].Units.Where(u => u.OwnerPlayerIndex == me && u.Definition.Id == "crusader"))
                    {
                        u.TrapLeafTurnsRemaining = 0;
                        if (u.IsTrapped) { u.CannotMoveThisTurn = false; u.IsTrapped = false; }
                        u.CleansedThisTurn = true;
                    }
                    MarkUsed("cleanse");
                    Log($"{pl.DisplayName} cleansed the Crusaders at {a} of all debuffs.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "convert", Name = "Conversion (Level 3)", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Platy-Pope level 3+: spend 2 Faith to remove 1 enemy basic unit on a tile with a Crusader, then place a Pilgrim there.",
                Prompt1 = "Click a tile with your Crusader and an enemy basic unit.",
                Check = pl => pl.PlatyPopeLevel < 3 ? "Needs Platy-Pope level 3." : pl.PlatypusFaith < 2 ? "Needs 2 Faith." : null,
                Legal1 = (pl, t) => Has(t, pl, "crusader") && t.Units.Any(u => u.OwnerPlayerIndex != me && !u.Definition.IsSpecialUnit),
                Choices = (pl, a, b) => Board.TryGetValue(a, out var t)
                    ? t.Units.Where(u => u.OwnerPlayerIndex != me && !u.Definition.IsSpecialUnit).Select(u => u.OwnerPlayerIndex).Distinct()
                           .Select(s => $"Convert a {t.Units.First(u => u.OwnerPlayerIndex == s && !u.Definition.IsSpecialUnit).Definition.Name} ({Players[s].DisplayName})").ToList()
                    : new List<string>(),
                Run = (pl, a, b, o) =>
                {
                    var t = Board[a];
                    var owners = t.Units.Where(u => u.OwnerPlayerIndex != me && !u.Definition.IsSpecialUnit).Select(u => u.OwnerPlayerIndex).Distinct().ToList();
                    int seat = owners[Mathf.Clamp(o, 0, owners.Count - 1)];
                    var victim = t.Units.First(u => u.OwnerPlayerIndex == seat && !u.Definition.IsSpecialUnit);
                    pl.PlatypusFaith -= 2;
                    RemoveUnitFromBoard(t, victim, me);
                    SpawnUnit(a, "pilgrim", pl, 1);
                    Log($"{pl.DisplayName} converted {Players[seat].DisplayName}'s {victim.Definition.Name} at {a} into a Pilgrim.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "faith_authority", Name = "Faith to Authority (Level 5)", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Platy-Pope level 5, once per turn: remove 3 Faith to gain 1 Authority.",
                Check = pl => pl.PlatyPopeLevel < 5 ? "Needs Platy-Pope level 5." : Uses("faith_authority") > 0 ? "Already used this turn." :
                              pl.PlatypusFaith < 3 ? "Needs 3 Faith." : null,
                Run = (pl, a, b, o) =>
                {
                    pl.PlatypusFaith -= 3;
                    MarkUsed("faith_authority");
                    AwardAuthority(me, 1, "Level 5: 3 Faith");
                    return true;
                }
            });
        }

        // ---------------- Trouble Making Tanukis ----------------
        private void AddTanukis(List<AbilitySpec> L, PlayerState p, int me)
        {
            Func<HexTileState, bool> ownShrine = t => t.Building != null && t.Building.Type == BuildingType.Shrine && t.Building.OwnerPlayerIndex == me;

            L.Add(new AbilitySpec
            {
                Id = "rustling", Name = "Rustling in the Trees", Phase = TurnPhase.DrawPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Draw phase: place a Fallen Leaves tile on a forest tile with a Shrine, or next to a Shrine, that has no enemy units.",
                Prompt1 = "Click a forest tile on or next to your Shrine with no enemy units.",
                Check = pl => Uses("rustling") > 0 ? "Already used this Draw phase." :
                              pl.FallenLeavesStack.Count == 0 ? "No Fallen Leaves tiles left in your supply." : null,
                Legal1 = (pl, t) => CanTakeForestOverlay(t) && !HasEnemyUnits(t, me) && (ownShrine(t) || Neighbors(t).Any(n => ownShrine(n))),
                Run = (pl, a, b, o) =>
                {
                    PlaceOverlayTile(a, FactionOverlayTileType.FallenLeaf_Unflipped, me);
                    MarkUsed("rustling");
                    Log($"{pl.DisplayName} placed a Fallen Leaves tile at {a} (Rustling in the Trees).");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "belly_drums", Name = "Belly Drums", Phase = TurnPhase.DrawPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Draw phase with the Band on the board: place a Fallen Leaves tile on up to 2 forest tiles that have a Coin Token and no enemy units (click them one at a time).",
                Prompt1 = "Click a forest tile with a Coin Token and no enemy units.",
                Check = pl => !OnBoard(pl, "band_of_troublemakers") ? "The Band isn't on the board." :
                              Uses("belly_drums") >= 2 ? "Already placed 2 tiles this Draw phase." :
                              pl.FallenLeavesStack.Count == 0 ? "No Fallen Leaves tiles left in your supply." : null,
                Legal1 = (pl, t) => CanTakeForestOverlay(t) && !HasEnemyUnits(t, me) && t.Tokens.Any(k => k.Type == TokenType.Coin),
                Run = (pl, a, b, o) =>
                {
                    PlaceOverlayTile(a, FactionOverlayTileType.FallenLeaf_Unflipped, me);
                    MarkUsed("belly_drums");
                    Log($"{pl.DisplayName} placed a Fallen Leaves tile at {a} (Belly Drums, {Uses("belly_drums")}/2).");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "running_amok", Name = "Running Amok", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Main phase: place a Fallen Leaves tile on a forest tile with a Tanuki unit.",
                Prompt1 = "Click a forest tile with your Tanuki units.",
                Check = pl => Uses("running_amok") > 0 ? "Already used this Main phase." :
                              pl.FallenLeavesStack.Count == 0 ? "No Fallen Leaves tiles left in your supply." : null,
                Legal1 = (pl, t) => CanTakeForestOverlay(t) && Has(t, pl, "tanuki"),
                Run = (pl, a, b, o) =>
                {
                    PlaceOverlayTile(a, FactionOverlayTileType.FallenLeaf_Unflipped, me);
                    MarkUsed("running_amok");
                    Log($"{pl.DisplayName} placed a Fallen Leaves tile at {a} (Running Amok).");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "summon_band", Name = "Summon A Band of Troublemakers", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "With 3+ Tanukis on one tile: remove 3 of them and discard 1 Gold to place the Band there. Only one Band at a time.",
                Prompt1 = "Click a tile with 3 or more of your Tanukis.",
                Check = pl => OnBoard(pl, "band_of_troublemakers") ? "The Band is already on the board." :
                              Count(pl, ResourceType.Gold) < 1 ? "Needs 1 Gold." : null,
                Legal1 = (pl, t) => t.Units.Count(u => u.OwnerPlayerIndex == me && u.Definition.Id == "tanuki") >= 3,
                Run = (pl, a, b, o) =>
                {
                    var t = Board[a];
                    RemoveResources(pl, ResourceType.Gold, 1);
                    for (int i = 0; i < 3; i++) t.Units.Remove(t.Units.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "tanuki"));
                    SpawnUnit(a, "band_of_troublemakers", pl, 1);
                    Log($"{pl.DisplayName} formed A Band of Troublemakers at {a}.");
                    return true;
                }
            });
        }

        // ---------------- The Necro-mouser ----------------
        private void AddNecromouser(List<AbilitySpec> L, PlayerState p, int me)
        {
            L.Add(new AbilitySpec
            {
                Id = "necrosis", Name = "Necrosis (explode a Bloat)", Phase = TurnPhase.DrawPhase, Target = AbilityTarget.Tile,
                Summary = "Draw phase: explode a Bloat. It deals 2 HP to every enemy unit on its tile (past the 1 HP cap). Locket of Memory: also the adjacent tiles. Ring of Favor: +2 Authority and a Miasma Tile there.",
                Prompt1 = "Click a tile with your Bloat.",
                Legal1 = (pl, t) => Has(t, pl, "the_bloat"),
                Run = (pl, a, b, o) =>
                {
                    var t = Board[a];
                    var bloat = t.Units.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "the_bloat");
                    bool locket = bloat.EquippedItems.Any(e => e.Id == "locket_of_memory");
                    bool ring = bloat.EquippedItems.Any(e => e.Id == "ring_of_favor");
                    t.Units.Remove(bloat); // exploding is not being removed by an enemy
                    var area = new List<HexTileState> { t };
                    if (locket) area.AddRange(Neighbors(t));
                    int hits = 0;
                    foreach (var at in area)
                        foreach (var enemy in at.Units.Where(u => u.OwnerPlayerIndex != me).ToList())
                        {
                            enemy.CurrentHP -= 2;
                            hits++;
                            if (enemy.CurrentHP <= 0) RemoveUnitFromBoard(at, enemy, me);
                        }
                    if (ring)
                    {
                        AwardAuthority(me, 2, "Ring of Favor explosion");
                        if (t.Overlay == FactionOverlayTileType.None && MiasmaTilesLeft(pl) > 0)
                            PlaceOverlayTile(a, FactionOverlayTileType.Miasma_Revealed, me);
                    }
                    Log($"The Bloat exploded at {a}, hitting {hits} enemy unit(s) for 2 HP.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "arise", Name = "Arise", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per turn: spend 1 Dead from the Cauldron to summon 1 Skeleton on a Miasma Tile.",
                Prompt1 = "Click a Miasma Tile.",
                Check = pl => Uses("arise") > 0 ? "Already used this turn." : pl.NecromouserCauldronDeadCount < 1 ? "The Cauldron is empty." :
                              UnitSupplyLeft(pl, "skeleton") <= 0 ? "No Skeletons left in your supply." : null,
                Legal1 = (pl, t) => IsMiasma(t) && CanAddUnitToTile(t, me),
                Run = (pl, a, b, o) =>
                {
                    pl.NecromouserCauldronDeadCount--;
                    SpawnUnit(a, "skeleton", pl, 1);
                    MarkUsed("arise");
                    Log($"{pl.DisplayName} raised a Skeleton at {a} (Arise).");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "bad_news", Name = "Spread the Bad News", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Main phase: remove 1 Skeleton from the board (not to the Cauldron) to place a Miasma Tile where it stood.",
                Prompt1 = "Click a tile with your Skeleton (and no other unique faction tile).",
                Check = pl => Uses("bad_news") > 0 ? "Already used this Main phase." : MiasmaTilesLeft(pl) <= 0 ? "All 6 Miasma Tiles are on the board." : null,
                Legal1 = (pl, t) => Has(t, pl, "skeleton") && t.Overlay == FactionOverlayTileType.None,
                Run = (pl, a, b, o) =>
                {
                    var t = Board[a];
                    t.Units.Remove(t.Units.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "skeleton"));
                    PlaceOverlayTile(a, pl.NecromouserAwakened ? FactionOverlayTileType.Miasma_Revealed : FactionOverlayTileType.Miasma_Hidden, me);
                    MarkUsed("bad_news");
                    Log($"{pl.DisplayName} spread a Miasma Tile to {a}.");
                    return true;
                }
            });

            foreach (var entry in new[] { ("the_bloat", "The Bloat", 1), ("the_vanguard", "The Vanguard", 3), ("the_brute", "The Brute", 4) })
            {
                string uid = entry.Item1, uname = entry.Item2; int cost = entry.Item3;
                L.Add(new AbilitySpec
                {
                    Id = "summon:" + uid, Name = "Necromancy: " + uname, Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                    Summary = $"With the Necromouser on the board, one summon per turn (4 per game): remove {cost} Dead from the Cauldron, gain {cost} Authority and summon {uname} on his tile.",
                    Check = pl =>
                    {
                        var nt = TileOf(pl, "necromouser");
                        if (nt == null) return "The Necromouser isn't on the board.";
                        if (Uses("necromancy") > 0) return "You already summoned this turn.";
                        if (pl.NecromouserSpecialSummonsUsed >= 4) return "All 4 summons for this game are used.";
                        if (pl.NecromouserCauldronDeadCount < cost) return $"Needs {cost} Dead in the Cauldron.";
                        if (UnitSupplyLeft(pl, uid) <= 0) return $"No {uname} left in your supply.";
                        return CanAddUnitToTile(nt, me) ? null : "The Necromouser's tile is full (6 units).";
                    },
                    Run = (pl, a, b, o) =>
                    {
                        var nt = TileOf(pl, "necromouser");
                        pl.NecromouserCauldronDeadCount -= cost;
                        pl.NecromouserSpecialSummonsUsed++;
                        MarkUsed("necromancy");
                        SpawnUnit(nt.Coord, uid, pl, 1);
                        AwardAuthority(me, cost, $"Necromancy: summoned {uname}");
                        return true;
                    }
                });
            }

            foreach (var eq in p.Faction.Equipment)
            {
                var e = eq;
                L.Add(new AbilitySpec
                {
                    Id = "equip:" + e.Id, Name = "Equip " + e.Name, Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                    Summary = $"Main phase: pay {e.Cost} to craft and equip it. {e.EffectText}" + (e.Cost.Gold > 0 ? " Plague: spending Gold lets you place a Miasma Tile next to a Miasma Tile." : ""),
                    Prompt1 = $"Click a tile with a unit that can take the {e.Name}.",
                    Check = pl => !CanAfford(pl, e.Cost) ? $"Costs {e.Cost}." :
                                  EquippedOnBoard(pl, e.Id) >= e.SupplyCount ? $"All {e.SupplyCount} {e.Name} cards are in use." : null,
                    Legal1 = (pl, t) => t.Units.Any(u => u.OwnerPlayerIndex == me && CanTakeEquipment(u, e)),
                    Run = (pl, a, b, o) =>
                    {
                        var target = Board[a].Units.First(u => u.OwnerPlayerIndex == me && CanTakeEquipment(u, e));
                        EquipUnit(pl, target, e);
                        if (target.Definition.Id == "the_vanguard") MarkUsed("vanguard_equip:" + target.InstanceId);
                        if (e.Cost.Gold > 0) OfferPlagueMiasma(pl);
                        return true;
                    }
                });
            }
        }

        // ---------------- Firestarting Squirrels ----------------
        private void AddSquirrels(List<AbilitySpec> L, PlayerState p, int me)
        {
            Func<HexTileState, bool> fire = t => t.Overlay == FactionOverlayTileType.Fire;

            L.Add(new AbilitySpec
            {
                Id = "tar_pit", Name = "Tar Pit Token", Phase = TurnPhase.ExcavationPhase, Target = AbilityTarget.Tile,
                Summary = "Excavation phase: discard 2 Charcoal to place a Tar Pit Token on or next to a tile with a Squirrel. Fire there deals +1 HP; enemies need 2 Main Phase actions to leave.",
                Prompt1 = "Click a tile with (or next to) your Squirrel.",
                Check = pl => Count(pl, ResourceType.Charcoal) < 2 ? "Needs 2 Charcoal." :
                              TokenSupplyLeft(pl, TokenType.TarPit) <= 0 ? "All 3 Tar Pit Tokens are on the board." : null,
                Legal1 = (pl, t) => CanPlaceTokenOn(t, TokenType.TarPit) && (Has(t, pl, "squirrel") || Neighbors(t).Any(n => Has(n, pl, "squirrel"))),
                Run = (pl, a, b, o) =>
                {
                    RemoveResources(pl, ResourceType.Charcoal, 2);
                    PlaceToken(a, TokenType.TarPit, pl);
                    Log($"{pl.DisplayName} placed a Tar Pit Token at {a}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "craft_firestarter", Name = "Craft Acorn Firestarter", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Once per Main phase: discard 1 Charcoal and 1 Wood to craft an Acorn Firestarter (you have 3 cards).",
                Check = pl => Uses("craft_firestarter") > 0 ? "Already crafted one this Main phase." :
                              pl.SquirrelsCraftedFirestarters >= 3 ? "All 3 Acorn Firestarters are crafted." :
                              !CanAfford(pl, new ResourceCost(wood: 1, charcoal: 1)) ? "Needs 1 Charcoal and 1 Wood." : null,
                Run = (pl, a, b, o) =>
                {
                    SpendResources(pl, new ResourceCost(wood: 1, charcoal: 1));
                    pl.SquirrelsCraftedFirestarters++;
                    MarkUsed("craft_firestarter");
                    Log($"{pl.DisplayName} crafted an Acorn Firestarter ({pl.SquirrelsCraftedFirestarters} ready).");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "firestarter", Name = "Light an Acorn Firestarter", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Main phase: discard a crafted Acorn Firestarter to place a Fire Tile on a forest tile with a Squirrel.",
                Prompt1 = "Click a forest tile with your Squirrel.",
                Check = pl => Uses("firestarter") > 0 ? "Already used this Main phase." :
                              pl.SquirrelsCraftedFirestarters < 1 ? "No crafted Acorn Firestarter." :
                              FireTilesLeft(pl) <= 0 ? "All 6 Fire Tiles are on the board." : null,
                Legal1 = (pl, t) => CanTakeForestOverlay(t) && Has(t, pl, "squirrel"),
                Run = (pl, a, b, o) =>
                {
                    pl.SquirrelsCraftedFirestarters--;
                    PlaceFireTile(Board[a], me);
                    MarkUsed("firestarter");
                    Log($"{pl.DisplayName} lit a Fire Tile at {a}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "unquenchable", Name = "Unquenchable Spirit", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per Main phase, uses 1 Main Phase action: a Squirrel next to a Fire Tile loots it for 1 Charcoal.",
                Prompt1 = "Click a Fire Tile next to your Squirrel.",
                Check = pl => Uses("unquenchable") > 0 ? "Already used this Main phase." :
                              MainPhaseActionsRemaining < 1 ? "No Main Phase actions left." :
                              Count(pl, ResourceType.Charcoal) + BurnedResources.Count(r => r == ResourceType.Charcoal) >= 6 ? "All 6 Charcoal cards are out." : null,
                Legal1 = (pl, t) => fire(t) && Neighbors(t).Any(n => Has(n, pl, "squirrel")),
                Run = (pl, a, b, o) =>
                {
                    MainPhaseActionsRemaining--;
                    pl.ResourcePile.Add(ResourceType.Charcoal);
                    MarkUsed("unquenchable");
                    Log($"{pl.DisplayName} looted the Fire Tile at {a} for 1 Charcoal.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "disruption", Name = "Disruption", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Once per Main phase: burn 2 of your resource cards to burn 1 card from the Excavation Area and 1 random card from each other player's Resource Pile. Burning any Gold (or a relic that disassembles for Gold) gains 1 Authority.",
                Check = pl => Uses("disruption") > 0 ? "Already used this Main phase." : pl.ResourcePile.Count < 2 ? "Needs 2 resource cards." : null,
                Choices = (pl, a, b) => ExcavationArea.Count == 0
                    ? new List<string> { "Burn (Excavation Area is empty)" }
                    : ExcavationArea.Select(c => $"Burn {c.Name} from the Excavation Area" + (c.DisassembleGain.Gold > 0 ? " (Gold)" : "")).ToList(),
                Run = (pl, a, b, o) =>
                {
                    MarkUsed("disruption");
                    bool gold = DiscardCheapest(pl, 2, burn: true).Contains(ResourceType.Gold);
                    if (ExcavationArea.Count > 0)
                    {
                        var card = ExcavationArea[Mathf.Clamp(o, 0, ExcavationArea.Count - 1)];
                        ExcavationArea.Remove(card);
                        BurnedRelics.Add(card);
                        if (card.DisassembleGain.Gold > 0) gold = true;
                        Log($"Disruption burned {card.Name} from the Excavation Area.");
                    }
                    foreach (var op in Players.Where(x => x.PlayerIndex != me && x.ResourcePile.Count > 0))
                    {
                        int idx = _rng.Next(op.ResourcePile.Count);
                        var r = op.ResourcePile[idx];
                        op.ResourcePile.RemoveAt(idx);
                        BurnedResources.Add(r);
                        if (r == ResourceType.Gold) gold = true;
                        Log($"Disruption burned 1 {r} from {op.DisplayName}.");
                    }
                    if (gold) AwardAuthority(me, 1, "Disruption burned Gold");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "summon_pyromaniac", Name = "Summon the Pyromaniac", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per game: burn 1 Wood, 1 Stone, 1 Gold and 1 Charcoal to summon the Pyromaniac on a Fire Tile.",
                Prompt1 = "Click a Fire Tile.",
                Check = pl => pl.PyromaniacSummonedOnce ? "The Pyromaniac can only be summoned once per game." :
                              !CanAfford(pl, new ResourceCost(wood: 1, stone: 1, gold: 1, charcoal: 1)) ? "Needs 1 Wood, 1 Stone, 1 Gold and 1 Charcoal." : null,
                Legal1 = (pl, t) => fire(t) && CanAddUnitToTile(t, me),
                Run = (pl, a, b, o) =>
                {
                    foreach (var r in new[] { ResourceType.Wood, ResourceType.Stone, ResourceType.Gold, ResourceType.Charcoal })
                    { pl.ResourcePile.Remove(r); BurnedResources.Add(r); }
                    pl.PyromaniacSummonedOnce = true;
                    SpawnUnit(a, "pyromaniac", pl, 1);
                    Log($"{pl.DisplayName} summoned the Pyromaniac at {a}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "flameweaver", Name = "Flameweaver", Phase = TurnPhase.MainPhase, Target = AbilityTarget.TwoTiles,
                Summary = "Pyromaniac, once per Main phase: burn 2 cards from your Hand to move 1 Fire Tile to an adjacent forest tile (not counted as units entering it).",
                Prompt1 = "Click the Fire Tile to move.", Prompt2 = "Click the adjacent forest tile it moves to.",
                Check = pl => !OnBoard(pl, "pyromaniac") ? "The Pyromaniac isn't on the board." : Uses("flameweaver") > 0 ? "Already used this Main phase." :
                              pl.RelicHand.Count < 2 ? "Needs 2 cards in your Hand." : null,
                Legal1 = (pl, t) => fire(t) && t.OverlayOwnerPlayerIndex == me && Neighbors(t).Any(CanTakeForestOverlay),
                Legal2 = (pl, t1, t2) => HexCoord.Distance(t1.Coord, t2.Coord) == 1 && CanTakeForestOverlay(t2),
                Run = (pl, a, b, o) =>
                {
                    for (int i = 0; i < 2; i++) { BurnedRelics.Add(pl.RelicHand[0]); pl.RelicHand.RemoveAt(0); }
                    Board[a].Overlay = FactionOverlayTileType.None;
                    Board[a].OverlayOwnerPlayerIndex = -1;
                    PlaceFireTile(Board[b], me);
                    MarkUsed("flameweaver");
                    Log($"Flameweaver moved the Fire Tile from {a} to {b}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "pyromancy", Name = "Pyromancy", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Burn 1 Wood and 1 Gold to place a Fire Tile on the Pyromaniac's tile.",
                Check = pl =>
                {
                    var t = TileOf(pl, "pyromaniac");
                    if (t == null) return "The Pyromaniac isn't on the board.";
                    if (!CanTakeForestOverlay(t)) return "The Pyromaniac's tile can't take a Fire Tile.";
                    if (FireTilesLeft(pl) <= 0) return "All 6 Fire Tiles are on the board.";
                    return CanAfford(pl, new ResourceCost(wood: 1, gold: 1)) ? null : "Needs 1 Wood and 1 Gold.";
                },
                Run = (pl, a, b, o) =>
                {
                    var t = TileOf(pl, "pyromaniac");
                    foreach (var r in new[] { ResourceType.Wood, ResourceType.Gold }) { pl.ResourcePile.Remove(r); BurnedResources.Add(r); }
                    PlaceFireTile(t, me);
                    Log($"Pyromancy set the Pyromaniac's tile {t.Coord} on fire.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "oil_spill", Name = "Oil Spill", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Pyromaniac: burn 1 Charcoal to place a Tar Pit Token next to a Fire Tile or a Tar Pit Token.",
                Prompt1 = "Click a tile next to a Fire Tile or Tar Pit Token.",
                Check = pl => !OnBoard(pl, "pyromaniac") ? "The Pyromaniac isn't on the board." : Count(pl, ResourceType.Charcoal) < 1 ? "Needs 1 Charcoal." :
                              TokenSupplyLeft(pl, TokenType.TarPit) <= 0 ? "All 3 Tar Pit Tokens are on the board." : null,
                Legal1 = (pl, t) => CanPlaceTokenOn(t, TokenType.TarPit) &&
                                    Neighbors(t).Any(n => fire(n) || n.Tokens.Any(k => k.Type == TokenType.TarPit)),
                Run = (pl, a, b, o) =>
                {
                    pl.ResourcePile.Remove(ResourceType.Charcoal);
                    BurnedResources.Add(ResourceType.Charcoal);
                    PlaceToken(a, TokenType.TarPit, pl);
                    Log($"Oil Spill placed a Tar Pit Token at {a}.");
                    return true;
                }
            });
        }

        // ---------------- The Capybara Caravan ----------------
        private void AddCapybara(List<AbilitySpec> L, PlayerState p, int me)
        {
            Func<HexTileState, bool> ownWallow = t => t.Building != null && t.Building.Type == BuildingType.Wallow && t.Building.OwnerPlayerIndex == me;

            L.Add(new AbilitySpec
            {
                Id = "trader_return", Name = "Bring Back the Capybara Trader", Phase = TurnPhase.ExcavationPhase, Target = AbilityTarget.Tile,
                Summary = "If the Trader isn't on the board: discard 2 relic cards from your Hand to place the Trader at a Wallow.",
                Prompt1 = "Click one of your Wallows.",
                Check = pl => OnBoard(pl, "capybara_trader") ? "The Trader is already on the board." : pl.RelicHand.Count < 2 ? "Needs 2 relic cards in your Hand." : null,
                Legal1 = (pl, t) => ownWallow(t) && CanAddUnitToTile(t, me),
                Run = (pl, a, b, o) =>
                {
                    for (int i = 0; i < 2; i++) { RelicDiscardPile.Add(pl.RelicHand[0]); pl.RelicHand.RemoveAt(0); }
                    SpawnUnit(a, "capybara_trader", pl, 1);
                    Log($"{pl.DisplayName} brought the Capybara Trader back at {a}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "capy_forage", Name = "Capybara Forage", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Once per Main phase with a Capybara on a stream tile: draw 1 relic deck card.",
                Check = pl => Uses("capy_forage") > 0 ? "Already used this Main phase." :
                              !Board.Values.Any(t => t.EffectiveTerrain == TileTerrainType.Stream && Has(t, pl, "capybara")) ? "Needs a Capybara on a stream tile." : null,
                Run = (pl, a, b, o) =>
                {
                    var d = DrawRelicCard();
                    if (d != null) pl.RelicHand.Add(d);
                    MarkUsed("capy_forage");
                    Log($"{pl.DisplayName} foraged the stream bed and drew a relic card.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "peddling", Name = "Peddling", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Once per turn, free: move the Capybara Trader up to 2 land tiles, or up to 3 tiles along streams.",
                Prompt1 = "Click where the Trader goes.",
                Check = pl =>
                {
                    if (Uses("peddling") > 0) return "The Trader already peddled this turn.";
                    var tr = AllUnits.FirstOrDefault(u => u.OwnerPlayerIndex == me && u.Definition.Id == "capybara_trader");
                    if (tr == null) return "The Trader isn't on the board.";
                    return tr.CannotMoveThisTurn ? "The Trader can't move this turn." : null;
                },
                Legal1 = (pl, t) => TraderReachable(pl).Contains(t.Coord) && CanAddUnitToTile(t, me),
                Run = (pl, a, b, o) =>
                {
                    var from = TileOf(pl, "capybara_trader");
                    var tr = from.Units.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "capybara_trader");
                    RelocateUnits(from, Board[a], new List<UnitInstance> { tr });
                    tr.HasMovedThisTurn = true;
                    MarkUsed("peddling");
                    Log($"The Capybara Trader peddled from {from.Coord} to {a}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "summon_riverkeeper", Name = "Summon the Riverkeeper", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "With 2+ Wallows on the board: discard 1 Stone and 2 Gold to summon the Riverkeeper on a Wallow.",
                Prompt1 = "Click one of your Wallows.",
                Check = pl => OnBoard(pl, "riverkeeper") ? "The Riverkeeper is already on the board." :
                              Board.Values.Count(ownWallow) < 2 ? "Needs 2 Wallows on the board." :
                              !CanAfford(pl, new ResourceCost(stone: 1, gold: 2)) ? "Needs 1 Stone and 2 Gold." : null,
                Legal1 = (pl, t) => ownWallow(t) && CanAddUnitToTile(t, me),
                Run = (pl, a, b, o) =>
                {
                    RemoveResources(pl, ResourceType.Stone, 1);
                    RemoveResources(pl, ResourceType.Gold, 2);
                    SpawnUnit(a, "riverkeeper", pl, 1);
                    WelcomeGuestsAtRiverkeeper(Board[a]);   // Watering Hole case 4
                    Log($"{pl.DisplayName} summoned the Riverkeeper at {a}.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "hospitality", Name = "Hospitality", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Riverkeeper, once per Main phase: a guest on its tile draws 1 relic card and you gain 1 Authority.",
                Check = pl =>
                {
                    if (Uses("hospitality") > 0) return "Already used this Main phase.";
                    var rk = TileOf(pl, "riverkeeper");
                    if (rk == null) return "The Riverkeeper isn't on the board.";
                    return rk.Units.Any(u => u.OwnerPlayerIndex != me && u.IsGuest) ? null : "No guest on the Riverkeeper's tile.";
                },
                Choices = (pl, a, b) =>
                {
                    var rk = TileOf(pl, "riverkeeper");
                    return rk == null ? new List<string>() :
                        rk.Units.Where(u => u.OwnerPlayerIndex != me && u.IsGuest).Select(u => u.OwnerPlayerIndex).Distinct().Select(s => $"Host {Players[s].DisplayName}").ToList();
                },
                Run = (pl, a, b, o) =>
                {
                    var rk = TileOf(pl, "riverkeeper");
                    var guests = rk.Units.Where(u => u.OwnerPlayerIndex != me && u.IsGuest).Select(u => u.OwnerPlayerIndex).Distinct().ToList();
                    var guest = Players[guests[Mathf.Clamp(o, 0, guests.Count - 1)]];
                    var drawn = DrawRelicCard();
                    if (drawn != null) guest.RelicHand.Add(drawn);
                    AwardAuthority(me, 1, "Hospitality");
                    MarkUsed("hospitality");
                    Log($"The Riverkeeper hosted {guest.DisplayName}, who drew a relic card.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "riverkeeper_commerce", Name = "Commerce", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Riverkeeper, any number of times in your Main phase: discard 2 Gold to heal the Riverkeeper 1 HP.",
                Check = pl =>
                {
                    var rk = AllUnits.FirstOrDefault(u => u.OwnerPlayerIndex == me && u.Definition.Id == "riverkeeper");
                    if (rk == null) return "The Riverkeeper isn't on the board.";
                    if (rk.CurrentHP >= rk.MaxHP) return "The Riverkeeper is at full HP.";
                    return Count(pl, ResourceType.Gold) < 2 ? "Needs 2 Gold." : null;
                },
                Run = (pl, a, b, o) =>
                {
                    var rk = AllUnits.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "riverkeeper");
                    RemoveResources(pl, ResourceType.Gold, 2);
                    rk.CurrentHP = Mathf.Min(rk.MaxHP, rk.CurrentHP + 1);
                    Log($"{pl.DisplayName} paid 2 Gold: the Riverkeeper healed to {rk.CurrentHP} HP (Commerce).");
                    return true;
                }
            });
        }

        /// <summary>Peddling: up to 2 tiles over any terrain, or up to 3 steps along stream tiles.</summary>
        private HashSet<HexCoord> TraderReachable(PlayerState p)
        {
            var result = new HashSet<HexCoord>();
            var from = TileOf(p, "capybara_trader");
            if (from == null) return result;
            foreach (var t in Board.Values)
            {
                int d = HexCoord.Distance(t.Coord, from.Coord);
                if (d >= 1 && d <= 2) result.Add(t.Coord);
            }
            if (from.EffectiveTerrain == TileTerrainType.Stream)
            {
                var frontier = new List<HexCoord> { from.Coord };
                var seen = new HashSet<HexCoord> { from.Coord };
                for (int step = 0; step < 3; step++)
                {
                    var next = new List<HexCoord>();
                    foreach (var c in frontier)
                        foreach (var n in c.GetNeighbors())
                            if (!seen.Contains(n) && Board.TryGetValue(n, out var nt) && nt.EffectiveTerrain == TileTerrainType.Stream)
                            { seen.Add(n); next.Add(n); result.Add(n); }
                    frontier = next;
                }
            }
            result.Remove(from.Coord);
            return result;
        }

        // ---------------- The Briared Flower ----------------
        private void AddBriar(List<AbilitySpec> L, PlayerState p, int me)
        {
            Func<HexTileState, bool> ownThicket = t => t.Building != null && t.Building.Type == BuildingType.PricklyThicket && t.Building.OwnerPlayerIndex == me;
            Func<int> barrageRange = () =>
            {
                var bt = TileOf(p, "blooming_briar");
                return bt == null ? 0 : 4 + Mathf.Min(2, bt.Units.Count(u => u.OwnerPlayerIndex == me && u.Definition.Id == "thorned_seed"));
            };

            L.Add(new AbilitySpec
            {
                Id = "barrage", Name = "Thistle Barrage", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Up to twice per Main phase (shared with the structure option), with 1+ Thorns: pick a tile with enemy units within 4 tiles of the Briar (+1 per Seed on its tile, max +2). Spend 1 Thorn and 1 resource per unit to deal 1 HP each, then place that many Thorned Seeds on a forest tile next to it. 1 Authority per 2 HP dealt.",
                Prompt1 = "Click a tile with enemy units in range of the Blooming Briar.",
                Check = pl => !OnBoard(pl, "blooming_briar") ? "The Blooming Briar isn't on the board." : pl.BriaredFlowerThorns < 1 ? "Needs 1 Thorn." :
                              Uses("barrage") >= 2 ? "Thistle Barrage was already used twice this Main phase." : pl.ResourcePile.Count < 1 ? "Needs 1 resource card per target." : null,
                Legal1 = (pl, t) =>
                {
                    var bt = TileOf(pl, "blooming_briar");
                    return bt != null && HexCoord.Distance(bt.Coord, t.Coord) <= barrageRange() && HasEnemyUnits(t, me);
                },
                Choices = (pl, a, b) =>
                {
                    if (!Board.TryGetValue(a, out var t)) return new List<string>();
                    int max = Mathf.Min(t.Units.Count(u => u.OwnerPlayerIndex != me), Mathf.Min(pl.BriaredFlowerThorns, pl.ResourcePile.Count));
                    var opts = new List<string>();
                    for (int k = max; k >= 1; k--) opts.Add(k == 1 ? "Hit 1 unit (1 Thorn, 1 resource)" : $"Hit {k} units ({k} Thorns, {k} resources)");
                    return opts;
                },
                Run = (pl, a, b, o) =>
                {
                    var t = Board[a];
                    var enemies = t.Units.Where(u => u.OwnerPlayerIndex != me).OrderBy(u => u.CurrentHP).ThenBy(u => u.Definition.IsSpecialUnit ? 0 : 1).ToList();
                    int max = Mathf.Min(enemies.Count, Mathf.Min(pl.BriaredFlowerThorns, pl.ResourcePile.Count));
                    int k = Mathf.Clamp(max - o, 1, max);
                    MarkUsed("barrage");
                    pl.BriaredFlowerThorns -= k;
                    DiscardCheapest(pl, k);
                    foreach (var enemy in enemies.Take(k))
                    {
                        enemy.CurrentHP -= 1;
                        if (enemy.CurrentHP <= 0) RemoveUnitFromBoard(t, enemy, me);
                    }
                    var seedTile = Neighbors(t).FirstOrDefault(n => n.EffectiveTerrain == TileTerrainType.Forest && CanAddUnitToTile(n, me));
                    if (seedTile != null) SpawnUnit(seedTile.Coord, "thorned_seed", pl, k);
                    Log($"Thistle Barrage hit {k} unit(s) at {a}" + (seedTile != null ? $"; Thorned Seeds sprouted at {seedTile.Coord}." : "."));
                    ScoreThistleDamage(pl, k);
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "barrage_clear", Name = "Thistle Barrage: clear structures", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Counts as a Thistle Barrage use: spend 1 Thorn to remove all enemy buildings and tokens on a tile within 3 tiles of the Briar when that faction has no units there (1 Authority each).",
                Prompt1 = "Click a tile within 3 tiles with unguarded enemy buildings or tokens.",
                Check = pl => !OnBoard(pl, "blooming_briar") ? "The Blooming Briar isn't on the board." : pl.BriaredFlowerThorns < 1 ? "Needs 1 Thorn." :
                              Uses("barrage") >= 2 ? "Thistle Barrage was already used twice this Main phase." : null,
                Legal1 = (pl, t) =>
                {
                    var bt = TileOf(pl, "blooming_briar");
                    if (bt == null || HexCoord.Distance(bt.Coord, t.Coord) > 3) return false;
                    return UnguardedStructures(t, me).Any() || (t.Building != null && t.Building.OwnerPlayerIndex != me && !t.Units.Any(u => u.OwnerPlayerIndex == t.Building.OwnerPlayerIndex));
                },
                Run = (pl, a, b, o) =>
                {
                    var t = Board[a];
                    MarkUsed("barrage");
                    pl.BriaredFlowerThorns--;
                    if (t.Building != null && t.Building.OwnerPlayerIndex != me && !t.Units.Any(u => u.OwnerPlayerIndex == t.Building.OwnerPlayerIndex))
                        RemoveStructureForAuthority(t, null, true, pl);
                    foreach (var tk in UnguardedStructures(t, me).ToList())
                        RemoveStructureForAuthority(t, tk, false, pl);
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "consume_thicket", Name = "Consume Prickly Thicket", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Once per Main phase, if the Blooming Briar is on a tile with a Prickly Thicket: remove the Thicket to give the Briar 1 Thorn.",
                Check = pl =>
                {
                    if (Uses("consume_thicket") > 0) return "Already used this Main phase.";
                    var bt = TileOf(pl, "blooming_briar");
                    if (bt == null) return "The Blooming Briar isn't on the board.";
                    if (!ownThicket(bt)) return "The Briar isn't on a Prickly Thicket.";
                    return pl.BriaredFlowerThorns >= 6 ? "The Briar already has 6 Thorns." : null;
                },
                Run = (pl, a, b, o) =>
                {
                    var bt = TileOf(pl, "blooming_briar");
                    bt.Building = null;
                    pl.BriaredFlowerThorns = Mathf.Min(6, pl.BriaredFlowerThorns + 1);
                    MarkUsed("consume_thicket");
                    Log($"The Blooming Briar consumed its Prickly Thicket: {pl.BriaredFlowerThorns}/6 Thorns.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "sun_eater", Name = "Sun Eater", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Once per turn, with 1+ Thorns: remove 2 Thorned Seeds on or next to the Briar's tile and 3 resource cards to gain 2 Thorns.",
                Check = pl =>
                {
                    if (Uses("sun_eater") > 0) return "Already used this turn.";
                    var bt = TileOf(pl, "blooming_briar");
                    if (bt == null) return "The Blooming Briar isn't on the board.";
                    if (pl.BriaredFlowerThorns < 1) return "Needs 1 Thorn (at 0 Thorns the Briar is in Frenzied Bloom).";
                    if (pl.BriaredFlowerThorns >= 6) return "The Briar already has 6 Thorns.";
                    if (pl.ResourcePile.Count < 3) return "Needs 3 resource cards.";
                    int seeds = bt.Units.Count(u => u.OwnerPlayerIndex == me && u.Definition.Id == "thorned_seed") +
                                Neighbors(bt).Sum(n => n.Units.Count(u => u.OwnerPlayerIndex == me && u.Definition.Id == "thorned_seed"));
                    return seeds < 2 ? "Needs 2 Thorned Seeds on or next to the Briar's tile." : null;
                },
                Run = (pl, a, b, o) =>
                {
                    var bt = TileOf(pl, "blooming_briar");
                    int removed = 0;
                    foreach (var t in new[] { bt }.Concat(Neighbors(bt)))
                        while (removed < 2 && Has(t, pl, "thorned_seed"))
                        { t.Units.Remove(t.Units.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "thorned_seed")); removed++; }
                    DiscardCheapest(pl, 3);
                    pl.BriaredFlowerThorns = Mathf.Min(6, pl.BriaredFlowerThorns + 2);
                    MarkUsed("sun_eater");
                    Log($"Sun Eater: the Briar regrew to {pl.BriaredFlowerThorns}/6 Thorns.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "bloom", Name = "Bursting Into Bloom", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "Remove up to 3 Thorned Seeds (click them one at a time). Each deals 1 HP to 1 enemy unit on its tile. Gain 1 Authority if you removed an enemy unit.",
                Prompt1 = "Click a tile with your Thorned Seed.",
                Check = pl => Uses("bloom") >= 3 ? "Already removed 3 Seeds this turn." : null,
                Legal1 = (pl, t) => Has(t, pl, "thorned_seed"),
                Run = (pl, a, b, o) =>
                {
                    var t = Board[a];
                    t.Units.Remove(t.Units.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "thorned_seed"));
                    MarkUsed("bloom");
                    var victim = t.Units.Where(u => u.OwnerPlayerIndex != me).OrderBy(u => u.CurrentHP).FirstOrDefault();
                    if (victim != null)
                    {
                        victim.CurrentHP -= 1;
                        Log($"A Thorned Seed burst at {a}: {victim.Definition.Name} took 1 HP.");
                        if (victim.CurrentHP <= 0)
                        {
                            RemoveUnitFromBoard(t, victim, me);
                            if (Uses("bloom_scored") == 0) { MarkUsed("bloom_scored"); AwardAuthority(me, 1, "Bursting Into Bloom removed an enemy unit"); }
                        }
                    }
                    else Log($"A Thorned Seed burst at {a} (no enemy units there).");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "lashing", Name = "Furious Lashing", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None,
                Summary = "Frenzied Bloom (0 Thorns), once per Main phase: deal 1 HP to every enemy unit within 3 tiles of the Briar. 1 Authority per 2 HP dealt.",
                Check = pl => !OnBoard(pl, "blooming_briar") ? "The Blooming Briar isn't on the board." : pl.BriaredFlowerThorns > 0 ? "Only at 0 Thorns (Frenzied Bloom)." :
                              Uses("lashing") > 0 ? "Already used this Main phase." : null,
                Run = (pl, a, b, o) =>
                {
                    var bt = TileOf(pl, "blooming_briar");
                    int dealt = 0;
                    foreach (var t in Board.Values.Where(x => HexCoord.Distance(x.Coord, bt.Coord) <= 3).ToList())
                        foreach (var enemy in t.Units.Where(u => u.OwnerPlayerIndex != me).ToList())
                        {
                            enemy.CurrentHP -= 1;
                            dealt++;
                            if (enemy.CurrentHP <= 0) RemoveUnitFromBoard(t, enemy, me);
                        }
                    MarkUsed("lashing");
                    if (dealt >= 2) AwardAuthority(me, dealt / 2, "Furious Lashing");
                    Log($"Furious Lashing dealt {dealt} HP within 3 tiles.");
                    return true;
                }
            });

            L.Add(new AbilitySpec
            {
                Id = "half_bloom", Name = "In Half Bloom", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile,
                Summary = "If the Briar isn't on the board: remove 2 resource cards and 3 Thorned Seeds to place the Blooming Briar with 2 Thorns on a Prickly Thicket.",
                Prompt1 = "Click one of your Prickly Thickets.",
                Check = pl => OnBoard(pl, "blooming_briar") ? "The Blooming Briar is already on the board." : pl.ResourcePile.Count < 2 ? "Needs 2 resource cards." :
                              AllUnits.Count(u => u.OwnerPlayerIndex == me && u.Definition.Id == "thorned_seed") < 3 ? "Needs 3 Thorned Seeds on the board." : null,
                Legal1 = (pl, t) => ownThicket(t),
                Run = (pl, a, b, o) =>
                {
                    int removed = 0;
                    foreach (var t in Board.Values.OrderBy(t => t.Coord.Equals(a) ? 1 : 0))
                        while (removed < 3 && Has(t, pl, "thorned_seed"))
                        { t.Units.Remove(t.Units.First(u => u.OwnerPlayerIndex == me && u.Definition.Id == "thorned_seed")); removed++; }
                    DiscardCheapest(pl, 2);
                    pl.BriaredFlowerThorns = 2;
                    SpawnUnit(a, "blooming_briar", pl, 1);
                    Log($"In Half Bloom: the Blooming Briar rose again at {a} with 2 Thorns.");
                    return true;
                }
            });
        }

        private static IEnumerable<TokenInstance> UnguardedStructures(HexTileState t, int me) =>
            t.Tokens.Where(k => k.OwnerPlayerIndex != me && k.Type != TokenType.Coin && !t.Units.Any(u => u.OwnerPlayerIndex == k.OwnerPlayerIndex));

        // ---------------- Abilities any player can use ----------------
        private void AddGeneral(List<AbilitySpec> L, PlayerState p, int me)
        {
            var horde = Players.FirstOrDefault(x => x.Faction != null && x.Faction.Id == FactionId.HordeOfTheMillions);
            if (horde != null)
            {
                int hs = horde.PlayerIndex;
                Func<HexTileState, bool> hordeLaunchable = t => t.Units.Any(u => u.OwnerPlayerIndex == hs && (u.Definition.Id == "millions" || u.Definition.Id == "king_of_the_millions"));

                L.Add(new AbilitySpec
                {
                    Id = "catapult_craft", Name = "Craft a Catapult", Phase = TurnPhase.ExcavationPhase, Target = AbilityTarget.Tile, General = true,
                    Summary = "Any player, free craft while one is in the supply (2 in the game): discard 2 Stone and 1 Wood to place a Catapult Token on a tile with a Millions unit and at least one of your units. It has 2 uses, once per turn.",
                    Prompt1 = "Click a tile with a Millions unit and your unit.",
                    Check = pl => TokenSupplyLeft(pl, TokenType.Catapult) <= 0 ? "Both Catapults are on the board." :
                                  !CanAfford(pl, new ResourceCost(wood: 1, stone: 2)) ? "Needs 2 Stone and 1 Wood." : null,
                    Legal1 = (pl, t) => t.Units.Any(u => u.OwnerPlayerIndex == hs && u.Definition.Id == "millions") &&
                                        t.Units.Any(u => u.OwnerPlayerIndex == me) && CanPlaceTokenOn(t, TokenType.Catapult),
                    Run = (pl, a, b, o) =>
                    {
                        SpendResources(pl, new ResourceCost(wood: 1, stone: 2));
                        PlaceToken(a, TokenType.Catapult, pl);
                        Log($"{pl.DisplayName} built a Catapult at {a}.");
                        return true;
                    }
                });

                L.Add(new AbilitySpec
                {
                    Id = "catapult_fire", Name = "Fire your Catapult", Phase = TurnPhase.MainPhase, Target = AbilityTarget.TwoTiles, General = true,
                    Summary = "Free, once per turn per Catapult: launch up to 3 Millions units (and/or the King) from the Catapult's tile up to 3 tiles in a straight line. Everyone except the Horde with a piece where they land draws a Forage card. Non-Horde owners gain 1 Authority.",
                    Prompt1 = "Click your Catapult's tile.", Prompt2 = "Click the landing tile (straight line, up to 3 tiles).",
                    Check = pl => !Board.Values.Any(t => t.Tokens.Any(k => k.Type == TokenType.Catapult && k.OwnerPlayerIndex == me)) ? "You don't own a Catapult on the board." : null,
                    Legal1 = (pl, t) => t.Tokens.Any(k => k.Type == TokenType.Catapult && k.OwnerPlayerIndex == me) &&
                                        Uses($"catapult@{t.Coord.q},{t.Coord.r}") == 0 && hordeLaunchable(t),
                    Legal2 = (pl, t1, t2) =>
                    {
                        if (!CanAddUnitToTile(t2, hs)) return false;
                        for (int dir = 0; dir < 6; dir++)
                        {
                            var c = t1.Coord;
                            for (int step = 1; step <= 3; step++)
                            {
                                c = c.Neighbor(dir);
                                if (c.Equals(t2.Coord)) return true;
                            }
                        }
                        return false;
                    },
                    Choices = (pl, a, b) =>
                    {
                        if (!Board.TryGetValue(a, out var t)) return new List<string>();
                        bool king = t.Units.Any(u => u.OwnerPlayerIndex == hs && u.Definition.Id == "king_of_the_millions");
                        bool millions = t.Units.Any(u => u.OwnerPlayerIndex == hs && u.Definition.Id == "millions");
                        var opts = new List<string>();
                        if (millions) opts.Add("Launch up to 3 Millions units");
                        if (king) opts.Add(millions ? "Launch the King and up to 2 Millions units" : "Launch the King");
                        return opts;
                    },
                    Run = (pl, a, b, o) =>
                    {
                        var from = Board[a];
                        var to = Board[b];
                        bool millions = from.Units.Any(u => u.OwnerPlayerIndex == hs && u.Definition.Id == "millions");
                        bool withKing = !millions || o == 1;
                        int room = MaxUnitsPerFactionPerTile - to.Units.Count(u => u.OwnerPlayerIndex == hs);
                        var group = new List<UnitInstance>();
                        if (withKing) group.AddRange(from.Units.Where(u => u.OwnerPlayerIndex == hs && u.Definition.Id == "king_of_the_millions"));
                        group.AddRange(from.Units.Where(u => u.OwnerPlayerIndex == hs && u.Definition.Id == "millions"));
                        group = group.Take(Mathf.Min(3, room)).ToList();
                        if (group.Count == 0) return false;
                        RelocateUnits(from, to, group);
                        ApplyFireEntryDamage(to, group, hs);
                        var cat = from.Tokens.First(k => k.Type == TokenType.Catapult && k.OwnerPlayerIndex == me);
                        cat.RemainingUses--;
                        MarkUsed($"catapult@{a.q},{a.r}");
                        Log($"{pl.DisplayName}'s Catapult launched {group.Count} unit(s) from {a} to {b}.");
                        if (cat.RemainingUses <= 0) { from.Tokens.Remove(cat); Log("The Catapult is used up and goes back to the supply."); }
                        foreach (var op in Players.Where(x => x.PlayerIndex != hs))
                        {
                            bool piece = to.Units.Any(u => u.OwnerPlayerIndex == op.PlayerIndex) ||
                                         (to.Building != null && to.Building.OwnerPlayerIndex == op.PlayerIndex) ||
                                         to.Tokens.Any(k => k.OwnerPlayerIndex == op.PlayerIndex);
                            if (piece) DrawAndResolveForageCard(op);
                        }
                        if (me != hs) AwardAuthority(me, 1, "Used a Catapult");
                        return true;
                    }
                });
            }

            var squirrels = Players.FirstOrDefault(x => x.Faction != null && x.Faction.Id == FactionId.FirestartingSquirrels);
            if (squirrels != null && squirrels.PlayerIndex != me)
            {
                L.Add(new AbilitySpec
                {
                    Id = "extinguish", Name = "Put out a Fire Tile", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile, General = true,
                    Summary = "With your unit on a Fire Tile: discard 2 Stone to remove that Fire Tile and gain 1 Authority.",
                    Prompt1 = "Click a Fire Tile with your unit on it.",
                    Check = pl => Count(pl, ResourceType.Stone) < 2 ? "Needs 2 Stone." : null,
                    Legal1 = (pl, t) => t.Overlay == FactionOverlayTileType.Fire && t.Units.Any(u => u.OwnerPlayerIndex == me),
                    Run = (pl, a, b, o) =>
                    {
                        RemoveResources(pl, ResourceType.Stone, 2);
                        Board[a].Overlay = FactionOverlayTileType.None;
                        Board[a].OverlayOwnerPlayerIndex = -1;
                        AwardAuthority(me, 1, "Put out a Fire Tile");
                        return true;
                    }
                });
            }
        }

        // ---------------- Traded Goods bought from the Capybara Trader ----------------
        private void AddTradedGoods(List<AbilitySpec> L, PlayerState p, int me)
        {
            Func<string, bool> has = id => p.PurchasedTradedGoods.Any(g => g.Id == id);
            Action<string> spend = id =>
            {
                var g = p.PurchasedTradedGoods.First(x => x.Id == id);
                p.PurchasedTradedGoods.Remove(g);
                TradedGoodsDiscard.Add(g);
            };
            var capy = Players.FirstOrDefault(x => x.Faction != null && x.Faction.Id == FactionId.CapybaraCaravan);

            if (has("tg_prayer_beads"))
                L.Add(new AbilitySpec
                {
                    Id = "tg_prayer_beads", Name = "Prayer Beads (Traded Good)", Phase = TurnPhase.ExcavationPhase, Target = AbilityTarget.None, General = true,
                    Summary = "Draw the top 2 cards of the relic deck, then discard this card.",
                    Run = (pl, a, b, o) =>
                    {
                        for (int i = 0; i < 2; i++) { var d = DrawRelicCard(); if (d != null) pl.RelicHand.Add(d); }
                        spend("tg_prayer_beads");
                        Log($"{pl.DisplayName} used Prayer Beads and drew 2 relic cards.");
                        return true;
                    }
                });

            if (has("tg_silver_spoon"))
                L.Add(new AbilitySpec
                {
                    Id = "tg_silver_spoon", Name = "Silver Spoon (Traded Good)", Phase = TurnPhase.ExcavationPhase, Target = AbilityTarget.None, General = true,
                    Summary = "Add 2 relic cards from the Discard Area to your Hand; you and the Capybara player each gain 1 Authority.",
                    Check = pl => RelicDiscardPile.Count == 0 ? "The Discard Area is empty." : null,
                    Run = (pl, a, b, o) =>
                    {
                        for (int i = 0; i < 2 && RelicDiscardPile.Count > 0; i++)
                        {
                            pl.RelicHand.Add(RelicDiscardPile[RelicDiscardPile.Count - 1]);
                            RelicDiscardPile.RemoveAt(RelicDiscardPile.Count - 1);
                        }
                        spend("tg_silver_spoon");
                        AwardAuthority(me, 1, "Silver Spoon");
                        if (capy != null) AwardAuthority(capy.PlayerIndex, 1, "Silver Spoon");
                        return true;
                    }
                });

            if (has("tg_broken_pickaxe"))
                L.Add(new AbilitySpec
                {
                    Id = "tg_broken_pickaxe", Name = "Broken Pickaxe (Traded Good)", Phase = TurnPhase.ExcavationPhase, Phase2 = TurnPhase.MainPhase,
                    Target = AbilityTarget.None, General = true,
                    Summary = "Add 1 relic card from the Discard Area to your Hand, then discard this card.",
                    Check = pl => RelicDiscardPile.Count == 0 ? "The Discard Area is empty." : null,
                    Run = (pl, a, b, o) =>
                    {
                        pl.RelicHand.Add(RelicDiscardPile[RelicDiscardPile.Count - 1]);
                        RelicDiscardPile.RemoveAt(RelicDiscardPile.Count - 1);
                        spend("tg_broken_pickaxe");
                        Log($"{pl.DisplayName} used the Broken Pickaxe to take a relic card from the Discard Area.");
                        return true;
                    }
                });

            if (has("tg_healing_salve"))
                L.Add(new AbilitySpec
                {
                    Id = "tg_healing_salve", Name = "Healing Salve (Traded Good)", Phase = TurnPhase.MainPhase, Target = AbilityTarget.Tile, General = true,
                    Summary = "Equip to one of your units: it heals 1 HP, then discard this card.",
                    Prompt1 = "Click a tile with your hurt unit.",
                    Legal1 = (pl, t) => t.Units.Any(u => u.OwnerPlayerIndex == me && u.CurrentHP < u.MaxHP),
                    Run = (pl, a, b, o) =>
                    {
                        var u = Board[a].Units.Where(x => x.OwnerPlayerIndex == me && x.CurrentHP < x.MaxHP)
                                    .OrderByDescending(x => x.Definition.IsSpecialUnit).First();   // special units first
                        u.CurrentHP++;
                        spend("tg_healing_salve");
                        Log($"{pl.DisplayName} used the Healing Salve: {u.Definition.Name} healed to {u.CurrentHP} HP.");
                        return true;
                    }
                });

            if (has("tg_horse_hair_brush"))
                L.Add(new AbilitySpec
                {
                    Id = "tg_horse_hair_brush", Name = "Horse Hair Brush (Traded Good)", Phase = TurnPhase.MainPhase, Target = AbilityTarget.None, General = true,
                    Summary = "Discard this card: your next unearth or assemble costs 1 less resource.",
                    Check = pl => pl.HorseHairBrushReady ? "A brush discount is already waiting." : null,
                    Run = (pl, a, b, o) =>
                    {
                        spend("tg_horse_hair_brush");
                        pl.HorseHairBrushReady = true;
                        Log($"{pl.DisplayName} used the Horse Hair Brush: their next unearth or assemble costs 1 less.");
                        return true;
                    }
                });

            if (has("tg_clay_edict"))
                L.Add(new AbilitySpec
                {
                    Id = "tg_clay_edict", Name = "Clay Edict (Traded Good)", Phase = TurnPhase.MainPhase,
                    Target = AbilityTarget.Tile, General = true,
                    Summary = "Equip to one of your units: it gets +1 tile movement this turn, then discard this card.",
                    Prompt1 = "Click a tile with your unit.",
                    Legal1 = (pl, t) => t.Units.Any(u => u.OwnerPlayerIndex == me && u.ClayEdictTurns == 0),
                    Run = (pl, a, b, o) =>
                    {
                        var u = Board[a].Units.Where(x => x.OwnerPlayerIndex == me && x.ClayEdictTurns == 0)
                                    .OrderByDescending(x => x.Definition.IsSpecialUnit).First();
                        u.ClayEdictTurns = 1;   // counts down at your next turn's start, so it lasts this turn
                        spend("tg_clay_edict");
                        Log($"{pl.DisplayName} equipped the Clay Edict to {u.Definition.Name} (+1 movement this turn).");
                        return true;
                    }
                });
        }

        private void PlaceFireTile(HexTileState t, int ownerSeat)
        {
            t.Overlay = FactionOverlayTileType.Fire;
            t.OverlayOwnerPlayerIndex = ownerSeat;
        }
    }
}
