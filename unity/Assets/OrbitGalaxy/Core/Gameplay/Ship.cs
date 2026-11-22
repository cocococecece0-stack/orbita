using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Pure C# state of one arena participant. All gameplay systems operate on this type;
    /// it intentionally has no Unity dependencies.
    /// </summary>
    public sealed class Ship
    {
        public int Id;
        public string Name;
        public bool IsPlayer;
        public bool IsBoss;
        public bool IsBot;
        public BotPersonality Personality;
        public BotProfile Profile;

        public Vec2 Position;
        public Vec2 Velocity;
        public Vec2 Facing;
        public Vec2 MoveInput;
        public Vec2 AimInput;

        public float Mass;
        public float Radius;
        public float Level;
        public float Xp;
        public float XpToNext;

        public float Hp;
        public float MaxHp;
        public float Shield;
        public float MaxShield;

        public float Energy;
        public float Speed;
        public float TurnRate;
        public float Damage;
        public float FireInterval;
        public float PickupRadius;

        public float FireTimer;
        public float Invulnerability;
        public float RespawnTimer;

        public float GravityPower;
        public float Lifesteal;
        public float DamageReduction;

        public ShipState State;
        public EvolutionBranch Branch;
        public int EvolutionTier;
        public string EvolutionId;

        public HullShape Hull;
        public string PrimaryHex;
        public string AccentHex;

        public WeaponKind Weapon;
        public AbilityKind ActiveAbility;

        public int Drones;
        public int ExtraProjectiles;

        public int Kills;
        public int Deaths;
        public float EnergyCollected;
        public int Crystals;
        public int DarkMatter;
        public int Artifacts;

        public float Score;
        public int Streak;
        public int BestStreak;
        public int MultiKill;
        public float LastKillTime;
        public int Rank;

        public EvolutionDef[] EvolutionsTaken;
        public float[] Cooldowns;
        public float[] EffectTimers;
        public List<string> ActionHistory;

        public BossDef BossDef;
        public int BossPhase;
        public float BossAttackTimer;

        // Shared gameplay state. Internal so the systems can cooperate without extending the contract.
        internal float ResearchEnergy;
        internal float ResearchAgility;
        internal float ResearchArmor;
        internal float ResearchMagnet;
        internal float ResearchLuck;

        internal float EvolutionMassMul;
        internal float EvolutionSpeedMul;
        internal float EvolutionDamageMul;
        internal float EvolutionHpMul;
        internal float EvolutionFireMul;
        internal float EvolutionPickupMul;
        internal float EvolutionShieldMul;

        internal float AbilityShieldBonus;
        internal float PassiveShieldBonus;
        internal bool NovaPending;
        internal bool ShieldBonusActive;
        internal Vec2 BlackHolePosition;
        internal float BossWeakWindow;
        internal int LastAttackerId;
        internal bool BossRewarded;

        public Ship()
        {
            Name = string.Empty;
            Facing = Vec2.Up;
            AimInput = Vec2.Up;
            Mass = GameConfig.BaseMass;
            Level = 1f;
            XpToNext = GameConfig.XpPerLevelBase;
            Hp = GameConfig.BaseHp;
            MaxHp = GameConfig.BaseHp;
            Energy = GameConfig.MaxEnergy;
            Speed = GameConfig.BaseSpeed;
            Damage = GameConfig.BaseDamage;
            FireInterval = GameConfig.BaseFireInterval;
            PickupRadius = GameConfig.BasePickupRadius;
            State = ShipState.Alive;
            Hull = HullShape.Dart;
            PrimaryHex = "#6CF6D2";
            AccentHex = "#B6FF3D";
            Weapon = WeaponKind.Laser;
            ActiveAbility = AbilityKind.Dash;
            EvolutionsTaken = new EvolutionDef[3];
            Cooldowns = new float[(int)AbilityKind.Phase + 1];
            EffectTimers = new float[(int)AbilityKind.Phase + 1];
            ActionHistory = new List<string>(24);
            EvolutionMassMul = 1f;
            EvolutionSpeedMul = 1f;
            EvolutionDamageMul = 1f;
            EvolutionHpMul = 1f;
            EvolutionFireMul = 1f;
            EvolutionPickupMul = 1f;
            EvolutionShieldMul = 1f;
            Radius = ComputeRadius(Mass);
            RecalculateStats(null);
        }

        public Ship(int id, string name, bool isPlayer) : this()
        {
            Id = id;
            Name = name ?? string.Empty;
            IsPlayer = isPlayer;
            IsBot = !isPlayer;
            Personality = isPlayer ? BotPersonality.Aggressive : BotPersonality.Farmer;
            Profile = isPlayer ? null : ContentDatabase.FindBotProfile(Personality);
        }

        public Ship(int id, string name, bool isPlayer, Vec2 position) : this(id, name, isPlayer)
        {
            Position = position;
        }

        public Ship(int id, string name, bool isPlayer, AccountProfile profile, Vec2 position)
            : this(id, name, isPlayer, position)
        {
            RecalculateStats(profile);
        }

        public Ship(int id, string name, bool isPlayer, Vec2 position, AccountProfile profile)
            : this(id, name, isPlayer, position)
        {
            RecalculateStats(profile);
        }

        public Ship(int id, string name, bool isPlayer, bool isBot, BotPersonality personality, Vec2 position)
            : this(id, name, isPlayer)
        {
            IsBot = isBot;
            Personality = personality;
            Profile = isBot ? ContentDatabase.FindBotProfile(personality) : null;
            Position = position;
        }

        public Ship(int id, string name, bool isPlayer, bool isBot, BotPersonality personality, Vec2 position, AccountProfile profile)
            : this(id, name, isPlayer, isBot, personality, position)
        {
            RecalculateStats(profile);
        }

        internal static float ComputeRadius(float mass)
        {
            float scaled = (float)Math.Pow(Math.Max(1f, mass) / GameConfig.BaseMass, 0.5f);
            return GameConfig.BaseRadius * MathGalaxy.Clamp(scaled, 0.65f, 5f);
        }

        private static float Research(AccountProfile profile, string id)
        {
            if (profile == null) return 0f;
            float value = profile.ResearchBonus(id);
            return value > 0f ? value : 0f;
        }

        /// <summary>
        /// Recomputes derived stats from mass, level, research, evolution multipliers and active buffs.
        /// A null profile keeps the bonuses cached from the last call with a real profile.
        /// </summary>
        public void RecalculateStats(AccountProfile profile)
        {
            if (profile != null)
            {
                ResearchEnergy = Research(profile, "res_energy");
                ResearchAgility = Research(profile, "res_agility");
                ResearchArmor = Research(profile, "res_armor");
                ResearchMagnet = Research(profile, "res_magnet");
                ResearchLuck = Research(profile, "res_luck");
            }

            if (Cooldowns == null || Cooldowns.Length <= (int)AbilityKind.Phase) Cooldowns = new float[(int)AbilityKind.Phase + 1];
            if (EffectTimers == null || EffectTimers.Length <= (int)AbilityKind.Phase) EffectTimers = new float[(int)AbilityKind.Phase + 1];

            float safeMass = Math.Max(1f, Mass);
            float sizeSpeed = (float)Math.Pow(GameConfig.BaseMass / safeMass, GameConfig.SpeedMassExponent);
            float sizeDamage = (float)Math.Pow(safeMass / GameConfig.BaseMass, GameConfig.DamageMassExponent);

            float overdrive = EffectTimers[(int)AbilityKind.Overdrive] > 0f
                ? ContentDatabase.FindAbility(AbilityKind.Overdrive).Power
                : 1f;
            float phasing = EffectTimers[(int)AbilityKind.Phase] > 0f ? 1.12f : 1f;

            Speed = GameConfig.BaseSpeed * Math.Max(GameConfig.MinimumSpeedFactor, sizeSpeed)
                * EvolutionSpeedMul * (1f + ResearchAgility) * overdrive * phasing;
            TurnRate = GameConfig.BaseTurnSpeed * Math.Max(0.55f, sizeSpeed)
                * (1f + ResearchAgility * 0.5f) * overdrive;
            Damage = GameConfig.BaseDamage * sizeDamage * EvolutionDamageMul * (1f + ResearchArmor * 0.25f);

            float levelHp = 1f + Math.Max(0f, Level - 1f) * 0.07f;
            float newMaxHp = GameConfig.BaseHp * levelHp * EvolutionHpMul * (1f + ResearchArmor);
            if (MaxHp > 0f && newMaxHp > MaxHp) Hp += newMaxHp - MaxHp;
            MaxHp = Math.Max(1f, newMaxHp);
            Hp = MathGalaxy.Clamp(Hp, 0f, MaxHp);

            float newMaxShield = (PassiveShieldBonus + AbilityShieldBonus) * EvolutionShieldMul;
            MaxShield = Math.Max(0f, newMaxShield);
            Shield = MathGalaxy.Clamp(Shield, 0f, MaxShield);

            WeaponDef weaponDef = ContentDatabase.FindWeapon(Weapon);
            FireInterval = Math.Max(0.05f, GameConfig.BaseFireInterval * weaponDef.IntervalMul
                * EvolutionFireMul / Math.Max(0.2f, overdrive));
            PickupRadius = GameConfig.BasePickupRadius * EvolutionPickupMul * (1f + ResearchMagnet);

            DamageReduction = MathGalaxy.Clamp(ResearchArmor * 0.12f
                + (EffectTimers[(int)AbilityKind.Phase] > 0f ? 0.85f : 0f), 0f, 0.9f);
            Energy = MathGalaxy.Clamp(Energy, 0f, GameConfig.MaxEnergy);
            Radius = ComputeRadius(safeMass);
        }

        public void AddMass(float amount, SimLog log, float time)
        {
            if (Math.Abs(amount) < 0.00001f) return;
            Mass = Math.Max(1f, Mass + amount);
            RecalculateStats(null);
        }

        public void AddXp(float amount, SimLog log, float time)
        {
            if (amount <= 0f) return;
            Xp += amount;
            int guard = 0;
            while (Xp >= XpToNext && guard < 64)
            {
                Xp -= XpToNext;
                Level += 1f;
                Mass += GameConfig.MassPerLevel;
                XpToNext = GameConfig.XpPerLevelBase * (float)Math.Pow(GameConfig.XpLevelGrowth, Level - 1f);
                guard++;
                if (log != null)
                {
                    log.Add(SimEventKind.LevelUp, Id, (int)Level,
                        "УРОВЕНЬ " + ((int)Level).ToString(), Position, time, Mass);
                }
                Remember("Уровень " + ((int)Level).ToString());
            }
            RecalculateStats(null);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            Hp = Math.Min(MaxHp, Hp + amount);
        }

        public void Remember(string action)
        {
            if (string.IsNullOrEmpty(action)) return;
            ActionHistory.Add(action);
            while (ActionHistory.Count > 24) ActionHistory.RemoveAt(0);
        }

        internal void AddPassiveShield(float amount, AccountProfile profile)
        {
            if (amount <= 0f) return;
            PassiveShieldBonus += amount;
            RecalculateStats(profile);
            Shield = Math.Min(MaxShield, Shield + amount);
        }
    }
}
