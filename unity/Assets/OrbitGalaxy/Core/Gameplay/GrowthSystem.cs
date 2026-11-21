using System;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Turns collected world objects into mass and experience and keeps per-tick growth state.
    /// All numbers come from GameConfig; no Unity types are involved.
    /// </summary>
    public static class GrowthSystem
    {
        public static float MassFromResource(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Energy: return GameConfig.MassGainEnergy;
                case ResourceKind.Crystal: return GameConfig.MassGainCrystal;
                case ResourceKind.DarkMatter: return GameConfig.MassGainDarkMatter;
                case ResourceKind.Artifact: return GameConfig.MassGainArtifact;
                default: return 0f;
            }
        }

        public static float XpFromResource(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Energy: return 3f;
                case ResourceKind.Crystal: return 20f;
                case ResourceKind.DarkMatter: return 38f;
                case ResourceKind.Artifact: return 80f;
                default: return 0f;
            }
        }

        private static ResourceKind KindOf(WorldObject obj)
        {
            if (obj == null) return ResourceKind.Energy;
            switch (obj.Kind)
            {
                case ObjectKind.Energy: return ResourceKind.Energy;
                case ObjectKind.Crystal: return ResourceKind.Crystal;
                case ObjectKind.DarkMatter: return ResourceKind.DarkMatter;
                case ObjectKind.Artifact: return ResourceKind.Artifact;
                case ObjectKind.Drop:
                    Drop drop = obj as Drop;
                    return drop == null ? ResourceKind.Energy : drop.Resource;
                default: return ResourceKind.Energy;
            }
        }

        private static string RussianName(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Energy: return "ЭНЕРГИЯ";
                case ResourceKind.Crystal: return "КРИСТАЛЛ";
                case ResourceKind.DarkMatter: return "ТЁМНАЯ МАТЕРИЯ";
                case ResourceKind.Artifact: return "АРТЕФАКТ";
                default: return "РЕСУРС";
            }
        }

        public static void ApplyPickup(Ship ship, WorldObject obj, SimLog log, float time, AccountProfile profile)
        {
            if (ship == null || obj == null || ship.State != ShipState.Alive || ship.Hp <= 0f) return;

            ResourceKind kind = KindOf(obj);
            float value = obj.Value > 0 ? obj.Value : 1;
            // ArtifactNode.Value encodes its rarity score, not a stack size: one relic = one pickup.
            if (obj.Kind == ObjectKind.Artifact) value = 1;
            float mass = MassFromResource(kind) * value;
            float xp = XpFromResource(kind) * value;

            if (obj.Kind == ObjectKind.Asteroid)
            {
                mass = MathGalaxy.Clamp(obj.Value * (obj.Alive ? 0.12f : 0.45f), 1f, 60f);
                xp = obj.Alive ? 4f : 12f;
            }

            float energyBonus = ship.ResearchEnergy;
            float luckBonus = ship.ResearchLuck;
            if (profile != null)
            {
                energyBonus = Math.Max(energyBonus, profile.ResearchBonus("res_energy"));
                luckBonus = Math.Max(luckBonus, profile.ResearchBonus("res_luck"));
            }
            if (kind == ResourceKind.Energy) mass *= 1f + energyBonus;
            if (kind == ResourceKind.Crystal || kind == ResourceKind.DarkMatter || kind == ResourceKind.Artifact)
                xp *= 1f + luckBonus * 0.5f;

            ship.AddMass(mass, log, time);
            ship.AddXp(xp, log, time);

            int count = 0;
            switch (kind)
            {
                case ResourceKind.Energy:
                    ship.EnergyCollected += value;
                    ship.Energy = MathGalaxy.Clamp(ship.Energy + 20f * value, 0f, GameConfig.MaxEnergy);
                    count = 1;
                    break;
                case ResourceKind.Crystal:
                    ship.Crystals += value;
                    count = value;
                    break;
                case ResourceKind.DarkMatter:
                    ship.DarkMatter += value;
                    ship.Shield = Math.Min(ship.MaxShield, ship.Shield + 8f * value);
                    count = value;
                    break;
                case ResourceKind.Artifact:
                    ship.Artifacts += value;
                    ship.AddPassiveShield(20f * value, profile);
                    ship.Lifesteal += 0.02f * value;
                    count = value;
                    break;
                default:
                    count = 1;
                    break;
            }

            if (log != null)
            {
                log.Add(SimEventKind.Pickup, ship.Id, count, RussianName(kind) + " +" + mass.ToString("F0"),
                    ship.Position, time, mass);
            }
            ship.Remember("Сбор: " + RussianName(kind));
        }

        public static void Update(Ship ship, float dt, SimLog log, float time)
        {
            if (ship == null || dt <= 0f) return;

            ship.Energy = MathGalaxy.Clamp(ship.Energy + GameConfig.EnergyRegenPerSecond
                * (1f + ship.ResearchEnergy * 0.5f) * dt, 0f, GameConfig.MaxEnergy);

            if (ship.State == ShipState.Respawning)
            {
                ship.RespawnTimer -= dt;
                if (ship.RespawnTimer <= 0f)
                {
                    ship.RespawnTimer = 0f;
                    ship.State = ShipState.Alive;
                    ship.Hp = ship.MaxHp;
                    ship.Shield = ship.MaxShield;
                    ship.Invulnerability = GameConfig.RespawnInvulnerability;
                    ship.FireTimer = 0f;
                    if (log != null) log.Add(SimEventKind.Tick, ship.Id, 0, "ВОЗВРАЩЕНИЕ В АРЕНУ", ship.Position, time);
                }
            }

            if (ship.MultiKill > 0 && time - ship.LastKillTime > GameConfig.MultiKillTime) ship.MultiKill = 0;

            // Evolution trigger: MatchSimulation and bots compare Level with
            // EvolutionSystem.NextEvolutionLevel(ship) and call EvolutionSystem.Offer/Choose.
        }
    }
}
