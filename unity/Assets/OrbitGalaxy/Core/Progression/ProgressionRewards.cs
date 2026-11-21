using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Orbit.Galaxy
{
    /// <summary>Сводка наград за матч. Все пользовательские строки — на русском языке.</summary>
    public sealed class ProgressionRewards
    {
        public int Credits;
        public int Crystals;
        public int DarkMatter;
        public float Xp;
        public int Levels;
        public int Matches;
        public bool Victory;
        public int Rank;
        public bool NewRecord;

        public readonly List<string> Achievements = new List<string>();
        public readonly List<string> Quests = new List<string>();
        public readonly List<string> Secrets = new List<string>();
        public readonly List<string> Messages = new List<string>();

        public int AchievementCount { get { return Achievements.Count; } }
        public int QuestCount { get { return Quests.Count; } }
        public int SecretCount { get { return Secrets.Count; } }
        public int MessageCount { get { return Messages.Count; } }

        public string Summary
        {
            get
            {
                StringBuilder text = new StringBuilder();
                text.Append("Награда: +").Append(Credits.ToString(CultureInfo.InvariantCulture)).Append(" кр., +");
                text.Append(Crystals.ToString(CultureInfo.InvariantCulture)).Append(" крист., +");
                text.Append(((int)Math.Round(Xp, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture)).Append(" опыта.");
                if (Levels > 0) text.Append(" Новый уровень: +").Append(Levels.ToString(CultureInfo.InvariantCulture)).Append(".");
                if (NewRecord) text.Append(" Новый рекорд!");
                if (Achievements.Count > 0) text.Append(" Достижения: ").Append(string.Join(", ", Achievements.ToArray()));
                if (Quests.Count > 0) text.Append(" Задания: ").Append(string.Join(", ", Quests.ToArray()));
                if (Secrets.Count > 0) text.Append(" Секреты: ").Append(string.Join(", ", Secrets.ToArray()));
                return text.ToString();
            }
        }

        public void Reset()
        {
            Credits = 0;
            Crystals = 0;
            DarkMatter = 0;
            Xp = 0f;
            Levels = 0;
            Matches = 0;
            Victory = false;
            Rank = 0;
            NewRecord = false;
            Achievements.Clear();
            Quests.Clear();
            Secrets.Clear();
            Messages.Clear();
        }
    }
}
