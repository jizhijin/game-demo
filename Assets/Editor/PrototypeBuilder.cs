using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GreenhousePrototypeBuilder
{
    public static void CreateSceneAndBuild()
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        string scenePath = "Assets/Scenes/Greenhouse.unity";
        string outputPath = Path.Combine(project, "Build", "WebGLPlayer");
        Directory.CreateDirectory(Path.Combine(project, "Assets", "Scenes"));
        Directory.CreateDirectory(Path.Combine(project, "Assets", "Shaders"));
        Directory.CreateDirectory(Path.Combine(project, "Assets", "Resources"));
        Directory.CreateDirectory(Path.Combine(project, "Assets", "Materials"));
        Directory.CreateDirectory(outputPath);
        AssetDatabase.Refresh();
        if (!EnsureGameMaterial())
        {
            EditorApplication.Exit(1);
            return;
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildEditableScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        PlayerSettings.companyName = "Student Prototype";
        PlayerSettings.productName = "Greenhouse Alarm Prototype";
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            string stylePath = Path.Combine(outputPath, "TemplateData", "style.css");
            string style = File.ReadAllText(stylePath);
            const string marker = "/* Compact prototype player. */";
            int markerIndex = style.IndexOf(marker);
            if (markerIndex >= 0) style = style.Substring(0, markerIndex);
            style += "\n" + marker + "\n" +
                "#unity-container.unity-desktop { width: min(100vw, calc(100vh * 16 / 9)); }\n" +
                "#unity-fullscreen-container { width: 100% !important; height: auto !important; aspect-ratio: 16 / 9; }\n" +
                "#unity-footer { display: none !important; }\n";
            File.WriteAllText(stylePath, style);
        }
        Debug.Log("Greenhouse WebGL build result: " + report.summary.result + "; output: " + outputPath);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static bool EnsureGameMaterial()
    {
        const string shaderPath = "Assets/Shaders/GreenhouseUnlit.shader";
        const string materialPath = "Assets/Resources/GreenhouseMaterial.mat";
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null)
        {
            Debug.LogError("Could not load " + shaderPath);
            return false;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = shader;
        }

        AssetDatabase.SaveAssets();
        return true;
    }

    private static void BuildEditableScene()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/GreenhouseUnlit.shader");
        Material floorMaterial = GetPreviewMaterial("Assets/Materials/ArenaFloor.mat", shader, new Color(0.07f, 0.16f, 0.13f));
        Material playerMaterial = GetPreviewMaterial("Assets/Materials/Player.mat", shader, new Color(0.28f, 0.95f, 0.48f));
        Material meleeMaterial = GetPreviewMaterial("Assets/Materials/MeleeEnemy.mat", shader, new Color(0.95f, 0.18f, 0.32f));
        Material shooterMaterial = GetPreviewMaterial("Assets/Materials/ShooterEnemy.mat", shader, new Color(1f, 0.60f, 0.16f));

        // Arena, camera, player, and enemy examples are real scene objects now.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Arena Floor";
        floor.transform.localScale = new Vector3(2.8f, 1f, 1.6f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
        Object.DestroyImmediate(floor.GetComponent<Collider>());

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 9.2f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.backgroundColor = new Color(0.025f, 0.05f, 0.045f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 35f, 0f), Quaternion.Euler(90f, 0f, 0f));

        GameObject lightObject = new GameObject("Arena Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -25f, 0f);

        GameObject playerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        playerObject.name = "Player";
        playerObject.transform.position = new Vector3(0f, 0.45f, 0f);
        playerObject.transform.localScale = Vector3.one * 0.8f;
        playerObject.GetComponent<MeshRenderer>().sharedMaterial = playerMaterial;
        Object.DestroyImmediate(playerObject.GetComponent<Collider>());
        GreenhousePlayer player = playerObject.AddComponent<GreenhousePlayer>();

        CreateEnemyPreview("Melee Enemy (2 HP)", new Vector3(-3f, 0.41f, 2.5f), 0.82f, meleeMaterial, false);
        CreateEnemyPreview("Shooter Enemy (1 HP)", new Vector3(3f, 0.29f, -2.5f), 0.58f, shooterMaterial, true);

        GameObject gameObject = new GameObject("Greenhouse Game");
        GreenhouseGame game = gameObject.AddComponent<GreenhouseGame>();
        SerializedObject serializedGame = new SerializedObject(game);
        serializedGame.FindProperty("gameCamera").objectReferenceValue = camera;
        serializedGame.FindProperty("player").objectReferenceValue = player;
        serializedGame.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateEnemyPreview(string objectName, Vector3 position, float size, Material material, bool shooter)
    {
        GameObject enemyObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        enemyObject.name = objectName;
        enemyObject.transform.position = position;
        enemyObject.transform.localScale = Vector3.one * size;
        enemyObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(enemyObject.GetComponent<Collider>());
        GreenhouseEnemy enemy = enemyObject.AddComponent<GreenhouseEnemy>();
        SerializedObject serializedEnemy = new SerializedObject(enemy);
        serializedEnemy.FindProperty("shooter").boolValue = shooter;
        serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material GetPreviewMaterial(string path, Shader shader, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
            material.color = color;
        }
        return material;
    }
}
