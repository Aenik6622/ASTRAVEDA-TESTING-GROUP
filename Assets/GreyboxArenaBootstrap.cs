using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(-1000)]
public class GreyboxArenaBootstrap : MonoBehaviour
{
    private const string RuntimeRootName = "Greybox Arena Runtime";
    private const string RuntimeHudName = "Greybox Match HUD";

    private static GreyboxArenaBootstrap instance;

    [Header("Auto Build")]
    [SerializeField] private bool rebuildArenaOnStart = true;
    [SerializeField] private bool buildInEditMode = true;
    [SerializeField] private bool removeExistingSceneBots = true;

    [Header("Prefabs")]
    [SerializeField] private GameObject oldPlaneWallPrefab;
    [SerializeField] private GameObject botPrefab;

    [Header("Palette")]
    [SerializeField] private Color greyboxColor = new Color(0.18f, 0.22f, 0.28f, 1f);
    [SerializeField] private Color trimColorA = new Color(0.15f, 0.95f, 1f, 1f);
    [SerializeField] private Color trimColorB = new Color(1f, 0.2f, 0.85f, 1f);
    [SerializeField] private Color allyUiColor = new Color(1f, 0.84f, 0.22f, 1f);
    [SerializeField] private Color enemyUiColor = new Color(1f, 0.26f, 0.3f, 1f);

    private Transform runtimeRoot;
    private Text allyCountLabel;
    private Text enemyCountLabel;
    private Text statusLabel;
    private readonly List<BotBehaviour> spawnedBots = new List<BotBehaviour>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeBootstrapExists()
    {
        if (FindFirstObjectByType<GreyboxArenaBootstrap>() != null)
        {
            return;
        }

        GameObject bootstrapObject = new GameObject("Greybox Arena Bootstrap");
        bootstrapObject.AddComponent<GreyboxArenaBootstrap>();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            DestroyImmediateSafe(gameObject);
            return;
        }

        instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

#if UNITY_EDITOR
        if (!Application.isPlaying && buildInEditMode)
        {
            EditorApplication.delayCall -= DelayedEditBuild;
            EditorApplication.delayCall += DelayedEditBuild;
        }
#endif
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!rebuildArenaOnStart)
        {
            return;
        }

        BuildArena();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            UpdateHud();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        if (!buildInEditMode)
        {
            return;
        }

        EditorApplication.delayCall -= DelayedEditBuild;
        EditorApplication.delayCall += DelayedEditBuild;
    }

    private void DelayedEditBuild()
    {
        if (this == null || !buildInEditMode)
        {
            return;
        }

        BuildArena();
    }
