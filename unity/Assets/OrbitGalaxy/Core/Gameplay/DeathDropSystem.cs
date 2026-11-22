using System;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Turns any ship death into a small, bounded cluster of energy motes. It is shared by snake
    /// collisions and by the match death sweep, so a body hit, a projectile, the border or a hazard
    /// all leave the same visible reward. The victim flag prevents the same death from spawning
    /// the pile twice when several systems notice it in one step.
    /// </summary>
    public static class DeathDropSystem
    {
        public const string EventTag = "ВЫПАДЕНИЕ МАССЫ";

        private const float FullCircle = 6.2831853f;

        public static void Spawn(Arena arena, Ship victim, SimLog log, float time, Rng rng)
        {
            Spawn(arena, victim, log, time, rng, victim == null ? 0f : victim.Mass);
        }

        /// <summary>
        /// Spawns the pile using a captured mass. Snake collisions call this before awarding the
        /// body-kill bonus so simultaneous mutual kills cannot change each other's drop size.
        /// </summary>
        public static void Spawn(Arena arena, Ship victim, SimLog log, float time, Rng rng, float victimMass)
        {
            if (arena == null || victim == null || victim.IsBoss || victim.DeathDropsSpawned ||
                victimMass <= 0f) return;
            victim.DeathDropsSpawned = true;

            int count = SnakeConfig.DeathDrops;
            float massPerDrop = victimMass * SnakeConfig.DeathDropMassShare / count;
            int value = (int)(massPerDrop / Math.Max(0.1f, GameConfig.MassGainEnergy));
            value = MathGalaxy.Clamp(value, 1, SnakeConfig.MaxDeathDropValue);

            float ring = Math.Max(13f, victim.Radius) + rng.Range(2f, 18f);
            float offset = rng.Range(0f, FullCircle);
            for (int i = 0; i < count; i++)
            {
                float angle = offset + i * FullCircle / count + rng.Range(-0.16f, 0.16f);
                Vec2 direction = Vec2.FromAngle(angle);
                Vec2 position = arena.ClampToSafe(victim.Position + direction * ring);
                Drop drop = new Drop(arena.NextId(), position, ResourceKind.Energy,
                    value, SnakeConfig.DropLifetime);
                // A short outward drift makes the pile bloom, then it settles as a readable cluster.
                drop.Velocity = direction * (26f + rng.Range(0f, 18f));
                arena.Add(drop);
            }

            if (log != null)
            {
                int total = (int)Math.Round(victimMass * SnakeConfig.DeathDropMassShare);
                log.Add(SimEventKind.Pickup, victim.Id, count,
                    EventTag + " +" + total.ToString(), victim.Position, time,
                    victimMass * SnakeConfig.DeathDropMassShare);
            }
        }

        /// <summary>True for the drop-cluster event (not for an ordinary resource pickup).</summary>
        public static bool IsDropEvent(SimEvent ev)
        {
            return ev != null && ev.Kind == SimEventKind.Pickup && ev.Text != null &&
                   ev.Text.StartsWith(EventTag, StringComparison.Ordinal);
        }
    }
}
