using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单场地生存原型：WASD 移动、自动射击，空格冲刺并反弹敌方弹丸。
/// 所有模型使用 Unity 内置基础几何体，不依赖外部美术包。
/// </summary>
public sealed class GreenhouseGame : MonoBehaviour
{
    // Tune these values on the Greenhouse Game component in the Inspector.
    [SerializeField, Min(0.1f)] private float playerSpeed = 5.5f;
    [SerializeField, Min(0.1f)] private float enemySpeed = 2.8f;
    [SerializeField, Min(0.1f)] private float spawnInterval = 1.15f;
    [SerializeField, Min(1)] private int maxEnemies = 18;

    // References to scene objects let Unity display and edit them before Play mode.
    [SerializeField] private Camera gameCamera;
    [SerializeField] private GreenhousePlayer player;

    private readonly List<GreenhouseEnemy> enemies = new List<GreenhouseEnemy>();
    private readonly List<GreenhouseProjectile> projectiles = new List<GreenhouseProjectile>();
    private Material floorMaterial;
    private Material playerMaterial;
    private Material meleeMaterial;
    private Material shooterMaterial;
    private Material playerShotMaterial;
    private Material hostileShotMaterial;
    private Material returnedShotMaterial;
    private float spawnTimer;
    private bool gameOver;
    private Vector2 arenaHalfExtents;

    private void Awake()
    {
        if (!CreateMaterials()) return;
        CreateArena();
        CreatePlayer();
        RestartRun();
        RegisterSceneEnemies();
    }

    private bool CreateMaterials()
    {
        // This Resources material explicitly references the game's unlit shader, so WebGL keeps it.
        Material template = Resources.Load<Material>("GreenhouseMaterial");
        if (template == null)
        {
            Debug.LogError("GreenhouseMaterial is missing from Resources.");
            return false;
        }

        floorMaterial = MakeMaterial(template, new Color(0.07f, 0.16f, 0.13f));
        playerMaterial = MakeMaterial(template, new Color(0.28f, 0.95f, 0.48f));
        meleeMaterial = MakeMaterial(template, new Color(0.95f, 0.18f, 0.32f));
        shooterMaterial = MakeMaterial(template, new Color(1f, 0.60f, 0.16f));
        playerShotMaterial = MakeMaterial(template, new Color(1f, 0.88f, 0.32f));
        hostileShotMaterial = MakeMaterial(template, new Color(1f, 0.16f, 0.72f));
        returnedShotMaterial = MakeMaterial(template, new Color(0.18f, 0.95f, 1f));
        return true;
    }

    private static Material MakeMaterial(Material template, Color color)
    {
        Material material = new Material(template);
        material.color = color;
        return material;
    }

    private void CreateArena()
    {
        bool createdCamera = false;
        if (gameCamera == null) gameCamera = Camera.main;
        if (gameCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            gameCamera = cameraObject.AddComponent<Camera>();
            createdCamera = true;
        }

        // Set defaults only for a fallback camera; scene-authored camera settings stay editable.
        if (createdCamera)
        {
            gameCamera.orthographic = true;
            gameCamera.orthographicSize = 9.2f;
            gameCamera.nearClipPlane = 0.1f;
            gameCamera.farClipPlane = 100f;
            gameCamera.backgroundColor = new Color(0.025f, 0.05f, 0.045f);
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.transform.SetPositionAndRotation(new Vector3(0f, 35f, 0f), Quaternion.Euler(90f, 0f, 0f));
        }

        GameObject floor = GameObject.Find("Arena Floor");
        bool createdFloor = floor == null;
        if (createdFloor) floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Arena Floor";
        if (createdFloor) floor.transform.localScale = new Vector3(2.8f, 1f, 1.6f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
        Destroy(floor.GetComponent<Collider>());
        arenaHalfExtents = new Vector2(floor.transform.lossyScale.x * 5f, floor.transform.lossyScale.z * 5f);

        RenderSettings.ambientLight = new Color(0.68f, 0.76f, 0.70f);
        if (FindAnyObjectByType<Light>() == null)
        {
            GameObject lightObject = new GameObject("Arena Light");
            Light sunlight = lightObject.AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
        }
    }

    private void CreatePlayer()
    {
        GameObject body = GameObject.Find("Player");
        if (body == null) body = CreateSphere("Player", new Vector3(0f, 0.45f, 0f), 0.8f, playerMaterial);
        body.GetComponent<MeshRenderer>().sharedMaterial = playerMaterial;
        if (player == null) player = body.GetComponent<GreenhousePlayer>();
        if (player == null) player = body.AddComponent<GreenhousePlayer>();
        player.Initialize(this);
    }

    private void RegisterSceneEnemies()
    {
        // Scene examples are also the first enemies in the play session.
        foreach (GreenhouseEnemy enemy in FindObjectsByType<GreenhouseEnemy>(FindObjectsSortMode.None))
        {
            enemy.Initialize(this, enemy.IsShooter);
            enemies.Add(enemy);
        }
    }

    private GameObject CreateSphere(string objectName, Vector3 position, float size, Material material)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        item.name = objectName;
        item.transform.position = position;
        item.transform.localScale = Vector3.one * size;
        item.GetComponent<MeshRenderer>().sharedMaterial = material;
        Destroy(item.GetComponent<Collider>());
        return item;
    }

