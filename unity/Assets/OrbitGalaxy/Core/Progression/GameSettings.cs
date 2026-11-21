using System;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Настройки игрока: звук, доступность, управление и качество.
    /// Значения качества/управления — компактные токены; локализованные подписи
    /// формирует UI, само ядро хранит только идентификаторы.
    /// </summary>
    public sealed class GameSettings
    {
        public const string QualityLow = "LOW";
        public const string QualityMedium = "MEDIUM";
        public const string QualityHigh = "HIGH";
        public const string QualityAuto = "AUTO";

        public const string ControlTouch = "TOUCH";
        public const string ControlGamepad = "GAMEPAD";
        public const string ControlKeyboard = "KEYBOARD";
        public const string ControlLeft = "LEFT";
        public const string ControlRight = "RIGHT";
        public const string ControlAlt = "ALT";

        public float MusicVolume;
        public float SfxVolume;
        public bool Muted;
        public bool Haptics;
        public bool ReducedMotion;
        public bool Colorblind;
        public bool LargeText;
        public string Quality;
        public string ControlLayout;
        public bool AutoFire;
        public bool ShowFps;

        public GameSettings()
        {
            MusicVolume = 0.7f;
            SfxVolume = 0.9f;
            Muted = false;
            Haptics = true;
            ReducedMotion = false;
            Colorblind = false;
            LargeText = false;
            Quality = QualityAuto;
            ControlLayout = ControlTouch;
            AutoFire = false;
            ShowFps = false;
        }

        public GameSettings(GameSettings other) : this()
        {
            if (other == null) return;
            MusicVolume = other.MusicVolume;
            SfxVolume = other.SfxVolume;
            Muted = other.Muted;
            Haptics = other.Haptics;
            ReducedMotion = other.ReducedMotion;
            Colorblind = other.Colorblind;
            LargeText = other.LargeText;
            Quality = other.Quality;
            ControlLayout = other.ControlLayout;
            AutoFire = other.AutoFire;
            ShowFps = other.ShowFps;
            Sanitize();
        }

        public GameSettings Clone()
        {
            return new GameSettings(this);
        }

        /// <summary>Приводит загруженные значения к безопасному диапазону, сохраняя неизвестные токены.</summary>
        public void Sanitize()
        {
            MusicVolume = ClampVolume(MusicVolume, 0.7f);
            SfxVolume = ClampVolume(SfxVolume, 0.9f);
            Quality = NormalizeToken(Quality, QualityAuto);
            ControlLayout = NormalizeControl(ControlLayout);
        }

        private static float ClampVolume(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static string NormalizeToken(string token, string fallback)
        {
            if (string.IsNullOrEmpty(token)) return fallback;
            string value = token.Trim().ToUpperInvariant();
            switch (value)
            {
                case "НИЗКОЕ": return QualityLow;
                case "СРЕДНЕЕ": return QualityMedium;
                case "ВЫСОКОЕ": return QualityHigh;
                case "АВТО": return QualityAuto;
                default: return token;
            }
        }

        private static string NormalizeControl(string token)
        {
            if (string.IsNullOrEmpty(token)) return ControlTouch;
            string value = token.Trim().ToUpperInvariant();
            switch (value)
            {
                case "СЕНСОР": return ControlTouch;
                case "ГЕЙМПАД": return ControlGamepad;
                case "КЛАВИАТУРА": return ControlKeyboard;
                case "ЛЕВЫЙ": return ControlLeft;
                case "ПРАВЫЙ": return ControlRight;
                case "АЛЬТЕРНАТИВНЫЙ": return ControlAlt;
                default: return token;
            }
        }
    }
}
