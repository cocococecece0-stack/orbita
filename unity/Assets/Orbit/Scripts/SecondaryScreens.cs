using System;
using System.Collections.Generic;
using System.Globalization;
using Orbit.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Orbit.Unity
{
    /// <summary>Общий прокручиваемый контейнер для второстепенных экранов.</summary>
    internal static class ScrollUi
    {
        public static void Create(Transform parent, string name, UiBox box, float contentHeight,
            out RectTransform viewport, out RectTransform content)
        {
            RectTransform root = UiFactory.Place(parent, name, box);
            UiFactory.Panel(root, "Фон", UiFactory.WithAlpha(UiFactory.Deep, 0.86f), UiBox.Full, UiFactory.RoundedSprite).type = Image.Type.Sliced;
            viewport = UiFactory.Place(root, "Область", UiBox.Inset(8f, 8f, 8f, 8f));
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiFactory.Place(viewport, "Содержимое",
                new UiBox(0f, 1f, 1f, 1f, 0f, -contentHeight, 0f, 0f));
            content.pivot = new Vector2(0.5f, 1f);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
        }

        public static UiBox Row(float top, float height, float x0 = 0.04f, float x1 = 0.96f)
        {
            return new UiBox(x0, 1f, x1, 1f, 0f, -(top + height), 0f, -top);
        }

        public static void Header(Transform parent, string title, string credits, UnityAction back)
        {
            UiFactory.Panel(parent, "Шапка", UiFactory.WithAlpha(UiFactory.Deep, 0.95f),
                new UiBox(0f, 0.9f, 1f, 1f, 0f, 0f, 0f, 0f));
            UiFactory.MakeButton(parent, "Назад", "‹ НАЗАД", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.03f, 0.925f, 0.26f, 0.982f, 0f, 0f, 0f, 0f), 24, back);
            UiFactory.TextLabel(parent, title, 43, UiFactory.Lime, TextAnchor.MiddleCenter,
                new UiBox(0.27f, 0.915f, 0.73f, 0.99f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            if (!string.IsNullOrEmpty(credits))
            {
                UiFactory.Chip(parent, "Кредиты", credits, UiFactory.Sun, UiFactory.Ink,
                    new UiBox(0.74f, 0.925f, 0.97f, 0.982f, 0f, 0f, 0f, 0f), 24);
            }
        }
    }

    /// <summary>Настройки звука, вибрации, анимации и цветовой схемы.</summary>
    public sealed class SettingsScreen : OrbitScreen
    {
        private Toggle _muteToggle;
        private Toggle _hapticsToggle;
        private Toggle _motionToggle;
        private Toggle _colorblindToggle;
        private Slider _musicSlider;
        private Slider _sfxSlider;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            UiFactory.Panel(transform, "Фон", UiFactory.Ink, UiBox.Full);
            ScrollUi.Header(transform, "НАСТРОЙКИ", string.Empty, delegate { if (App != null) App.GoHub(); });

            RectTransform viewport;
            RectTransform content;
            ScrollUi.Create(transform, "Список", new UiBox(0.03f, 0.05f, 0.97f, 0.885f, 0f, 0f, 0f, 0f), 1150f, out viewport, out content);

            UiFactory.TextLabel(content, "ИГРА", 26, UiFactory.Lime, TextAnchor.MiddleLeft,
                ScrollUi.Row(10f, 60f), FontStyle.Bold);
            _muteToggle = MakeToggle(content, "Звук включён", 80f, delegate (bool value)
            {
                GameSettings s = Settings();
                if (s == null) return;
                s.muted = !value;
                ApplyAudio();
            });
            _hapticsToggle = MakeToggle(content, "Вибрация", 158f, delegate (bool value)
            {
                GameSettings s = Settings();
                if (s == null) return;
                s.haptics = value;
                Commit();
            });
            _motionToggle = MakeToggle(content, "Уменьшить анимацию", 236f, delegate (bool value)
            {
                GameSettings s = Settings();
                if (s == null) return;
                s.reducedMotion = value;
                Commit();
            });
            _colorblindToggle = MakeToggle(content, "Цветослепая палитра", 314f, delegate (bool value)
            {
                GameSettings s = Settings();
                if (s == null) return;
                s.colorblind = value;
                Commit();
            });

            UiFactory.TextLabel(content, "ГРОМКОСТЬ", 26, UiFactory.Lime, TextAnchor.MiddleLeft,
                ScrollUi.Row(405f, 60f), FontStyle.Bold);
            _musicSlider = MakeSlider(content, "Музыка", 475f, delegate (float value)
            {
                GameSettings s = Settings();
                if (s == null) return;
                s.musicVolume = Mathf.Clamp01(value);
                ApplyAudio();
            });
            _sfxSlider = MakeSlider(content, "Эффекты", 590f, delegate (float value)
            {
                GameSettings s = Settings();
                if (s == null) return;
                s.sfxVolume = Mathf.Clamp01(value);
                ApplyAudio();
            });

            UiFactory.TextLabel(content, "Настройки сохраняются автоматически.", 20,
                UiFactory.WithAlpha(UiFactory.Paper, 0.6f), TextAnchor.MiddleLeft, ScrollUi.Row(710f, 50f));
        }

        private GameSettings Settings()
        {
            return App != null && App.Save != null ? App.Save.settings : null;
        }

        private void ApplyAudio()
        {
            if (App == null) return;
            App.Persist();
        }

        private void Commit()
        {
            if (App != null) App.Persist();
        }

        private Toggle MakeToggle(Transform parent, string label, float top, UnityAction<bool> changed)
        {
            RectTransform row = UiFactory.Place(parent, "Переключатель " + label, ScrollUi.Row(top, 66f));
            UiFactory.Panel(row, "Фон", UiFactory.WithAlpha(UiFactory.Slate, 0.8f), UiBox.Full, UiFactory.RoundedSprite).type = Image.Type.Sliced;
            UiFactory.TextLabel(row, label, 27, UiFactory.Paper, TextAnchor.MiddleLeft,
                new UiBox(0.02f, 0f, 0.66f, 1f, 22f, 0f, 0f, 0f));

            RectTransform tg = UiFactory.Place(row, "Toggle", new UiBox(0.72f, 0.18f, 0.96f, 0.82f, 0f, 0f, 0f, 0f));
            Image bg = tg.gameObject.AddComponent<Image>();
            bg.sprite = UiFactory.RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = UiFactory.Deep;
            Toggle toggle = tg.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            RectTransform mark = UiFactory.Place(tg, "Метка", UiBox.Inset(6f, 6f, 6f, 6f));
            Image markImage = mark.gameObject.AddComponent<Image>();
            markImage.sprite = UiFactory.CircleSprite;
            markImage.color = UiFactory.Lime;
            toggle.graphic = markImage;
            if (changed != null) toggle.onValueChanged.AddListener(changed);
            return toggle;
        }

        private Slider MakeSlider(Transform parent, string label, float top, UnityAction<float> changed)
        {
            RectTransform row = UiFactory.Place(parent, "Слайдер " + label, ScrollUi.Row(top, 96f));
            UiFactory.TextLabel(row, label, 26, UiFactory.Paper, TextAnchor.MiddleLeft,
                new UiBox(0.02f, 0.48f, 0.4f, 1f, 22f, 0f, 0f, 0f), FontStyle.Bold);
            RectTransform track = UiFactory.Place(row, "Дорожка", new UiBox(0.04f, 0.1f, 0.96f, 0.42f, 0f, 0f, 0f, 0f));
            UiFactory.Panel(track, "Фон", UiFactory.Deep, new UiBox(0f, 0.3f, 1f, 0.7f, 0f, 0f, 0f, 0f), UiFactory.RoundedSprite).type = Image.Type.Sliced;
            Image fill = UiFactory.Panel(track, "Заполнение", UiFactory.Lime, new UiBox(0f, 0.3f, 1f, 0.7f, 0f, 0f, 0f, 0f));
            RectTransform handle = UiFactory.Place(track, "Ручка", new UiBox(0f, 0f, 0.07f, 1f, 0f, 0f, 0f, 0f));
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = UiFactory.CircleSprite;
            handleImage.color = UiFactory.Paper;
            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            if (changed != null) slider.onValueChanged.AddListener(changed);
            return slider;
        }

        public override void Refresh()
        {
            GameSettings s = Settings();
            if (s == null) return;
            if (_muteToggle != null) _muteToggle.SetIsOnWithoutNotify(!s.muted);
            if (_hapticsToggle != null) _hapticsToggle.SetIsOnWithoutNotify(s.haptics);
            if (_motionToggle != null) _motionToggle.SetIsOnWithoutNotify(s.reducedMotion);
            if (_colorblindToggle != null) _colorblindToggle.SetIsOnWithoutNotify(s.colorblind);
            if (_musicSlider != null) _musicSlider.SetValueWithoutNotify(Mathf.Clamp01(s.musicVolume));
            if (_sfxSlider != null) _sfxSlider.SetValueWithoutNotify(Mathf.Clamp01(s.sfxVolume));
        }
    }

    /// <summary>Магазин: косметика и улучшения из SaveData.</summary>
    public sealed class ShopScreen : OrbitScreen
    {
        private Text _creditsLabel;
        private Text _statusLabel;
        private Button[] _rows;
        private Text[] _rowLabels;
        private string[] _rowCategory;
        private int[] _rowIndex;
        private int[] _rowPrice;
        private int _rowCount;
        private string _status = "ВЫБЕРИ ПРЕДМЕТ";

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            UiFactory.Panel(transform, "Фон", UiFactory.Ink, UiBox.Full);
            UiFactory.Panel(transform, "Шапка", UiFactory.WithAlpha(UiFactory.Deep, 0.95f),
                new UiBox(0f, 0.9f, 1f, 1f, 0f, 0f, 0f, 0f));
            UiFactory.MakeButton(transform, "Назад", "‹ НАЗАД", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.03f, 0.925f, 0.26f, 0.982f, 0f, 0f, 0f, 0f), 24,
                delegate { if (App != null) App.GoHub(); });
            UiFactory.TextLabel(transform, "МАГАЗИН", 43, UiFactory.Lime, TextAnchor.MiddleCenter,
                new UiBox(0.27f, 0.915f, 0.70f, 0.99f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            _creditsLabel = UiFactory.Chip(transform, "Кредиты", "◈ 0", UiFactory.Sun, UiFactory.Ink,
                new UiBox(0.72f, 0.925f, 0.97f, 0.982f, 0f, 0f, 0f, 0f), 24).Label;
            _statusLabel = UiFactory.TextLabel(transform, _status, 21, UiFactory.Coral, TextAnchor.MiddleCenter,
                new UiBox(0.03f, 0.855f, 0.97f, 0.895f, 0f, 0f, 0f, 0f), FontStyle.Bold);

            RectTransform viewport;
            RectTransform content;
            ScrollUi.Create(transform, "Список", new UiBox(0.03f, 0.035f, 0.97f, 0.85f, 0f, 0f, 0f, 0f), 1520f, out viewport, out content);

            _rows = new Button[17];
            _rowLabels = new Text[17];
            _rowCategory = new string[17];
            _rowIndex = new int[17];
            _rowPrice = new int[17];

            Section(content, 12f, "КОРАБЛИ");
            for (int i = 0; i < 4; i++) AddRow(content, 78f + i * 70f, "ship", i, (i + 1) * 150);
            Section(content, 370f, "СЛЕДЫ");
            for (int i = 0; i < 4; i++) AddRow(content, 436f + i * 70f, "trail", i, (i + 1) * 130);
            Section(content, 728f, "ПЛАНЕТЫ");
            for (int i = 0; i < 4; i++) AddRow(content, 794f + i * 70f, "planet", i, (i + 1) * 180);
            Section(content, 1086f, "УЛУЧШЕНИЯ");
            for (int i = 0; i < 5; i++) AddRow(content, 1152f + i * 70f, "upgrade", i, (i + 1) * 200);
        }

        private static void Section(Transform content, float top, string title)
        {
            UiFactory.TextLabel(content, title, 28, UiFactory.Lime, TextAnchor.MiddleLeft,
                ScrollUi.Row(top, 54f), FontStyle.Bold);
        }

        private void AddRow(Transform content, float top, string category, int index, int price)
        {
            int slot = _rowCount++;
            _rowCategory[slot] = category;
            _rowIndex[slot] = index;
            _rowPrice[slot] = price;
            Button button = UiFactory.MakeButton(content, "Товар " + slot, string.Empty, UiFactory.Slate, UiFactory.Paper,
                ScrollUi.Row(top, 58f), 24, delegate { Buy(slot); });
            _rows[slot] = button;
            _rowLabels[slot] = button.GetComponentInChildren<Text>();
        }

        private void Buy(int slot)
        {
            SaveData save = App != null ? App.Save : null;
            if (save == null) return;
            string category = _rowCategory[slot];
            int index = _rowIndex[slot];
            int price = _rowPrice[slot];
            string id = index.ToString(CultureInfo.InvariantCulture);

            if (category == "upgrade")
            {
                int level = index >= 0 && index < save.upgrades.Count ? save.upgrades[index] : 0;
                if (level >= 5) { _status = "УЖЕ МАКСИМУМ"; Refresh(); return; }
                if (!Spend(save, price)) { Refresh(); return; }
                save.upgrades[index] = level + 1;
                _status = "УЛУЧШЕНИЕ УЛУЧШЕНО";
            }
            else
            {
                List<string> owned = null;
                if (category == "ship") owned = save.ownedShips;
                else if (category == "trail") owned = save.ownedTrails;
                else if (category == "planet") owned = save.ownedPlanets;
                if (owned == null) return;
                if (!owned.Contains(id))
                {
                    if (!Spend(save, price)) { Refresh(); return; }
                    owned.Add(id);
                    _status = "КУПЛЕНО!";
                }
                else _status = "ЭКИПИРОВАНО";
                if (category == "ship") save.equippedShip = index;
                else if (category == "trail") save.equippedTrail = index;
                else if (category == "planet") save.equippedPlanet = index;
            }
            App.Persist();
            Refresh();
        }

        private bool Spend(SaveData save, int price)
        {
            if (save.credits < price)
            {
                _status = "НЕ ХВАТАЕТ КРЕДИТОВ";
                return false;
            }
            save.credits -= price;
            return true;
        }

        public override void Refresh()
        {
            SaveData save = App != null ? App.Save : null;
            if (save == null) return;
            _creditsLabel.text = "◈ " + save.credits.ToString("N0", CultureInfo.InvariantCulture);
            _statusLabel.text = _status;

            for (int slot = 0; slot < _rowCount; slot++)
            {
                string category = _rowCategory[slot];
                int index = _rowIndex[slot];
                string id = index.ToString(CultureInfo.InvariantCulture);
                bool equipped = false;
                bool owned = false;
                string title;
                if (category == "upgrade")
                {
                    int level = index >= 0 && index < save.upgrades.Count ? save.upgrades[index] : 0;
                    title = "УЛУЧШЕНИЕ " + (index + 1) + " · УР " + level + "/5";
                    owned = level >= 5;
                    equipped = level >= 5;
                }
                else
                {
                    List<string> list = category == "ship" ? save.ownedShips :
                                        category == "trail" ? save.ownedTrails : save.ownedPlanets;
                    owned = list != null && list.Contains(id);
                    equipped = category == "ship" ? save.equippedShip == index :
                               category == "trail" ? save.equippedTrail == index : save.equippedPlanet == index;
                    string noun = category == "ship" ? "КОРАБЛЬ" : category == "trail" ? "СЛЕД" : "ПЛАНЕТА";
                    title = noun + " " + (index + 1) + " · " + (equipped ? "ЭКИПИРОВАН" : owned ? "КУПЛЕНО" : "◈ " + _rowPrice[slot]);
                }
                _rowLabels[slot].text = title;
                Image bg = _rows[slot].GetComponent<Image>();
                bg.color = equipped ? UiFactory.Lime : owned ? UiFactory.Paper : UiFactory.Slate;
                _rowLabels[slot].color = equipped || owned ? UiFactory.Ink : UiFactory.Paper;
            }
        }
    }

    /// <summary>Статистика: история, рекорды, достижения и ежедневные задания.</summary>
    public sealed class StatsScreen : OrbitScreen
    {
        private RectTransform _content;
        private readonly List<Button> _claimButtons = new List<Button>();
        private readonly List<int> _claimIndices = new List<int>();

        private void Awake()
        {
            UiFactory.Panel(transform, "Фон", UiFactory.Ink, UiBox.Full);
            ScrollUi.Header(transform, "СТАТИСТИКА", string.Empty, delegate { if (App != null) App.GoHub(); });
            RectTransform viewport;
            ScrollUi.Create(transform, "Список", new UiBox(0.03f, 0.035f, 0.97f, 0.885f, 0f, 0f, 0f, 0f), 2700f, out viewport, out _content);
        }

        public override void Refresh()
        {
            Clear();
            if (App == null || App.Save == null || _content == null) return;
            SaveData save = App.Save;
            float top = 12f;

            top = Title(top, "ПОСЛЕДНИЕ ЗАБЕГИ");
            int shown = Mathf.Min(8, save.history == null ? 0 : save.history.Count);
            if (shown == 0) top = Line(top, "ПОКА НЕТ ЗАБЕГОВ", UiFactory.WithAlpha(UiFactory.Paper, 0.6f));
            for (int i = 0; i < shown; i++)
            {
                RunRecord record = save.history[i];
                string mode = Content.Mode(Content.ModeFromId(record.mode)).Short;
                string text = "#" + (i + 1) + "  " + mode + " · СЕКТОР " + record.sector +
                              "     ◈ " + record.score.ToString("N0", CultureInfo.InvariantCulture) +
                              (record.completed ? "  ✓" : string.Empty);
                top = Line(top, text, record.completed ? UiFactory.Lime : UiFactory.Paper, 22);
            }

            top = Title(top + 16f, "ЛУЧШИЕ РЕЗУЛЬТАТЫ");
            for (int i = 0; i < Content.Modes.Length; i++)
            {
                string id = Content.Modes[i].Id;
                int best;
                if (save.bestByMode == null || !save.bestByMode.TryGetValue(id, out best)) best = 0;
                top = Line(top, Content.Modes[i].Short + " — " + best.ToString("N0", CultureInfo.InvariantCulture), UiFactory.Sun, 22);
            }

            top = Title(top + 16f, "ДОСТИЖЕНИЯ");
            HashSet<string> earned = new HashSet<string>(save.achievements ?? new List<string>());
            for (int i = 0; i < Content.Achievements.Length; i++)
            {
                AchievementDef def = Content.Achievements[i];
                bool done = earned.Contains(def.Id);
                top = Line(top, (done ? "★ " : "☆ ") + def.Title + " — " + def.Detail,
                    done ? UiFactory.Lime : UiFactory.WithAlpha(UiFactory.Paper, 0.42f), 20);
            }

            top = Title(top + 16f, "ЕЖЕДНЕВНЫЕ ЗАДАНИЯ · " + save.dailyDate);
            List<string[]> missions = Content.MissionsFor(save.dailyDate);
            for (int i = 0; i < missions.Count; i++)
            {
                string[] mission = missions[i];
                int target;
                if (!int.TryParse(mission[1], out target) || target <= 0) target = 1;
                int progress = i < save.dailyProgress.Count ? save.dailyProgress[i] : 0;
                bool claimed = i < save.dailyClaimed.Count && save.dailyClaimed[i];
                bool complete = progress >= target;
                string text = mission[2] + "  " + Mathf.Min(progress, target) + "/" + target;
                top = Line(top, text, complete ? UiFactory.Lime : UiFactory.Paper, 22);

                int index = i;
                string label = claimed ? "ЗАБРАНО" : complete ? "ЗАБРАТЬ ◈ 60" : "НЕ ВЫПОЛНЕНО";
                Button claim = UiFactory.MakeButton(_content, "Задание " + i, label,
                    complete && !claimed ? UiFactory.Sun : UiFactory.Slate, UiFactory.Ink,
                    ScrollUi.Row(top, 52f), 21, delegate { Claim(index); });
                claim.interactable = complete && !claimed;
                _claimButtons.Add(claim);
                _claimIndices.Add(index);
                top += 60f;
            }
        }

        private void Claim(int index)
        {
            SaveData save = App != null ? App.Save : null;
            if (save == null || index < 0 || index >= save.dailyClaimed.Count) return;
            if (save.dailyClaimed[index]) return;
            List<string[]> missions = Content.MissionsFor(save.dailyDate);
            if (index >= missions.Count) return;
            int target;
            if (!int.TryParse(missions[index][1], out target) || target <= 0) target = 1;
            int progress = index < save.dailyProgress.Count ? save.dailyProgress[index] : 0;
            if (progress < target) return;
            save.dailyClaimed[index] = true;
            save.credits += 60;
            App.Persist();
            Refresh();
        }

        private void Clear()
        {
            _claimButtons.Clear();
            _claimIndices.Clear();
            if (_content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                GameObject child = _content.GetChild(i).gameObject;
                Destroy(child);
            }
        }

        private float Title(float top, string title)
        {
            UiFactory.TextLabel(_content, title, 28, UiFactory.Lime, TextAnchor.MiddleLeft,
                ScrollUi.Row(top, 54f), FontStyle.Bold);
            return top + 58f;
        }

        private float Line(float top, string text, Color color, int size = 22)
        {
            UiFactory.Panel(_content, "Строка", UiFactory.WithAlpha(UiFactory.Slate, 0.55f), ScrollUi.Row(top, 42f));
            UiFactory.TextLabel(_content, text, size, color, TextAnchor.MiddleLeft,
                new UiBox(0.04f, 1f, 0.96f, 1f, 12f, -(top + 42f), -12f, -top));
            return top + 48f;
        }
    }
}