    private void Update()
    {
        if (gameOver)
        {
            // R 重新开始，避免为原型增加游戏结束界面。
            if (Input.GetKeyDown(KeyCode.R)) RestartRun();
            return;
        }

        enemies.RemoveAll(item => item == null);
        projectiles.RemoveAll(item => item == null);
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f && enemies.Count < maxEnemies)
        {
            SpawnEnemy();
            spawnTimer = spawnInterval;
        }
    }

    private void SpawnEnemy()
    {
        bool shooter = Random.value < 0.38f;
        float halfWidth = arenaHalfExtents.x - 0.6f;
        float halfDepth = arenaHalfExtents.y - 0.6f;
        int edge = Random.Range(0, 4);
        Vector2 position = edge < 2
            ? new Vector2(edge == 0 ? -halfWidth : halfWidth, Random.Range(-halfDepth, halfDepth))
            : new Vector2(Random.Range(-halfWidth, halfWidth), edge == 2 ? -halfDepth : halfDepth);

        float size = shooter ? 0.58f : 0.82f;
        Material material = shooter ? shooterMaterial : meleeMaterial;
        GameObject body = CreateSphere(shooter ? "Shooting Spore" : "Chasing Spore", new Vector3(position.x, size * 0.5f, position.y), size, material);
        GreenhouseEnemy enemy = body.AddComponent<GreenhouseEnemy>();
        enemy.Initialize(this, shooter);
        enemies.Add(enemy);
    }

    public void FirePlayerShot(Vector2 position, Vector2 direction)
    {
        CreateProjectile("Seed Shot", position, direction, false);
    }

    public void FireEnemyShot(Vector2 position, Vector2 direction)
    {
        CreateProjectile("Pink Spore Shot", position, direction, true);
    }

    private void CreateProjectile(string objectName, Vector2 position, Vector2 direction, bool hostile)
    {
        Material material = hostile ? hostileShotMaterial : playerShotMaterial;
        GameObject shot = CreateSphere(objectName, new Vector3(position.x, 0.28f, position.y), hostile ? 0.28f : 0.20f, material);
        GreenhouseProjectile projectile = shot.AddComponent<GreenhouseProjectile>();
        projectile.Initialize(this, direction, hostile);
        projectiles.Add(projectile);
    }

    public GreenhouseEnemy FindNearestEnemy(Vector2 position)
    {
        GreenhouseEnemy nearest = null;
        float closest = float.MaxValue;
        foreach (GreenhouseEnemy enemy in enemies)
        {
            if (enemy == null) continue;
            float distance = (Planar(enemy.transform.position) - position).sqrMagnitude;
            if (distance < closest) { closest = distance; nearest = enemy; }
        }
        return nearest;
    }

    public bool HitEnemyAt(Vector2 position)
    {
        foreach (GreenhouseEnemy enemy in enemies)
        {
            if (enemy == null) continue;
            float radius = enemy.HitRadius;
            if ((Planar(enemy.transform.position) - position).sqrMagnitude <= radius * radius)
            {
                enemy.TakeHit();
                return true;
            }
        }
        return false;
    }

    public void RewardDeflection()
    {
        // 成功反弹会返还一部分冲刺冷却，让玩家愿意主动迎向弹丸。
        if (player != null) player.RefundDashCooldown(0.45f);
    }

    public void EndRun() { gameOver = true; }
    public bool IsGameOver { get { return gameOver; } }
    public GreenhousePlayer Player { get { return player; } }
    public float PlayerSpeed { get { return playerSpeed; } }
    public float EnemySpeed { get { return enemySpeed; } }
    public Vector2 ArenaHalfExtents { get { return arenaHalfExtents; } }
    public Material ReturnedShotMaterial { get { return returnedShotMaterial; } }
    public static Vector2 Planar(Vector3 position) { return new Vector2(position.x, position.z); }

    private void RestartRun()
    {
        foreach (GreenhouseEnemy enemy in enemies) if (enemy != null) Destroy(enemy.gameObject);
        foreach (GreenhouseProjectile shot in projectiles) if (shot != null) Destroy(shot.gameObject);
        enemies.Clear();
        projectiles.Clear();
        if (player != null) player.ResetPlayer();
        gameOver = false;
        spawnTimer = 0.8f;
    }
}

