using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// One bot's brain. It thinks on a distance-based LOD interval, keeps a short action history
    /// and a fading memory of its last target, but steers every simulation step so it never freezes.
    /// </summary>
    public sealed class BotBrain
    {
        private const int MaxLocalActions = 12;

        public BotGoal Goal;
        public Vec2 TargetPoint;
        public Ship TargetShip;
        public float ThinkTimer;
        public float LodInterval;
        public bool IsElite;
        public Ship Ship;

        // Extra state used by the AI and by the match layer.
        public BotProfile Profile;
        public Rng Rng;
        public Difficulty DifficultyLevel;
        public BotPerception Perception;
        public BotNavigation Navigation;
        public float StuckTimer;
        public int StuckSign = 1;
        public float OrbitSign = 1f;
        public readonly List<string> Actions = new List<string>(MaxLocalActions);

        private float _fireTimer;
        private float _abilityTimer;
        private int _lastEvolutionLevel;
        private int _memoryTargetId;
        private Vec2 _memoryPoint;
        private float _memoryTimer;
        private Vec2 _lastPosition;
        private float _aimJitter;

        public BotBrain(Ship ship, BotProfile profile, Rng rng, Difficulty difficulty)
        {
            Ship = ship;
            Profile = profile ?? ContentDatabase.BotProfiles[1];
            Rng = rng;
            DifficultyLevel = difficulty;
            Perception = new BotPerception();
            Navigation = new BotNavigation();
            Goal = BotGoal.Patrol;
            ThinkTimer = Rng.Range(0f, 0.18f);
            LodInterval = GameConfig.AiNearInterval;
            IsElite = Profile.Personality == BotPersonality.Elite ||
                      (ship != null && ship.IsBoss) ||
                      (difficulty >= Difficulty.Hard && Rng.Chance(0.08f));
            if (ship != null)
            {
                _lastPosition = ship.Position;
                TargetPoint = ship.Position + Vec2.FromAngle(Rng.Range(0f, 6.2831853f)) * 320f;
            }
            _fireTimer = Rng.Range(0f, 0.3f);
            _abilityTimer = Rng.Range(0.5f, 2f);
        }

        public void Update(float dt, MatchSimulation match, SimLog log)
        {
            if (Ship == null) return;

            if (Ship.State != ShipState.Alive)
            {
                Ship.MoveInput = Vec2.Zero;
                Ship.AimInput = Vec2.Zero;
                TargetShip = null;
                TryEvolution(match, log);
                return;
            }

            float moved = Vec2.DistanceSqr(Ship.Position, _lastPosition);
            _lastPosition = Ship.Position;
            if (moved < 16f * dt + 1f)
            {
                StuckTimer += dt;
                if (StuckTimer > 1.6f)
                {
                    StuckSign = -StuckSign;
                    StuckTimer = 0.55f;
                }
            }
            else
            {
                StuckTimer = 0f;
            }

            ThinkTimer -= dt;
            if (ThinkTimer <= 0f)
            {
                Think(match, log);
                ThinkTimer = LodInterval;
            }

            _abilityTimer -= dt;
            _fireTimer -= dt;
            Ship.MoveInput = Navigation.Steer(this, Ship, TargetPoint, match);
            Ship.AimInput = ComputeAim();
            ControlFire(match);
        }

        public string DebugInfo()
        {
            if (Ship == null) return "Бот недоступен";
            string target = TargetShip == null ? "—" : TargetShip.Name;
            string hp = Math.Round(Ship.Hp, 0) + "/" + Math.Round(Ship.MaxHp, 0);
            string last = Actions.Count == 0 ? "—" : Actions[Actions.Count - 1];
            return Ship.Name + " | " + GoalName(Goal) + " | цель: " + target +
                   " | LOD " + LodInterval.ToString("F2") + " с | HP " + hp +
                   " | элита: " + (IsElite ? "да" : "нет") + " | последнее: " + last;
        }

        public static string GoalName(BotGoal goal)
        {
            switch (goal)
            {
                case BotGoal.Farm: return "Сбор ресурсов";
                case BotGoal.Hunt: return "Охота";
                case BotGoal.Flee: return "Бегство";
                case BotGoal.CollectRare: return "Редкие ресурсы";
                case BotGoal.AvoidHazard: return "Обход опасности";
                case BotGoal.ContestEvent: return "Участие в событии";
                case BotGoal.AttackBoss: return "Атака босса";
                case BotGoal.Patrol: return "Патрулирование";
                case BotGoal.Regroup: return "Перегруппировка";
                default: return "Ожидание";
            }
        }

        private void Think(MatchSimulation match, SimLog log)
        {
            UpdateLod(match);
            Perception.Refresh(this, Ship, match);
            BotGoal previous = Goal;
            Goal = BotDecisionSystem.Decide(this, Ship, match, Perception);
            if (Goal != previous)
            {
                Remember("Смена цели: " + GoalName(Goal));
                OrbitSign = Rng.Chance(0.5f) ? 1f : -1f;
            }

            SelectTarget(match);
            ConsiderAbilities(match, log);
            TryEvolution(match, log);
            AccelerateMemory();
        }

        private void UpdateLod(MatchSimulation match)
        {
            float distance = float.MaxValue;
            if (match != null && match.Player != null && match.Player.State == ShipState.Alive)
            {
                distance = Vec2.Distance(Ship.Position, match.Player.Position);
            }
            float interval = distance <= GameConfig.AiNearRange ? GameConfig.AiNearInterval :
                             (distance <= GameConfig.AiMidRange ? GameConfig.AiMidInterval :
                                                                  GameConfig.AiFarInterval);
            float difficultyFactor = 1f;
            switch (DifficultyLevel)
            {
                case Difficulty.Casual: difficultyFactor = 1.28f; break;
                case Difficulty.Normal: difficultyFactor = 1f; break;
                case Difficulty.Hard: difficultyFactor = 0.88f; break;
                case Difficulty.Expert: difficultyFactor = 0.76f; break;
            }
            if (IsElite) difficultyFactor *= 0.78f;
            float reaction = Profile.Reaction > 0f ? MathGalaxy.Clamp(1.2f - Profile.Reaction * 0.25f, 0.8f, 1.25f) : 1f;
            LodInterval = MathGalaxy.Clamp(interval * difficultyFactor * reaction, 0.05f, 1.4f);

            float aim = Profile.AimAccuracy;
            if (DifficultyLevel == Difficulty.Casual) aim *= 0.78f;
            else if (DifficultyLevel == Difficulty.Hard) aim = MathGalaxy.Clamp(aim * 1.12f, 0f, 0.98f);
            else if (DifficultyLevel == Difficulty.Expert) aim = MathGalaxy.Clamp(aim * 1.22f, 0f, 0.99f);
            if (IsElite) aim = MathGalaxy.Clamp(aim + 0.06f, 0f, 0.99f);
            _aimJitter = (1f - MathGalaxy.Clamp(aim, 0f, 1f)) * 260f;
        }

        private void SelectTarget(MatchSimulation match)
        {
            TargetShip = null;
            Vec2 point = Ship.Position;
            float travel = 0.45f;

            switch (Goal)
            {
                case BotGoal.Hunt:
                    TargetShip = Perception.NearestPrey;
                    break;
                case BotGoal.AttackBoss:
                    TargetShip = Perception.NearestBoss;
                    break;
                case BotGoal.Flee:
                    TargetShip = Perception.NearestThreat;
                    if (TargetShip != null)
                    {
                        Vec2 away = Ship.Position - TargetShip.Position;
                        point = Ship.Position + (away.SqrMagnitude > 1f ? away.Normalized : Vec2.Right) * 620f;
                    }
                    else if (Perception.NearestHazard != null)
                    {
                        Vec2 away = Ship.Position - Perception.NearestHazard.Position;
                        point = Ship.Position + (away.SqrMagnitude > 1f ? away.Normalized : Vec2.Right) * 520f;
                    }
                    break;
                case BotGoal.Farm:
                    if (Perception.NearestResource != null) point = Perception.NearestResource.Position;
                    break;
                case BotGoal.CollectRare:
                    if (Perception.NearestRare != null) point = Perception.NearestRare.Position;
                    break;
                case BotGoal.AvoidHazard:
                    if (Perception.NearestHazard != null)
                    {
                        Vec2 away = Ship.Position - Perception.NearestHazard.Position;
                        point = Ship.Position + (away.SqrMagnitude > 1f ? away.Normalized : Vec2.Right) *
                                (Perception.NearestHazard.Radius + 340f);
                    }
                    break;
                case BotGoal.ContestEvent:
                    point = EventPoint(match);
                    break;
                case BotGoal.Regroup:
                {
                    Ship ally = FindAlly(match);
                    if (ally != null) point = ally.Position;
                    else if (match != null && match.Player != null) point = match.Player.Position;
                    break;
                }
                case BotGoal.Patrol:
                    point = PatrolPoint(match);
                    break;
                default:
                    point = Ship.Position + Ship.Facing * 140f;
                    break;
            }

            if (TargetShip != null && Goal != BotGoal.Flee)
            {
                _memoryTargetId = TargetShip.Id;
                _memoryPoint = TargetShip.Position;
                _memoryTimer = 5f;
                float predicted = travel + Profile.Reaction;
                point = TargetShip.Position + TargetShip.Velocity * predicted;
            }
            else if (_memoryTimer > 0f && Goal != BotGoal.Flee && Goal != BotGoal.AvoidHazard)
            {
                point = _memoryPoint;
            }

            if (match != null && match.Arena != null)
            {
                point = match.Arena.ClampToSafe(point);
            }
            TargetPoint = point;
        }

        private Vec2 EventPoint(MatchSimulation match)
        {
            if (match == null || match.Arena == null) return Vec2.Zero;
            // Events spawn rewards near the centre or near ancient relics; prefer the nearest one.
            WorldObject best = null;
            float bestDist = float.MaxValue;
            List<WorldObject> objects = match.Arena.Objects;
            if (objects != null)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    WorldObject obj = objects[i];
                    if (obj == null || !obj.Alive) continue;
                    if (obj.Kind != ObjectKind.Artifact && obj.Kind != ObjectKind.DarkMatter &&
                        obj.Kind != ObjectKind.Crystal) continue;
                    float d = Vec2.DistanceSqr(Ship.Position, obj.Position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = obj;
                    }
                }
            }
            if (best != null) return best.Position;
            return Vec2.FromAngle(Rng.Range(0f, 6.2831853f)) * match.Arena.SafeRadius * 0.45f;
        }

        private Ship FindAlly(MatchSimulation match)
        {
            if (match == null) return null;
            Ship best = null;
            float bestDist = float.MaxValue;
            List<Ship> ships = match.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship other = ships[i];
                if (other == null || other == Ship || other.IsBoss || other.State != ShipState.Alive) continue;
                if (other.Mass > Ship.Mass * 1.4f) continue;
                float d = Vec2.DistanceSqr(Ship.Position, other.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = other;
                }
            }
            return best;
        }

        private Vec2 PatrolPoint(MatchSimulation match)
        {
            float radius = match == null || match.Arena == null ? GameConfig.SafeRadiusStart : match.Arena.SafeRadius;
            Vec2 wander = Vec2.FromAngle(Rng.Range(0f, 6.2831853f)) * Rng.Range(260f, 760f);
            Vec2 candidate = Ship.Position + wander;
            if (candidate.Magnitude > radius * 0.93f)
            {
                candidate = candidate.Normalized * radius * 0.72f;
            }
            return candidate;
        }

        private void AccelerateMemory()
        {
            if (_memoryTimer > 0f) _memoryTimer -= LodInterval;
            if (_memoryTargetId != 0 && _memoryTimer <= 0f) _memoryTargetId = 0;
        }

        private Vec2 ComputeAim()
        {
            Ship target = TargetShip;
            if (target == null || target.State != ShipState.Alive)
            {
                return Ship.MoveInput.SqrMagnitude > 0.0001f ? Ship.MoveInput.Normalized :
                       (Ship.Facing.SqrMagnitude > 0.0001f ? Ship.Facing.Normalized : Vec2.Right);
            }
            Vec2 lead = target.Position + target.Velocity * (0.32f + Profile.Reaction);
            Vec2 toTarget = lead - Ship.Position;
            if (toTarget.SqrMagnitude < 1f) return Ship.Facing;
            Vec2 dir = toTarget.Normalized;
            if (_aimJitter > 0f)
            {
                float angle = Rng.Range(-_aimJitter, _aimJitter) / Math.Max(80f, toTarget.Magnitude);
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);
                dir = new Vec2(dir.X * cos - dir.Y * sin, dir.X * sin + dir.Y * cos);
            }
            return dir;
        }

        private void ControlFire(MatchSimulation match)
        {
            if (match == null || match.Combat == null || TargetShip == null ||
                TargetShip.State != ShipState.Alive || _fireTimer > 0f) return;
            float range = 520f + Ship.Radius * 6f;
            if (Vec2.DistanceSqr(Ship.Position, TargetShip.Position) > range * range) return;
            if (Goal != BotGoal.Hunt && Goal != BotGoal.AttackBoss && Goal != BotGoal.Flee) return;
            match.Combat.Fire(Ship, Ship.AimInput, match.Log, match.Time);
            _fireTimer = MathGalaxy.Clamp(Ship.FireInterval * Rng.Range(0.92f, 1.12f), 0.08f, 2f);
        }

        private void ConsiderAbilities(MatchSimulation match, SimLog log)
        {
            if (_abilityTimer > 0f || match == null) return;
            float bias = MathGalaxy.Clamp(Profile.AbilityBias, 0f, 1f);
            if (DifficultyLevel == Difficulty.Casual) bias *= 0.75f;
            else if (DifficultyLevel == Difficulty.Hard) bias = MathGalaxy.Clamp(bias * 1.15f, 0f, 1f);
            else if (DifficultyLevel == Difficulty.Expert) bias = MathGalaxy.Clamp(bias * 1.3f, 0f, 1f);
            if (!Rng.Chance(MathGalaxy.Clamp(bias, 0.05f, 0.95f))) return;

            AbilityKind kind = AbilityKind.Dash;
            bool found = false;
            float hpRatio = Ship.MaxHp > 0f ? Ship.Hp / Ship.MaxHp : 1f;
            if ((Perception.ThreatScore > 4.5f || hpRatio < 0.45f) && CanUse(AbilityKind.Shield))
            {
                kind = AbilityKind.Shield;
                found = true;
            }
            else if (Perception.NearestHazard != null && CanUse(AbilityKind.Phase))
            {
                kind = AbilityKind.Phase;
                found = true;
            }
            else if (Goal == BotGoal.Flee && CanUse(AbilityKind.Dash))
            {
                kind = AbilityKind.Dash;
                found = true;
            }
            else if (Goal == BotGoal.Farm && CanUse(AbilityKind.Magnet))
            {
                kind = AbilityKind.Magnet;
                found = true;
            }
            else if (Goal == BotGoal.Hunt && CanUse(AbilityKind.Overdrive))
            {
                kind = AbilityKind.Overdrive;
                found = true;
            }
            else if (CountCrowd() >= 3 && CanUse(AbilityKind.Nova))
            {
                kind = AbilityKind.Nova;
                found = true;
            }
            else if (Goal == BotGoal.Hunt && CanUse(AbilityKind.BlackHole))
            {
                kind = AbilityKind.BlackHole;
                found = true;
            }
            if (!found)
            {
                AbilityKind[] fallback = { AbilityKind.Dash, AbilityKind.Shield, AbilityKind.Magnet };
                for (int i = 0; i < fallback.Length; i++)
                {
                    if (CanUse(fallback[i]))
                    {
                        kind = fallback[i];
                        found = true;
                        break;
                    }
                }
            }
            if (!found) return;

            if (AbilitySystem.Activate(Ship, kind, log, match.Time))
            {
                Remember("Способность: " + ContentDatabase.FindAbility(kind).Name);
                _abilityTimer = Rng.Range(1.2f, 3f) * (IsElite ? 0.7f : 1f);
            }
            else
            {
                _abilityTimer = 0.4f;
            }
        }

        private bool CanUse(AbilityKind kind)
        {
            return Ship.Cooldowns != null && (int)kind < Ship.Cooldowns.Length &&
                   Ship.Cooldowns[(int)kind] <= 0f && AbilitySystem.CanActivate(Ship, kind);
        }

        private int CountCrowd()
        {
            int count = 0;
            for (int i = 0; i < Perception.NearbyShips.Count; i++)
            {
                Ship other = Perception.NearbyShips[i];
                if (other != null && other.State == ShipState.Alive &&
                    Vec2.DistanceSqr(Ship.Position, other.Position) < 320f * 320f) count++;
            }
            return count;
        }

        private void TryEvolution(MatchSimulation match, SimLog log)
        {
            if (match == null || Ship == null) return;
            int nextLevel = EvolutionSystem.NextEvolutionLevel(Ship);
            if (nextLevel <= 0 || Ship.Level < nextLevel || nextLevel <= _lastEvolutionLevel) return;
            List<EvolutionDef> offers = EvolutionSystem.Offer(Ship, Rng);
            if (offers == null || offers.Count == 0)
            {
                _lastEvolutionLevel = nextLevel;
                return;
            }
            EvolutionDef choice = ChooseEvolution(offers);
            if (choice != null && EvolutionSystem.IsAvailable(Ship, choice))
            {
                EvolutionSystem.Choose(Ship, choice, log, match.Time, match.Profile);
                Remember("Эволюция: " + choice.Name);
                _lastEvolutionLevel = nextLevel;
            }
        }

        private EvolutionDef ChooseEvolution(List<EvolutionDef> offers)
        {
            EvolutionDef best = null;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < offers.Count; i++)
            {
                EvolutionDef def = offers[i];
                if (def == null) continue;
                float score = Rng.NextFloat() * 0.35f + def.Tier * 0.6f;
                switch (def.Branch)
                {
                    case EvolutionBranch.Titan:
                        score += Profile.Caution * 3f + (Goal == BotGoal.Flee ? 1.5f : 0f);
                        break;
                    case EvolutionBranch.Phantom:
                        score += Profile.Aggression * 3f + (Goal == BotGoal.Hunt ? 1.8f : 0f);
                        break;
                    case EvolutionBranch.Swarm:
                        score += Profile.Greed * 2.6f + (Goal == BotGoal.Farm ? 1.4f : 0f);
                        break;
                    case EvolutionBranch.Singularity:
                        score += Profile.Risk * 2.8f + (Goal == BotGoal.CollectRare ? 1.2f : 0f);
                        break;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = def;
                }
            }
            return best;
        }

        private void Remember(string action)
        {
            if (Ship != null) Ship.Remember(action);
            Actions.Add(action);
            if (Actions.Count > MaxLocalActions) Actions.RemoveAt(0);
        }
    }
}
