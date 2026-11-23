using System.Collections.Generic;
using UnityEngine;

namespace Orbit.Galaxy.Unity
{
    /// <summary>
    /// Отрисовка арены пулом SpriteRenderer: объекты, корабли, снаряды, эффекты, границы и сетка.
    /// Всё отсекается по камере, частицы ограничены GameConfig.MaxVisibleParticles.
    /// </summary>
    public sealed class ArenaRenderer : MonoBehaviour
    {
        private const int PoolLimit = 1100;
        private const int ProjectileLimit = 320;
        private const int MaxSnakeSegmentsDrawn = 360;

        private GalaxyApp _app;
        private Camera _camera;
        private GameObject _root;
        private Transform _poolRoot;
        private SpriteRenderer _background;
        private SpriteRenderer _nebula;
        private SpriteRenderer _grid;
        private SpriteRenderer _border;
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>(512);
        private readonly List<Particle> _particles = new List<Particle>(GameConfig.MaxVisibleParticles);
        private int _cursor;
        private int _segmentsDrawn;
        private bool _idle;
        private int _particleLimit = GameConfig.MaxVisibleParticles;

        /// <summary>Качество влияет на реальный бюджет частиц, а не только на частоту кадров.</summary>
        public void SetQuality(string quality)
        {
            if (quality == GameSettings.QualityLow) _particleLimit = Mathf.Max(24, GameConfig.MaxVisibleParticles / 4);
            else if (quality == GameSettings.QualityMedium) _particleLimit = Mathf.Max(48, GameConfig.MaxVisibleParticles / 2);
            else _particleLimit = GameConfig.MaxVisibleParticles;
            if (_particles.Count > _particleLimit)
                _particles.RemoveRange(_particleLimit, _particles.Count - _particleLimit);
        }

        public void Initialize(GalaxyApp app)
        {
            _app = app;
            if (_root != null) UnityEngine.Object.Destroy(_root);

            // Pooled renderers are children of _root. Dropping the list here is required before a
            // new root is created, otherwise a second match keeps drawing through objects that
            // Unity is about to destroy (MissingReferenceException / invisible arena).
            _pool.Clear();
            _cursor = 0;
            _camera = null;

            _root = new GameObject("ArenaView");
            _root.transform.SetParent(transform, false);
            _background = MakeLayer("Фон", TextureFactory.Square(), -102);
            _nebula = MakeLayer("Туманность", TextureFactory.Nebula(), -101);
            _grid = MakeLayer("Сетка", TextureFactory.Grid(), -100);
            _border = MakeLayer("Граница", TextureFactory.ThinRing(), -99);
            GameObject objects = new GameObject("Объекты");
            objects.transform.SetParent(_root.transform, false);
            _poolRoot = objects.transform;

            _particles.Clear();
            _cursor = 0;
            _idle = false;
            _camera = app != null && app.CameraRig != null ? app.CameraRig.Camera : Camera.main;
        }

        public void Render(MatchSimulation match)
        {
            if (_root == null) Initialize(_app);
            if (_camera == null) _camera = Camera.main;

            // In the menu there is nothing to draw: hide the pool once, then skip the per-frame
            // walk over up to PoolLimit renderers so idle screens stay cheap on weak devices.
            if (match == null)
            {
                if (!_idle)
                {
                    ShowDecorations(false);
                    _particles.Clear();
                    BeginFrame();
                    EndFrame();
                    _idle = true;
                }
                return;
            }
            _idle = false;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.06f);
            if (dt < 0f) dt = 0f;
            BeginFrame();

            ShowDecorations(true);
            float radius = match.Arena == null ? GameConfig.ArenaRadius : match.Arena.Radius;
            UpdateDecorations(match, radius);

            Vec2 center = _camera == null
                ? Vec2.Zero
                : new Vec2(_camera.transform.position.x, _camera.transform.position.y);
            float halfHeight = _camera == null ? 420f : _camera.orthographicSize;
            float halfWidth = halfHeight * (_camera == null ? 1.8f : Mathf.Max(0.5f, _camera.aspect));
            const float margin = 140f;