public sealed class GreenhousePlayer : MonoBehaviour
{
    private GreenhouseGame game;
    private Material bodyMaterial;
    private Vector3 spawnPosition;
    private Vector2 lastDirection = Vector2.up;
    private Vector2 dashDirection = Vector2.up;
    private float dashTimer;
    private float dashCooldown;
    private float fireTimer;
    private float invulnerabilityTimer;
    private float cyanFlashTimer;
    private int health = 5;

    public bool IsDashing { get { return dashTimer > 0f; } }

    public void Initialize(GreenhouseGame owner)
    {
        game = owner;
        spawnPosition = transform.position;
        bodyMaterial = GetComponent<MeshRenderer>().material;
    }

    public void ResetPlayer()
    {
        health = 5;
        dashTimer = 0f;
        dashCooldown = 0f;
        fireTimer = 0f;
        invulnerabilityTimer = 0f;
        cyanFlashTimer = 0f;
        lastDirection = Vector2.up;
        dashDirection = Vector2.up;
        transform.position = spawnPosition;
        bodyMaterial.color = new Color(0.28f, 0.95f, 0.48f);
    }

    private void Update()
    {
        if (game == null || game.IsGameOver) return;
        float dt = Time.deltaTime;
        dashTimer = Mathf.Max(0f, dashTimer - dt);
        dashCooldown = Mathf.Max(0f, dashCooldown - dt);
        invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - dt);
        cyanFlashTimer = Mathf.Max(0f, cyanFlashTimer - dt);
        fireTimer -= dt;

        Vector2 move = Vector2.zero;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
        if (move.sqrMagnitude > 1f) move.Normalize();
        if (move.sqrMagnitude > 0.01f) lastDirection = move;

        if (Input.GetKeyDown(KeyCode.Space) && dashCooldown <= 0f)
        {
            dashDirection = move.sqrMagnitude > 0.01f ? move : lastDirection;
            dashTimer = 0.20f;
            dashCooldown = 1.35f;
        }

        Vector2 direction = IsDashing ? dashDirection * 2.6f : move;
        Vector2 next = GreenhouseGame.Planar(transform.position) + direction * game.PlayerSpeed * dt;
        Vector2 halfExtents = game.ArenaHalfExtents - Vector2.one * 0.75f;
        next.x = Mathf.Clamp(next.x, -halfExtents.x, halfExtents.x);
        next.y = Mathf.Clamp(next.y, -halfExtents.y, halfExtents.y);
        transform.position = new Vector3(next.x, spawnPosition.y, next.y);
        transform.localScale = Vector3.one * (IsDashing ? 0.95f : 0.8f);
        bodyMaterial.color = cyanFlashTimer > 0f ? new Color(0.18f, 0.95f, 1f)
            : invulnerabilityTimer > 0f ? new Color(1f, 0.18f, 0.20f)
            : new Color(0.28f, 0.95f, 0.48f);

        if (fireTimer <= 0f)
        {
            GreenhouseEnemy target = game.FindNearestEnemy(GreenhouseGame.Planar(transform.position));
            if (target != null)
            {
                Vector2 aim = (GreenhouseGame.Planar(target.transform.position) - GreenhouseGame.Planar(transform.position)).normalized;
                game.FirePlayerShot(GreenhouseGame.Planar(transform.position) + aim * 0.5f, aim);
                fireTimer = 0.42f;
            }
        }
    }

    public void TakeHit()
    {
        if (IsDashing || invulnerabilityTimer > 0f || game.IsGameOver) return;
        health--;
        invulnerabilityTimer = 0.65f;
        cyanFlashTimer = 0f;
        bodyMaterial.color = new Color(1f, 0.18f, 0.20f);
        if (health <= 0) game.EndRun();
    }

    public void RefundDashCooldown(float amount)
    {
        dashCooldown = Mathf.Max(0f, dashCooldown - amount);
        cyanFlashTimer = 0.24f;
    }
}

