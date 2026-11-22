using System;
using System.IO;
using System.Text;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Файловое хранилище профиля v5. Все операции защищены: повреждённый файл
    /// превращается в новый профиль, старые сохранения читаются, но не удаляются.
    /// </summary>
    public static class SaveManager
    {
        public const string SaveFileName = "orbita_galaxy_save_v5.json";
        public const string BackupExtension = ".bak";
        public const string PlayerPrefsKey = "orbita.galaxy.save.v5";
        public const string LegacySaveFileName = "orbita_save.json";
        public const string LegacyPlayerPrefsKey = "orbita.save.v4";

        private static string _path;

        public static string SavePath
        {
            get { return string.IsNullOrEmpty(_path) ? DefaultPath() : _path; }
        }

        public static string BackupPath
        {
            get { return SavePath + BackupExtension; }
        }

        public static void SetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _path = null;
                return;
            }
            string candidate = path.Trim();
            try
            {
                if (Directory.Exists(candidate)) candidate = Path.Combine(candidate, SaveFileName);
                _path = Path.GetFullPath(candidate);
            }
            catch (Exception)
            {
                _path = candidate;
            }
        }

        public static AccountProfile Load()
        {
            // Each path is isolated: an unreadable main file must not hide a healthy backup.
            AccountProfile profile = TryLoadPath(SavePath);
            if (profile != null) return profile;
            profile = TryLoadPath(BackupPath);
            if (profile != null) return profile;
            return new AccountProfile();
        }

        private static AccountProfile TryLoadPath(string path)
        {
            try
            {
                string json = ReadValidJson(path);
                if (json == null) return null;
                return SaveMigration.FromJson(json) ?? new AccountProfile();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(AccountProfile profile)
        {
            if (profile == null) return;
            try
            {
                string json = SaveMigration.ToJson(profile);
                if (string.IsNullOrEmpty(json)) return;
                string path = SavePath;
                EnsureDirectory(path);
                // Previous good version is kept in .bak before the main file is replaced.
                Backup();
                WriteAtomic(path, json);
            }
            catch (Exception)
            {
                // Недоступный диск не должен ронять игру: профиль остаётся в памяти.
            }
        }

        /// <summary>Копирует текущий корректный файл в orbita_galaxy_save_v5.json.bak.</summary>
        public static void Backup()
        {
            try
            {
                string json = ReadValidJson(SavePath);
                if (json == null) return;
                EnsureDirectory(SavePath);
                WriteAtomic(BackupPath, json);
            }
            catch (Exception)
            {
                // Отсутствие прав на резервную копию не блокирует сохранение.
            }
        }

        /// <summary>Восстанавливает профиль из .bak. Возвращает false, если копии нет или она бита.</summary>
        public static bool Restore()
        {
            try
            {
                string json = ReadValidJson(BackupPath);
                if (json == null) return false;
                string path = SavePath;
                EnsureDirectory(path);
                WriteAtomic(path, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Явная миграция строки legacy v3/v4. Исходный текст никак не изменяется.</summary>
        public static AccountProfile Migrate(string legacyJson)
        {
            return SaveMigration.FromLegacy(legacyJson);
        }

        /// <summary>Миграция из файла старой версии без его удаления или перезаписи.</summary>
        public static AccountProfile MigrateFile(string legacyPath)
        {
            try
            {
                if (string.IsNullOrEmpty(legacyPath) || !File.Exists(legacyPath)) return new AccountProfile();
                return SaveMigration.FromLegacy(File.ReadAllText(legacyPath));
            }
            catch (Exception)
            {
                return new AccountProfile();
            }
        }

        /// <summary>Первый существующий файл старого сохранения рядом с текущим (или null).</summary>
        public static string FindLegacyPath()
        {
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (string.IsNullOrEmpty(directory)) directory = Directory.GetCurrentDirectory();
                string[] names = { LegacySaveFileName, "orbita_galaxy_save.json", "orbita.save.v4.json", "orbita_save_v4.json" };
                for (int i = 0; i < names.Length; i++)
                {
                    string candidate = Path.Combine(directory, names[i]);
                    if (File.Exists(candidate)) return candidate;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string DefaultPath()
        {
            try
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SaveFileName);
            }
            catch (Exception)
            {
                return SaveFileName;
            }
        }

        private static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
        }

        /// <summary>Reads a file only when it contains a valid JSON object; null means unusable.</summary>
        private static string ReadValidJson(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return null;
            if (GalaxyJson.Object(GalaxyJson.Parse(json)) == null) return null;
            return json;
        }

        /// <summary>
        /// Writes through a temporary file so an interrupted write never leaves a half-written
        /// main save. The temporary file is removed on failure.
        /// </summary>
        private static void WriteAtomic(string path, string json)
        {
            string temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    try { File.Delete(temp); }
                    catch (Exception) { }
                }
            }
        }
    }
}