            if (match.Arena != null)
            {
                List<WorldObject> objects = match.Arena.Objects;
                if (objects != null)
                {
                    for (int i = 0; i < objects.Count; i++)
                    {
                        WorldObject o = objects[i];
                        if (o == null || !o.Alive) continue;
                        if (!Visible(o.Position, o.Radius, center, halfWidth, halfHeight, margin)) continue;
                        DrawObject(o);
                    }
                }

                List<HazardZone> hazards = match.Arena.Hazards;
                if (hazards != null)
                {
                    for (int i = 0; i < hazards.Count; i++)
                    {
                        HazardZone zone = hazards[i];
                        if (zone == null || !zone.Alive) continue;
                        if (!Visible(zone.Position, zone.Radius, center, halfWidth, halfHeight, margin)) continue;
                        DrawObject(zone);
                    }
                }

                List<Anomaly> anomalies = match.Arena.Anomalies;
                if (anomalies != null)
                {
                    for (int i = 0; i < anomalies.Count; i++)
                    {
                        Anomaly anomaly = anomalies[i];
                        if (anomaly == null || !anomaly.Alive) continue;
                        if (!Visible(anomaly.Position, anomaly.Radius, center, halfWidth, halfHeight, margin)) continue;
                        DrawObject(anomaly);
                    }
                }
            }

            int drawn = 0;
            Ship player = match.Player;
            if (player != null && Visible(player.Position, player.Radius, center, halfWidth, halfHeight, margin))
            {
                DrawSnakeBody(player, center, halfWidth, halfHeight, margin);
                DrawShip(player);
                drawn++;
            }
            List<Ship> ships = match.Ships;
            if (ships != null && drawn < GameConfig.MaxVisibleShips)
            {
                for (int i = 0; i < ships.Count && drawn < GameConfig.MaxVisibleShips; i++)
                {
                    Ship ship = ships[i];
                    if (ship == null || ship == player || !ship.IsBoss) continue;
                    if (!Visible(ship.Position, ship.Radius, center, halfWidth, halfHeight, margin)) continue;
                    DrawSnakeBody(ship, center, halfWidth, halfHeight, margin);
                    DrawShip(ship);
                    drawn++;
                }
                for (int i = 0; i < ships.Count && drawn < GameConfig.MaxVisibleShips; i++)
                {
                    Ship ship = ships[i];
                    if (ship == null || ship == player || ship.IsBoss) continue;
                    if (!Visible(ship.Position, ship.Radius, center, halfWidth, halfHeight, margin)) continue;
                    DrawSnakeBody(ship, center, halfWidth, halfHeight, margin);
                    DrawShip(ship);
                    drawn++;
                }
            }

            if (match.Combat != null && match.Combat.Projectiles != null)
            {
                List<Projectile> projectiles = match.Combat.Projectiles;
                int limit = Mathf.Min(projectiles.Count, ProjectileLimit);
                for (int i = 0; i < limit; i++)
                {
                    Projectile projectile = projectiles[i];
                    if (projectile == null) continue;
                    if (!Visible(projectile.Position, projectile.Radius, center, halfWidth, halfHeight, margin)) continue;
                    DrawProjectile(projectile);
                }
            }

            StepParticles(dt);
            DrawParticles();
            EndFrame();
        }

