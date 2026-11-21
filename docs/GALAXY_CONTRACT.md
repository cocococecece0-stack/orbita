# ORBITA GALAXY — контракт ядра

Этот файл — обязательный контракт для всех параллельных разработчиков. Он фиксирует публичный API чисто C#-ядра и разделение файлов. Файлы-основы **нельзя менять без согласования**:

- `unity/Assets/OrbitGalaxy/Core/Types.cs`
- `unity/Assets/OrbitGalaxy/Core/GameConfig.cs`
- `unity/Assets/OrbitGalaxy/Core/ContentDatabase.cs`

Пространство имён ядра: `Orbit.Galaxy`. Пространство имён Unity-слоя: `Orbit.Galaxy.Unity`. Старый код в `unity/Assets/Orbit` и `Orbit.Core` остаётся нетронутым.

## Уже определено в основах

`Vec2`, `Rng`, `SimEvent`, `SimLog`, `MathGalaxy`, все перечисления (`GameModeKind`, `BotPersonality`, `EvolutionBranch`, `AbilityKind`, `WeaponKind`, `ResourceKind`, `ObjectKind`, `WorldEventKind`, `SimEventKind`, `ShipState`, `Difficulty`, `BotGoal`, `HullShape`), `GameConfig`, `ContentDatabase` и все `*Def` классы.

## Владельцы файлов (не пересекать!)

| Зона | Файлы | Что реализует |
| --- | --- | --- |
| World | `Core/World/*.cs` | `WorldObject` и наследники, `Arena`, `SpatialHash`, `WorldEventSystem` |
| Gameplay | `Core/Gameplay/*.cs` | `Ship`, `Projectile`, `GrowthSystem`, `AbsorptionSystem`, `CombatSystem`, `AbilitySystem`, `EvolutionSystem`, `BossController` |
| AI | `Core/AI/*.cs` | `BotPerception`, `BotDecisionSystem`, `BotNavigation`, `BotBrain` |
| Match | `Core/Match/*.cs` | `MatchSimulation`, `MatchResult`, `LeaderboardEntry`, `ModeSettings` |
| Progression | `Core/Progression/*.cs` | `AccountProfile`, `SaveManager`, `ProgressionSystem`, `SaveMigration` |
| Tests | `Tests/Galaxy/*.cs`, `Tests/Galaxy/Orbit.Galaxy.Core.Tests.csproj` | xUnit-тесты |
| Runtime | `Assets/OrbitGalaxy/Runtime/*.cs` | Unity-рендер, ввод, камера, UI, звук |
| Editor | `Assets/OrbitGalaxy/Editor/*.cs` | ScriptableObject-конфиги, headless-сборка |
| CI/docs | `.github/workflows/galaxy-*.yml`, `docs/GALAXY_*.md`,根目录 README/CHANGELOG | workflows и документация |

## World API

```csharp
public abstract class WorldObject {
    int Id; ObjectKind Kind; Vec2 Position; float Radius; bool Alive;
    int Value; float Rotation; string OwnerId;
}

public sealed class EnergyNode : WorldObject { public EnergyNode(int id, Vec2 pos, int value); }
public sealed class CrystalNode : WorldObject { ... }
public sealed class DarkMatterNode : WorldObject { ... }
public sealed class ArtifactNode : WorldObject
{
    ArtifactNode(int id, Vec2 pos, string effectId, float rotationSpeed);
    string EffectId; float RotationSpeed;
}
public sealed class Asteroid : WorldObject { float Health; float Mass; }
public sealed class Drop : WorldObject { ResourceKind Resource; float Lifetime; }
public sealed class HazardZone : WorldObject { float DamagePerSecond; float Lifetime; bool Slows; }
public sealed class Anomaly : WorldObject { float Pull; float Lifetime; bool GrantsShield; }

public sealed class SpatialHash {
    SpatialHash(float cellSize);
    void Insert(WorldObject o); void Remove(WorldObject o); void Update(WorldObject o);
    void Query(Vec2 center, float radius, List<WorldObject> output);
    void Clear();
}

public sealed class Arena {
    Arena(GameModeKind mode, int seed, float dangerScale);
    float Radius; float SafeRadius; Rng Rng; SpatialHash Index;
    List<WorldObject> Objects; List<HazardZone> Hazards; List<Anomaly> Anomalies;
    void Initialize();
    void Update(float dt, SimLog log, float time);
    void Add(WorldObject obj); void Remove(WorldObject obj);
    bool IsInside(Vec2 p);
    Vec2 ClampToSafe(Vec2 p);
    float DangerAt(Vec2 p);
    void DamageBorder(Ship ship, float dt, SimLog log, float time);
    List<WorldObject> Query(Vec2 center, float radius); // allocates a fresh list; used by AI
    void QueryInto(Vec2 center, float radius, List<WorldObject> output);
    WorldObject FindNearestResource(Vec2 pos, float radius, ResourceKind? filter = null);
    void Shrink(float amount);
    float SafeRadiusStart;
}

public sealed class WorldEventSystem {
    WorldEventKind? ActiveKind; WorldEventKind? PendingKind;
    float WarningRemaining; float ActiveRemaining; float NextEventIn;
    WorldEventSystem(Arena arena, Rng rng, bool chaos);
    void Update(float dt, MatchSimulation match, SimLog log);
    void Trigger(WorldEventKind kind, MatchSimulation match, SimLog log);
    string ActiveTitle; string ActiveDescription;
}
```

