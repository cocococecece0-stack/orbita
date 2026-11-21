using System.Globalization;
using Orbit.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Orbit.Unity
{
    /// <summary>Экран финиша: результат, цель, награды и быстрый рестарт.</summary>
    public sealed class ResultScreen : OrbitScreen
    {
        private Text _scoreText;
        private Text _recordBadge;
        private Text _objectiveText;
        private Text _statsText;
        private Text _rewardText;
        private Text _achievementsText;
        private Text _modeText;
        private GameEngineCore _lastEngine;
        private RunRecord _lastRecord;
        private RewardSummary _lastReward;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            UiFactory.Panel(transform, "Фон", UiFactory.Ink, UiBox.Full);
            UiFactory.Circle(transform, "Декор", UiFactory.WithAlpha(UiFactory.Violet, 0.12f), UiBox.At(900f, 1600f, 700f, 700f));

            _modeText = UiFactory.Chip(transform, "Режим", "КЛАССИКА", UiFactory.Violet, UiFactory.Ink,
                new UiBox(0.06f, 0.9f, 0.45f, 0.965f, 0f, 0f, 0f, 0f), 26).Label;
            UiFactory.Chip(transform, "Финиш", "ФИНИШ", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.72f, 0.9f, 0.94f, 0.965f, 0f, 0f, 0f, 0f), 26);

            UiFactory.TextLabel(transform, "ТВОЙ СЧЁТ", 24, UiFactory.WithAlpha(UiFactory.Paper, 0.7f), TextAnchor.MiddleCenter,
                new UiBox(0.05f, 0.845f, 0.95f, 0.895f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            _scoreText = UiFactory.TextLabel(transform, "0", 120, UiFactory.Lime, TextAnchor.MiddleCenter,
                new UiBox(0.04f, 0.71f, 0.96f, 0.85f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            UiFactory.FitText(_scoreText, 54, 126);

            _recordBadge = UiFactory.Chip(transform, "Рекорд", string.Empty, UiFactory.Coral, UiFactory.Ink,
                new UiBox(0.22f, 0.655f, 0.78f, 0.705f, 0f, 0f, 0f, 0f), 30).Label;

            _objectiveText = UiFactory.TextLabel(transform, "ЦЕЛЬ", 30, UiFactory.Sun, TextAnchor.MiddleCenter,
                new UiBox(0.05f, 0.575f, 0.95f, 0.65f, 0f, 0f, 0f, 0f), FontStyle.Bold);

            UiFactory.Panel(transform, "Статистика фон", UiFactory.WithAlpha(UiFactory.Slate, 0.9f),
                new UiBox(0.04f, 0.4f, 0.96f, 0.565f, 0f, 0f, 0f, 0f), UiFactory.RoundedSprite).type = Image.Type.Sliced;
            _statsText = UiFactory.TextLabel(transform, string.Empty, 26, UiFactory.Paper, TextAnchor.MiddleCenter,
                new UiBox(0.06f, 0.41f, 0.94f, 0.555f, 0f, 0f, 0f, 0f), FontStyle.Bold);

            UiFactory.Panel(transform, "Награды фон", UiFactory.WithAlpha(UiFactory.Deep, 0.9f),
                new UiBox(0.04f, 0.22f, 0.96f, 0.39f, 0f, 0f, 0f, 0f), UiFactory.RoundedSprite).type = Image.Type.Sliced;
            UiFactory.TextLabel(transform, "НАГРАДА ЗА ЗАБЕГ", 22, UiFactory.Lime, TextAnchor.MiddleLeft,
                new UiBox(0.07f, 0.355f, 0.6f, 0.385f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            _rewardText = UiFactory.TextLabel(transform, string.Empty, 28, UiFactory.Sun, TextAnchor.MiddleLeft,
                new UiBox(0.07f, 0.285f, 0.93f, 0.355f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            _achievementsText = UiFactory.TextLabel(transform, string.Empty, 19, UiFactory.Paper, TextAnchor.MiddleLeft,
                new UiBox(0.07f, 0.225f, 0.93f, 0.285f, 0f, 0f, 0f, 0f));

            UiFactory.MakeButton(transform, "Ещё", "ЕЩЁ", UiFactory.Lime, UiFactory.Ink,
                new UiBox(0.05f, 0.08f, 0.49f, 0.195f, 0f, 0f, 0f, 0f), 56,
                delegate
                {
                    if (App == null || _lastEngine == null) return;
                    App.StartRun(_lastEngine.Mode);
                });
            UiFactory.MakeButton(transform, "В меню", "В МЕНЮ", UiFactory.Violet, UiFactory.Ink,
                new UiBox(0.51f, 0.08f, 0.95f, 0.195f, 0f, 0f, 0f, 0f), 56,
                delegate { if (App != null) App.GoHub(); });

            UiFactory.TextLabel(transform, "ORBITA 4.0", 16, UiFactory.WithAlpha(UiFactory.Paper, 0.4f),
                TextAnchor.MiddleCenter, new UiBox(0f, 0.015f, 1f, 0.06f, 0f, 0f, 0f, 0f));
        }

        public void ShowResult(GameEngineCore engine, RunRecord record, RewardSummary reward, SaveData save)
        {
            _lastEngine = engine;
            _lastRecord = record;
            _lastReward = reward;
            Refresh();
        }

        public override void Refresh()
        {
            if (_lastRecord == null || _lastEngine == null) return;
            RunRecord record = _lastRecord;
            GameEngineCore engine = _lastEngine;
            ModeDef mode = Content.Mode(Content.ModeFromId(record.mode));
            ObjectiveDef objective = engine.Objective();
            bool stack = Content.ModeFromId(record.mode) == ModeKind.Stack;

            _modeText.text = mode.Title + " · СЕКТОР " + record.sector;
            _scoreText.text = record.score.ToString("N0", CultureInfo.InvariantCulture);
            _recordBadge.text = engine.IsNewRecord ? "★ НОВЫЙ РЕКОРД ★" : "РЕКОРД " + engine.Best.ToString("N0", CultureInfo.InvariantCulture);
            _recordBadge.color = engine.IsNewRecord ? UiFactory.Sun : UiFactory.Slate;

            string status = record.completed ? "ЦЕЛЬ ВЫПОЛНЕНА" : "ЦЕЛЬ НЕ ДОСТИГНУТА";
            _objectiveText.text = status + "\n" + objective.Title + " " + engine.ObjectiveValue() + " / " + objective.Target;
            _objectiveText.color = record.completed ? UiFactory.Lime : UiFactory.Coral;

            string linesLabel = stack ? "ЛИНИИ" : "СФЕРЫ";
            _statsText.text =
                linesLabel + " " + record.orbs + "     УРОВЕНЬ " + record.level + "     КОМБО x" + record.maxCombo + "\n" +
                "ВРЕМЯ " + record.seconds + " С     УКЛОНЕНИЯ " + engine.Stats.dodges + "     РЕДКИЕ " + record.rareOrbs;

            if (_lastReward != null)
            {
                _rewardText.text = "◈ +" + _lastReward.credits + "     XP +" + _lastReward.xp +
                                   (_lastReward.levels > 0 ? "     НОВЫЙ УРОВЕНЬ +" + _lastReward.levels : string.Empty);
                _achievementsText.text = _lastReward.achievements.Count == 0
                    ? "НОВЫХ ДОСТИЖЕНИЙ НЕТ"
                    : "ДОСТИЖЕНИЯ: " + string.Join(", ", _lastReward.achievements.ToArray());
            }
            else
            {
                _rewardText.text = string.Empty;
                _achievementsText.text = string.Empty;
            }
        }
    }
}