        public void OnEvent(SimEvent ev)
        {
            if (ev == null || _particles == null) return;

            // Drop clusters reuse the existing Pickup event kind, but they get their own bloom so
            // the reward for a kill is readable at a glance without adding a protected enum value.
            if (DeathDropSystem.IsDropEvent(ev))
            {
                SpawnBurst(ev.Position, TextureFactory.Lime, 18, 190f, 11f, 0.8f, true);
                SpawnBurst(ev.Position, TextureFactory.Cyan, 10, 120f, 8f, 0.55f, false);
                return;
            }

            switch (ev.Kind)
            {
                case SimEventKind.Pickup:
                    SpawnBurst(ev.Position, TextureFactory.Cyan, 5, 70f, 7f, 0.42f, false);
                    break;
                case SimEventKind.LevelUp:
                    SpawnBurst(ev.Position, TextureFactory.Lime, 20, 230f, 11f, 0.85f, true);
                    break;
                case SimEventKind.Evolution:
                    SpawnBurst(ev.Position, TextureFactory.Violet, 32, 300f, 14f, 1.1f, true);
                    break;
                case SimEventKind.Kill:
                {
                    bool playerDeath = _app != null && _app.Match != null && _app.Match.Player != null &&
                                       ev.Value == _app.Match.Player.Id;
                    if (playerDeath)
                    {
                        SpawnBurst(ev.Position, TextureFactory.Coral, 42, 350f, 17f, 1.25f, true);
                        SpawnBurst(ev.Position, TextureFactory.Sun, 18, 220f, 12f, 0.9f, true);
                    }
                    else SpawnBurst(ev.Position, TextureFactory.Coral, 22, 260f, 13f, 0.7f, false);
                    break;
                }
                case SimEventKind.Absorb:
                case SimEventKind.LeaderKilled:
                    SpawnBurst(ev.Position, TextureFactory.Coral, 22, 260f, 13f, 0.7f, false);
                    break;
                case SimEventKind.Damage:
                    SpawnBurst(ev.Position, TextureFactory.Orange, 5, 90f, 8f, 0.3f, false);
                    break;
                case SimEventKind.Ability:
                    SpawnAbilityEffect(ev);
                    break;
                case SimEventKind.BossSpawn:
                    SpawnBurst(ev.Position, TextureFactory.Coral, 38, 340f, 17f, 1.3f, true);
                    break;
                case SimEventKind.EventWarning:
                case SimEventKind.Warning:
                    SpawnBurst(ev.Position, TextureFactory.Sun, 8, 120f, 9f, 0.7f, true);
                    break;
                case SimEventKind.EventStart:
                    SpawnBurst(ev.Position, TextureFactory.Violet, 26, 280f, 12f, 1f, false);
                    break;
                case SimEventKind.EventEnd:
                    SpawnBurst(ev.Position, TextureFactory.Lime, 12, 150f, 9f, 0.6f, true);
                    break;
                case SimEventKind.Victory:
                case SimEventKind.Defeat:
                case SimEventKind.MatchEnd:
                    SpawnBurst(ev.Position, ev.Kind == SimEventKind.Victory ? TextureFactory.Sun : TextureFactory.Violet,
                        44, 380f, 16f, 1.5f, false);
                    break;
                default:
                    break;
            }
        }

