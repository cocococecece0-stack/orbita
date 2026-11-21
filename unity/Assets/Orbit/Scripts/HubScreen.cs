using System;
using System.Collections.Generic;
using System.Globalization;
using Orbit.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Orbit.Unity
{
    /// <summary>Хаб в духе .io: крупная типографика, постер цели, чипы режимов.</summary>
    public sealed class HubScreen : OrbitScreen
    {
        private Text _creditsText;
        private Text _levelText;
        private Text _objectiveTitle;
        private Text _objectiveNumber;
        private Text _modeName;
        private Text _sectorName;
        private Text _seasonText;
        private Text _streakText;
        private Text _topText;
        private BarView _seasonBar;

        private Button[] _modeButtons;
        private Image[] _modeBackgrounds;
        private Text[] _modeLabels;
        private Button[] _sectorButtons;
        private Image[] _sectorBackgrounds;
        private Text[] _sectorLabels;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            UiFactory.Panel(transform, "Фон", UiFactory.Ink, UiBox.Full);
            UiFactory.Circle(transform, "Декор 1", UiFactory.WithAlpha(UiFactory.Violet, 0.10f), UiBox.At(150f, 1450f, 620f, 620f));
            UiFactory.Circle(transform, "Декор 2", UiFactory.WithAlpha(UiFactory.Lime, 0.07f), UiBox.At(960f, 620f, 460f, 460f));
            UiFactory.Circle(transform, "Декор 3", UiFactory.WithAlpha(UiFactory.Coral, 0.06f), UiBox.At(80f, 380f, 300f, 300f));

            UiFactory.Panel(transform, "Шапка", UiFactory.WithAlpha(UiFactory.Deep, 0.86f), new UiBox(0f, 0.895f, 1f, 1f, 0f, 0f, 0f, 0f));
            UiFactory.TextLabel(transform, "ORBITA", 96, UiFactory.Lime, TextAnchor.MiddleLeft,
                new UiBox(0.03f, 0.895f, 0.52f, 1f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            UiFactory.Chip(transform, "Версия", "4.0", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.87f, 0.925f, 0.97f, 0.982f, 0f, 0f, 0f, 0f), 24);
            _levelText = UiFactory.Chip(transform, "Уровень", "LV 1", UiFactory.Violet, UiFactory.Ink,
                new UiBox(0.72f, 0.925f, 0.86f, 0.982f, 0f, 0f, 0f, 0f), 24).Label;
            _creditsText = UiFactory.Chip(transform, "Кредиты", "◈ 120", UiFactory.Sun, UiFactory.Ink,
                new UiBox(0.54f, 0.925f, 0.71f, 0.982f, 0f, 0f, 0f, 0f), 24).Label;

            Image poster = UiFactory.Panel(transform, "Постер", UiFactory.WithAlpha(UiFactory.Violet, 0.92f),
                new UiBox(0.03f, 0.545f, 0.97f, 0.885f, 0f, 0f, 0f, 0f), UiFactory.RoundedSprite);
            poster.type = Image.Type.Sliced;
            UiFactory.Panel(transform, "Акцент", UiFactory.Lime, new UiBox(0.05f, 0.864f, 0.95f, 0.88f, 0f, 0f, 0f, 0f));
            UiFactory.Panel(transform, "Внутренний фон", UiFactory.WithAlpha(UiFactory.Deep, 0.42f),
                new UiBox(0.055f, 0.565f, 0.945f, 0.855f, 0f, 0f, 0f, 0f), UiFactory.RoundedSprite).type = Image.Type.Sliced;

            _objectiveTitle = UiFactory.TextLabel(transform, "ЦЕЛЬ", 30, UiFactory.Paper, TextAnchor.MiddleCenter,
                new UiBox(0.08f, 0.795f, 0.92f, 0.855f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            _objectiveNumber = UiFactory.TextLabel(transform, "1200", 170, UiFactory.Sun, TextAnchor.MiddleCenter,
                new UiBox(0.06f, 0.615f, 0.94f, 0.80f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            UiFactory.FitText(_objectiveNumber, 70, 180);

            _modeName = UiFactory.TextLabel(transform, "КЛАССИКА", 46, UiFactory.Paper, TextAnchor.MiddleLeft,
                new UiBox(0.08f, 0.565f, 0.60f, 0.625f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            UiFactory.FitText(_modeName, 24, 46);
            _sectorName = UiFactory.TextLabel(transform, "СЕКТОР", 20, UiFactory.WithAlpha(UiFactory.Paper, 0.8f), TextAnchor.MiddleLeft,
                new UiBox(0.08f, 0.562f, 0.62f, 0.595f, 0f, 0f, 0f, 0f));
            _seasonText = UiFactory.Chip(transform, "Сезон", "СЕЗОН", UiFactory.Coral, UiFactory.Paper,
                new UiBox(0.63f, 0.575f, 0.945f, 0.635f, 0f, 0f, 0f, 0f), 22).Label;

            UiFactory.Panel(transform, "Топ фон", UiFactory.WithAlpha(UiFactory.Slate, 0.9f),
                new UiBox(0.03f, 0.455f, 0.97f, 0.535f, 0f, 0f, 0f, 0f), UiFactory.RoundedSprite).type = Image.Type.Sliced;
            UiFactory.TextLabel(transform, "ТОП-3 · ВЫБРАННЫЙ РЕЖИМ", 21, UiFactory.Lime, TextAnchor.MiddleLeft,
                new UiBox(0.05f, 0.495f, 0.7f, 0.532f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            _topText = UiFactory.TextLabel(transform, "ПОКА НЕТ РЕКОРДОВ", 24, UiFactory.Paper, TextAnchor.MiddleLeft,
                new UiBox(0.05f, 0.458f, 0.95f, 0.5f, 0f, 0f, 0f, 0f), FontStyle.Bold);

            _streakText = UiFactory.Chip(transform, "Стрик", "СТРИК 0 ДН.", UiFactory.Orange, UiFactory.Ink,
                new UiBox(0.03f, 0.405f, 0.30f, 0.447f, 0f, 0f, 0f, 0f), 22).Label;
            UiFactory.TextLabel(transform, "СЕЗОН", 19, UiFactory.Paper, TextAnchor.MiddleLeft,
                new UiBox(0.32f, 0.405f, 0.44f, 0.447f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            _seasonBar = UiFactory.Bar(transform, "Сезонный прогресс", UiFactory.Slate, UiFactory.Lime,
                new UiBox(0.45f, 0.414f, 0.97f, 0.44f, 0f, 0f, 0f, 0f));

            UiFactory.TextLabel(transform, "РЕЖИМ", 19, UiFactory.Lime, TextAnchor.MiddleLeft,
                new UiBox(0.03f, 0.378f, 0.3f, 0.402f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            BuildModeChips();

            UiFactory.TextLabel(transform, "СЕКТОР", 19, UiFactory.Lime, TextAnchor.MiddleLeft,
                new UiBox(0.03f, 0.272f, 0.3f, 0.298f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            BuildSectorChips();

            UiFactory.MakeButton(transform, "Играть", "ИГРАТЬ ▸", UiFactory.Lime, UiFactory.Ink,
                new UiBox(0.06f, 0.105f, 0.94f, 0.195f, 0f, 0f, 0f, 0f), 64,
                delegate
                {
                    if (App != null) App.StartRun(Content.ModeFromId(App.Save != null ? App.Save.selectedMode : "classic"));
                });

            UiFactory.MakeButton(transform, "Настройки", "НАСТРОЙКИ", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.03f, 0.042f, 0.34f, 0.092f, 0f, 0f, 0f, 0f), 25,
                delegate { if (App != null) App.Open(ScreenKind.Settings); });
            UiFactory.MakeButton(transform, "Магазин", "МАГАЗИН", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.35f, 0.042f, 0.66f, 0.092f, 0f, 0f, 0f, 0f), 25,
                delegate { if (App != null) App.Open(ScreenKind.Shop); });
            UiFactory.MakeButton(transform, "Статистика", "СТАТИСТИКА", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.67f, 0.042f, 0.97f, 0.092f, 0f, 0f, 0f, 0f), 25,
                delegate { if (App != null) App.Open(ScreenKind.Stats); });

            UiFactory.TextLabel(transform, "ORBITA 4.0 · АРКАДА ОРБИТЫ", 16, UiFactory.WithAlpha(UiFactory.Paper, 0.45f),
                TextAnchor.MiddleCenter, new UiBox(0f, 0.005f, 1f, 0.038f, 0f, 0f, 0f, 0f));
        }

        private void BuildModeChips()
        {
            int count = Content.Modes.Length;
            _modeButtons = new Button[count];
            _modeBackgrounds = new Image[count];
            _modeLabels = new Text[count];
            for (int i = 0; i < count; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float x0 = 0.03f + col * 0.318f;
                float x1 = x0 + 0.306f;
                float y1 = 0.375f - row * 0.0375f;
                float y0 = y1 - 0.034f;
                int index = i;
                _modeButtons[i] = UiFactory.MakeButton(transform, "Режим " + i, Content.Modes[i].Short,
                    UiFactory.Slate, UiFactory.Paper, new UiBox(x0, y0, x1, y1, 0f, 0f, 0f, 0f), 22,
                    delegate { if (App != null) App.SelectMode((ModeKind)index); });
                _modeBackgrounds[i] = _modeButtons[i].GetComponent<Image>();
                _modeLabels[i] = _modeButtons[i].GetComponentInChildren<Text>();
            }
        }

        private void BuildSectorChips()
        {
            int count = Content.Sectors.Length;
            _sectorButtons = new Button[count];
            _sectorBackgrounds = new Image[count];
            _sectorLabels = new Text[count];
            for (int i = 0; i < count; i++)
            {
                int col = i % 6;
                int row = i / 6;
                float x0 = 0.03f + col * 0.157f;
                float x1 = x0 + 0.151f;
                float y1 = 0.27f - row * 0.027f;
                float y0 = y1 - 0.024f;
                int index = i;
                string title = Content.Sectors[i].Name;
                _sectorButtons[i] = UiFactory.MakeButton(transform, "Сектор " + i, title,
                    UiFactory.Slate, UiFactory.Paper, new UiBox(x0, y0, x1, y1, 0f, 0f, 0f, 0f), 16,
                    delegate { if (App != null) App.SelectSector(index); });
                _sectorBackgrounds[i] = _sectorButtons[i].GetComponent<Image>();
                _sectorLabels[i] = _sectorButtons[i].GetComponentInChildren<Text>();
            }
        }

        public override void Refresh()
        {
            if (App == null || App.Save == null) return;
            SaveData save = App.Save;
            ModeKind mode = Content.ModeFromId(save.selectedMode);
            ModeDef def = Content.Mode(mode);
            SectorDef sector = Content.Sector(save.selectedSector);
            ObjectiveDef objective = Content.Objective(mode, save.selectedSector);
            SeasonModifier season = Content.SeasonModifier(save.seasonId);

            _creditsText.text = "◈ " + save.credits.ToString("N0", CultureInfo.InvariantCulture);
            _levelText.text = "LV " + save.playerLevel;
            _objectiveTitle.text = "ЦЕЛЬ · " + objective.Title;
            _objectiveNumber.text = objective.Target.ToString("N0", CultureInfo.InvariantCulture);
            _modeName.text = def.Title;
            _sectorName.text = "СЕКТОР " + sector.Id + " · " + sector.Name + " — " + sector.Subtitle;
            _seasonText.text = "СЕЗОН " + save.seasonId + " · " + season.Title;
            _streakText.text = "СТРИК " + save.dailyStreak + " ДН.";
            if (_seasonBar != null) _seasonBar.Set(save.seasonXp / 400f);

            List<int> top;
            if (save.topByMode == null || !save.topByMode.TryGetValue(def.Id, out top) || top == null || top.Count == 0)
            {
                _topText.text = "ПОКА НЕТ РЕКОРДОВ — СТАВЬ ПЕРВЫЙ!";
            }
            else
            {
                string[] parts = new string[Mathf.Min(3, top.Count)];
                for (int i = 0; i < parts.Length; i++)
                    parts[i] = (i + 1) + ". " + top[i].ToString("N0", CultureInfo.InvariantCulture);
                _topText.text = string.Join("     ", parts);
            }

            for (int i = 0; i < _modeButtons.Length; i++)
            {
                bool active = (ModeKind)i == mode;
                _modeBackgrounds[i].color = active ? UiFactory.Lime : UiFactory.Slate;
                _modeLabels[i].color = active ? UiFactory.Ink : UiFactory.Paper;
            }
            for (int i = 0; i < _sectorButtons.Length; i++)
            {
                bool active = i == save.selectedSector;
                _sectorBackgrounds[i].color = active ? UiFactory.Coral : UiFactory.Slate;
                _sectorLabels[i].color = active ? UiFactory.Ink : UiFactory.Paper;
            }
        }
    }
}
