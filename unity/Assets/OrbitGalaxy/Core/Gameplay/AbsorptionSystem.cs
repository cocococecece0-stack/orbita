namespace Orbit.Galaxy
{
    /// <summary>
    /// Size-based absorption. A sufficiently larger ship consumes a smaller one on contact;
    /// comparable ships exchange kinetic damage instead.
    /// </summary>
    public static class AbsorptionSystem
    {
        public static float MassRatio(Ship attacker, Ship target)
        {
            if (attacker == null || target == null) return 0f;
            return attacker.Mass / MathGalaxy.Clamp(target.Mass, 1f, 100000f);
        }

        public static bool CanAbsorb(Ship attacker, Ship target)
        {
            if (attacker == null || target == null) return false;
            if (attacker == target || attacker.Id == target.Id) return false;
            if (attacker.State != ShipState.Alive || target.State != ShipState.Alive) return false;
            if (attacker.Hp <= 0f || target.Hp <= 0f) return false;
            if (target.IsBoss || target.Invulnerability > 0f) return false;
            if (MassRatio(attacker, target) < GameConfig.AbsorbMassRatio) return false;

            float reach = attacker.Radius + target.Radius;
            return Vec2.DistanceSqr(attacker.Position, target.Position) <= reach * reach * 1.1f;
        }

        public static bool TryAbsorb(Ship attacker, Ship target, SimLog log, float time)
        {
            if (!CanAbsorb(attacker, target)) return false;

            float transferred = target.Mass * GameConfig.AbsorbEfficiency;
            if (transferred <= 0f) return false;

            target.Mass = MathGalaxy.Clamp(target.Mass - transferred, 1f, 100000f);
            target.Radius = Ship.ComputeRadius(target.Mass);
            attacker.AddMass(transferred, log, time);
            attacker.AddXp(transferred * 1.4f, log, time);
            attacker.Heal(transferred * 1.6f);
            attacker.GravityPower += 0.02f;

            if (log != null)
            {
                log.Add(SimEventKind.Absorb, attacker.Id, target.Id, "ПОГЛОЩЕНИЕ: " + target.Name,
                    target.Position, time, transferred);
            }
            attacker.Remember("Поглощён " + target.Name);

            CombatSystem.ApplyDamage(target, target.Hp + target.Shield + target.Mass * 4f + 1000f,
                attacker, log, time, false);
            return true;
        }

        public static float ContactDamage(Ship attacker, Ship target)
        {
            if (attacker == null || target == null || attacker.Hp <= 0f) return 0f;
            float ratio = MassRatio(attacker, target);
            return MathGalaxy.Clamp(attacker.Damage * 0.55f * MathGalaxy.Clamp(ratio, 0.4f, 1.8f), 0f, 100000f);
        }
    }
}