        private SpriteRenderer MakeLayer(string name, Sprite sprite, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void ShowDecorations(bool visible)
        {
            if (_background != null) _background.gameObject.SetActive(visible);
            if (_nebula != null) _nebula.gameObject.SetActive(visible);
            if (_grid != null) _grid.gameObject.SetActive(visible);
            if (_border != null) _border.gameObject.SetActive(visible);
        }

        private void UpdateDecorations(MatchSimulation match, float radius)
        {
            float diameter = radius * 2.1f;
            if (_background != null)
            {
                _background.transform.localScale = new Vector3(diameter, diameter, 1f);
                _background.color = TextureFactory.Ink;
            }
            if (_nebula != null)
            {
                float nebulaSize = diameter * 1.18f;
                _nebula.transform.localScale = new Vector3(nebulaSize, nebulaSize, 1f);
                _nebula.color = new Color(0.82f, 0.88f, 1f, 0.62f);
            }
            if (_grid != null)
            {
                _grid.transform.localScale = new Vector3(diameter, diameter, 1f);
                float alpha = 0.085f + (GalaxyUiTheme.ReducedMotion ? 0f : 0.02f * Mathf.Sin(Time.unscaledTime * 0.6f));
                _grid.color = new Color(0.35f, 0.62f, 0.9f, alpha);
            }
            if (_border != null)
            {
                float borderSize = radius * 2.02f;
                _border.transform.localScale = new Vector3(borderSize, borderSize, 1f);
                float shrink = 1f;
                if (match.Arena != null && match.Arena.SafeRadiusStart > 0f)
                    shrink = Mathf.Clamp01(match.Arena.SafeRadius / match.Arena.SafeRadiusStart);
                bool warning = match.Events != null && match.Events.PendingKind.HasValue;
                Color color = warning ? TextureFactory.Coral : Color.Lerp(TextureFactory.Coral, TextureFactory.Sun, shrink);
                color.a = 0.36f + (GalaxyUiTheme.ReducedMotion ? 0f : 0.2f * Mathf.PingPong(Time.unscaledTime * 0.8f, 1f));
                _border.color = color;
            }
        }

        private static bool Visible(Vec2 point, float radius, Vec2 center, float halfWidth, float halfHeight, float margin)
        {
            float x = Mathf.Abs(point.X - center.X);
            float y = Mathf.Abs(point.Y - center.Y);
            return x <= halfWidth + radius + margin && y <= halfHeight + radius + margin;
        }

        private void DrawObject(WorldObject o)
        {
            Sprite sprite;
            Color color;
            float size = Mathf.Max(8f, o.Radius * 2f);
            float rotation = o.Rotation * Mathf.Rad2Deg;
            int order = 3;

            switch (o.Kind)
            {
                case ObjectKind.Energy:
                    sprite = TextureFactory.Circle();
                    color = TextureFactory.Cyan;
                    // Richer motes read as more valuable at a glance; the slow pulse keeps a
                    // dense field alive without adding per-particle simulation state.
                    size *= Mathf.Min(1.5f, 1f + Mathf.Max(0, o.Value) * 0.06f) *
                            Pulse(3f, o.Id, 0.08f);
                    break;
                case ObjectKind.Crystal:
                    sprite = TextureFactory.Diamond();
                    color = TextureFactory.Violet;
                    break;
                case ObjectKind.DarkMatter:
                    sprite = TextureFactory.Glow();
                    color = TextureFactory.Pink;
                    size *= 1.35f;
                    break;
                case ObjectKind.Artifact:
                    sprite = TextureFactory.Star();
                    color = TextureFactory.Sun;
                    ArtifactNode artifact = o as ArtifactNode;
                    if (artifact != null) rotation += Time.unscaledTime * artifact.RotationSpeed * Mathf.Rad2Deg;
                    size *= 1.25f;
                    break;
                case ObjectKind.Asteroid:
                    sprite = TextureFactory.Rock();
                    color = new Color(0.58f, 0.63f, 0.72f, 1f);
                    break;
                case ObjectKind.Drop:
                    sprite = TextureFactory.Glow();
                    Drop drop = o as Drop;
                    color = drop == null ? TextureFactory.Paper : DropColor(drop.Resource);
                    // Rich motes read as more valuable; the slow pulse keeps a fresh pile alive
                    // without any extra per-particle simulation state. Fade only near expiry.
                    size *= 1.3f * Mathf.Min(1.7f, 1f + Mathf.Max(0, o.Value) * 0.02f) *
                            Pulse(4f, o.Id, 0.08f);
                    color.a = Mathf.Clamp01(drop == null ? 1f : drop.Lifetime / 2.5f);
                    order = 4;
                    break;
                case ObjectKind.Hazard:
                    sprite = TextureFactory.Ring();
                    color = TextureFactory.Coral;
                    color.a = 0.28f + (GalaxyUiTheme.ReducedMotion ? 0f : 0.18f * Mathf.Sin(Time.unscaledTime * 3f + o.Id));
                    size *= 1.5f;
                    order = 1;
                    break;
                case ObjectKind.Anomaly:
                    sprite = TextureFactory.Ring();
                    Anomaly anomaly = o as Anomaly;
                    color = anomaly != null && anomaly.GrantsShield ? TextureFactory.Lime : TextureFactory.Violet;
                    color.a = 0.32f + (GalaxyUiTheme.ReducedMotion ? 0f : 0.18f * Mathf.Sin(Time.unscaledTime * 2f + o.Id));
                    size *= 1.8f;
                    order = 1;
                    break;
                default:
                    sprite = TextureFactory.Circle();
                    color = TextureFactory.Paper;
                    break;
            }
            Spawn(o.Position, size, color, sprite, rotation, order);
        }

        /// <summary>Reduced motion keeps pickups readable but freezes the size pulse.</summary>
        private static float Pulse(float speed, float phase, float amplitude)
        {
            return GalaxyUiTheme.ReducedMotion ? 1f : 1f + amplitude * Mathf.Sin(Time.unscaledTime * speed + phase);
        }

        private static Color DropColor(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Crystal: return TextureFactory.Violet;
                case ResourceKind.DarkMatter: return TextureFactory.Pink;
                case ResourceKind.Artifact: return TextureFactory.Sun;
                default: return TextureFactory.Cyan;
            }
        }

