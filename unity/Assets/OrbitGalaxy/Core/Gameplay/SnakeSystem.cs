using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Lightweight owner of every snake body in a match. It deliberately does not add segments to
    /// MatchSimulation.Ships: all existing combat, AI, leaderboard and reward code expects exactly
    /// one circular participant per ship. Steering and body following are separate steps so the
    /// head direction is ready before combat, while the tail samples the final post-movement path.
    /// </summary>
    public sealed class SnakeSystem
    {
        private readonly List<Ship> _snakes = new List<Ship>(GameConfig.MaxParticipants + 8);
        private readonly SnakeCollisionSystem _collisions;

        public SnakeSystem(Rng rng)
        {
            _collisions = new SnakeCollisionSystem(rng);
        }

        public int Count { get { return _snakes.Count; } }

        public void Initialize(List<Ship> ships)
        {
            _snakes.Clear();
            if (ships == null) return;
            for (int i = 0; i < ships.Count; i++) Add(ships[i]);
        }

        public void Add(Ship ship)
        {
            if (ship == null || ship.IsBoss || _snakes.Contains(ship)) return;
            if (ship.Body == null)
            {
                ship.Body = new SnakeBody();
                ship.Body.Reset(ship.Position, ship.Facing, ship.Radius, ship.Mass);
            }
            _snakes.Add(ship);
        }

        public void Remove(Ship ship)
        {
            if (ship == null) return;
            _snakes.Remove(ship);
            ship.Body = null;
        }

        public void Reset(Ship ship)
        {
            if (ship == null || ship.Body == null) return;
            ship.Body.Reset(ship.Position, ship.Facing, ship.Radius, ship.Mass);
        }

        /// <summary>
        /// Turns the head toward the requested heading at a limited rate, then makes the snake glide
        /// forward. With no input the head keeps its current heading instead of stopping abruptly.
        /// </summary>
        public void Steer(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _snakes.Count; i++)
            {
                Ship ship = _snakes[i];
                if (ship == null || ship.State != ShipState.Alive || ship.Body == null) continue;

                Vec2 desired = ship.MoveInput;
                if (desired.SqrMagnitude > 1f) desired = desired.Normalized;
                if (desired.SqrMagnitude > 0.0001f)
                {
                    float maxStep = Math.Max(0.01f, ship.TurnRate) * dt;
                    float angle = TurnAngle(ship.Facing.Angle(), desired.Angle(), maxStep);
                    ship.Facing = Vec2.FromAngle(angle);
                }
                ship.MoveInput = ship.Facing;
            }
        }

        /// <summary>Samples each head path after movement so segments never jump between frames.</summary>
        public void Follow(float dt)
        {
            for (int i = 0; i < _snakes.Count; i++)
            {
                Ship ship = _snakes[i];
                if (ship == null || ship.Body == null) continue;
                if (ship.State != ShipState.Alive || ship.Hp <= 0f) continue;
                ship.Body.Update(ship.Position, ship.Radius, ship.Mass, dt);
            }
        }

        /// <summary>Resolves head-into-body deaths using the post-movement tails.</summary>
        public void ResolveCollisions(Arena arena, SimLog log, float time)
        {
            _collisions.Resolve(_snakes, arena, log, time);
        }

        private static float TurnAngle(float current, float target, float maxStep)
        {
            float delta = target - current;
            while (delta > Math.PI) delta -= 2f * (float)Math.PI;
            while (delta < -Math.PI) delta += 2f * (float)Math.PI;
            if (delta > maxStep) delta = maxStep;
            if (delta < -maxStep) delta = -maxStep;
            return current + delta;
        }
    }
}