`WorldEventSystem` обязана: показывать предупреждение до старта, запускать механику (спавн метеоров/	void-зон/боссов/артефактов/энергии), влиять на арену (`Shrink`), завершаться событием `EventEnd`, не запускать более одного активного события и уведомлять ботов через арену/магч.

## Gameplay API

```csharp
public sealed class Ship {
    int Id; string Name; bool IsPlayer; bool IsBoss; bool IsBot;
    BotPersonality Personality; BotProfile Profile;
    Vec2 Position; Vec2 Velocity; Vec2 Facing; Vec2 MoveInput; Vec2 AimInput;
    float Mass; float Radius; float Level; float Xp; float XpToNext;
    float Hp; float MaxHp; float Shield; float MaxShield;
    float Energy; float Speed; float Damage; float FireInterval; float PickupRadius;
    float FireTimer; float Invulnerability; float RespawnTimer;
    float GravityPower; float Lifesteal; float DamageReduction;
    ShipState State; EvolutionBranch Branch; int EvolutionTier; string EvolutionId;
    HullShape Hull; string PrimaryHex; string AccentHex;
    WeaponKind Weapon; AbilityKind ActiveAbility;
    int Drones; int ExtraProjectiles;
    int Kills; int Deaths; float EnergyCollected; int Crystals; int DarkMatter; int Artifacts;
    float Score; int Streak; int BestStreak; int MultiKill;
    float LastKillTime; int Rank;
    EvolutionDef[] EvolutionsTaken; // fills as chosen
    float[] Cooldowns; // indexed by (int)AbilityKind
    float[] EffectTimers; // indexed by (int)AbilityKind
    List<string> ActionHistory; // bounded to 24 records
    BossDef BossDef; int BossPhase; float BossAttackTimer;
    void RecalculateStats(AccountProfile profile);
    void AddMass(float amount, SimLog log, float time);
    void AddXp(float amount, SimLog log, float time);
    void Heal(float amount);
    void Remember(string action);
}

public sealed class Projectile {
    int Id; int OwnerId; Vec2 Position; Vec2 Velocity; float Damage;
    float Radius; float Lifetime; WeaponKind Weapon; bool Friendly;
    Vec2 PreviousPosition;
}

public static class GrowthSystem {
    static void ApplyPickup(Ship ship, WorldObject obj, SimLog log, float time, AccountProfile profile);
    static void Update(Ship ship, float dt, SimLog log, float time);
    static float MassFromResource(ResourceKind kind);
    static float XpFromResource(ResourceKind kind);
}

public static class AbsorptionSystem {
    static float MassRatio(Ship attacker, Ship target);
    static bool CanAbsorb(Ship attacker, Ship target);
    static bool TryAbsorb(Ship attacker, Ship target, SimLog log, float time);
    static float ContactDamage(Ship attacker, Ship target);
}

public static class CombatSystem {
    CombatSystem(Rng rng);
    List<Projectile> Projectiles;
    void Fire(Ship shooter, Vec2 direction, SimLog log, float time);
    void Update(float dt, List<Ship> ships, Arena arena, SimLog log, float time, AccountProfile profile);
    static void ApplyDamage(Ship target, float amount, Ship source, SimLog log, float time, bool fromProjectile);
}

public static class AbilitySystem {
    static bool CanActivate(Ship ship, AbilityKind kind);
    static bool Activate(Ship ship, AbilityKind kind, SimLog log, float time);
    static void Update(float dt, List<Ship> ships, Arena arena, List<Projectile> projectiles, SimLog log, float time);
    static bool IsActive(Ship ship, AbilityKind kind);
}

public static class EvolutionSystem {
    static List<EvolutionDef> Offer(Ship ship, Rng rng);
    static bool IsAvailable(Ship ship, EvolutionDef def);
    static void Choose(Ship ship, EvolutionDef def, SimLog log, float time, AccountProfile profile);
    static int NextEvolutionLevel(Ship ship);
    static void Apply(Ship ship, EvolutionDef def, AccountProfile profile);
}

public sealed class BossController {
    BossController(Rng rng);
    Ship Spawn(BossDef def, Vec2 position, MatchSimulation match, SimLog log, float time);
    void Update(float dt, List<Ship> ships, Arena arena, SimLog log, float time, CombatSystem combat);
}
```