        /// <summary>
        /// Draws the trailing body before the hull so the head always sits on top. Long snakes use
        /// a stride and the whole pass has a hard budget: the sprite pool is shared with objects,
        /// projectiles and particles, so bodies must never starve the rest of the arena.
        /// </summary>
        private void DrawSnakeBody(Ship ship, Vec2 center, float halfWidth, float halfHeight, float margin)
        {
            SnakeBody body = ship.Body;
            if (body == null || ship.State == ShipState.Dead || body.Count < 2) return;

            int count = body.Count;
            int stride = count > 20 ? 2 : 1;
            float alpha = ship.State == ShipState.Respawning ? 0.26f : 0.92f;
            if (ship.Invulnerability > 0f)
                alpha *= GalaxyUiTheme.ReducedMotion ? 0.8f : 0.62f + 0.38f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 14f));

            Color primary = TextureFactory.Parse(ship.PrimaryHex, TextureFactory.Cyan);
            Color accent = TextureFactory.Parse(ship.AccentHex, TextureFactory.Lime);
            Sprite sprite = TailSprite(ship.TailStyle);
            bool energyTrail = SnakeAbilities.IsTrailActive(ship);
            if (energyTrail)
            {
                primary = TextureFactory.Cyan;
                accent = Color.white;
                sprite = TextureFactory.Glow();
            }
            if (AbilitySystem.IsActive(ship, AbilityKind.Phase)) accent = TextureFactory.Violet;

            for (int i = count - 1; i >= 0; i -= stride)
            {
                if (_segmentsDrawn >= MaxSnakeSegmentsDrawn) return;
                float radius = body.GetRadius(i, ship.Radius);
                Vec2 point = body.GetPoint(i);
                if (!Visible(point, radius, center, halfWidth, halfHeight, margin)) continue;

                float along = i / (float)Mathf.Max(1, count - 1);
                float wave = (ship.TailStyle == SnakeTailStyle.Pulse || energyTrail) && !GalaxyUiTheme.ReducedMotion
                    ? 0.76f + 0.24f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f + i * 0.32f))
                    : 1f;
                Color color = Color.Lerp(accent, primary, along);
                if (ship.TailStyle == SnakeTailStyle.Spectrum && !energyTrail)
                    color = Color.Lerp(color, TextureFactory.Violet, 0.35f * Mathf.Abs(Mathf.Sin(i * 0.4f)));
                color.a = alpha * (0.58f + 0.34f * along) * wave;
                float size = Mathf.Max(6f, radius * 2f) * wave
                    * (ship.TailStyle == SnakeTailStyle.Ribbon ? 1.2f : 1f);
                Spawn(point, size, color, sprite, 0f, 9);
                _segmentsDrawn++;
            }
        }

        private static Sprite TailSprite(SnakeTailStyle style)
        {
            switch (style)
            {
                case SnakeTailStyle.Spark: return TextureFactory.Star();
                case SnakeTailStyle.Ribbon: return TextureFactory.Circle();
                case SnakeTailStyle.Spectrum: return TextureFactory.Diamond();
                default: return TextureFactory.Glow();
            }
        }

        private void DrawShip(Ship ship)
        {
            if (ship.State == ShipState.Dead) return;
            float size = Mathf.Max(14f, ship.Radius * 2.2f);
            float rotation = ship.Facing.Angle() * Mathf.Rad2Deg;
            float alpha = ship.State == ShipState.Respawning ? 0.28f : 1f;
            if (ship.Invulnerability > 0f)
                alpha *= GalaxyUiTheme.ReducedMotion ? 0.8f : 0.62f + 0.38f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 14f));
            Color tint = new Color(1f, 1f, 1f, alpha);

            Sprite sprite = TextureFactory.Hull(ship.Hull, ship.PrimaryHex, ship.AccentHex);
            Spawn(ship.Position, size, tint, sprite, rotation, 12);

            if (ship.Shield > 0f && ship.MaxShield > 0f)
            {
                Color shieldColor = TextureFactory.Cyan;
                shieldColor.a = (0.18f + 0.32f * Mathf.Clamp01(ship.Shield / ship.MaxShield)) * alpha;
                Spawn(ship.Position, size * 1.48f, shieldColor, TextureFactory.Ring(), rotation, 11);
            }

            if (ship.Branch != EvolutionBranch.None && ship.EvolutionTier > 0)
            {
                Color aura = TextureFactory.BranchColor(ship.Branch);
                aura.a = (0.24f + 0.05f * ship.EvolutionTier) * alpha;
                float auraSize = size * (1.42f + 0.14f * ship.EvolutionTier);
                Spawn(ship.Position, auraSize, aura, TextureFactory.Ring(), rotation + Time.unscaledTime * 24f, 10);
            }

            if (ship.IsBoss)
            {
                Color bossAura = TextureFactory.Coral;
                bossAura.a = 0.42f * alpha;
                Spawn(ship.Position, size * 1.85f, bossAura, TextureFactory.Ring(), -Time.unscaledTime * 36f, 10);
            }

            if (ship.IsBot && ship.Profile != null)
            {
                Vec2 pipPosition = ship.Position + ship.Facing.Perpendicular * (size * 0.68f);
                Color pip = TextureFactory.PersonalityColor(ship.Personality);
                pip.a = alpha;
                Spawn(pipPosition, size * 0.18f, pip, TextureFactory.Circle(), 0f, 14);
            }

            if (!ship.IsPlayer && (ship.IsBoss || ship.Hp < ship.MaxHp))
            {
                float fraction = Mathf.Clamp01(ship.Hp / Mathf.Max(1f, ship.MaxHp));
                Vec2 barCenter = ship.Position + Vec2.Up * (size * 0.92f);
                float barWidth = size * 1.05f;
                Spawn(barCenter, new Vector2(barWidth, size * 0.11f), new Color(0f, 0f, 0f, 0.65f * alpha),
                    TextureFactory.Square(), 0f, 15);
                Vec2 fillCenter = new Vec2(barCenter.X - barWidth * (1f - fraction) * 0.5f, barCenter.Y);
                Color fillColor = fraction > 0.45f ? TextureFactory.Lime : TextureFactory.Coral;
                fillColor.a = alpha;
                Spawn(fillCenter, new Vector2(barWidth * fraction, size * 0.08f), fillColor, TextureFactory.Square(), 0f, 16);
            }
        }

        private void DrawProjectile(Projectile projectile)
        {
            Color color = TextureFactory.WeaponColor(projectile.Weapon);
            color.a = projectile.Friendly ? 0.95f : 0.88f;
            float size = Mathf.Max(8f, projectile.Radius * 3f);
            float angle = projectile.Velocity.Angle() * Mathf.Rad2Deg;
            Spawn(projectile.Position, size, color, TextureFactory.Glow(), angle, 13);

            Vec2 trailPosition = projectile.Position - projectile.Velocity.Normalized * (size * 0.5f);
            Color trail = color;
            trail.a = 0.26f;
            Spawn(trailPosition, size * 0.75f, trail, TextureFactory.Glow(), angle, 12);
        }

        private void StepParticles(float dt)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle particle = _particles[i];
                particle.Life -= dt;
                if (particle.Life <= 0f)
                {
                    _particles.RemoveAt(i);
                    continue;
                }
                particle.Position = particle.Position + particle.Velocity * dt;
                particle.Velocity = particle.Velocity * Mathf.Max(0f, 1f - 1.9f * dt);
                _particles[i] = particle;
            }
        }

        private void DrawParticles()
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                Particle particle = _particles[i];
                float t = Mathf.Clamp01(particle.Life / Mathf.Max(0.001f, particle.MaxLife));
                Color color = particle.Color;
                color.a *= t;
                float size = particle.Size * (0.4f + 0.6f * t);
                Spawn(particle.Position, size, color, TextureFactory.Glow(), 0f, 20);
            }
        }

        /// <summary>Each snake ability gets a distinct activation signature without new event types.</summary>
        private void SpawnAbilityEffect(SimEvent ev)
        {
            AbilityKind kind = (AbilityKind)ev.Value;
            switch (kind)
            {
                case AbilityKind.Dash:
                    SpawnDirectionalBurst(ev.Position, AbilityFacing(ev.ActorId), TextureFactory.Cyan,
                        14, 300f, 11f, 0.45f);
                    SpawnBurst(ev.Position, TextureFactory.Lime, 8, 150f, 9f, 0.4f, true);
                    break;
                case AbilityKind.Shield:
                    SpawnBurst(ev.Position, TextureFactory.Cyan, 24, 230f, 14f, 0.9f, true);
                    break;
                case AbilityKind.Magnet:
                    SpawnBurst(ev.Position, TextureFactory.Violet, 30, 360f, 16f, 1.1f, true);
                    SpawnBurst(ev.Position, TextureFactory.Lime, 8, 90f, 8f, 0.7f, false);
                    break;
                case AbilityKind.Overdrive:
                    SpawnBurst(ev.Position, TextureFactory.Sun, 18, 260f, 12f, 0.9f, true);
                    SpawnBurst(ev.Position, TextureFactory.Lime, 10, 180f, 9f, 0.6f, false);
                    break;
                case AbilityKind.Phase:
                    SpawnBurst(ev.Position, TextureFactory.Violet, 22, 320f, 12f, 0.7f, false);
                    SpawnBurst(ev.Position, TextureFactory.Pink, 8, 180f, 10f, 0.5f, true);
                    break;
                default:
                    SpawnBurst(ev.Position, TextureFactory.Cyan, 14, 190f, 10f, 0.6f, true);
                    break;
            }
        }

        private Vec2 AbilityFacing(int actorId)
        {
            MatchSimulation match = _app == null ? null : _app.Match;
            if (match != null && match.Ships != null)
            {
                for (int i = 0; i < match.Ships.Count; i++)
                {
                    Ship ship = match.Ships[i];
                    if (ship != null && ship.Id == actorId) return ship.Facing;
                }
            }
            return Vec2.Up;
        }

        private void SpawnDirectionalBurst(Vec2 position, Vec2 facing, Color color, int count,
            float speed, float size, float life)
        {
            int available = _particleLimit - _particles.Count;
            if (available <= 0) return;
            int amount = Mathf.Min(count, available);
            Vec2 dir = facing.SqrMagnitude > 0.0001f ? facing.Normalized : Vec2.Up;
            Vec2 side = dir.Perpendicular;
            for (int i = 0; i < amount; i++)
            {
                float along = speed * UnityEngine.Random.Range(0.7f, 1.3f);
                float across = speed * UnityEngine.Random.Range(-0.22f, 0.22f);
                Particle particle = new Particle();
                particle.Position = position;
                particle.Velocity = dir * along + side * across;
                particle.Life = life * UnityEngine.Random.Range(0.8f, 1.2f);
                particle.MaxLife = particle.Life;
                particle.Size = size * UnityEngine.Random.Range(0.7f, 1.3f);
                particle.Color = color;
                _particles.Add(particle);
            }
        }

        private void SpawnBurst(Vec2 position, Color color, int count, float speed, float size, float life, bool ring)
        {
            int available = _particleLimit - _particles.Count;
            if (available <= 0) return;
            int amount = Mathf.Min(count, available);
            for (int i = 0; i < amount; i++)
            {
                float angle = ring
                    ? i / (float)amount * Mathf.PI * 2f
                    : UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float velocity = speed * (ring ? 1f : UnityEngine.Random.Range(0.45f, 1.2f));
                float particleLife = life * (ring ? 1f : UnityEngine.Random.Range(0.7f, 1.3f));
                Particle particle = new Particle();
                particle.Position = position;
                particle.Velocity = Vec2.FromAngle(angle) * velocity;
                particle.Life = particleLife;
                particle.MaxLife = particleLife;
                particle.Size = size * UnityEngine.Random.Range(0.7f, 1.3f);
                particle.Color = color;
                _particles.Add(particle);
            }
        }

        private void BeginFrame()
        {
            _cursor = 0;
            _segmentsDrawn = 0;
        }

        private SpriteRenderer Spawn(Vec2 position, float size, Color color, Sprite sprite, float rotation, int order)
        {
            return Spawn(position, new Vector2(size, size), color, sprite, rotation, order);
        }

        private SpriteRenderer Spawn(Vec2 position, Vector2 size, Color color, Sprite sprite, float rotation, int order)
        {
            if (_poolRoot == null || _cursor >= PoolLimit) return null;
            SpriteRenderer renderer;
            if (_cursor < _pool.Count)
            {
                renderer = _pool[_cursor];
            }
            else
            {
                GameObject go = new GameObject("Спрайт");
                go.transform.SetParent(_poolRoot, false);
                renderer = go.AddComponent<SpriteRenderer>();
                _pool.Add(renderer);
            }
            _cursor++;
            renderer.gameObject.SetActive(true);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            Transform t = renderer.transform;
            t.localPosition = new Vector3(position.X, position.Y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, rotation);
            t.localScale = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);
            return renderer;
        }

        private void EndFrame()
        {
            for (int i = _cursor; i < _pool.Count; i++)
            {
                if (_pool[i] != null) _pool[i].gameObject.SetActive(false);
            }
        }

        private struct Particle
        {
            public Vec2 Position;
            public Vec2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
        }
    }
}
