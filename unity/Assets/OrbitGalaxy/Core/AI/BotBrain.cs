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

        // Per-bot identity and skill. Shared profiles stay immutable; only these are personal.
        public BotArchetype Archetype;
        public BotTuning Tuning;

        private float _fireTimer;
        private float _abilityTimer;
        private int _lastEvolutionLevel;
        private int _memoryTargetId;
        private Vec2 _memoryPoint;
        private float _memoryTimer;
        private Vec2 _lastPosition;
        private float _aimJitter;

        public BotBrain(Ship ship, BotProfile profile, Rng rng, Difficulty difficulty)
            : this(ship, profile, rng, difficulty, null)
        {
        }

        public BotBrain(Ship ship, BotProfile profile, Rng rng, Difficulty difficulty, BotArchetype archetype)
        {
            Ship = ship;
            Profile = profile ?? ContentDatabase.BotProfiles[1];
            Archetype = archetype ?? BotArchetypeCatalog.Neutral;
            Tuning = BotTuning.Create(difficulty, Archetype);
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
            VaryRoute(match);
            ConsiderAbilities(match, log);
            TryEvolution(match, log);
            AccelerateMemory();
        }

        /// <summary>
        /// Unpredictable bots occasionally bend an otherwise purposeful route. The destination is
        /// still a resource, prey or patrol point — this is a change of approach, not a random walk.
        /// </summary>
        private void VaryRoute(MatchSimulation match)
        {
            if (Archetype == null || Archetype.RouteVariance <= 1.2f || Ship == null) return;
            if (!Rng.Chance(0.12f * (Tuning != null ? Tuning.SwitchRate : 1f))) return;
            Vec2 toGoal = TargetPoint - Ship.Position;
            if (toGoal.SqrMagnitude < 400f) return;
            float angle = Rng.Range(-1.1f, 1.1f);
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            Vec2 turned = new Vec2(toGoal.X * cos - toGoal.Y * sin, toGoal.X * sin + toGoal.Y * cos);
            Vec2 candidate = Ship.Position + turned;
            if (match != null && match.Arena != null) candidate = match.Arena.ClampToSafe(candidate);
            TargetPoint = candidate;
            OrbitSign = -OrbitSign;
            Remember("Смена маршрута");
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
            // Difficulty changes how often a brain re-plans, not how fast its ship moves.
            float cadence = Tuning != null ? Tuning.ReactionSpeed : 1f;
            if (IsElite) cadence *= 0.78f;
            float reaction = Profile.Reaction > 0f ? MathGalaxy.Clamp(1.2f - Profile.Reaction * 0.25f, 0.8f, 1.25f) : 1f;
            LodInterval = MathGalaxy.Clamp(interval / cadence * reaction, 0.05f, 1.4f);

            float aim = MathGalaxy.Clamp(Profile.AimAccuracy * (Tuning != null ? Tuning.Aim : 1f), 0f, 0.99f);
            if (IsElite) aim = MathGalaxy.Clamp(aim + 0.06f, 0f, 0.99f);
            _aimJitter = (1f - aim) * 260f;
        }

        /// <summary>Aim scatter in world units; exposed so balance tests can compare difficulties.</summary>
        public float AimJitter { get { return _aimJitter; } }

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
                        // Run from where the threat is going, not from where it was.
                        Vec2 away = Ship.Position - BotPerception.Predict(Ship.Position, TargetShip,
                            1.2f * (Tuning != null ? Tuning.DecisionQuality : 1f));
                        point = Ship.Position + (away.SqrMagnitude > 1f ? away.Normalized : Vec2.Right) * 620f;
                    }
                    else if (Perception.NearestSnakeThreat != null)
                    {
                        Vec2 away = Ship.Position - Perception.NearestSnakeThreat.Position;
                        point = Ship.Position + (away.SqrMagnitude > 1f ? away.Normalized : Vec2.Right) * 520f;
                    }
                    else if (Perception.NearestHazard != null)
                    {
                        Vec2 away = Ship.Position - Perception.NearestHazard.Position;
                        point = Ship.Position + (away.SqrMagnitude > 1f ? away.Normalized : Vec2.Right) * 520f;
                    }
                    break;
                case BotGoal.Farm:
                    // The collector specialises in energy motes; other farmers take the best value.
                    if (Archetype != null && Archetype.Id == "collector" && Perception.NearestEnergy != null)
                        point = Perception.NearestEnergy.Position;
                    else if (Perception.NearestResource != null) point = Perception.NearestResource.Position;
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
                float horizon = travel + Profile.Reaction / Math.Max(0.5f, Tuning != null ? Tuning.DecisionQuality : 1f);
                point = BotPerception.Predict(Ship.Position, TargetShip, horizon);
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
            Vec2 lead = BotPerception.Predict(Ship.Position, target,
                (0.32f + Profile.Reaction) * (Tuning != null ? Tuning.DecisionQuality : 1f));
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
            float bias = MathGalaxy.Clamp(Profile.AbilityBias * (Tuning != null ? Tuning.Ability : 1f), 0f, 1f);
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
                // Bots must never inherit the player's research bonuses. Their ships were created
                // without an AccountProfile, so a null profile keeps their own neutral bonuses.
                EvolutionSystem.Choose(Ship, choice, log, match.Time, null);
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

        /// <summary>Called by the match when a bot returns to the arena. No stale chase or stuck state.</summary>
        public void OnRespawn()
        {
            StuckTimer = 0f;
            StuckSign = Rng.Chance(0.5f) ? 1 : -1;
            OrbitSign = Rng.Chance(0.5f) ? 1f : -1f;
            _memoryTimer = 0f;
            _memoryTargetId = 0;
            _memoryPoint = Vec2.Zero;
            _lastPosition = Ship != null ? Ship.Position : Vec2.Zero;
            ThinkTimer = 0f;
            TargetShip = null;
            Goal = Profile != null && Profile.Greed >= Profile.Aggression ? BotGoal.Farm : BotGoal.Patrol;
            if (Ship != null) TargetPoint = Ship.Position + Ship.Facing * 320f;
        }

        private void Remember(string action)
        {
            if (Ship != null) Ship.Remember(action);
            Actions.Add(action);
            if (Actions.Count > MaxLocalActions) Actions.RemoveAt(0);
        }
    }
}