public sealed class GreenhouseEnemy : MonoBehaviour
{
    private GreenhouseGame game;
    [SerializeField] private bool shooter;
    private int health;
    private float shotTimer = 1.1f;
    public float HitRadius { get { return shooter ? 0.40f : 0.52f; } }
    public bool IsShooter { get { return shooter; } }

    public void Initialize(GreenhouseGame owner, bool isShooter)
    {
        game = owner;
        shooter = isShooter;
        health = shooter ? 1 : 2;
    }

    private void Update()
    {
        if (game == null || game.IsGameOver || game.Player == null) return;
        Vector2 here = GreenhouseGame.Planar(transform.position);
        Vector2 toPlayer = GreenhouseGame.Planar(game.Player.transform.position) - here;
        float distance = toPlayer.magnitude;
        Vector2 towardPlayer = distance > 0.01f ? toPlayer / distance : Vector2.zero;
        float dt = Time.deltaTime;

        if (shooter)
        {
            if (distance > 5.5f) here += towardPlayer * 1.3f * dt;
            else if (distance < 3.6f) here -= towardPlayer * 1.1f * dt;
            shotTimer -= dt;
            if (shotTimer <= 0f)
            {
                game.FireEnemyShot(here, towardPlayer);
                shotTimer = Random.Range(1.35f, 1.8f);
            }
        }
        else
        {
            here += towardPlayer * game.EnemySpeed * dt;
            if (distance < 0.68f) game.Player.TakeHit();
        }

        Vector2 halfExtents = game.ArenaHalfExtents - Vector2.one * 0.4f;
        here.x = Mathf.Clamp(here.x, -halfExtents.x, halfExtents.x);
        here.y = Mathf.Clamp(here.y, -halfExtents.y, halfExtents.y);
        transform.position = new Vector3(here.x, transform.localScale.y * 0.5f, here.y);
    }

    public void TakeHit()
    {
        health--;
        if (health <= 0) Destroy(gameObject);
    }
}

public sealed class GreenhouseProjectile : MonoBehaviour
{
    private GreenhouseGame game;
    private Vector2 direction;
    private bool hostile;
    private float speed;
    private float lifetime = 6f;

    public void Initialize(GreenhouseGame owner, Vector2 travelDirection, bool enemyShot)
    {
        game = owner;
        direction = travelDirection.normalized;
        hostile = enemyShot;
        speed = hostile ? 4.5f : 8f;
    }

    private void Update()
    {
        if (game == null || game.IsGameOver) return;
        float dt = Time.deltaTime;
        lifetime -= dt;
        if (lifetime <= 0f) { Destroy(gameObject); return; }
        transform.position += new Vector3(direction.x, 0f, direction.y) * speed * dt;
        Vector2 here = GreenhouseGame.Planar(transform.position);
        if (Mathf.Abs(here.x) > 15f || Mathf.Abs(here.y) > 9f) { Destroy(gameObject); return; }

        if (hostile && game.Player != null)
        {
            float distance = Vector2.Distance(here, GreenhouseGame.Planar(game.Player.transform.position));
            if (game.Player.IsDashing && distance < 0.82f) Reflect();
            else if (distance < 0.40f) { game.Player.TakeHit(); Destroy(gameObject); }
        }
        else if (!hostile && game.HitEnemyAt(here)) Destroy(gameObject);
    }

    private void Reflect()
    {
        GreenhouseEnemy target = game.FindNearestEnemy(GreenhouseGame.Planar(transform.position));
        if (target == null) { Destroy(gameObject); return; }
        direction = (GreenhouseGame.Planar(target.transform.position) - GreenhouseGame.Planar(transform.position)).normalized;
        hostile = false;
        speed = 10f;
        lifetime = 3.5f;
        transform.localScale = Vector3.one * 0.38f;
        GetComponent<MeshRenderer>().sharedMaterial = game.ReturnedShotMaterial;
        game.RewardDeflection();
    }
}
