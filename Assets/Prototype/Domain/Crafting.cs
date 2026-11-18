using System.Linq;
using UnityEngine;

namespace DungeonsNShops
{
    public sealed partial class GameWorld
    {
        public const int MaxReagents = 6;
        public const int PressesToGrind = 3;

        private string Craft(PlayerState player, GameAction action)
        {
            if (action.kind == ActionKind.BeginCraft)
            {
                if (!At(player, Site.Bench)) return "error.reach";
                if (State.craft != null || player.ready || player.night == NightChoice.Sleeping) return "error.busy";
                if (!ValidIngredients(player, action.ingredients)) return "error.ingredients";
                State.craft = new StationSession
                {
                    id = State.nextId++,
                    owner = player.id,
                    ingredients = (int[])action.ingredients.Clone()
                };
                for (int item = 0; item < Catalog.Count; item++)
                    for (int n = 0; n < action.ingredients[item]; n++)
                        State.craft.pieces.Add(new ReagentPiece { item = item });
                if (State.phase == DayPhase.Night && player.night == NightChoice.Free)
                    player.night = NightChoice.Working;
                return null;
            }

            StationSession session = State.craft;
            if (session == null || session.owner != player.id || action.value != session.id)
                return "error.busy";

            switch (action.kind)
            {
                case ActionKind.CancelCraft:
                    State.craft = null;
                    break;
                case ActionKind.CraftZone:
                case ActionKind.GrindIngredient:
                case ActionKind.AddIngredient:
                    return ProcessReagent(session, action.kind, action.extra);
                case ActionKind.Stir:
                    if (session.step != CraftStep.Stir || action.extra is < 1 or > 250) return "error.sequence";
                    session.stir += Mathf.Min(action.extra / 1000f, session.elapsed - session.lastStirSample);
                    session.lastStirSample = session.elapsed;
                    break;
                case ActionKind.FinishCraft:
                    if (session.step != CraftStep.Stir || session.stir < 0.1f) return "error.sequence";
                    if (!ValidIngredients(player, session.ingredients)) return "error.ingredients";
                    FinishBrew(player, session);
                    break;
            }

            return null;
        }

        private static string ProcessReagent(StationSession session, ActionKind action, int index)
        {
            if (session.step != CraftStep.Ingredients || index < 0 || index >= session.pieces.Count)
                return "error.sequence";
            ReagentPiece piece = session.pieces[index];
            switch (action)
            {
                case ActionKind.CraftZone:
                    if (piece.stage != ReagentStage.Tray || session.pieces.Any(p => p.stage is ReagentStage.Mortar or ReagentStage.Ground))
                        return "error.mortarOccupied";
                    piece.stage = ReagentStage.Mortar;
                    Record(session, CraftStep.Mortar, index);
                    break;
                case ActionKind.GrindIngredient:
                    if (piece.stage != ReagentStage.Mortar || session.elapsed < piece.nextPress) return "error.sequence";
                    piece.nextPress = session.elapsed + 0.20f;
                    piece.presses++;
                    if (piece.presses >= PressesToGrind)
                    {
                        piece.stage = ReagentStage.Ground;
                        piece.ground = true;
                        Record(session, CraftStep.Grind, index);
                    }
                    break;
                case ActionKind.AddIngredient:
                    if (piece.stage == ReagentStage.Pot) return "error.sequence";
                    piece.stage = ReagentStage.Pot;
                    Record(session, CraftStep.Cauldron, index);
                    if (session.pieces.All(p => p.stage == ReagentStage.Pot))
                    {
                        session.step = CraftStep.Stir;
                        session.boilStart = session.lastStirSample = session.elapsed;
                    }
                    break;
            }
            return null;
        }

        private static void Record(StationSession session, CraftStep step, int index) =>
            session.journal.Add(new CraftEvent { step = step, reagent = index, time = session.elapsed });

        private static bool ValidIngredients(PlayerState player, int[] ingredients)
        {
            if (ingredients?.Length != Catalog.Count) return false;
            int total = 0;
            for (int item = 0; item < ingredients.Length; item++)
            {
                int count = ingredients[item];
                if (count < 0 || count > player.Count(item) || (count > 0 && !Catalog.IsIngredient(item))) return false;
                total += count;
            }
            return total is >= 2 and <= MaxReagents;
        }

        private void FinishBrew(PlayerState player, StationSession session)
        {
            Record(session, CraftStep.Stir, -1);
            State.lastCraft = session.journal.ToArray();
            bool wellStirred = session.stir >= balance.stirSeconds && session.stir <= balance.stirSeconds + 2;
            int quality = wellStirred ? (session.elapsed - session.boilStart <= 8 ? 100 : 80) : 50;
            State.craftQuality = quality;
            PotionEffects formula = PotionEffects.Mix(session.pieces, quality);

            for (int item = 0; item < session.ingredients.Length; item++)
                InventoryLayout.Consume(player, item, session.ingredients[item]);
            AddCrate((int)Item.Brew, 1, 0, quality, formula);
            State.craft = null;
            Notify(player, "notice.blend");
        }

        private void TickCraft(float delta)
        {
            if (State.craft == null) return;
            State.craft.elapsed += delta;
            if (State.craft.elapsed < 120) return;
            PlayerState owner = State.players.Find(p => p.id == State.craft.owner);
            if (owner != null) Notify(owner, "error.craftTime");
            State.craft = null;
            Dirty = true;
        }
    }
}
