using System;

namespace Orbit.Galaxy
{
    /// <summary>
    /// A behaviour package for one bot. Archetypes reuse the existing seven foundation
    /// personalities so protected files never have to change, but they carry their own tuned
    /// profile and decision traits. Only per-brain fields store the archetype: profiles are shared
    /// static-like data and must stay immutable during a match.
    /// </summary>
    public sealed class BotArchetype
    {
        public readonly string Id;
        public readonly string Label;
        public readonly BotPersonality Personality;
        public readonly BotProfile Profile;

        /// <summary>Smaller values keep a goal longer; unpredictable bots switch eagerly.</summary>
        public readonly float SwitchRate;

        /// <summary>Multiplier for the small route wobble that prevents lock-step movement.</summary>
        public readonly float RouteVariance;

        public BotArchetype(string id, string label, BotPersonality personality, BotProfile profile,
            float switchRate, float routeVariance)
        {
            Id = id;
            Label = label;
            Personality = personality;
            Profile = profile;
            SwitchRate = switchRate;
            RouteVariance = routeVariance;
        }
    }

    /// <summary>
    /// Derived skill multipliers. Difficulty changes decision quality, reaction, aim and
    /// willingness to take risks — never movement speed, fire rate or respawn rules.
    /// </summary>
    public sealed class BotTuning
    {
        public readonly float DecisionQuality;
        public readonly float ReactionSpeed;
        public readonly float Aggression;
        public readonly float Caution;
        public readonly float Greed;
        public readonly float Risk;
        public readonly float Aim;
        public readonly float Ability;
        public readonly float SwitchRate;
        public readonly float RouteVariance;

        public BotTuning(float decisionQuality, float reactionSpeed, float aggression, float caution,
            float greed, float risk, float aim, float ability, float switchRate, float routeVariance)
        {
            DecisionQuality = decisionQuality;
            ReactionSpeed = reactionSpeed;
            Aggression = aggression;
            Caution = caution;
            Greed = greed;
            Risk = risk;
            Aim = aim;
            Ability = ability;
            SwitchRate = switchRate;
            RouteVariance = routeVariance;
        }

        public static BotTuning Create(Difficulty difficulty, BotArchetype archetype)
        {
            float quality = 1f;
            float reaction = 1f;
            float aggression = 1f;
            float caution = 1f;
            float greed = 1f;
            float risk = 1f;
            float aim = 1f;
            float ability = 1f;
            switch (difficulty)
            {
                case Difficulty.Casual:
                    quality = 0.78f; reaction = 0.78f; aggression = 0.78f;
                    caution = 1.15f; greed = 0.95f; risk = 0.75f;
                    aim = 0.76f; ability = 0.7f;
                    break;
                case Difficulty.Hard:
                    quality = 1.16f; reaction = 1.18f; aggression = 1.14f;
                    caution = 0.94f; greed = 1.05f; risk = 1.12f;
                    aim = 1.14f; ability = 1.18f;
                    break;
                case Difficulty.Expert:
                    quality = 1.3f; reaction = 1.35f; aggression = 1.28f;
                    caution = 0.88f; greed = 1.1f; risk = 1.25f;
                    aim = 1.28f; ability = 1.35f;
                    break;
            }
            BotArchetype a = archetype ?? BotArchetypeCatalog.Neutral;
            return new BotTuning(quality, reaction, aggression, caution, greed, risk, aim, ability,
                a.SwitchRate, a.RouteVariance);
        }
    }

    /// <summary>
    /// Six distinct opponents. The rotation starts with every archetype represented even at the
    /// minimum participant count, then repeats with a per-match offset so lobbies do not feel
    /// identical. Selection is pure/index based, so a seed always rebuilds the same lobby.
    /// </summary>
    public static class BotArchetypeCatalog
    {
        public const int Count = 6;

        private static readonly BotArchetype[] All =
        {
            new BotArchetype("collector", "СОБИРАТЕЛЬ", BotPersonality.Farmer,
                new BotProfile(BotPersonality.Farmer, "СОБИРАТЕЛЬ",
                    "Собирает энергию и постепенно растёт, избегая риска.",
                    0.10f, 1.00f, 0.78f, 0.10f, 0.52f, 0.46f, 0.30f, 470f),
                0.85f, 0.55f),
            new BotArchetype("aggressive", "АГРЕССОР", BotPersonality.Aggressive,
                new BotProfile(BotPersonality.Aggressive, "АГРЕССОР",
                    "Ищет слабых и перехватывает их путь.",
                    1.00f, 0.30f, 0.12f, 0.68f, 0.74f, 0.30f, 0.70f, 195f),
                0.95f, 0.85f),
            new BotArchetype("cautious", "ОСТОРОЖНЫЙ", BotPersonality.Survivor,
                new BotProfile(BotPersonality.Survivor, "ОСТОРОЖНЫЙ",
                    "Держится вдали от крупных змей и опасных зон.",
                    0.08f, 0.58f, 1.00f, 0.05f, 0.60f, 0.28f, 0.60f, 560f),
                0.55f, 0.35f),
            new BotArchetype("hunter", "ОХОТНИК", BotPersonality.Hunter,
                new BotProfile(BotPersonality.Hunter, "ОХОТНИК",
                    "Выбирает цель, предсказывает её ход и атакует.",
                    0.90f, 0.25f, 0.38f, 0.50f, 0.90f, 0.20f, 0.90f, 300f),
                0.85f, 0.60f),
            new BotArchetype("competitor", "КОНКУРЕНТ", BotPersonality.Elite,
                new BotProfile(BotPersonality.Elite, "КОНКУРЕНТ",
                    "Борется за первое место и меняет стратегию по ситуации.",
                    0.72f, 0.78f, 0.62f, 0.42f, 0.88f, 0.14f, 0.95f, 350f),
                1.00f, 0.60f),
            new BotArchetype("unpredictable", "НЕПРЕДСКАЗУЕМЫЙ", BotPersonality.Chaotic,
                new BotProfile(BotPersonality.Chaotic, "НЕПРЕДСКАЗУЕМЫЙ",
                    "Часто меняет маршрут и иногда идёт на риск.",
                    0.66f, 0.52f, 0.16f, 1.00f, 0.66f, 0.22f, 0.74f, 260f),
                2.20f, 2.20f)
        };

        /// <summary>Neutral package used by legacy/unit tests that construct a brain directly.</summary>
        public static readonly BotArchetype Neutral = new BotArchetype("bot", "БОТ",
            BotPersonality.Farmer, ContentDatabase.BotProfiles[1], 1f, 1f);

        public static BotArchetype Get(int index)
        {
            int slot = index % Count;
            if (slot < 0) slot += Count;
            return All[slot];
        }

        public static BotArchetype Pick(int index, Difficulty difficulty)
        {
            BotArchetype archetype = Get(index);
            // Later waves get a difficulty-flavoured mix while all six archetypes stay present in
            // the first rotation. This affects behaviour, never raw ship speed.
            // The first two rotations are untouched, so every lobby contains all six archetypes.
            if (index >= Count * 2)
            {
                int wave = index - Count * 2;
                if (difficulty == Difficulty.Casual && (wave % 4) == 0) return All[2];
                if (difficulty >= Difficulty.Hard && (wave % 4) == 1) return All[4];
                if (difficulty >= Difficulty.Hard && (wave % 4) == 3) return All[3];
            }
            return archetype;
        }

        public static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }
    }
}
