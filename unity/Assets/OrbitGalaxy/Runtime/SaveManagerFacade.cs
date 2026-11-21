using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Orbit.Galaxy.Unity
{
    /// <summary>
    /// Экземплярный фасад над статическим Orbit.Galaxy.SaveManager. Ядро остаётся чистым C#,
    /// а Unity-слой получает объект Saves с тем же контрактом (Load/Save/Backup/Restore/Migrate).
    /// Вызовы через отражение позволяют оба варианта ядра: статический и экземплярный менеджер.
    /// </summary>
    public sealed class SaveManager
    {
        private const string CoreTypeName = "Orbit.Galaxy.SaveManager";

        private static readonly Type CoreType = ResolveCoreType();
        private static readonly Dictionary<string, MethodInfo> Methods = new Dictionary<string, MethodInfo>();
        private object _instance;
        private bool _warned;

        public string SavePath
        {
            get
            {
                if (CoreType != null)
                {
                    PropertyInfo property = CoreType.GetProperty("SavePath", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                    if (property != null)
                    {
                        object target = property.GetGetMethod(false) != null && property.GetGetMethod(false).IsStatic ? null : Instance;
                        object value = property.GetValue(target, null);
                        if (value is string) return (string)value;
                    }
                    FieldInfo field = CoreType.GetField("SavePath", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                    if (field != null)
                    {
                        object value = field.GetValue(field.IsStatic ? null : Instance);
                        if (value is string) return (string)value;
                    }
                }
                return Path.Combine(Application.persistentDataPath, GameConfig.SaveFileName);
            }
        }

        public void SetPath(string path)
        {
            Invoke("SetPath", path);
        }

        public AccountProfile Load()
        {
            return Invoke("Load") as AccountProfile;
        }

        public void Save(AccountProfile profile)
        {
            Invoke("Save", profile);
        }

        public void Backup()
        {
            Invoke("Backup");
        }

        public bool Restore()
        {
            object result = Invoke("Restore");
            return result is bool && (bool)result;
        }

        public AccountProfile Migrate(string legacyJson)
        {
            return Invoke("Migrate", legacyJson) as AccountProfile;
        }

        private object Instance
        {
            get
            {
                if (CoreType == null) return null;
                if (_instance == null)
                {
                    try { _instance = Activator.CreateInstance(CoreType); }
                    catch (Exception) { _instance = null; }
                }
                return _instance;
            }
        }

        private object Invoke(string name, params object[] args)
        {
            if (CoreType == null) return null;
            MethodInfo method = FindMethod(name, args.Length);
            if (method == null)
            {
                Warn("метод " + name + " не найден");
                return null;
            }
            try
            {
                object target = method.IsStatic ? null : Instance;
                if (!method.IsStatic && target == null) return null;
                return method.Invoke(target, args);
            }
            catch (Exception ex)
            {
                Warn("ошибка " + name + ": " + ex.Message);
                return null;
            }
        }

        private MethodInfo FindMethod(string name, int argumentCount)
        {
            string key = name + "@" + argumentCount;
            MethodInfo cached;
            if (Methods.TryGetValue(key, out cached)) return cached;
            MethodInfo[] all = CoreType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            for (int i = 0; i < all.Length; i++)
            {
                if (!string.Equals(all[i].Name, name, StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = all[i].GetParameters();
                if (parameters.Length != argumentCount) continue;
                Methods[key] = all[i];
                return all[i];
            }
            return null;
        }

        private void Warn(string message)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning("ORBITA GALAXY: сохранение недоступно — " + message);
        }

        private static Type ResolveCoreType()
        {
            try
            {
                Assembly core = typeof(GameConfig).Assembly;
                return core.GetType(CoreTypeName, false);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
