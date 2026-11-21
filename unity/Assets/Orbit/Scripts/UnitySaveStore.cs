using System;
using Orbit.Core;
using UnityEngine;

namespace Orbit.Unity
{
    /// <summary>PlayerPrefs-хранилище сохранения v4 с безопасными fallback-ветками.</summary>
    public static class UnitySaveStore
    {
        public const string Key = "orbita.save.v4";

        public static SaveData Load()
        {
            try
            {
                string json = PlayerPrefs.GetString(Key, string.Empty);
                SaveData save = SaveCodec.Deserialize(json);
                if (save == null) save = new SaveData();
                save.EnsureDefaults();
                return save;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Orbit Save: не удалось прочитать сохранение — " + ex.Message);
                SaveData fallback = new SaveData();
                try { fallback.EnsureDefaults(); }
                catch (Exception) { /* оставляем как есть */ }
                return fallback;
            }
        }

        public static void Save(SaveData save)
        {
            if (save == null) return;
            try
            {
                string json = SaveCodec.Serialize(save);
                if (string.IsNullOrEmpty(json)) return;
                PlayerPrefs.SetString(Key, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Orbit Save: не удалось записать сохранение — " + ex.Message);
            }
        }

        public static void Clear()
        {
            try
            {
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Orbit Save: не удалось очистить сохранение — " + ex.Message);
            }
        }
    }
}
