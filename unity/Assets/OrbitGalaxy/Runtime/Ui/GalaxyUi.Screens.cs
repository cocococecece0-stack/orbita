using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Orbit.Galaxy.Unity
{
    public sealed partial class GalaxyUi
    {
        private int _profileTab;
        private int _shopTab;
        private int _collectionTab;
        private int _statsTab;

        private static readonly string[] ProfileTabs = { "ОБЗОР", "ЗАДАНИЯ", "ДОСТИЖЕНИЯ" };
        private static readonly string[] ShopTabs = { "КОРАБЛИ", "СНАРЯЖЕНИЕ", "ИССЛЕДОВАНИЯ" };
        private static readonly string[] CollectionTabs = { "КОРАБЛИ", "ЭФФЕКТЫ", "ПРОЧЕЕ" };
        private static readonly string[] StatsTabs = { "ОБЗОР", "РЕКОРДЫ", "ДОСТИЖЕНИЯ" };
        private static readonly string[] QualityTokens =
        {
            GameSettings.QualityLow, GameSettings.QualityMedium, GameSettings.QualityHigh, GameSettings.QualityAuto
        };
        private static readonly string[] QualityLabels = { "НИЗКОЕ", "СРЕДНЕЕ", "ВЫСОКОЕ", "АВТО" };
        private static readonly string[] ControlTokens =
        {
            GameSettings.ControlTouch, GameSettings.ControlGamepad, GameSettings.ControlKeyboard,
            GameSettings.ControlLeft, GameSettings.ControlRight, GameSettings.ControlAlt
        };
        private static readonly string[] ControlLabels =
        {
            "СЕНСОР", "ГЕЙМПАД", "КЛАВИАТУРА", "ЛЕВЫЙ", "ПРАВЫЙ", "АЛЬТ"
        };

        // ───────────────────────────── Evolution ─────────────────────────────

        public void ShowEvolution(List<EvolutionDef> options)
        {
            RectTransform root = BeginScreen("evolution", false);
            if (root == null) return;
            DrawBackdrop(root, GalaxyUiTheme.Violet);
            GalaxyUiTheme.Label(root, "ВЫБОР ЭВОЛЮЦИИ", 54, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                GBox.At(-560f, 394f, 700f, 72f), FontStyle.Bold);
            GalaxyUiTheme.Label(root, "Выберите ветвь — изменения сохраняются до конца матча", 21, GalaxyUiTheme.AccentC,
                TextAnchor.MiddleLeft, GBox.At(-560f, 350f, 780f, 34f));

            List<EvolutionDef> cards = PickEvolutionOptions(options);
            float[] xs = { -530f, 0f, 530f };
            for (int i = 0; i < cards.Count && i < 3; i++)
            {
                EvolutionDef def = cards[i];
                Color accent = BranchColor(def.Branch);
                Image card = GalaxyUiTheme.Panel(root, "Эволюция " + def.Id, new Color32(16, 23, 48, 248),
                    GBox.At(xs[i], -16f, 492f, 636f), GalaxyUiTheme.Round);
                GalaxyUiTheme.Panel(card.transform, "Верхняя полоса", accent, new GBox(0f, 1f, 1f, 1f, 0f, -8f, 0f, 0f));
                GalaxyUiTheme.Label(card.transform, BranchName(def.Branch) + " · УРОВЕНЬ " + def.Tier, 18, accent,
                    TextAnchor.MiddleCenter, GBox.Inset(12f, 588f, 12f, 14f), FontStyle.Bold);
                GalaxyUiTheme.Label(card.transform, def.Name, 38, GalaxyUiTheme.Paper, TextAnchor.MiddleCenter,
                    GBox.Inset(12f, 536f, 12f, 50f), FontStyle.Bold);

                RectTransform hullRt = GalaxyUiTheme.Place(card.transform, "Корпус", GBox.At(0f, 152f, 150f, 150f));
                Image hull = hullRt.gameObject.AddComponent<Image>();
                hull.sprite = GalaxyUiTheme.Hull(def.Hull);
                hull.color = accent;
                hull.raycastTarget = false;

                GalaxyUiTheme.Label(card.transform, def.Description, 18, GalaxyUiTheme.Muted, TextAnchor.UpperCenter,
                    new GBox(0f, 0f, 1f, 0f, 24f, 330f, -24f, 390f));
                GalaxyUiTheme.Label(card.transform, StatDeltas(def) + "\n" + ExtraEvolutionInfo(def), 17,
                    GalaxyUiTheme.Paper, TextAnchor.UpperLeft,
                    new GBox(0f, 0f, 1f, 0f, 28f, 100f, -28f, 320f));

                EvolutionDef captured = def;
                GalaxyUiTheme.MakeButton(card.transform, "Выбрать", "ВЫБРАТЬ ВЕТВЬ", accent, GalaxyUiTheme.Ink,
                    GBox.Inset(24f, 20f, 24f, 548f), 24, delegate
                    {
                        if (_app != null) _app.ChooseEvolution(captured);
                        ShowHud();
                    });
            }

            GalaxyUiTheme.Label(root, "Подсказка: Титан держит удар, Фантом охотится, Рой контролирует поле, Сингулярность притягивает ресурсы.",
                18, GalaxyUiTheme.Muted, TextAnchor.MiddleCenter, GBox.At(0f, -376f, 1500f, 40f));
        }

        private List<EvolutionDef> PickEvolutionOptions(List<EvolutionDef> options)
        {
            List<EvolutionDef> result = new List<EvolutionDef>();
            if (options != null)
            {
                for (int i = 0; i < options.Count && result.Count < 3; i++)
                    if (options[i] != null && !result.Contains(options[i])) result.Add(options[i]);
            }
            if (result.Count >= 3) return result;
            EvolutionBranch[] branches =
            {
                EvolutionBranch.Titan, EvolutionBranch.Phantom, EvolutionBranch.Swarm, EvolutionBranch.Singularity
            };
            int tier = 1;
            if (_app != null && _app.Match != null && _app.Match.Player != null)
                tier = Mathf.Clamp(Mathf.FloorToInt(_app.Match.Player.Level / 5f) + 1, 1, 3);
            for (int i = 0; i < branches.Length && result.Count < 3; i++)
            {
                EvolutionDef def = ContentDatabase.FindEvolution(branches[i], tier);
                if (def != null && !result.Contains(def)) result.Add(def);
            }
            return result;
        }

        private static string StatDeltas(EvolutionDef def)
        {
            List<string> rows = new List<string>();
            AddDelta(rows, "МАССА", def.MassMul);
            AddDelta(rows, "СКОРОСТЬ", def.SpeedMul);
            AddDelta(rows, "УРОН", def.DamageMul);
            AddDelta(rows, "ПРОЧНОСТЬ", def.HpMul);
            AddDelta(rows, "СКОРОСТРЕЛЬНОСТЬ", def.FireRateMul);
            AddDelta(rows, "СБОР", def.PickupMul);
            AddDelta(rows, "ЩИТ", def.ShieldMul);
            if (rows.Count == 0) rows.Add("Без изменений базовых характеристик");
            return string.Join("\n", rows.ToArray());
        }

        private static void AddDelta(List<string> rows, string label, float multiplier)
        {
            int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
            if (percent == 0) return;
            rows.Add(label + "  " + (percent > 0 ? "+" : string.Empty) + percent + "%");
        }

        private static string ExtraEvolutionInfo(EvolutionDef def)
        {
            List<string> rows = new List<string>();
            rows.Add("ОРУЖИЕ: " + ContentDatabase.FindWeapon(def.Weapon).Name);
            if (def.Drones > 0) rows.Add("ДРОНЫ: +" + def.Drones);
            if (def.ExtraProjectiles > 0) rows.Add("СНАРЯДЫ: +" + def.ExtraProjectiles);
            if (def.GravityPower > 0f) rows.Add("ГРАВИТАЦИЯ: " + def.GravityPower.ToString("F2"));
            if (def.Lifesteal > 0f) rows.Add("ВОМПИРИЗМ: " + Mathf.RoundToInt(def.Lifesteal * 100f) + "%");
            return string.Join("\n", rows.ToArray());
        }

        private static Color BranchColor(EvolutionBranch branch)
        {
            switch (branch)
            {
                case EvolutionBranch.Titan: return GalaxyUiTheme.AccentB;
                case EvolutionBranch.Phantom: return GalaxyUiTheme.AccentC;
                case EvolutionBranch.Swarm: return GalaxyUiTheme.AccentA;
                case EvolutionBranch.Singularity: return GalaxyUiTheme.Violet;
                default: return GalaxyUiTheme.Sun;
            }
        }

        private static string BranchName(EvolutionBranch branch)
        {
            switch (branch)
            {
                case EvolutionBranch.Titan: return "ВЕТВЬ ТИТАНА";
                case EvolutionBranch.Phantom: return "ВЕТВЬ ФАНТОМА";
                case EvolutionBranch.Swarm: return "ВЕТВЬ РОЯ";
                case EvolutionBranch.Singularity: return "ВЕТВЬ СИНГУЛЯРНОСТИ";
                default: return "НОВАЯ ВЕТВЬ";
            }
        }

        // ───────────────────────────── Result ─────────────────────────────

        public void ShowResult(MatchResult result)
        {
            RectTransform root = BeginScreen("result", false);
            if (root == null || result == null) return;
            DrawBackdrop(root, result.Victory ? GalaxyUiTheme.AccentA : GalaxyUiTheme.AccentB);

            Color tone = result.Victory ? GalaxyUiTheme.AccentA : GalaxyUiTheme.AccentB;
            GalaxyUiTheme.Label(root, result.Victory ? "ПОБЕДА" : "МАТЧ ЗАВЕРШЁН", 62, tone, TextAnchor.MiddleLeft,
                GBox.At(-560f, 398f, 700f, 80f), FontStyle.Bold);
            ModeDef mode = ContentDatabase.FindMode(result.Mode);
            GalaxyUiTheme.Label(root, mode.Name + " · место " + result.Rank + " из " + result.Participants +
                " · длительность " + FormatTime(result.Duration), 21, GalaxyUiTheme.Muted,
                TextAnchor.MiddleLeft, GBox.At(-560f, 354f, 900f, 34f));

            Image stats = GalaxyUiTheme.Panel(root, "Сводка", new Color32(14, 21, 45, 246), GBox.At(-450f, 112f, 660f, 400f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(stats.transform, "РЕЗУЛЬТАТ", 20, tone, TextAnchor.MiddleLeft,
                GBox.Inset(22f, 356f, 22f, 12f), FontStyle.Bold);
            string lines =
                "Очки              " + FormatNumber(Mathf.RoundToInt(result.Score)) + "\n" +
                "Масса             " + result.Mass.ToString("F1") + "\n" +
                "Уровень           " + result.Level + "\n" +
                "Убийства / смерти " + result.Kills + " / " + result.Deaths + "\n" +
                "Лучшая серия      " + result.BestStreak + "\n" +
                "Энергия           " + result.EnergyCollected.ToString("F0") + "\n" +
                "Кристаллы / ТМ    " + result.Crystals + " / " + result.DarkMatter + "\n" +
                "Артефакты / боссы " + result.Artifacts + " / " + result.BossKills;
            GalaxyUiTheme.Label(stats.transform, lines, 22, GalaxyUiTheme.Paper, TextAnchor.UpperLeft,
                GBox.Inset(24f, 22f, 24f, 52f));

            Image rewards = GalaxyUiTheme.Panel(root, "Награды", new Color32(20, 32, 46, 246), GBox.At(250f, 250f, 620f, 124f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(rewards.transform, "НАГРАДЫ", 19, GalaxyUiTheme.AccentA, TextAnchor.MiddleLeft,
                GBox.Inset(18f, 84f, 18f, 8f), FontStyle.Bold);
            GalaxyUiTheme.Label(rewards.transform, "+" + result.Credits + " кредитов    +" + result.CrystalsReward +
                " кристаллов    +" + result.Xp + " XP    +" + result.AccountXp + " XP аккаунта", 20, GalaxyUiTheme.Paper,
                TextAnchor.MiddleLeft, GBox.Inset(18f, 18f, 18f, 46f));

            Image streak = GalaxyUiTheme.Panel(root, "Серии", new Color32(32, 22, 52, 246), GBox.At(250f, 112f, 620f, 124f), GalaxyUiTheme.Round);
            AccountProfile profile = _app == null ? null : _app.Profile;
            GalaxyUiTheme.Label(streak.transform, "СЕРИИ И ПРОГРЕСС", 19, GalaxyUiTheme.Violet, TextAnchor.MiddleLeft,
                GBox.Inset(18f, 84f, 18f, 8f), FontStyle.Bold);
            GalaxyUiTheme.Label(streak.transform, "В матче: " + result.BestStreak +
                "   ·   Текущая серия побед: " + (profile == null ? 0 : profile.WinStreak) +
                "   ·   Рекорд: " + (profile == null ? 0 : profile.BestWinStreak), 19, GalaxyUiTheme.Paper,
                TextAnchor.MiddleLeft, GBox.Inset(18f, 16f, 18f, 48f));

            Image achievements = GalaxyUiTheme.Panel(root, "Достижения", new Color32(20, 28, 58, 246), GBox.At(250f, -20f, 620f, 128f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(achievements.transform, "ДОСТИЖЕНИЯ МАТЧА", 19, GalaxyUiTheme.Sun, TextAnchor.MiddleLeft,
                GBox.Inset(18f, 88f, 18f, 8f), FontStyle.Bold);
            List<string> earned = EarnedAchievements(result, profile);
            string earnedText = earned.Count == 0
                ? "В этом матче новых достижений нет — продолжайте охоту."
                : string.Join("   ·   ", earned.ToArray());
            GalaxyUiTheme.Label(achievements.transform, earnedText, 17, GalaxyUiTheme.Paper, TextAnchor.UpperLeft,
                GBox.Inset(18f, 10f, 18f, 48f));

            Image board = GalaxyUiTheme.Panel(root, "Таблица", new Color32(10, 16, 36, 246), GBox.At(-420f, -258f, 720f, 232f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(board.transform, "ИТОГОВАЯ ТАБЛИЦА (TOP " + Mathf.Min(8, result.Leaderboard == null ? 0 : result.Leaderboard.Count) + ")",
                19, GalaxyUiTheme.AccentC, TextAnchor.MiddleLeft, GBox.Inset(18f, 198f, 18f, 8f), FontStyle.Bold);
            string boardText = BuildResultBoard(result);
            GalaxyUiTheme.Label(board.transform, boardText, 16, GalaxyUiTheme.Paper, TextAnchor.UpperLeft,
                GBox.Inset(18f, 10f, 18f, 42f));

            Image highlights = GalaxyUiTheme.Panel(root, "Моменты", new Color32(14, 21, 45, 246), GBox.At(320f, -222f, 680f, 296f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(highlights.transform, "КЛЮЧЕВЫЕ МОМЕНТЫ", 19, GalaxyUiTheme.AccentB, TextAnchor.MiddleLeft,
                GBox.Inset(18f, 262f, 18f, 8f), FontStyle.Bold);
            GalaxyUiTheme.Label(highlights.transform, BuildHighlights(result), 17, GalaxyUiTheme.Paper, TextAnchor.UpperLeft,
                GBox.Inset(18f, 14f, 18f, 48f));

            GalaxyUiTheme.MakeButton(root, "Реванш", "РЕМАТЧ", GalaxyUiTheme.AccentA, GalaxyUiTheme.Ink,
                GBox.At(-260f, -412f, 460f, 76f), 26, delegate { if (_app != null) _app.RestartMatch(); });
            GalaxyUiTheme.MakeButton(root, "Меню", "В МЕНЮ", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(280f, -412f, 460f, 76f), 26, delegate { if (_app != null) _app.ReturnToMenu(); });
        }

        private static string BuildResultBoard(MatchResult result)
        {
            if (result.Leaderboard == null || result.Leaderboard.Count == 0) return "Таблица недоступна.";
            StringBuilder sb = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < result.Leaderboard.Count && shown < 8; i++)
            {
                LeaderboardEntry e = result.Leaderboard[i];
                if (e == null) continue;
                int rank = e.Rank > 0 ? e.Rank : shown + 1;
                string row = rank.ToString("00") + "  " + Clip(e.Name, 13) + "  " + Mathf.RoundToInt(e.Score) +
                             "  м" + Mathf.RoundToInt(e.Mass) + "  ур" + e.Level + "  к" + e.Kills;
                sb.Append(row);
                if (shown < 7) sb.Append('\n');
                shown++;
            }
            return sb.ToString();
        }

        private static string BuildHighlights(MatchResult result)
        {
            if (result.Highlights == null || result.Highlights.Count == 0)
                return "Запись событий матча пуста.";
            StringBuilder sb = new StringBuilder();
            int count = Mathf.Min(7, result.Highlights.Count);
            for (int i = 0; i < count; i++)
            {
                SimEvent ev = result.Highlights[i];
                string text = FormatEvent(ev);
                if (string.IsNullOrEmpty(text)) continue;
                sb.Append(FormatTime(ev.Time)).Append("  ").Append(text);
                if (i < count - 1) sb.Append('\n');
            }
            return sb.Length == 0 ? "Ярких моментов не зафиксировано." : sb.ToString();
        }

        private static List<string> EarnedAchievements(MatchResult result, AccountProfile profile)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < ContentDatabase.Achievements.Length; i++)
            {
                AchievementDef def = ContentDatabase.Achievements[i];
                if (def == null || !AchievementEarned(def, result, profile)) continue;
                if (names.Count < 6) names.Add(def.Name);
            }
            return names;
        }

        private static bool AchievementEarned(AchievementDef def, MatchResult result, AccountProfile profile)
        {
            switch (def.Id)
            {
                case "first_blood": return result.Kills >= def.Target;
                case "energy_1000": return result.EnergyCollected >= def.Target;
                case "mass_100": return result.Mass >= def.Target;
                case "kills_10": return result.Kills >= def.Target;
                case "streak_5": return result.BestStreak >= def.Target;
                case "boss_1": return profile != null && profile.BossKills >= def.Target;
                case "boss_5": return profile != null && profile.BossKills >= def.Target;
                case "rank_1": return result.Rank == 1;
                case "evolution_3": return profile != null && ProfileStat(profile, "evolutionTier") >= def.Target;
                case "artifact_10": return profile != null && ProfileStat(profile, "artifacts") >= def.Target;
                case "dark_100": return profile != null && ProfileStat(profile, "darkMatter") >= def.Target;
                case "long_run": return result.Duration >= def.Target;
                case "hardcore_win": return result.Mode == GameModeKind.Hardcore && result.Victory;
                case "perfect": return result.Rank == 1 && result.Deaths == 0;
                default:
                    return profile != null && profile.Achievements != null && profile.Achievements.Contains(def.Id);
            }
        }

        private static int ProfileStat(AccountProfile profile, string key)
        {
            if (profile == null || profile.Stats == null) return 0;
            int value;
            return profile.Stats.TryGetValue(key, out value) ? value : 0;
        }

        // ───────────────────────────── Profile ─────────────────────────────

        public void ShowProfile()
        {
            RectTransform root = BeginScreen("profile", false);
            if (root == null) return;
            DrawBackdrop(root, GalaxyUiTheme.AccentA);
            DrawHeader(root, "ПРОФИЛЬ", "Аккаунт, задания и достижения", ShowMenu);
            DrawTabs(root, ProfileTabs, _profileTab, delegate(int index) { _profileTab = index; ShowProfile(); });

            if (_profileTab == 0) DrawProfileOverview(root);
            else if (_profileTab == 1) DrawQuests(root);
            else DrawAchievementList(root);
        }

        private void DrawProfileOverview(RectTransform root)
        {
            AccountProfile p = _app == null ? null : _app.Profile;
            float[] xs = { -530f, 0f, 530f };
            float[] ys = { 198f, 58f, -82f };
            StatCard(root, xs[0], ys[0], "УРОВЕНЬ АККАУНТА", p == null ? "—" : p.Level.ToString(), GalaxyUiTheme.AccentA);
            StatCard(root, xs[1], ys[0], "КРЕДИТЫ", p == null ? "—" : FormatNumber(p.Credits), GalaxyUiTheme.Sun);
            StatCard(root, xs[2], ys[0], "КРИСТАЛЛЫ / ТМ", p == null ? "—" : p.Crystals + " / " + p.DarkMatter, GalaxyUiTheme.AccentC);
            StatCard(root, xs[0], ys[1], "МАТЧИ / ПОБЕДЫ", p == null ? "—" : p.Matches + " / " + p.Wins, GalaxyUiTheme.Paper);
            StatCard(root, xs[1], ys[1], "СЕРИЯ ПОБЕД", p == null ? "—" : p.WinStreak + " (рекорд " + p.BestWinStreak + ")", GalaxyUiTheme.AccentB);
            StatCard(root, xs[2], ys[1], "БОССЫ", p == null ? "—" : p.BossKills.ToString(), GalaxyUiTheme.Violet);

            Image gear = GalaxyUiTheme.Panel(root, "Снаряжение", new Color32(14, 21, 45, 246), GBox.At(0f, -272f, 1500f, 210f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(gear.transform, "ТЕКУЩЕЕ СНАРЯЖЕНИЕ", 19, GalaxyUiTheme.AccentA, TextAnchor.MiddleLeft,
                GBox.Inset(20f, 174f, 20f, 8f), FontStyle.Bold);
            string loadout = p == null
                ? "Профиль не загружен"
                : "КОРАБЛЬ " + NameOrDash(p.SelectedShip) + "    СЛЕД " + NameOrDash(p.SelectedTrail) +
                  "    ЭФФЕКТ " + NameOrDash(p.SelectedDeathFx) + "\n" +
                  "РАМКА " + NameOrDash(p.SelectedFrame) + "    АВАТАР " + NameOrDash(p.SelectedAvatar) +
                  "    ТИТУЛ " + NameOrDash(p.SelectedTitle);
            GalaxyUiTheme.Label(gear.transform, loadout, 21, GalaxyUiTheme.Paper, TextAnchor.UpperLeft,
                GBox.Inset(24f, 18f, 24f, 52f));
            GalaxyUiTheme.MakeButton(gear.transform, "В магазин", "ИЗМЕНИТЬ В МАГАЗИНЕ", GalaxyUiTheme.PanelSoft,
                GalaxyUiTheme.Paper, new GBox(0f, 0f, 1f, 1f, 1220f, 16f, -20f, -120f), 18, ShowShop);

            GalaxyUiTheme.Label(root, "Открыто косметики: " + (p == null || p.Owned == null ? 0 : p.Owned.Count) +
                "   ·   Секретов: " + (p == null || p.Secrets == null ? 0 : p.Secrets.Count) +
                "   ·   Достижений: " + (p == null || p.Achievements == null ? 0 : p.Achievements.Count),
                19, GalaxyUiTheme.Muted, TextAnchor.MiddleCenter, GBox.At(0f, -404f, 1300f, 34f));
        }

        private static void StatCard(RectTransform root, float x, float y, string caption, string value, Color accent)
        {
            Image card = GalaxyUiTheme.Panel(root, caption, new Color32(16, 23, 48, 246), GBox.At(x, y, 500f, 120f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Panel(card.transform, "Полоса", accent, GBox.Inset(0f, 0f, -8f, 0f));
            GalaxyUiTheme.Label(card.transform, caption, 16, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                GBox.Inset(24f, 76f, 20f, 14f));
            GalaxyUiTheme.Label(card.transform, value, 28, accent, TextAnchor.MiddleLeft,
                GBox.Inset(24f, 14f, 20f, 50f), FontStyle.Bold);
        }

        private void DrawQuests(RectTransform root)
        {
            RectTransform content;
            GalaxyUiTheme.Scroll(root, "Задания", GBox.At(0f, -50f, 1520f, 610f), out content);
            AccountProfile p = _app == null ? null : _app.Profile;
            float y = 0f;
            GalaxyUiTheme.Label(content, "ЕЖЕДНЕВНЫЕ ЗАДАНИЯ", 22, GalaxyUiTheme.AccentA, TextAnchor.MiddleLeft,
                GBox.TopRow(y, 40f, 22f, 22f), FontStyle.Bold);
            y += 48f;
            List<QuestDef> daily = ContentDatabase.DailyQuests(DateTime.UtcNow);
            for (int i = 0; i < daily.Count; i++) y = DrawQuestRow(content, daily[i], y, p, false);
            y += 18f;
            GalaxyUiTheme.Label(content, "НЕДЕЛЬНЫЕ ЗАДАНИЯ", 22, GalaxyUiTheme.Violet, TextAnchor.MiddleLeft,
                GBox.TopRow(y, 40f, 22f, 22f), FontStyle.Bold);
            y += 48f;
            List<QuestDef> weekly = ContentDatabase.WeeklyQuests(DateTime.UtcNow);
            for (int i = 0; i < weekly.Count; i++) y = DrawQuestRow(content, weekly[i], y, p, true);
            y += 18f;
            GalaxyUiTheme.Label(content, "Прогресс заданий обновляется симуляцией после каждого матча. Награды начисляются автоматически.",
                18, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, GBox.TopRow(y, 44f, 22f, 22f));
            y += 56f;
            GalaxyUiTheme.SetScrollHeight(content, y + 12f);
        }

        private static float DrawQuestRow(RectTransform content, QuestDef q, float y, AccountProfile p, bool weekly)
        {
            const float h = 96f;
            Image row = GalaxyUiTheme.Panel(content, "Задание " + q.Id, new Color32(18, 26, 54, 248),
                GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
            int progress = 0;
            if (p != null)
            {
                Dictionary<string, int> source = weekly ? p.WeeklyProgress : p.DailyProgress;
                if (source != null && !source.TryGetValue(q.Id, out progress)) progress = 0;
            }
            float ratio = q.Target > 0 ? Mathf.Clamp01((float)progress / q.Target) : 0f;
            Color accent = weekly ? GalaxyUiTheme.Violet : GalaxyUiTheme.AccentA;
            GalaxyUiTheme.Label(row.transform, q.Name, 23, accent, TextAnchor.MiddleLeft,
                GBox.Inset(20f, 58f, 20f, 8f), FontStyle.Bold);
            GalaxyUiTheme.Label(row.transform, "Цель: " + FieldName(q.Field) + " " + q.Target + "   ·   Награда: " +
                q.Credits + " кредитов, " + q.Crystals + " кристаллов", 17, GalaxyUiTheme.Muted,
                TextAnchor.MiddleLeft, GBox.Inset(20f, 32f, 20f, 42f));
            GBar bar = GalaxyUiTheme.Bar(row.transform, "Прогресс", new Color32(30, 38, 66, 255), accent,
                GBox.Inset(20f, 8f, 120f, 30f));
            bar.Set(ratio);
            GalaxyUiTheme.Label(row.transform, progress + " / " + q.Target, 20, GalaxyUiTheme.Paper,
                TextAnchor.MiddleRight, new GBox(0f, 0f, 1f, 1f, 1280f, 8f, -20f, -42f), FontStyle.Bold);
            return y + h + 10f;
        }

        private void DrawAchievementList(RectTransform root)
        {
            RectTransform content;
            GalaxyUiTheme.Scroll(root, "Достижения", GBox.At(0f, -50f, 1520f, 610f), out content);
            AccountProfile p = _app == null ? null : _app.Profile;
            float y = 0f;
            for (int i = 0; i < ContentDatabase.Achievements.Length; i++)
            {
                AchievementDef def = ContentDatabase.Achievements[i];
                bool unlocked = p != null && p.Achievements != null && p.Achievements.Contains(def.Id);
                const float h = 84f;
                Image row = GalaxyUiTheme.Panel(content, "Достижение " + def.Id,
                    unlocked ? new Color32(22, 40, 34, 248) : new Color32(16, 23, 48, 248),
                    GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
                Image dot = GalaxyUiTheme.CircleImage(row.transform, "Маркер", unlocked ? GalaxyUiTheme.AccentA : new Color32(70, 82, 116, 255),
                    new GBox(0f, 0.5f, 0f, 0.5f, 22f, -15f, 52f, 15f));
                GalaxyUiTheme.Label(row.transform, def.Name, 21, unlocked ? GalaxyUiTheme.AccentA : GalaxyUiTheme.Paper,
                    TextAnchor.MiddleLeft, new GBox(0f, 0f, 1f, 1f, 68f, 40f, -360f, -6f), FontStyle.Bold);
                GalaxyUiTheme.Label(row.transform, def.Description, 16, GalaxyUiTheme.Muted,
                    TextAnchor.MiddleLeft, new GBox(0f, 0f, 1f, 1f, 68f, 8f, -360f, -42f));
                GalaxyUiTheme.Label(row.transform, (unlocked ? "ПОЛУЧЕНО" : "ЦЕЛЬ " + def.Target) + "\n+" + def.Credits +
                    " кр · +" + def.Crystals + " крист.", 17, unlocked ? GalaxyUiTheme.AccentA : GalaxyUiTheme.Sun,
                    TextAnchor.MiddleRight, new GBox(0f, 0f, 1f, 1f, 1120f, 8f, -22f, -8f), FontStyle.Bold);
                if (dot != null) dot.raycastTarget = false;
                y += h + 8f;
            }
            GalaxyUiTheme.SetScrollHeight(content, y + 12f);
        }

        // ───────────────────────────── Shop ─────────────────────────────

        public void ShowShop()
        {
            RectTransform root = BeginScreen("shop", false);
            if (root == null) return;
            DrawBackdrop(root, GalaxyUiTheme.Sun);
            AccountProfile p = _app == null ? null : _app.Profile;
            DrawHeader(root, "МАГАЗИН", "Кредиты: " + (p == null ? 0 : p.Credits) + "   ·   Кристаллы: " + (p == null ? 0 : p.Crystals), ShowMenu);
            DrawTabs(root, ShopTabs, _shopTab, delegate(int index) { _shopTab = index; ShowShop(); });
            if (_shopTab == 0) DrawShipShop(root);
            else if (_shopTab == 1) DrawEquipmentShop(root);
            else DrawResearch(root);
        }

        private void DrawShipShop(RectTransform root)
        {
            RectTransform content;
            GalaxyUiTheme.Scroll(root, "Корабли", GBox.At(0f, -50f, 1520f, 610f), out content);
            AccountProfile p = _app == null ? null : _app.Profile;
            float y = 0f;
            for (int i = 0; i < ContentDatabase.Cosmetics.Length; i++)
            {
                CosmeticDef def = ContentDatabase.Cosmetics[i];
                if (def.Category != "ship") continue;
                ShipVisualDef visual = ContentDatabase.FindShipVisual(def.Id);
                HullShape hull = visual == null ? HullShape.Dart : visual.Hull;
                y = DrawCosmeticRow(content, def, hull, y, p, p != null && p.SelectedShip == def.Id);
            }
            GalaxyUiTheme.SetScrollHeight(content, y + 12f);
        }

        private void DrawEquipmentShop(RectTransform root)
        {
            RectTransform content;
            GalaxyUiTheme.Scroll(root, "Снаряжение", GBox.At(0f, -50f, 1520f, 610f), out content);
            AccountProfile p = _app == null ? null : _app.Profile;
            string[] categories = { "trail", "death", "frame", "avatar", "title" };
            string[] captions = { "СЛЕДЫ", "ЭФФЕКТЫ УНИЧТОЖЕНИЯ", "РАМКИ", "АВАТАРЫ", "ТИТУЛЫ" };
            float y = 0f;
            for (int c = 0; c < categories.Length; c++)
            {
                GalaxyUiTheme.Label(content, captions[c], 21, GalaxyUiTheme.AccentC, TextAnchor.MiddleLeft,
                    GBox.TopRow(y, 38f, 22f, 22f), FontStyle.Bold);
                y += 44f;
                for (int i = 0; i < ContentDatabase.Cosmetics.Length; i++)
                {
                    CosmeticDef def = ContentDatabase.Cosmetics[i];
                    if (def.Category != categories[c]) continue;
                    y = DrawCosmeticRow(content, def, HullShape.Hex, y, p, IsEquipped(p, def));
                }
                y += 14f;
            }
            GalaxyUiTheme.SetScrollHeight(content, y + 12f);
        }

        private static float DrawCosmeticRow(RectTransform content, CosmeticDef def, HullShape hull, float y, AccountProfile p, bool equipped)
        {
            const float h = 88f;
            Color accent = ParseHex(def.Hex, GalaxyUiTheme.AccentC);
            Image row = GalaxyUiTheme.Panel(content, "Предмет " + def.Id, equipped ? new Color32(24, 44, 40, 248) : new Color32(16, 23, 48, 248),
                GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
            RectTransform iconRt = GalaxyUiTheme.Place(row.transform, "Иконка",
                new GBox(0f, 0.5f, 0f, 0.5f, 26f, -26f, 78f, 26f));
            Image icon = iconRt.gameObject.AddComponent<Image>();
            icon.sprite = GalaxyUiTheme.Hull(hull);
            icon.color = accent;
            icon.raycastTarget = false;
            GalaxyUiTheme.Label(row.transform, def.Name, 22, accent, TextAnchor.MiddleLeft,
                new GBox(0f, 0f, 1f, 1f, 94f, 44f, -520f, -4f), FontStyle.Bold);
            GalaxyUiTheme.Label(row.transform, def.UnlockHint, 15, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                new GBox(0f, 0f, 1f, 1f, 94f, 10f, -520f, -42f));

            bool owned = p != null && p.Owned != null && p.Owned.Contains(def.Id);
            string status = equipped ? "НАДЕТО" : (owned ? "КУПЛЕНО" : (def.Cost > 0 ? def.Cost + " ◈" : "БЕСПЛАТНО"));
            GalaxyUiTheme.Label(row.transform, status, 19, equipped ? GalaxyUiTheme.AccentA : GalaxyUiTheme.Paper,
                TextAnchor.MiddleRight, new GBox(0f, 0f, 1f, 1f, 960f, 8f, -220f, -8f), FontStyle.Bold);

            CosmeticDef captured = def;
            if (!owned)
            {
                GalaxyUiTheme.MakeButton(row.transform, "Купить", "КУПИТЬ", GalaxyUiTheme.AccentA, GalaxyUiTheme.Ink,
                    new GBox(0f, 0f, 1f, 1f, 1100f, 14f, -20f, -14f), 17, delegate { BuyCosmetic(captured); });
            }
            else if (!equipped)
            {
                GalaxyUiTheme.MakeButton(row.transform, "Надеть", "НАДЕТЬ", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                    new GBox(0f, 0f, 1f, 1f, 1100f, 14f, -20f, -14f), 17, delegate { EquipCosmetic(captured); });
            }
            else
            {
                GalaxyUiTheme.Label(row.transform, "АКТИВНО", 18, GalaxyUiTheme.AccentA, TextAnchor.MiddleCenter,
                    new GBox(0f, 0f, 1f, 1f, 1100f, 14f, -20f, -14f), FontStyle.Bold);
            }
            return y + h + 10f;
        }

        private void DrawResearch(RectTransform root)
        {
            RectTransform content;
            GalaxyUiTheme.Scroll(root, "Исследования", GBox.At(0f, -50f, 1520f, 610f), out content);
            AccountProfile p = _app == null ? null : _app.Profile;
            GalaxyUiTheme.Label(content, "Исследования дают постоянный бонус ко всем матчам. Уровень растёт за кредиты.",
                19, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, GBox.TopRow(0f, 40f, 22f, 22f));
            float y = 52f;
            for (int i = 0; i < ContentDatabase.Research.Length; i++)
            {
                ResearchDef def = ContentDatabase.Research[i];
                int level = 0;
                if (p != null && p.Research != null && !p.Research.TryGetValue(def.Id, out level)) level = 0;
                int cost = def.BaseCost * (level + 1);
                bool maxed = level >= def.MaxLevel;
                const float h = 86f;
                Image row = GalaxyUiTheme.Panel(content, "Исследование " + def.Id, new Color32(18, 26, 54, 248),
                    GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
                GalaxyUiTheme.Label(row.transform, def.Name, 22, GalaxyUiTheme.Violet, TextAnchor.MiddleLeft,
                    GBox.Inset(22f, 50f, 20f, 6f), FontStyle.Bold);
                GalaxyUiTheme.Label(row.transform, def.Description + "   Уровень " + level + " / " + def.MaxLevel +
                    "   ·   Бонус сейчас " + Mathf.RoundToInt(level * def.BonusPerLevel * 100f) + "%", 16,
                    GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, GBox.Inset(22f, 10f, 20f, 50f));
                GBar bar = GalaxyUiTheme.Bar(row.transform, "Уровень", new Color32(30, 38, 66, 255), GalaxyUiTheme.Violet,
                    new GBox(0f, 0f, 1f, 1f, 900f, 30f, -440f, -44f));
                bar.Set(def.MaxLevel > 0 ? (float)level / def.MaxLevel : 0f);
                ResearchDef captured = def;
                GalaxyUiTheme.MakeButton(row.transform, "Исследовать", maxed ? "МАКСИМУМ" : cost + " ◈",
                    maxed ? GalaxyUiTheme.PanelSoft : GalaxyUiTheme.Violet, GalaxyUiTheme.Ink,
                    new GBox(0f, 0f, 1f, 1f, 1240f, 14f, -20f, -14f), 18, delegate { BuyResearch(captured); });
                y += h + 10f;
            }
            GalaxyUiTheme.SetScrollHeight(content, y + 12f);
        }

        private void BuyCosmetic(CosmeticDef def)
        {
            AccountProfile p = _app == null ? null : _app.Profile;
            if (p == null || def == null || p.IsUnlocked(def.Id)) return;
            if (def.Cost > 0)
            {
                if (p.Credits < def.Cost) { ShowToast("Недостаточно кредитов"); return; }
                p.Credits -= def.Cost;
            }
            p.Unlock(def.Id);
            Persist();
            ShowToast("Открыто: " + def.Name);
            ShowShop();
        }

        private void EquipCosmetic(CosmeticDef def)
        {
            AccountProfile p = _app == null ? null : _app.Profile;
            if (p == null || def == null || !IsOwned(p, def.Id)) return;
            switch (def.Category)
            {
                case "ship": p.SelectedShip = def.Id; break;
                case "trail": p.SelectedTrail = def.Id; break;
                case "death": p.SelectedDeathFx = def.Id; break;
                case "frame": p.SelectedFrame = def.Id; break;
                case "avatar": p.SelectedAvatar = def.Id; break;
                case "title": p.SelectedTitle = def.Id; break;
                default: return;
            }
            Persist();
            ShowToast("Надето: " + def.Name);
            ShowShop();
        }

        private void BuyResearch(ResearchDef def)
        {
            AccountProfile p = _app == null ? null : _app.Profile;
            if (p == null || def == null || p.Research == null) return;
            int level;
            if (!p.Research.TryGetValue(def.Id, out level)) level = 0;
            if (level >= def.MaxLevel) return;
            int cost = def.BaseCost * (level + 1);
            if (p.Credits < cost) { ShowToast("Недостаточно кредитов"); return; }
            p.Credits -= cost;
            p.Research[def.Id] = level + 1;
            Persist();
            ShowToast("Исследование: " + def.Name + " " + (level + 1) + " ур.");
            ShowShop();
        }

        private static bool IsOwned(AccountProfile p, string id)
        {
            return p != null && p.Owned != null && p.Owned.Contains(id);
        }

        private static bool IsEquipped(AccountProfile p, CosmeticDef def)
        {
            if (p == null || def == null) return false;
            switch (def.Category)
            {
                case "ship": return p.SelectedShip == def.Id;
                case "trail": return p.SelectedTrail == def.Id;
                case "death": return p.SelectedDeathFx == def.Id;
                case "frame": return p.SelectedFrame == def.Id;
                case "avatar": return p.SelectedAvatar == def.Id;
                case "title": return p.SelectedTitle == def.Id;
                default: return false;
            }
        }

        // ───────────────────────────── Collection ─────────────────────────────

        public void ShowCollection()
        {
            RectTransform root = BeginScreen("collection", false);
            if (root == null) return;
            DrawBackdrop(root, GalaxyUiTheme.Violet);
            AccountProfile p = _app == null ? null : _app.Profile;
            int ownedCount = p == null || p.Owned == null ? 0 : p.Owned.Count;
            DrawHeader(root, "КОЛЛЕКЦИЯ", "Открыто " + ownedCount + " из " + ContentDatabase.Cosmetics.Length +
                " предметов · сведений: " + (p == null || p.Secrets == null ? 0 : p.Secrets.Count), ShowMenu);
            DrawTabs(root, CollectionTabs, _collectionTab, delegate(int index) { _collectionTab = index; ShowCollection(); });

            RectTransform content;
            GalaxyUiTheme.Scroll(root, "Коллекция", GBox.At(0f, -50f, 1520f, 610f), out content);
            float y = 0f;
            string[] categories = _collectionTab == 0 ? new[] { "ship" }
                : _collectionTab == 1 ? new[] { "trail", "death" }
                : new[] { "frame", "avatar", "title" };
            for (int c = 0; c < categories.Length; c++)
            {
                int total = 0;
                int owned = 0;
                for (int i = 0; i < ContentDatabase.Cosmetics.Length; i++)
                {
                    CosmeticDef def = ContentDatabase.Cosmetics[i];
                    if (def.Category != categories[c]) continue;
                    total++;
                    if (IsOwned(p, def.Id)) owned++;
                }
                GalaxyUiTheme.Label(content, CategoryName(categories[c]) + " — " + owned + " / " + total, 21,
                    GalaxyUiTheme.AccentC, TextAnchor.MiddleLeft, GBox.TopRow(y, 38f, 22f, 22f), FontStyle.Bold);
                y += 44f;
                for (int i = 0; i < ContentDatabase.Cosmetics.Length; i++)
                {
                    CosmeticDef def = ContentDatabase.Cosmetics[i];
                    if (def.Category != categories[c]) continue;
                    bool has = IsOwned(p, def.Id);
                    const float h = 66f;
                    Image row = GalaxyUiTheme.Panel(content, "Коллекция " + def.Id,
                        has ? new Color32(20, 38, 36, 248) : new Color32(14, 19, 40, 248),
                        GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
                    Image dot = GalaxyUiTheme.CircleImage(row.transform, "Маркер",
                        has ? ParseHex(def.Hex, GalaxyUiTheme.AccentA) : new Color32(62, 72, 104, 255),
                        new GBox(0f, 0.5f, 0f, 0.5f, 20f, -14f, 48f, 14f));
                    dot.raycastTarget = false;
                    GalaxyUiTheme.Label(row.transform, has ? def.Name : "???", 21,
                        has ? GalaxyUiTheme.Paper : GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                        new GBox(0f, 0f, 1f, 1f, 64f, 32f, -520f, -4f), FontStyle.Bold);
                    GalaxyUiTheme.Label(row.transform, has ? def.UnlockHint : "Ещё не открыто", 16,
                        GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, new GBox(0f, 0f, 1f, 1f, 64f, 6f, -520f, -36f));
                    GalaxyUiTheme.Label(row.transform, IsEquipped(p, def) ? "НАДЕТО" : (has ? "В АНГАРЕ" : "ЗАКРЫТО"),
                        18, IsEquipped(p, def) ? GalaxyUiTheme.AccentA : GalaxyUiTheme.Muted,
                        TextAnchor.MiddleRight, new GBox(0f, 0f, 1f, 1f, 1120f, 8f, -22f, -8f), FontStyle.Bold);
                    y += h + 8f;
                }
                y += 18f;
            }
            GalaxyUiTheme.Label(content, "Новые предметы открываются в магазине и за достижения в матчах.", 18,
                GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, GBox.TopRow(y, 40f, 22f, 22f));
            y += 48f;
            GalaxyUiTheme.SetScrollHeight(content, y + 12f);
            GalaxyUiTheme.MakeButton(root, "В магазин", "ОТКРЫТЬ МАГАЗИН", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(540f, -408f, 400f, 62f), 19, ShowShop);
        }

        // ───────────────────────────── Stats ─────────────────────────────

        public void ShowStats()
        {
            RectTransform root = BeginScreen("stats", false);
            if (root == null) return;
            DrawBackdrop(root, GalaxyUiTheme.AccentC);
            DrawHeader(root, "СТАТИСТИКА", "Все показатели берутся из сохранённого профиля", ShowMenu);
            DrawTabs(root, StatsTabs, _statsTab, delegate(int index) { _statsTab = index; ShowStats(); });
            AccountProfile p = _app == null ? null : _app.Profile;

            if (_statsTab == 0)
            {
                float winRate = p != null && p.Matches > 0 ? (float)p.Wins / p.Matches * 100f : 0f;
                float[] xs = { -530f, 0f, 530f };
                float[] ys = { 200f, 58f, -84f };
                StatCard(root, xs[0], ys[0], "МАТЧЕЙ", p == null ? "—" : p.Matches.ToString(), GalaxyUiTheme.AccentA);
                StatCard(root, xs[1], ys[0], "ПОБЕД", p == null ? "—" : p.Wins.ToString(), GalaxyUiTheme.AccentC);
                StatCard(root, xs[2], ys[0], "ПРОЦЕНТ ПОБЕД", winRate.ToString("F0") + "%", GalaxyUiTheme.Sun);
                StatCard(root, xs[0], ys[1], "УНИЧТОЖЕНИЙ", ProfileStat(p, "kills").ToString(), GalaxyUiTheme.AccentB);
                StatCard(root, xs[1], ys[1], "СОБРАНО ЭНЕРГИИ", FormatNumber(ProfileStat(p, "energy")), GalaxyUiTheme.AccentA);
                StatCard(root, xs[2], ys[1], "БОССОВ", p == null ? "—" : p.BossKills.ToString(), GalaxyUiTheme.Violet);
                Image extra = GalaxyUiTheme.Panel(root, "Детали", new Color32(14, 21, 45, 246), GBox.At(0f, -276f, 1500f, 220f), GalaxyUiTheme.Round);
                GalaxyUiTheme.Label(extra.transform, "ПОСЛЕДНИЕ СЕРИИ", 19, GalaxyUiTheme.AccentC, TextAnchor.MiddleLeft,
                    GBox.Inset(20f, 182f, 20f, 8f), FontStyle.Bold);
                GalaxyUiTheme.Label(extra.transform,
                    "Текущая серия побед: " + (p == null ? 0 : p.WinStreak) + "\n" +
                    "Лучшая серия побед: " + (p == null ? 0 : p.BestWinStreak) + "\n" +
                    "Побед над боссами: " + (p == null ? 0 : p.BossKills) + "\n" +
                    "Среднее убийств за матч: " + (p == null || p.Matches == 0 ? "0.0" : (ProfileStat(p, "kills") / (float)p.Matches).ToString("F1")),
                    21, GalaxyUiTheme.Paper, TextAnchor.UpperLeft, GBox.Inset(24f, 18f, 24f, 50f));
            }
            else if (_statsTab == 1)
            {
                RectTransform content;
                GalaxyUiTheme.Scroll(root, "Рекорды", GBox.At(0f, -50f, 1520f, 610f), out content);
                float y = 0f;
                List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>();
                if (p != null && p.Records != null)
                {
                    foreach (KeyValuePair<string, int> pair in p.Records) rows.Add(pair);
                    rows.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });
                }
                if (rows.Count == 0)
                {
                    GalaxyUiTheme.Label(content, "Рекорды появятся после первого завершённого матча.", 22,
                        GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, GBox.TopRow(0f, 50f, 24f, 24f));
                    y = 60f;
                }
                for (int i = 0; i < rows.Count; i++)
                {
                    const float h = 60f;
                    Image row = GalaxyUiTheme.Panel(content, "Рекорд " + rows[i].Key, new Color32(18, 26, 54, 248),
                        GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
                    GalaxyUiTheme.Label(row.transform, RecordName(rows[i].Key), 20, GalaxyUiTheme.Paper,
                        TextAnchor.MiddleLeft, GBox.Inset(20f, 6f, 20f, 6f));
                    GalaxyUiTheme.Label(row.transform, FormatNumber(rows[i].Value), 22, GalaxyUiTheme.AccentA,
                        TextAnchor.MiddleRight, new GBox(0f, 0f, 1f, 1f, 1200f, 6f, -20f, -6f), FontStyle.Bold);
                    y += h + 8f;
                }
                y += 16f;
                GalaxyUiTheme.Label(content, "Общие счётчики", 21, GalaxyUiTheme.AccentC, TextAnchor.MiddleLeft,
                    GBox.TopRow(y, 38f, 22f, 22f), FontStyle.Bold);
                y += 44f;
                if (p != null && p.Stats != null)
                {
                    foreach (KeyValuePair<string, int> pair in p.Stats)
                    {
                        GalaxyUiTheme.Label(content, FieldName(pair.Key) + "  ·  " + FormatNumber(pair.Value), 18,
                            GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, GBox.TopRow(y, 34f, 24f, 24f));
                        y += 38f;
                    }
                }
                GalaxyUiTheme.SetScrollHeight(content, y + 12f);
            }
            else
            {
                DrawAchievementList(root);
            }
        }

        // ───────────────────────────── Settings ─────────────────────────────

        public void ShowSettings()
        {
            RectTransform root = BeginScreen("settings", false);
            if (root == null) return;
            DrawBackdrop(root, GalaxyUiTheme.AccentA);
            DrawHeader(root, "НАСТРОЙКИ", "Звук, управление, доступность и сохранения", ShowMenu);
            AccountProfile p = _app == null ? null : _app.Profile;
            GameSettings s = p == null ? null : p.Settings;
            if (s == null) return;

            RectTransform content;
            GalaxyUiTheme.Scroll(root, "Настройки", GBox.At(0f, -50f, 1520f, 610f), out content);
            float y = 0f;
            y = DrawSliderSetting(content, "МУЗЫКА", "Громкость музыкальных циклов", s.MusicVolume, y,
                delegate(float v) { ChangeSetting(delegate(GameSettings g) { g.MusicVolume = v; }); });
            y = DrawSliderSetting(content, "ЗВУКОВЫЕ ЭФФЕКТЫ", "Выстрелы, поглощения, уведомления", s.SfxVolume, y,
                delegate(float v) { ChangeSetting(delegate(GameSettings g) { g.SfxVolume = v; }); });
            y = DrawToggleSetting(content, "ВЫКЛЮЧИТЬ ЗВУК", "Глобальное отключение аудио", s.Muted, y,
                delegate(bool v) { ChangeSetting(delegate(GameSettings g) { g.Muted = v; }); });
            y = DrawToggleSetting(content, "ВИБРАЦИЯ", "Тактильная отдача на мобильных устройствах", s.Haptics, y,
                delegate(bool v) { ChangeSetting(delegate(GameSettings g) { g.Haptics = v; }); });
            y = DrawToggleSetting(content, "МЕНЬШЕ ДВИЖЕНИЯ", "Отключает анимации и мерцание интерфейса", s.ReducedMotion, y,
                delegate(bool v) { ChangeSetting(delegate(GameSettings g) { g.ReducedMotion = v; }, true); });
            y = DrawToggleSetting(content, "ЦВЕТОСЛЕПОСТЬ", "Альтернативная палитра акцентов", s.Colorblind, y,
                delegate(bool v) { ChangeSetting(delegate(GameSettings g) { g.Colorblind = v; }, true); });
            y = DrawToggleSetting(content, "КРУПНЫЙ ТЕКСТ", "Увеличивает подписи на 16%", s.LargeText, y,
                delegate(bool v) { ChangeSetting(delegate(GameSettings g) { g.LargeText = v; }, true); });
            y = DrawToggleSetting(content, "АВТОСТРЕЛЬБА", "Огонь по ближайшей цели без удержания", s.AutoFire, y,
                delegate(bool v) { ChangeSetting(delegate(GameSettings g) { g.AutoFire = v; }); });
            y = DrawToggleSetting(content, "ПОКАЗЫВАТЬ FPS", "Счётчик кадров в углу экрана", s.ShowFps, y,
                delegate(bool v) { ChangeSetting(delegate(GameSettings g) { g.ShowFps = v; }); });
            y = DrawSegmentSetting(content, "КАЧЕСТВО", "Детализация эффектов и целевая частота кадров",
                QualityLabels, QualityTokens, QualityIndex(s.Quality), y,
                delegate(int index) { ChangeSetting(delegate(GameSettings g) { g.Quality = QualityTokens[index]; }); });
            y = DrawSegmentSetting(content, "СХЕМА УПРАВЛЕНИЯ", "Расположение элементов управления",
                ControlLabels, ControlTokens, ControlIndex(s.ControlLayout), y,
                delegate(int index) { ChangeSetting(delegate(GameSettings g) { g.ControlLayout = ControlTokens[index]; }); });

            GalaxyUiTheme.Label(content, "СОХРАНЕНИЯ", 21, GalaxyUiTheme.AccentC, TextAnchor.MiddleLeft,
                GBox.TopRow(y, 38f, 22f, 22f), FontStyle.Bold);
            y += 44f;
            GalaxyUiTheme.MakeButton(content, "Резервная копия", "СОЗДАТЬ КОПИЮ", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.TopRow(y, 64f, 22f, 780f), 20, delegate { ShowToast(SaveBackup() ? "Копия сохранения создана" : "Не удалось создать копию"); });
            GalaxyUiTheme.MakeButton(content, "Восстановить", "ВОССТАНОВИТЬ", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.TopRow(y, 64f, 800f, 22f), 20, delegate
                {
                    if (!SaveRestore()) { ShowToast("Копия не найдена"); return; }
                    RestoreProfile();
                    ShowToast("Сохранение восстановлено");
                    ShowSettings();
                });
            y += 72f;
            GalaxyUiTheme.Label(content, "Изменения применяются сразу и сохраняются в профиле.", 17,
                GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, GBox.TopRow(y, 40f, 22f, 22f));
            y += 48f;
            GalaxyUiTheme.SetScrollHeight(content, y + 12f);
        }

        private float DrawSliderSetting(RectTransform content, string title, string hint, float value, float y, Action<float> changed)
        {
            const float h = 92f;
            Image row = GalaxyUiTheme.Panel(content, "Настройка " + title, new Color32(16, 23, 48, 248),
                GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(row.transform, title, 22, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                new GBox(0f, 0f, 1f, 1f, 22f, 50f, -620f, -4f), FontStyle.Bold);
            GalaxyUiTheme.Label(row.transform, hint, 16, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                new GBox(0f, 0f, 1f, 1f, 22f, 10f, -620f, -48f));
            GSlider slider = GalaxyUiTheme.Slider(row.transform, "Ползунок", value, GalaxyUiTheme.AccentA,
                new GBox(0f, 0f, 1f, 1f, 940f, 36f, -120f, -36f),
                delegate(float v) { changed(v); });
            Text percent = GalaxyUiTheme.Label(row.transform, Mathf.RoundToInt(value * 100f) + "%", 20, GalaxyUiTheme.AccentA,
                TextAnchor.MiddleRight, new GBox(0f, 0f, 1f, 1f, 1180f, 14f, -22f, -14f), FontStyle.Bold);
            if (slider != null && slider.Slider != null)
                slider.Slider.onValueChanged.AddListener(delegate(float v)
                {
                    if (percent != null) percent.text = Mathf.RoundToInt(v * 100f) + "%";
                });
            return y + h + 10f;
        }

        private float DrawToggleSetting(RectTransform content, string title, string hint, bool value, float y, Action<bool> changed)
        {
            const float h = 92f;
            Image row = GalaxyUiTheme.Panel(content, "Настройка " + title, new Color32(16, 23, 48, 248),
                GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(row.transform, title, 22, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                new GBox(0f, 0f, 1f, 1f, 22f, 50f, -620f, -4f), FontStyle.Bold);
            GalaxyUiTheme.Label(row.transform, hint, 16, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                new GBox(0f, 0f, 1f, 1f, 22f, 10f, -620f, -48f));
            GalaxyUiTheme.Toggle(row.transform, "Переключатель", value, GalaxyUiTheme.AccentA,
                new GBox(0f, 0.5f, 0f, 0.5f, 1040f, -20f, 1090f, 20f),
                delegate(bool v) { changed(v); });
            return y + h + 10f;
        }

        private float DrawSegmentSetting(RectTransform content, string title, string hint,
            string[] labels, string[] tokens, int active, float y, Action<int> changed)
        {
            const float h = 104f;
            Image row = GalaxyUiTheme.Panel(content, "Настройка " + title, new Color32(16, 23, 48, 248),
                GBox.TopRow(y, h, 18f, 18f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(row.transform, title, 22, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                new GBox(0f, 0f, 1f, 1f, 22f, 58f, -620f, -4f), FontStyle.Bold);
            GalaxyUiTheme.Label(row.transform, hint, 16, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                new GBox(0f, 0f, 1f, 1f, 22f, 8f, -620f, -50f));
            int count = Mathf.Min(labels.Length, tokens.Length);
            const float areaLeft = 540f;
            const float areaWidth = 900f;
            float step = areaWidth / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                int captured = i;
                Color fill = i == active ? GalaxyUiTheme.AccentA : GalaxyUiTheme.PanelSoft;
                Color text = i == active ? GalaxyUiTheme.Ink : GalaxyUiTheme.Paper;
                float left = areaLeft + i * step + 5f;
                float right = areaLeft + (i + 1) * step - 5f;
                GalaxyUiTheme.MakeButton(row.transform, "Вариант " + tokens[i], labels[i], fill, text,
                    new GBox(0f, 0f, 0f, 1f, left, 20f, right, -20f),
                    17, delegate { changed(captured); });
            }
            return y + h + 10f;
        }

        private void ChangeSetting(Action<GameSettings> mutate, bool rebuild = false)
        {
            AccountProfile p = _app == null ? null : _app.Profile;
            if (p == null || p.Settings == null) return;
            mutate(p.Settings);
            GalaxyUiTheme.ReducedMotion = p.Settings.ReducedMotion;
            GalaxyUiTheme.SetLargeText(p.Settings.LargeText);
            GalaxyUiTheme.SetColorblind(p.Settings.Colorblind);
            if (_app != null) _app.ApplySettings();
            Persist();
            if (rebuild) ShowSettings();
        }

        private static int QualityIndex(string quality)
        {
            for (int i = 0; i < QualityTokens.Length; i++)
                if (string.Equals(quality, QualityTokens[i], StringComparison.OrdinalIgnoreCase)) return i;
            return QualityTokens.Length - 1;
        }

        private static int ControlIndex(string layout)
        {
            for (int i = 0; i < ControlTokens.Length; i++)
                if (string.Equals(layout, ControlTokens[i], StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        // ───────────────────────────── Screen chrome ─────────────────────────────

        private void DrawHeader(RectTransform root, string title, string subtitle, Action onBack)
        {
            GalaxyUiTheme.Panel(root, "Шапка", new Color32(8, 13, 31, 232), new GBox(0f, 1f, 1f, 1f, 0f, -96f, 0f, 0f));
            GalaxyUiTheme.Label(root, title, 46, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                GBox.At(-540f, 402f, 620f, 66f), FontStyle.Bold);
            GalaxyUiTheme.Label(root, subtitle, 19, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                GBox.At(-540f, 360f, 900f, 32f));
            GalaxyUiTheme.MakeButton(root, "Назад", "НАЗАД", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(650f, 384f, 230f, 62f), 20, delegate { if (onBack != null) onBack(); });
        }

        private static void DrawTabs(RectTransform root, string[] tabs, int active, Action<int> onSelect)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int captured = i;
                Color fill = i == active ? GalaxyUiTheme.AccentA : GalaxyUiTheme.PanelSoft;
                Color text = i == active ? GalaxyUiTheme.Ink : GalaxyUiTheme.Paper;
                GalaxyUiTheme.MakeButton(root, "Вкладка " + tabs[i], tabs[i], fill, text,
                    GBox.At(-500f + i * 340f, 302f, 320f, 58f), 20, delegate { onSelect(captured); });
            }
        }

        private static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length < 7 || hex[0] != '#') return fallback;
            string r = hex.Substring(1, 2);
            string g = hex.Substring(3, 2);
            string b = hex.Substring(5, 2);
            byte br, bg, bb;
            if (!byte.TryParse(r, System.Globalization.NumberStyles.HexNumber, null, out br)) return fallback;
            if (!byte.TryParse(g, System.Globalization.NumberStyles.HexNumber, null, out bg)) return fallback;
            if (!byte.TryParse(b, System.Globalization.NumberStyles.HexNumber, null, out bb)) return fallback;
            return new Color32(br, bg, bb, 255);
        }

        private static string NameOrDash(string id)
        {
            if (string.IsNullOrEmpty(id)) return "—";
            CosmeticDef cosmetic = ContentDatabase.FindCosmetic(id);
            return cosmetic == null ? id : cosmetic.Name;
        }

        private static string CategoryName(string category)
        {
            switch (category)
            {
                case "ship": return "КОРАБЛИ";
                case "trail": return "СЛЕДЫ";
                case "death": return "ЭФФЕКТЫ";
                case "frame": return "РАМКИ";
                case "avatar": return "АВАТАРЫ";
                case "title": return "ТИТУЛЫ";
                default: return category.ToUpperInvariant();
            }
        }

        private static string FieldName(string field)
        {
            switch (field)
            {
                case "kills": return "Уничтожения";
                case "deaths": return "Смерти";
                case "energy": return "Энергия";
                case "mass": return "Масса";
                case "bestMass": return "Лучшая масса";
                case "crystals": return "Кристаллы";
                case "darkMatter": return "Тёмная материя";
                case "matches": return "Матчи";
                case "top3": return "Топ-3";
                case "wins": return "Победы";
                case "bosses": return "Боссы";
                case "score": return "Очки";
                case "bestScore": return "Лучший счёт";
                case "lastScore": return "Последний счёт";
                case "bestKills": return "Лучший результат по убийствам";
                case "bestRank": return "Лучшее место";
                case "rank1": return "Победы на первом месте";
                case "bestStreak": return "Лучшая серия";
                case "recordCount": return "Новые рекорды";
                case "evolutionTier": return "Уровень эволюции";
                case "artifacts": return "Артефакты";
                default: return field;
            }
        }

        private static string RecordName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Рекорд";
            for (int i = 0; i < ContentDatabase.Modes.Length; i++)
            {
                GameModeKind kind = ContentDatabase.Modes[i].Kind;
                if (string.Equals(key, kind.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "mode_" + kind, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "record_" + kind, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "score_" + kind, StringComparison.OrdinalIgnoreCase))
                    return ContentDatabase.Modes[i].Name;
            }
            return FieldName(key);
        }

        // ───────────────────────────── Save bridge (reflection, no core dependency) ─────────────────────────────

        private object GetAppMember(string name)
        {
            if (_app == null) return null;
            Type type = _app.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return property.GetValue(_app, null);
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return field == null ? null : field.GetValue(_app);
        }

        private void Persist()
        {
            if (_app == null || _app.Profile == null) return;
            object saves = GetAppMember("Saves");
            if (saves != null)
            {
                MethodInfo withProfile = saves.GetType().GetMethod("Save", new[] { typeof(AccountProfile) });
                if (withProfile != null)
                {
                    withProfile.Invoke(saves, new object[] { _app.Profile });
                    return;
                }
                MethodInfo noArgs = saves.GetType().GetMethod("Save", Type.EmptyTypes);
                if (noArgs != null)
                {
                    noArgs.Invoke(saves, null);
                    return;
                }
            }
            InvokeStaticSave("Save", new object[] { _app.Profile });
        }

        private bool SaveBackup()
        {
            object saves = GetAppMember("Saves");
            if (saves != null)
            {
                MethodInfo method = saves.GetType().GetMethod("Backup", Type.EmptyTypes);
                if (method != null)
                {
                    object result = method.Invoke(saves, null);
                    return !(result is bool) || (bool)result;
                }
            }
            object staticResult = InvokeStaticSave("Backup", null);
            return !(staticResult is bool) || (bool)staticResult;
        }

        private bool SaveRestore()
        {
            object saves = GetAppMember("Saves");
            if (saves != null)
            {
                MethodInfo method = saves.GetType().GetMethod("Restore", Type.EmptyTypes);
                if (method != null)
                {
                    object result = method.Invoke(saves, null);
                    return !(result is bool) || (bool)result;
                }
            }
            object staticResult = InvokeStaticSave("Restore", null);
            return !(staticResult is bool) || (bool)staticResult;
        }

        private void RestoreProfile()
        {
            object saves = GetAppMember("Saves");
            AccountProfile profile = null;
            if (saves != null)
            {
                MethodInfo load = saves.GetType().GetMethod("Load", Type.EmptyTypes);
                if (load != null) profile = load.Invoke(saves, null) as AccountProfile;
            }
            if (profile == null) profile = InvokeStaticSave("Load", null) as AccountProfile;
            if (profile == null || _app == null) return;
            if (profile.Settings == null) profile.Settings = new GameSettings();
            FieldInfo profileField = typeof(GalaxyApp).GetField("_profile", BindingFlags.Instance | BindingFlags.NonPublic);
            if (profileField != null) profileField.SetValue(_app, profile);
            GalaxyUiTheme.ReducedMotion = profile.Settings.ReducedMotion;
            GalaxyUiTheme.SetLargeText(profile.Settings.LargeText);
            GalaxyUiTheme.SetColorblind(profile.Settings.Colorblind);
            _app.ApplySettings();
        }

        private static object InvokeStaticSave(string methodName, object[] args)
        {
            try
            {
                Assembly assembly = typeof(GalaxyApp).Assembly;
                Type manager = assembly.GetType("Orbit.Galaxy.SaveManager");
                if (manager == null) return null;
                Type[] signature = args == null ? Type.EmptyTypes : new[] { typeof(AccountProfile) };
                MethodInfo method = manager.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, signature, null);
                return method == null ? null : method.Invoke(null, args);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