## AI API

```csharp
public sealed class BotPerception {
    void Refresh(BotBrain brain, Ship self, MatchSimulation match);
    Ship NearestThreat; Ship NearestPrey; Ship NearestBoss;
    WorldObject NearestResource; WorldObject NearestRare;
    HazardZone NearestHazard; Anomaly NearestAnomaly;
    float ThreatScore; float ResourceScore;
    List<WorldObject> NearbyObjects; List<Ship> NearbyShips;
}

public static class BotDecisionSystem {
    static BotGoal Decide(BotBrain brain, Ship self, MatchSimulation match, BotPerception perception);
    static float ScoreGoal(BotGoal goal, Ship self, BotPerception p, MatchSimulation match);
}

public sealed class BotNavigation {
    Vec2 Steer(BotBrain brain, Ship self, Vec2 goal, MatchSimulation match);
    Vec2 AvoidObstacles(Ship self, Vec2 desired, MatchSimulation match);
    Vec2 Separation(Ship self, MatchSimulation match);
}

public sealed class BotBrain {
    BotBrain(Ship ship, BotProfile profile, Rng rng, Difficulty difficulty);
    BotGoal Goal; Vec2 TargetPoint; Ship TargetShip;
    float ThinkTimer; float LodInterval; bool IsElite;
    Ship Ship;
    void Update(float dt, MatchSimulation match, SimLog log);
    string DebugInfo();
}
```

Боты обязаны: думать с LOD-интервалом, иметь гистерезис цели, вести историю действий, убегать от более крупных, охотиться на более мелких, собирать ресурсы, избегать зон/границ, использовать способности, взаимодействовать друг с другом вдали от игрока и не застревать.

## Match API

```csharp
public sealed class LeaderboardEntry {
    int ShipId; string Name; float Score; float Mass; int Level; int Kills;
    int Rank; bool IsPlayer; BotPersonality Personality; bool IsBoss;
}

public sealed class MatchResult {
    GameModeKind Mode; float Duration; int Rank; int Participants;
    float Score; float Mass; int Level; int Kills; int Deaths;
    float EnergyCollected; int Crystals; int DarkMatter; int Artifacts;
    int BossKills; int BestStreak; bool Victory;
    int Credits; int CrystalsReward; int Xp; int AccountXp;
    List<LeaderboardEntry> Leaderboard; List<SimEvent> Highlights;
}

public sealed class MatchSimulation {
    MatchSimulation(GameModeKind mode, int seed, Difficulty difficulty, AccountProfile profile);
    GameModeKind Mode; ModeSettings Settings; Rng Rng; int Seed;
    Arena Arena; WorldEventSystem Events; CombatSystem Combat;
    BossController Bosses; SimLog Log;
    List<Ship> Ships; List<BotBrain> Bots; Ship Player;
    float Time; bool Finished; MatchResult Result;
    List<LeaderboardEntry> Leaderboard; int PlayerRank;
    void Update(float dt);
    void PlayerMove(Vec2 direction);
    void PlayerAim(Vec2 direction);
    void PlayerBoost(bool enabled);
    void PlayerActivateAbility();
    void PlayerDash();
    void PlayerChooseEvolution(EvolutionDef def);
    void Finish(bool voluntary);
}
```