#endif

    [ContextMenu("Build Greybox Arena")]
    public void BuildArena()
    {
        ResolveReferences();
        CleanupPreviousRuntime();

        runtimeRoot = new GameObject(RuntimeRootName).transform;
        runtimeRoot.SetParent(transform, false);
        BuildEnvironment(runtimeRoot);
        BuildLighting(runtimeRoot);
        BuildBots(runtimeRoot);
        PositionPlayer();

        if (Application.isPlaying)
        {
            BuildHud();
        }
    }

    private void ResolveReferences()
    {
#if UNITY_EDITOR
        if (oldPlaneWallPrefab == null)
        {
            oldPlaneWallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OldPlaneWall.prefab");
        }

        if (botPrefab == null)
        {
            botPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bot.prefab");
        }
#endif
    }

    private void CleanupPreviousRuntime()
    {
        spawnedBots.Clear();

        Transform oldRoot = transform.Find(RuntimeRootName);
        if (oldRoot != null)
        {
            DestroyImmediateSafe(oldRoot.gameObject);
        }

        GameObject oldHud = GameObject.Find(RuntimeHudName);
        if (oldHud != null)
        {
            DestroyImmediateSafe(oldHud);
        }

        if (!removeExistingSceneBots)
        {
            return;
        }

        BotBehaviour[] sceneBots = FindObjectsByType<BotBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneBots.Length; i++)
        {
            if (sceneBots[i] != null && sceneBots[i].transform.root != transform)
            {
                DestroyImmediateSafe(sceneBots[i].gameObject);
            }
        }
    }

    private void BuildEnvironment(Transform parent)
    {
        Quaternion floorRotation = Quaternion.Euler(90f, 0f, 0f);
        Quaternion sideWallRotation = Quaternion.Euler(0f, 90f, 0f);

        Transform shellRoot = new GameObject("Arena Shell").transform;
        shellRoot.SetParent(parent, false);

        CreatePanel(shellRoot, "Base Floor", new Vector3(0f, 0f, 0f), new Vector3(40f, 1f, 34f), floorRotation, greyboxColor);
        CreatePanel(shellRoot, "Back Wall", new Vector3(0f, 9f, -17f), new Vector3(40f, 18f, 1f), Quaternion.identity, greyboxColor);
        CreatePanel(shellRoot, "Left Wall", new Vector3(-20f, 9f, 0f), new Vector3(34f, 18f, 1f), sideWallRotation, greyboxColor);
        CreatePanel(shellRoot, "Right Wall", new Vector3(20f, 9f, 0f), new Vector3(34f, 18f, 1f), sideWallRotation, greyboxColor);
        CreatePanel(shellRoot, "Front Right Wall", new Vector3(14f, 5f, 17f), new Vector3(12f, 10f, 1f), Quaternion.identity, greyboxColor);
        CreatePanel(shellRoot, "Front Left Rail", new Vector3(-12f, 1.2f, 17f), new Vector3(16f, 2.4f, 1f), Quaternion.identity, greyboxColor);

        Transform platformsRoot = new GameObject("Platforms").transform;
        platformsRoot.SetParent(parent, false);

        CreatePanel(platformsRoot, "Upper West", new Vector3(-10f, 10f, -9f), new Vector3(18f, 1f, 10f), floorRotation, greyboxColor);
        CreatePanel(platformsRoot, "Upper East", new Vector3(10.5f, 10f, -8.5f), new Vector3(18f, 1f, 10f), floorRotation, greyboxColor);
        CreatePanel(platformsRoot, "Upper Bridge", new Vector3(0f, 10f, -5f), new Vector3(9f, 1f, 4f), floorRotation, greyboxColor);

        CreatePanel(platformsRoot, "Mid Left", new Vector3(-8f, 5.5f, 1f), new Vector3(18f, 1f, 12f), floorRotation, greyboxColor);
        CreatePanel(platformsRoot, "Mid Center", new Vector3(3.5f, 5.5f, 1f), new Vector3(12f, 1f, 9f), floorRotation, greyboxColor);
        CreatePanel(platformsRoot, "South Walkway", new Vector3(-1.5f, 2.6f, 10f), new Vector3(22f, 1f, 4f), floorRotation, greyboxColor);
        CreatePanel(platformsRoot, "South Balcony", new Vector3(10f, 2.6f, 9.5f), new Vector3(14f, 1f, 5f), floorRotation, greyboxColor);

        Transform stairsRoot = new GameObject("Stairs").transform;
        stairsRoot.SetParent(parent, false);
        BuildStepRun(stairsRoot, new Vector3(-1f, 0.6f, 13.5f), 6, new Vector3(2.8f, 0.6f, 1.2f), new Vector3(-1.1f, 1f, -1.1f), greyboxColor);
        BuildStepRun(stairsRoot, new Vector3(0.5f, 6.1f, -1.5f), 5, new Vector3(2.4f, 0.55f, 1.2f), new Vector3(0f, 1f, -1f), greyboxColor);

        Transform coverRoot = new GameObject("Cover").transform;
        coverRoot.SetParent(parent, false);
        CreatePanel(coverRoot, "Mid Crate", new Vector3(-2f, 6.35f, 0.2f), new Vector3(3.2f, 1.7f, 1f), Quaternion.identity, greyboxColor * 1.12f);
        CreatePanel(coverRoot, "South Left Cover", new Vector3(-12f, 1.2f, 8f), new Vector3(3f, 2.4f, 1f), sideWallRotation, greyboxColor * 1.08f);
        CreatePanel(coverRoot, "South Center Cover", new Vector3(1f, 1.1f, 7.8f), new Vector3(4f, 2.2f, 1f), Quaternion.identity, greyboxColor * 1.1f);
        CreatePanel(coverRoot, "Upper Rail West", new Vector3(-10f, 11.2f, -4.5f), new Vector3(12f, 1.4f, 1f), Quaternion.identity, greyboxColor * 1.15f);
        CreatePanel(coverRoot, "Upper Rail East", new Vector3(10f, 11.2f, -4.5f), new Vector3(12f, 1.4f, 1f), Quaternion.identity, greyboxColor * 1.15f);

        Transform trimRoot = new GameObject("Trim").transform;
        trimRoot.SetParent(parent, false);
        CreatePanel(trimRoot, "Trim A", new Vector3(-14f, 10.05f, -8f), new Vector3(8f, 0.12f, 1.2f), floorRotation, trimColorA);
        CreatePanel(trimRoot, "Trim B", new Vector3(11f, 10.05f, -9.2f), new Vector3(8f, 0.12f, 1.2f), floorRotation, trimColorB);
        CreatePanel(trimRoot, "Trim C", new Vector3(7.5f, 2.7f, 9.2f), new Vector3(7f, 0.12f, 1f), floorRotation, trimColorA);
        CreatePanel(trimRoot, "Trim D", new Vector3(-5.5f, 2.7f, 10.6f), new Vector3(8f, 0.12f, 1f), floorRotation, trimColorB);
        CreatePanel(trimRoot, "Arena Mark", new Vector3(9f, 0.08f, 8f), new Vector3(10f, 0.08f, 6f), floorRotation, new Color(0.2f, 0.9f, 0.95f, 0.75f));
    }

    private void BuildLighting(Transform parent)
    {
        CreatePointLight(parent, "Cyan Light", new Vector3(-10f, 12f, -8f), trimColorA, 6.2f, 18f);
        CreatePointLight(parent, "Pink Light", new Vector3(11f, 12f, -7f), trimColorB, 6.2f, 18f);
        CreatePointLight(parent, "Floor Light", new Vector3(7f, 3.5f, 9f), trimColorA, 5f, 15f);
        CreatePointLight(parent, "Mid Light", new Vector3(-3f, 7.5f, 0f), trimColorB, 4.5f, 12f);

        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
            {
                lights[i].color = new Color(0.56f, 0.66f, 0.88f, 1f);
                lights[i].intensity = 0.55f;
                lights[i].transform.rotation = Quaternion.Euler(40f, -35f, 0f);
                break;
            }
        }
    }

    private void BuildBots(Transform parent)
    {
        if (botPrefab == null)
        {
            Debug.LogWarning("GreyboxArenaBootstrap could not find Bot.prefab. Bots were not spawned.");
            return;
        }

        Transform botRoot = new GameObject("Bots").transform;
        botRoot.SetParent(parent, false);

        SpawnBot(botRoot, "Ally Upper 01", new Vector3(-15f, 11f, -8f), BotBehaviour.BotTeam.Ally, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Ally Upper 01 Patrol", new Vector3(-16f, 11f, -11f), new Vector3(-7f, 11f, -9f)));
        SpawnBot(botRoot, "Ally Upper 02", new Vector3(-18f, 11f, -2f), BotBehaviour.BotTeam.Ally, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Ally Upper 02 Patrol", new Vector3(-18f, 11f, -4f), new Vector3(-11f, 11f, -3f)));
        SpawnBot(botRoot, "Ally Mid 01", new Vector3(-10f, 6.5f, 2f), BotBehaviour.BotTeam.Ally, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Ally Mid 01 Patrol", new Vector3(-14f, 6.5f, 3f), new Vector3(-4f, 6.5f, 1f)));
        SpawnBot(botRoot, "Ally Mid 02", new Vector3(-6f, 6.5f, 5.5f), BotBehaviour.BotTeam.Ally, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Ally Mid 02 Patrol", new Vector3(-9f, 6.5f, 5.5f), new Vector3(-2f, 6.5f, 5.5f)));
        SpawnBot(botRoot, "Ally South 01", new Vector3(-14f, 3.5f, 11f), BotBehaviour.BotTeam.Ally, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Ally South 01 Patrol", new Vector3(-15f, 3.5f, 12f), new Vector3(-8f, 3.5f, 10f)));
        SpawnBot(botRoot, "Ally South 02", new Vector3(-4f, 3.5f, 10f), BotBehaviour.BotTeam.Ally, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Ally South 02 Patrol", new Vector3(-6f, 3.5f, 10f), new Vector3(2f, 3.5f, 10f)));
        SpawnBot(botRoot, "Ally Floor 01", new Vector3(-10f, 1f, 16f), BotBehaviour.BotTeam.Ally, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Ally Floor 01 Patrol", new Vector3(-12f, 1f, 14f), new Vector3(-6f, 1f, 16f)));
        SpawnBot(botRoot, "Ally Floor 02", new Vector3(2f, 1f, 15f), BotBehaviour.BotTeam.Ally, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Ally Floor 02 Patrol", new Vector3(-1f, 1f, 14f), new Vector3(7f, 1f, 16f)));

        SpawnBot(botRoot, "Enemy Upper 01", new Vector3(14f, 11f, -8f), BotBehaviour.BotTeam.Enemy, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Enemy Upper 01 Patrol", new Vector3(8f, 11f, -8f), new Vector3(16f, 11f, -6f)));
        SpawnBot(botRoot, "Enemy Upper 02", new Vector3(18f, 11f, -3f), BotBehaviour.BotTeam.Enemy, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Enemy Upper 02 Patrol", new Vector3(11f, 11f, -3f), new Vector3(18f, 11f, -1f)));
        SpawnBot(botRoot, "Enemy Mid 01", new Vector3(6f, 6.5f, 1f), BotBehaviour.BotTeam.Enemy, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Enemy Mid 01 Patrol", new Vector3(2f, 6.5f, 0f), new Vector3(8f, 6.5f, 3f)));
        SpawnBot(botRoot, "Enemy South 01", new Vector3(10f, 3.5f, 10f), BotBehaviour.BotTeam.Enemy, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Enemy South 01 Patrol", new Vector3(7f, 3.5f, 9.5f), new Vector3(14f, 3.5f, 11f)));
        SpawnBot(botRoot, "Enemy Floor 01", new Vector3(13f, 1f, 15f), BotBehaviour.BotTeam.Enemy, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Enemy Floor 01 Patrol", new Vector3(10f, 1f, 13f), new Vector3(16f, 1f, 15f)));
        SpawnBot(botRoot, "Enemy Floor 02", new Vector3(18f, 1f, 4f), BotBehaviour.BotTeam.Enemy, BotBehaviour.BotRole.Standard, CreatePatrol(botRoot, "Enemy Floor 02 Patrol", new Vector3(16f, 1f, 1f), new Vector3(18f, 1f, 9f)));
        SpawnBot(botRoot, "Enemy Tank", new Vector3(18f, 1f, 18f), BotBehaviour.BotTeam.Enemy, BotBehaviour.BotRole.Tank, CreatePatrol(botRoot, "Enemy Tank Patrol", new Vector3(14f, 1f, 16f), new Vector3(18f, 1f, 18f)));
    }

    private void PositionPlayer()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            BaseCharacter character = FindFirstObjectByType<BaseCharacter>();
            if (character != null)
            {
                player = character.gameObject;
            }
        }

        if (player == null)
        {
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        player.transform.position = new Vector3(-11f, 11.4f, -12.5f);
        player.transform.rotation = Quaternion.Euler(0f, 34f, 0f);

        Camera playerCamera = player.GetComponentInChildren<Camera>(true);
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        if (controller != null)
        {
            controller.enabled = true;
        }

        if (Application.isPlaying)
        {
            AbilityHudOverlay.EnsureFor(player);
        }
    }

    private void BuildHud()
    {
        GameObject canvasObject = new GameObject(RuntimeHudName);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        CreateHudPanel(canvasObject.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(420f, 120f), new Color(0.05f, 0.08f, 0.12f, 0.84f));
        statusLabel = CreateHudText(canvasObject.transform, font, "NEON GREYBOX ENCOUNTER", 24, TextAnchor.UpperCenter, Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(380f, 32f));
        allyCountLabel = CreateHudText(canvasObject.transform, font, "ALLY 0", 22, TextAnchor.MiddleLeft, allyUiColor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -76f), new Vector2(150f, 28f));
        enemyCountLabel = CreateHudText(canvasObject.transform, font, "ENEMY 0", 22, TextAnchor.MiddleRight, enemyUiColor, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(18f, -76f), new Vector2(150f, 28f));
        CreateHudText(canvasObject.transform, font, "YELLOW BOTS = ALLY | RED BOTS = ENEMY", 18, TextAnchor.UpperCenter, new Color(0.8f, 0.9f, 1f, 0.95f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(520f, 24f));

        UpdateHud();
    }

    private void UpdateHud()
    {
        if (allyCountLabel == null || enemyCountLabel == null)
        {
            return;
        }

        int allyAlive = CountLivingBots(BotBehaviour.BotTeam.Ally);
        int enemyAlive = CountLivingBots(BotBehaviour.BotTeam.Enemy);

        allyCountLabel.text = "ALLY " + allyAlive;
        enemyCountLabel.text = "ENEMY " + enemyAlive;

        if (statusLabel == null)
        {
            return;
        }

        if (allyAlive > 0 && enemyAlive > 0)
        {
            statusLabel.text = "NEON GREYBOX ENCOUNTER";
        }
        else if (allyAlive <= 0 && enemyAlive <= 0)
        {
            statusLabel.text = "DRAW";
        }
        else if (allyAlive > 0)
        {
            statusLabel.text = "ALLY TEAM ADVANTAGE";
        }
        else
        {
            statusLabel.text = "ENEMY TEAM ADVANTAGE";
        }
    }

    private int CountLivingBots(BotBehaviour.BotTeam team)
    {
        spawnedBots.RemoveAll(bot => bot == null);
        return spawnedBots.Count(bot => bot != null && bot.Team == team && !bot.IsDead && bot.gameObject.activeInHierarchy);
    }

    private BotBehaviour SpawnBot(Transform parent, string botName, Vector3 position, BotBehaviour.BotTeam team, BotBehaviour.BotRole role, Transform[] patrol)
    {
        GameObject botObject = InstantiatePrefabOrClone(botPrefab, position, Quaternion.identity, parent);
        botObject.name = botName;
        BotBehaviour behaviour = botObject.GetComponent<BotBehaviour>();
        if (behaviour != null)
        {
            behaviour.ConfigureRuntime(team, role, patrol, null, true);
            spawnedBots.Add(behaviour);
        }

        BotHealthBar healthBar = botObject.GetComponent<BotHealthBar>();
        if (Application.isPlaying && healthBar == null)
        {
            BotHealthBar.EnsureFor(behaviour);
        }

        return behaviour;
    }

    private Transform[] CreatePatrol(Transform parent, string name, params Vector3[] points)
    {
        Transform patrolRoot = new GameObject(name).transform;
        patrolRoot.SetParent(parent, false);

        List<Transform> transforms = new List<Transform>();
        for (int i = 0; i < points.Length; i++)
        {
            GameObject waypoint = new GameObject(name + " Point " + (i + 1));
            waypoint.transform.SetParent(patrolRoot, false);
            waypoint.transform.position = points[i];
            transforms.Add(waypoint.transform);
        }

        return transforms.ToArray();
    }

    private void BuildStepRun(Transform parent, Vector3 start, int steps, Vector3 stepSize, Vector3 offsetPerStep, Color color)
    {
        for (int i = 0; i < steps; i++)
        {
            CreatePanel(parent, "Step " + i, start + (offsetPerStep * i), stepSize, Quaternion.Euler(90f, 0f, 0f), color);
        }
    }

    private void CreatePointLight(Transform parent, string lightName, Vector3 position, Color color, float intensity, float range)
    {
        GameObject lightObject = new GameObject(lightName);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = position;

        Light lightComponent = lightObject.AddComponent<Light>();
        lightComponent.type = LightType.Point;
        lightComponent.color = color;
        lightComponent.intensity = intensity;
        lightComponent.range = range;
    }

    private GameObject CreatePanel(Transform parent, string panelName, Vector3 position, Vector3 size, Quaternion rotation, Color color)
    {
        GameObject wrapper = new GameObject(panelName);
        wrapper.transform.SetParent(parent, false);
        wrapper.transform.position = position;
        wrapper.transform.rotation = rotation;

        GameObject panelObject;
        if (oldPlaneWallPrefab != null)
        {
            panelObject = InstantiatePrefabOrClone(oldPlaneWallPrefab, wrapper.transform.position, wrapper.transform.rotation, wrapper.transform);
            panelObject.name = panelName + " Mesh";
            NormalizePrefabPanel(panelObject, wrapper.transform.position, size);
        }
        else
        {
            panelObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelObject.transform.SetParent(wrapper.transform, false);
            panelObject.transform.localScale = size;
        }

        ApplyColor(panelObject, color);
        SetLayerRecursively(panelObject, 0);
        return wrapper;
    }

    private void NormalizePrefabPanel(GameObject panelObject, Vector3 desiredCenter, Vector3 desiredSize)
    {
        Renderer[] renderers = panelObject.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            panelObject.transform.localScale = desiredSize;
            panelObject.transform.position = desiredCenter;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 currentSize = bounds.size;
        currentSize.x = Mathf.Max(0.01f, currentSize.x);
        currentSize.y = Mathf.Max(0.01f, currentSize.y);
        currentSize.z = Mathf.Max(0.01f, currentSize.z);

        Vector3 localScale = panelObject.transform.localScale;
        localScale = new Vector3(
            localScale.x * (desiredSize.x / currentSize.x),
            localScale.y * (desiredSize.y / currentSize.y),
            localScale.z * (desiredSize.z / currentSize.z));
        panelObject.transform.localScale = localScale;

        renderers = panelObject.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        panelObject.transform.position += desiredCenter - bounds.center;
    }

    private void ApplyColor(GameObject target, Color color)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers == null)
        {
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderers[i].SetPropertyBlock(block);
        }
    }

    private GameObject InstantiatePrefabOrClone(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if (prefab == null)
        {
            return null;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object createdObject = PrefabUtility.InstantiatePrefab(prefab);
            GameObject instanceObject = createdObject as GameObject;
            if (instanceObject == null && createdObject is Component createdComponent)
            {
                instanceObject = createdComponent.gameObject;
            }

            if (instanceObject != null)
            {
                if (parent != null)
                {
                    instanceObject.transform.SetParent(parent, false);
                }

                instanceObject.transform.position = position;
                instanceObject.transform.rotation = rotation;
                return instanceObject;
            }

            Debug.LogWarning("GreyboxArenaBootstrap could not create prefab instance for " + prefab.name + ". Falling back to normal Instantiate.");
        }
#endif

        return Instantiate(prefab, position, rotation, parent);
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }

    private static Image CreateHudPanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.AddComponent<Image>();
        image.color = color;

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return image;
    }

    private static Text CreateHudText(Transform parent, Font font, string text, int fontSize, TextAnchor alignment, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(text);
        textObject.transform.SetParent(parent, false);
        Text textComponent = textObject.AddComponent<Text>();
        textComponent.font = font;
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        textComponent.supportRichText = true;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return textComponent;
    }

    private static void DestroyImmediateSafe(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}