`MatchSimulation` — единственный владелец симуляции. Она вызывает подсистемы в фиксированном порядке: ввод → AI → abilities → combat → growth → world → events → leaderboard → итог. `Update` должен быть детерминирован и не ссылаться на Unity. Для вывода эволюций использовать `EvolutionSystem.Offer`, при достижении `NextEvolutionLevel` приостанавливать автоматический выбор игрока (поле `PendingEvolutionChoices`), но боты выбирают сразу.

## Progression API

```csharp
public sealed class AccountProfile {
    int SchemaVersion; int Level; float Xp;
    int Credits; int Crystals; int DarkMatter;
    string SelectedShip; string SelectedTrail; string SelectedDeathFx;
    string SelectedFrame; string SelectedAvatar; string SelectedTitle;
    List<string> Owned; List<string> Achievements; List<string> Secrets;
    Dictionary<string,int> Records; Dictionary<string,int> Research;
    Dictionary<string,int> Stats; Dictionary<string,int> QuestProgress;
    Dictionary<string,int> DailyProgress; Dictionary<string,int> WeeklyProgress;
    string DailyDate; string WeeklyKey;
    int WinStreak; int BestWinStreak; int Matches; int Wins; int BossKills;
    GameSettings Settings;
    void AddXp(float amount);
    void AddCredits(int amount);
    void Unlock(string id);
    bool IsUnlocked(string id);
    float ResearchBonus(string id);
    void ApplyResult(MatchResult result, DateTime now);
}

public sealed class GameSettings {
    float MusicVolume; float SfxVolume; bool Muted; bool Haptics;
    bool ReducedMotion; bool Colorblind; bool LargeText;
    string Quality; string ControlLayout; bool AutoFire; bool ShowFps;
}

public static class SaveManager {
    string SavePath { get; }
    void SetPath(string path);
    AccountProfile Load();
    void Save(AccountProfile profile);
    void Backup();
    bool Restore();
    AccountProfile Migrate(string legacyJson);
}

public static class SaveMigration {
    static AccountProfile FromLegacy(string json);
    static string ToJson(AccountProfile profile);
    static AccountProfile FromJson(string json);
}
```

Схема JSON — версия 5. Миграция читает старый `orbita_save.json`/v4 и переносит совместимые поля, не удаляя файл. Резервная копия — `*.bak`.

## Unity layer

`GalaxyBootstrap` создаёт `GalaxyApp` через `RuntimeInitializeOnLoadMethod`. `GalaxyApp` владеет `MatchSimulation`, `AccountProfile`, `SaveManager`, камерой, рендером, вводом, UI и звуком. Визуал: процедурные спрайты/материалы без внешних шрифтов, Canvas uGUI для HUD/меню, SpriteRenderer + object pool для арены. Не использовать TextMeshPro.

## Тесты

`unity/Tests/Galaxy/Orbit.Galaxy.Core.Tests.csproj` подключает **только** `../../Assets/OrbitGalaxy/Core/**/*.cs` и `*.cs` из своей папки. Старый проект `unity/Tests/Orbit.Core.Tests.csproj` остаётся без изменений. Нужны проверки роста, поглощения, разницы масс, урона/щита, всех способностей, эволюций, LOD-решений ботов, генерации арены, событий, рейтинга, сохранений/миграций, завершения матча, сценария 35 участников и длительного прогона.

## Изоляция от старой версии (по результатам аудита)

- Новые сборки используют define-символ `ORBITA_GALAXY`. Старый `OrbitBootstrap` нужно отключить этим символом (минимальная правка старого файла), иначе он создаст старый UI в Galaxy-сборке.
- Galaxy-ассеты лежат только в `unity/Assets/OrbitGalaxy/**`. Звук — в `unity/Assets/OrbitGalaxy/Resources/GalaxyAudio/` (не в старом `Resources/Audio/`).
- Сейв: `orbita_galaxy_save_v5.json` / PlayerPrefs-ключ `orbita.galaxy.save.v5`. Старые сохранения `orbita.save.v4` не перезаписываются.
- Добавить `.asmdef`: `Orbit.Galaxy.Core` (без Unity-ссылок), `Orbit.Galaxy.Runtime` (ссылка на Core + UnityEngine.UI), `Orbit.Galaxy.Editor` (ссылка на Core/Runtime + UnityEditor).
- Ядро не должно зависеть от Newtonsoft: использовать встроенный минимальный JSON-кодек в `Core/Progression/`.
- Тег релиза Galaxy — `g*`, workflow `.github/workflows/galaxy-build.yml`, старые `u*`/`v*` не трогать.
