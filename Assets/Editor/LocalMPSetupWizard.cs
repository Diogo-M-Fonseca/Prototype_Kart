using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Janela de setup do multijogador local.
/// Menu: Tools > Karts > Local Multiplayer Setup.
/// Colocar este ficheiro numa pasta chamada "Editor".
/// </summary>
public class LocalMPSetupWizard : EditorWindow
{
    private const string KartLayerName = "Kart";

    private static readonly string[] NonBodyKeywords =
    {
        "wheel", "tire", "tyre", "pneu", "roda", "glass", "vidro", "window", "janela"
    };

    [SerializeField] private GameObject _kartPrefab;
    [SerializeField] private GameObject _cameraPrefab;
    [SerializeField] private bool _isolateKartLayer = true;
    [SerializeField] private bool _removeSceneKartAndCamera = true;
    [SerializeField] private string _prefabsFolder = "Assets/Prefabs";
    [SerializeField] private string _scenesFolder = "Assets/Scenes";
    [SerializeField] private string _menuScene = "MainMenu";
    [SerializeField] private string _singlePlayerScene = "SinglePlayer";
    [SerializeField] private string _lobbyScene = "LocalMultiplayerLobby";
    [SerializeField] private string _gameScene = "Game";
    [SerializeField] private int _minPlayers = 2;
    [SerializeField] private int _laps = 3;
    [SerializeField] private Object _skyboxSource;
    [SerializeField] private bool _setCamerasToSkybox = true;

    private enum Level { Ok, Warn, Error }

    private readonly List<KeyValuePair<Level, string>> _checks = new List<KeyValuePair<Level, string>>();
    private readonly List<string> _log = new List<string>();
    private Vector2 _scroll;

    [MenuItem("Tools/Karts/Local Multiplayer Setup")]
    private static void Open()
    {
        LocalMPSetupWizard window = GetWindow<LocalMPSetupWizard>("Local MP Setup");
        window.minSize = new Vector2(460f, 560f);
    }

    private void OnEnable()
    {
        RunChecks();
    }

    private void OnFocus()
    {
        RunChecks();
    }

    // ------------------------------------------------------------------ GUI

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Prepara o split-screen local (2-4 jogadores): prefabs, cenas de Menu / Lobby / Game e Build Settings. " +
            "Podes carregar em SETUP COMPLETO ou fazer cada passo à parte.", MessageType.Info);

        Section("1. Prefabs");
        _kartPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab do kart", _kartPrefab, typeof(GameObject), false);
        _cameraPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab da câmara", _cameraPrefab, typeof(GameObject), false);
        _isolateKartLayer = EditorGUILayout.ToggleLeft(
            "Isolar os karts na layer \"Kart\" (um kart deixa de ser detetado como chão)", _isolateKartLayer);
        _prefabsFolder = EditorGUILayout.TextField("Pasta dos prefabs", _prefabsFolder);

        ActionButton("Auto-detetar prefabs do projeto", AutoDetectPrefabs);
        ActionButton("Criar prefab do kart a partir da cena SinglePlayer", CreateKartPrefabFromScene);
        ActionButton("Preparar prefab do kart (KartColorizer, layer, trackMask)", PrepareKartPrefab);
        ActionButton("Criar prefab da câmara (copia a da cena SinglePlayer)", CreateCameraPrefab);

        Section("2. Cenas");
        _scenesFolder = EditorGUILayout.TextField("Pasta das cenas", _scenesFolder);
        _menuScene = EditorGUILayout.TextField("Cena do menu", _menuScene);
        _singlePlayerScene = EditorGUILayout.TextField("Cena single-player", _singlePlayerScene);
        _lobbyScene = EditorGUILayout.TextField("Cena do lobby", _lobbyScene);
        _gameScene = EditorGUILayout.TextField("Cena de jogo (multi)", _gameScene);
        _minPlayers = EditorGUILayout.IntSlider("Mínimo de jogadores", _minPlayers, 1, 4);
        _removeSceneKartAndCamera = EditorGUILayout.ToggleLeft(
            "Na cena Game, remover o kart e a câmara existentes (só ficam os dos jogadores)", _removeSceneKartAndCamera);

        ActionButton("Criar / substituir cena do Lobby", CreateLobbySceneClicked);
        ActionButton("Criar / substituir cena do Menu", CreateMenuSceneClicked);
        ActionButton("Criar cena Game (cópia da SinglePlayer + manager)", CreateGameSceneClicked);
        ActionButton("Configurar a cena aberta como cena Game", ConfigureOpenSceneClicked);
        ActionButton("Adicionar cenas às Build Settings", AddScenesToBuildSettings);

        Section("3. Corrida (voltas e posições) - na cena Game aberta");
        EditorGUILayout.HelpBox(
            "Põe a vista da Scene a olhar na direção da pista, sobre a linha de meta, e carrega em \"Novo checkpoint\". " +
            "Repete ao longo da pista, por ordem, até voltar perto da meta. O primeiro checkpoint é a linha de meta. " +
            "São precisos pelo menos 2 (recomendado 6 ou mais, para não haver atalhos).", MessageType.Info);
        _laps = EditorGUILayout.IntSlider("Voltas", _laps, 1, 10);
        ActionButton("Criar / atualizar RaceManager na cena aberta", () => EnsureRaceManager());
        EditorGUILayout.HelpBox(
            "O HUD da corrida (volta, posição e resultados) fica criado na cena, em RaceCanvas > PlayerHUD_1..4. " +
            "Personaliza à vontade (posições, fontes, cores, imagens). Para editar o ResultsPanel, ativa-o na Hierarchy: " +
            "em jogo é o script que o liga e desliga. Não apagues os textos referenciados no componente RacerHud.", MessageType.None);
        ActionButton("Recriar HUD da corrida (apaga o atual)", RecreateRaceHud);
        ActionButton("Novo checkpoint (na vista da Scene)", AddCheckpoint);
        ActionButton("Atualizar lista de checkpoints (ordem do Hierarchy)", RefreshCheckpoints);

        Section("4. Skybox (todas as cenas)");
        _skyboxSource = EditorGUILayout.ObjectField("Skybox (material ou textura)", _skyboxSource, typeof(Object), false);
        EditorGUILayout.HelpBox(
            "Arrasta um material de skybox, ou uma textura panorâmica (HDRI) / cubemap: nesse caso o tool cria o material ao lado da textura. " +
            "É aplicado às cenas do menu, single-player, lobby e jogo e a todas as que estiverem nas Build Settings.", MessageType.None);
        _setCamerasToSkybox = EditorGUILayout.ToggleLeft(
            "Pôr em Skybox as câmaras que estão em cor sólida (cenas e prefab da câmara)", _setCamerasToSkybox);
        ActionButton("Aplicar skybox a todas as cenas", ApplySkyboxClicked);

        EditorGUILayout.Space(10f);
        if (GUILayout.Button("SETUP COMPLETO", GUILayout.Height(38f)))
            EditorApplication.delayCall += () => Run(FullSetup);

        Section("Verificação");
        if (GUILayout.Button("Verificar agora"))
            RunChecks();

        GUIStyle style = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
        foreach (KeyValuePair<Level, string> check in _checks)
        {
            string color = check.Key == Level.Ok ? "#4caf50" : check.Key == Level.Warn ? "#e0a030" : "#e05050";
            string icon = check.Key == Level.Ok ? "✔" : check.Key == Level.Warn ? "⚠" : "✖";
            EditorGUILayout.LabelField($"<color={color}>{icon}</color> {check.Value}", style);
        }

        if (_log.Count > 0)
        {
            Section("Registo");
            EditorGUILayout.HelpBox(string.Join("\n", _log.Skip(Mathf.Max(0, _log.Count - 14))), MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void Section(string title)
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void ActionButton(string label, System.Action action)
    {
        if (GUILayout.Button(label))
            EditorApplication.delayCall += () => Run(action);
    }

    private void Run(System.Action action)
    {
        try
        {
            action();
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            Log("ERRO: " + e.Message);
        }

        RunChecks();
        Repaint();
    }

    private void Log(string message)
    {
        _log.Add(message);
        Debug.Log("[LocalMP Setup] " + message);
    }

    // --------------------------------------------------------------- Prefabs

    private void AutoDetectPrefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (go == null)
                continue;

            if (_kartPrefab == null && go.GetComponent<KartController>() != null)
                _kartPrefab = go;

            if (_cameraPrefab == null && go.GetComponent<KartCamera>() != null && go.GetComponent<Camera>() != null)
                _cameraPrefab = go;
        }

        Log(_kartPrefab != null ? $"Prefab do kart: {_kartPrefab.name}" : "Nenhum prefab de kart encontrado.");
        Log(_cameraPrefab != null ? $"Prefab da câmara: {_cameraPrefab.name}" : "Nenhum prefab de câmara encontrado.");
    }

    private void CreateKartPrefabFromScene()
    {
        Scene extra = OpenSinglePlayerAdditive(out bool openedHere);
        GameObject clone = null;

        try
        {
            KartController sceneKart = FindInLoadedScenes<KartController>();
            if (sceneKart == null)
            {
                Log("Não encontrei nenhum kart na cena aberta nem na cena SinglePlayer. Arrasta o prefab para o campo.");
                return;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(sceneKart.gameObject);
            if (source != null)
            {
                _kartPrefab = source.transform.root.gameObject;
                Log($"O kart da cena já é uma instância do prefab \"{_kartPrefab.name}\"; vou usar esse.");
                return;
            }

            clone = Instantiate(sceneKart.gameObject);
            ToPlainObject(clone);
            clone.name = "Kart";

            foreach (KartCamera kc in clone.GetComponentsInChildren<KartCamera>(true))
            {
                if (kc != null && kc.gameObject != clone)
                    DestroyImmediate(kc.gameObject);
            }

            EnsureFolder(_prefabsFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{_prefabsFolder.TrimEnd('/')}/Kart.prefab");
            _kartPrefab = PrefabUtility.SaveAsPrefabAsset(clone, path);
            Log($"Prefab do kart criado: {path}");
        }
        finally
        {
            if (clone != null)
                DestroyImmediate(clone);

            CloseIfOpened(extra, openedHere);
        }
    }

    private void PrepareKartPrefab()
    {
        if (_kartPrefab == null || !PrefabUtility.IsPartOfPrefabAsset(_kartPrefab))
        {
            Log("Define primeiro um prefab (asset) do kart.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(_kartPrefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            if (root.GetComponent<KartController>() == null)
            {
                Log("O prefab não tem KartController.");
                return;
            }

            KartColorizer colorizer = root.GetComponent<KartColorizer>();
            bool added = colorizer == null;
            if (added)
                colorizer = root.AddComponent<KartColorizer>();

            if (added || CountRenderers(colorizer) == 0)
            {
                int count = FillRenderers(colorizer, root);
                Log($"KartColorizer: {count} renderers (pneus e vidros excluídos pelo nome). Confirma a lista no prefab.");
            }

            if (_isolateKartLayer)
            {
                int layer = EnsureLayer(KartLayerName);
                if (layer < 0)
                {
                    Log("Não há layers livres para criar \"Kart\".");
                }
                else
                {
                    SetLayerRecursively(root.transform, layer);

                    KartGroundDetector detector = root.GetComponent<KartGroundDetector>();
                    if (detector != null)
                    {
                        SerializedObject so = new SerializedObject(detector);
                        so.FindProperty("trackMask").intValue = ~(1 << layer);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    Log($"Kart na layer \"{KartLayerName}\" ({layer}); trackMask ignora essa layer.");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private void CreateCameraPrefab()
    {
        Scene extra = OpenSinglePlayerAdditive(out bool openedHere);
        GameObject clone = null;

        try
        {
            KartCamera template = FindInLoadedScenes<KartCamera>();

            if (template != null)
            {
                clone = Instantiate(template.gameObject);
                ToPlainObject(clone);
                Log("Câmara copiada da cena (mantém os valores do KartCamera e a config. do render pipeline).");
            }
            else
            {
                clone = new GameObject("PlayerCamera");
                Log("Não encontrei uma câmara com KartCamera; criei uma câmara nova.");
            }

            clone.name = "PlayerCamera";
            clone.tag = "Untagged";

            Camera cam = clone.GetComponent<Camera>();
            if (cam == null)
                cam = clone.AddComponent<Camera>();
            cam.rect = new Rect(0f, 0f, 1f, 1f);

            if (clone.GetComponent<AudioListener>() == null)
                clone.AddComponent<AudioListener>();

            KartCamera kartCamera = clone.GetComponent<KartCamera>();
            if (kartCamera == null)
                kartCamera = clone.AddComponent<KartCamera>();

            SerializedObject so = new SerializedObject(kartCamera);
            so.FindProperty("target").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(_prefabsFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{_prefabsFolder.TrimEnd('/')}/PlayerCamera.prefab");
            _cameraPrefab = PrefabUtility.SaveAsPrefabAsset(clone, path);
            Log($"Prefab da câmara criado: {path}");
        }
        finally
        {
            if (clone != null)
                DestroyImmediate(clone);

            CloseIfOpened(extra, openedHere);
        }
    }

    private static int CountRenderers(KartColorizer colorizer)
    {
        SerializedProperty prop = new SerializedObject(colorizer).FindProperty("_renderers");
        int count = 0;

        for (int i = 0; i < prop.arraySize; i++)
        {
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue != null)
                count++;
        }

        return count;
    }

    private static int FillRenderers(KartColorizer colorizer, GameObject root)
    {
        List<Renderer> list = new List<Renderer>();

        foreach (Renderer candidate in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(candidate is MeshRenderer) && !(candidate is SkinnedMeshRenderer))
                continue;

            if (IsNonBodyPart(candidate.transform, root.transform))
                continue;

            list.Add(candidate);
        }

        SerializedObject so = new SerializedObject(colorizer);
        SerializedProperty prop = so.FindProperty("_renderers");
        prop.arraySize = list.Count;

        for (int i = 0; i < list.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        return list.Count;
    }

    private static bool IsNonBodyPart(Transform part, Transform root)
    {
        for (Transform current = part; current != null && current != root; current = current.parent)
        {
            string lower = current.name.ToLowerInvariant();

            foreach (string keyword in NonBodyKeywords)
            {
                if (lower.Contains(keyword))
                    return true;
            }
        }

        return false;
    }

    private static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;

        foreach (Transform child in t)
            SetLayerRecursively(child, layer);
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
            return existing;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
            return -1;

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);

            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }

        return -1;
    }

    private static void ToPlainObject(GameObject clone)
    {
        if (PrefabUtility.IsOutermostPrefabInstanceRoot(clone))
            PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    // ----------------------------------------------------------------- Cenas

    private void CreateLobbySceneClicked()
    {
        string path = ScenePathFor(_lobbyScene);
        if (!ConfirmOverwrite(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        BuildLobbyScene(path);
    }

    private void CreateMenuSceneClicked()
    {
        string path = ScenePathFor(_menuScene);
        if (!ConfirmOverwrite(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        BuildMenuScene(path);
    }

    private void CreateGameSceneClicked()
    {
        string dst = ScenePathFor(_gameScene);
        if (!ConfirmOverwrite(dst) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        CreateGameSceneFromSinglePlayer(dst);
    }

    private void ConfigureOpenSceneClicked()
    {
        if (SetupOpenSceneAsGame())
            Log("Cena configurada. Grava a cena (Ctrl+S) e põe os SpawnPoints na linha de partida.");
    }

    private void BuildLobbyScene(string path)
    {
        EnsureFolder(Path.GetDirectoryName(path));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Canvas canvas = CreateCanvas();
        CreateLabel(canvas.transform, "Title", "Multijogador Local", 72f, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1400f, 120f));

        TMP_Text[] slots = new TMP_Text[4];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = CreateLabel(canvas.transform, $"Slot_{i + 1}", $"Jogador {i + 1}", 44f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 160f - i * 100f), new Vector2(1500f, 80f));
        }

        TMP_Text hint = CreateLabel(canvas.transform, "Hint", string.Empty, 32f,
            new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1700f, 80f));

        GameObject lobbyObject = new GameObject("LocalMPLobby");
        LocalMPLobby lobby = lobbyObject.AddComponent<LocalMPLobby>();

        SerializedObject so = new SerializedObject(lobby);
        SetString(so, "_gameSceneName", _gameScene);
        SetString(so, "_menuSceneName", _menuScene);

        SerializedProperty slotsProp = so.FindProperty("_slotLabels");
        slotsProp.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

        so.FindProperty("_hintLabel").objectReferenceValue = hint;
        so.FindProperty("_minPlayersToStart").intValue = _minPlayers;
        so.ApplyModifiedPropertiesWithoutUndo();

        ApplyConfiguredSkyboxToOpenScene();
        EditorSceneManager.SaveScene(scene, path);
        Log($"Cena do lobby criada: {path}");
    }

    private void BuildMenuScene(string path)
    {
        EnsureFolder(Path.GetDirectoryName(path));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Canvas canvas = CreateCanvas();
        CreateLabel(canvas.transform, "Title", Application.productName, 96f, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1600f, 160f));

        Button single = CreateButton(canvas.transform, "Button_SinglePlayer", "Um Jogador", new Vector2(0f, 60f));
        Button multi = CreateButton(canvas.transform, "Button_LocalMultiplayer", "Multijogador Local", new Vector2(0f, -70f));
        Button quit = CreateButton(canvas.transform, "Button_Quit", "Sair", new Vector2(0f, -200f));

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        GameObject menuObject = new GameObject("MainMenu");
        MainMenu menu = menuObject.AddComponent<MainMenu>();

        SerializedObject so = new SerializedObject(menu);
        SetString(so, "_singlePlayerSceneName", _singlePlayerScene);
        SetString(so, "_localMultiplayerLobbySceneName", _lobbyScene);
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(single.onClick, menu.PlaySinglePlayer);
        UnityEventTools.AddPersistentListener(multi.onClick, menu.OpenLocalMultiplayer);
        UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);

        ApplyConfiguredSkyboxToOpenScene();
        EditorSceneManager.SaveScene(scene, path);
        Log($"Cena do menu criada: {path}");
    }

    private bool CreateGameSceneFromSinglePlayer(string dst)
    {
        string src = FindScenePath(_singlePlayerScene);

        if (src == null)
        {
            Log($"Cena \"{_singlePlayerScene}\" não encontrada; vou criar uma cena Game vazia.");
            return CreateBlankGameScene(dst);
        }

        if (src == dst)
        {
            Log("A cena Game e a SinglePlayer têm o mesmo nome. Muda um dos nomes.");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(dst) != null)
            AssetDatabase.DeleteAsset(dst);

        EnsureFolder(Path.GetDirectoryName(dst));

        if (!AssetDatabase.CopyAsset(src, dst))
        {
            Log($"Não consegui copiar {src} para {dst}.");
            return false;
        }

        EditorSceneManager.OpenScene(dst, OpenSceneMode.Single);
        return SetupAndSaveOpenScene(dst);
    }

    private bool CreateBlankGameScene(string dst)
    {
        EnsureFolder(Path.GetDirectoryName(dst));
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), dst);
        bool ok = SetupAndSaveOpenScene(dst);
        Log("Cena Game vazia: adiciona a pista e põe os SpawnPoints na linha de partida.");
        return ok;
    }

    private bool SetupAndSaveOpenScene(string path)
    {
        if (!SetupOpenSceneAsGame())
            return false;

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
        Log($"Cena de jogo guardada: {path}");
        return true;
    }

    private bool SetupOpenSceneAsGame()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Log("Sai do modo de edição de prefab antes de configurar a cena.");
            return false;
        }

        if (_kartPrefab == null || _cameraPrefab == null)
        {
            Log("Define primeiro o prefab do kart e o da câmara.");
            return false;
        }

        KartController kartComponent = _kartPrefab.GetComponent<KartController>();
        KartCamera cameraComponent = _cameraPrefab.GetComponent<KartCamera>();

        if (kartComponent == null || cameraComponent == null)
        {
            Log("O prefab do kart precisa de KartController e o da câmara de KartCamera.");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();

        KartController[] sceneKarts = Object
            .FindObjectsByType<KartController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(k => k.gameObject.scene == scene)
            .OrderBy(k => k.name)
            .ToArray();

        Vector3 basePosition = new Vector3(0f, 1f, 0f);
        Quaternion baseRotation = Quaternion.identity;

        if (sceneKarts.Length > 0)
        {
            basePosition = sceneKarts[0].transform.position;
            baseRotation = sceneKarts[0].transform.rotation;
        }

        LocalMPManager manager = Object.FindFirstObjectByType<LocalMPManager>();

        bool keepSpawns = false;
        if (manager != null)
        {
            SerializedProperty existing = new SerializedObject(manager).FindProperty("_spawnPoints");
            keepSpawns = existing != null && existing.arraySize > 0 && existing.GetArrayElementAtIndex(0).objectReferenceValue != null;
        }

        Transform[] spawns = new Transform[4];

        if (!keepSpawns)
        {
            GameObject spawnRoot = new GameObject("SpawnPoints");
            Undo.RegisterCreatedObjectUndo(spawnRoot, "Criar SpawnPoints");
            Texture2D icon = EditorGUIUtility.IconContent("sv_label_3").image as Texture2D;

            for (int i = 0; i < spawns.Length; i++)
            {
                GameObject point = new GameObject($"Spawn_P{i + 1}");
                point.transform.SetParent(spawnRoot.transform, false);

                Vector3 offset = new Vector3(i % 2 == 0 ? -1.5f : 1.5f, 0f, -(i / 2) * 4f);
                point.transform.SetPositionAndRotation(basePosition + baseRotation * offset, baseRotation);

                if (icon != null)
                    EditorGUIUtility.SetIconForObject(point, icon);

                spawns[i] = point.transform;
            }
        }

        if (manager == null)
        {
            GameObject managerObject = new GameObject("LocalMPManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Criar LocalMPManager");
            manager = managerObject.AddComponent<LocalMPManager>();
        }

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("_kartPrefab").objectReferenceValue = kartComponent;
        so.FindProperty("_cameraPrefab").objectReferenceValue = cameraComponent;

        if (!keepSpawns)
        {
            SerializedProperty array = so.FindProperty("_spawnPoints");
            array.arraySize = spawns.Length;

            for (int i = 0; i < spawns.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
        }

        so.ApplyModifiedProperties();

        if (_removeSceneKartAndCamera)
        {
            foreach (KartController kart in sceneKarts)
            {
                if (kart != null)
                    RemoveOrDisable(kart.gameObject);
            }

            foreach (KartCamera kc in Object.FindObjectsByType<KartCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (kc != null && kc.gameObject.scene == scene)
                    RemoveOrDisable(kc.gameObject);
            }

            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (cam.gameObject.scene == scene && cam.CompareTag("MainCamera"))
                {
                    Undo.RecordObject(cam.gameObject, "Desativar Main Camera");
                    cam.gameObject.SetActive(false);
                }
            }
        }

        EnsureRaceManager();
        ApplyConfiguredSkyboxToOpenScene();

        EditorSceneManager.MarkSceneDirty(scene);
        Log(keepSpawns
            ? "LocalMPManager atualizado (spawn points mantidos)."
            : "LocalMPManager e 4 SpawnPoints criados. Move-os para a linha de partida.");
        return true;
    }

    private static void RemoveOrDisable(GameObject go)
    {
        bool nestedInPrefab = PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsOutermostPrefabInstanceRoot(go);

        if (nestedInPrefab)
        {
            Undo.RecordObject(go, "Desativar objeto");
            go.SetActive(false);
        }
        else
        {
            Undo.DestroyObjectImmediate(go);
        }
    }

    private void AddScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        string[] names = { _menuScene, _singlePlayerScene, _lobbyScene, _gameScene };

        foreach (string sceneName in names)
        {
            string path = FindScenePath(sceneName);

            if (path == null)
            {
                Log($"Cena \"{sceneName}\" ainda não existe; ignorada.");
                continue;
            }

            int index = scenes.FindIndex(s => s.path == path);

            if (index >= 0)
                scenes[index].enabled = true;
            else
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        string menuPath = FindScenePath(_menuScene);
        int menuIndex = menuPath == null ? -1 : scenes.FindIndex(s => s.path == menuPath);

        if (menuIndex > 0)
        {
            EditorBuildSettingsScene menu = scenes[menuIndex];
            scenes.RemoveAt(menuIndex);
            scenes.Insert(0, menu);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Log("Build Settings atualizadas (menu em primeiro).");
    }

    // ---------------------------------------------------------------- Corrida

    private RaceManager EnsureRaceManager()
    {
        RaceManager manager = Object.FindFirstObjectByType<RaceManager>();

        if (manager == null)
        {
            GameObject go = new GameObject("RaceManager");
            Undo.RegisterCreatedObjectUndo(go, "Criar RaceManager");
            manager = go.AddComponent<RaceManager>();
        }

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("_totalLaps").intValue = _laps;
        SetString(so, "_menuSceneName", _menuScene);
        so.ApplyModifiedProperties();

        EnsureRaceHud(manager, false);

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Log($"RaceManager pronto ({_laps} voltas).");
        return manager;
    }

    private void RecreateRaceHud()
    {
        if (!EditorUtility.DisplayDialog(
                "Recriar HUD da corrida?",
                "Isto apaga o canvas da corrida atual (incluindo as tuas alterações) e cria um novo, com o aspeto original.",
                "Recriar", "Cancelar"))
        {
            return;
        }

        EnsureRaceHud(EnsureRaceManager(), true);
    }

    /// <summary>
    /// Cria na cena o canvas da corrida com um HUD por jogador e liga-o ao RaceManager.
    /// Se já existir, não toca nele (para preservar as alterações do utilizador), a não ser que recreate seja true.
    /// </summary>
    private void EnsureRaceHud(RaceManager manager, bool recreate)
    {
        SerializedObject so = new SerializedObject(manager);
        SerializedProperty array = so.FindProperty("_huds");

        int assigned = 0;
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue != null)
                assigned++;
        }

        if (!recreate)
        {
            if (assigned >= LocalMPSession.MaxPlayers)
                return;

            if (assigned > 0)
            {
                Log("O HUD da corrida está incompleto (faltam HUDs no RaceManager). Usa \"Recriar HUD da corrida\" para o refazer.");
                return;
            }
        }
        else
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                RacerHud existing = array.GetArrayElementAtIndex(i).objectReferenceValue as RacerHud;

                if (existing == null)
                    continue;

                Canvas oldCanvas = existing.GetComponentInParent<Canvas>();
                GameObject toDestroy = oldCanvas != null ? oldCanvas.rootCanvas.gameObject : existing.gameObject;

                if (toDestroy != null)
                    Undo.DestroyObjectImmediate(toDestroy);
            }
        }

        GameObject canvasObject = new GameObject("RaceCanvas", typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Criar HUD da corrida");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        so.Update();
        array = so.FindProperty("_huds");
        array.arraySize = LocalMPSession.MaxPlayers;

        for (int i = 0; i < LocalMPSession.MaxPlayers; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = BuildPlayerHud(canvasObject.transform, i);

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Log("HUD da corrida criado na cena (RaceCanvas > PlayerHUD_1..4). Podes personalizá-lo à vontade.");
    }

    private static RacerHud BuildPlayerHud(Transform canvas, int index)
    {
        Color playerColor = LocalMPSession.GetPlayerColor(index);
        string ordinal = ((char)0x00BA).ToString();

        GameObject root = new GameObject($"PlayerHUD_{index + 1}", typeof(RectTransform), typeof(RacerHud));
        root.transform.SetParent(canvas, false);

        // Pré-visualização em grelha 2x2. Em jogo o script ajusta ao ecrã real de cada jogador.
        float x = index % 2 * 0.5f;
        float y = index / 2 == 0 ? 0.5f : 0f;
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(x, y);
        rootRect.anchorMax = new Vector2(x + 0.5f, y + 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        TextMeshProUGUI lap = CreateHudText(rootRect, "Lap", "Volta 1/3", 44f, TextAlignmentOptions.TopLeft, Color.white);
        PlaceAt(lap.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(520f, 80f));

        TextMeshProUGUI position = CreateHudText(rootRect, "Position", "1" + ordinal, 80f, TextAlignmentOptions.TopRight, playerColor);
        PlaceAt(position.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(320f, 120f));

        GameObject panel = new GameObject("ResultsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(rootRect, false);
        Stretch((RectTransform)panel.transform, Vector2.zero, Vector2.one);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);
        background.raycastTarget = false;

        TextMeshProUGUI title = CreateHudText(panel.transform, "Title", $"Jogador {index + 1}: 1{ordinal} lugar", 60f, TextAlignmentOptions.Center, playerColor);
        Stretch(title.rectTransform, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.96f));
        MakeAutoSize(title, 60f);

        TextMeshProUGUI rows = CreateHudText(panel.transform, "Rows", $"1{ordinal}   Jogador 1   01:23.45\n2{ordinal}   Jogador 2   01:25.10\n3{ordinal}   ...\n4{ordinal}   ...", 56f, TextAlignmentOptions.Left, Color.white);
        Stretch(rows.rectTransform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.78f));
        MakeAutoSize(rows, 56f);

        TextMeshProUGUI hint = CreateHudText(panel.transform, "Hint", "Start / Enter para voltar ao menu", 32f, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f));
        Stretch(hint.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.18f));
        MakeAutoSize(hint, 32f);

        // Fica desligado: em jogo é o script que o liga quando o jogador acaba.
        panel.SetActive(false);

        RacerHud hud = root.GetComponent<RacerHud>();
        SerializedObject hudSo = new SerializedObject(hud);
        hudSo.FindProperty("_lapLabel").objectReferenceValue = lap;
        hudSo.FindProperty("_positionLabel").objectReferenceValue = position;
        hudSo.FindProperty("_resultsPanel").objectReferenceValue = panel;
        hudSo.FindProperty("_titleLabel").objectReferenceValue = title;
        hudSo.FindProperty("_rowsLabel").objectReferenceValue = rows;
        hudSo.FindProperty("_exitHint").objectReferenceValue = hint.gameObject;
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        // Slot do item (KartItemHud), em baixo à esquerda para não tapar a volta nem a posição.
        ItemSetupWindow.BuildItemHud(hud, ItemSetupWindow.ItemHudCorner.BottomLeft, 110f, 24f);

        return hud;
    }

    private static TextMeshProUGUI CreateHudText(Transform parent, string name, string text, float size, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private static void PlaceAt(RectTransform rect, Vector2 corner, Vector2 position, Vector2 size)
    {
        rect.anchorMin = corner;
        rect.anchorMax = corner;
        rect.pivot = corner;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void MakeAutoSize(TextMeshProUGUI label, float maxSize)
    {
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = maxSize;
    }

    private void AddCheckpoint()
    {
        EnsureRaceManager();

        GameObject root = GameObject.Find("Checkpoints");
        if (root == null)
        {
            root = new GameObject("Checkpoints");
            Undo.RegisterCreatedObjectUndo(root, "Criar Checkpoints");
        }

        SceneView view = SceneView.lastActiveSceneView;
        Vector3 position = view != null ? view.pivot : Vector3.zero;
        Quaternion rotation = view != null ? Quaternion.Euler(0f, view.rotation.eulerAngles.y, 0f) : Quaternion.identity;

        if (Physics.Raycast(position + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point;

        GameObject go = new GameObject($"Checkpoint_{root.transform.childCount}");
        Undo.RegisterCreatedObjectUndo(go, "Novo checkpoint");
        go.transform.SetParent(root.transform, false);
        go.transform.SetPositionAndRotation(position, rotation);

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(20f, 6f, 3f);
        box.center = new Vector3(0f, 3f, 0f);

        go.AddComponent<RaceCheckpoint>();

        RefreshCheckpoints();
        Selection.activeGameObject = go;
    }

    private void RefreshCheckpoints()
    {
        RaceManager manager = EnsureRaceManager();
        GameObject root = GameObject.Find("Checkpoints");

        if (root == null)
        {
            Log("Não há um objeto \"Checkpoints\" na cena. Cria checkpoints com \"Novo checkpoint\".");
            return;
        }

        RaceCheckpoint[] list = root.GetComponentsInChildren<RaceCheckpoint>(true);

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty array = so.FindProperty("_checkpoints");
        array.arraySize = list.Length;

        for (int i = 0; i < list.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = list[i];

        so.ApplyModifiedProperties();
        Log($"{list.Length} checkpoints ligados ao RaceManager (o primeiro é a linha de meta).");
    }

    // ---------------------------------------------------------------- Skybox

    private void ApplySkyboxClicked()
    {
        Material skybox = ResolveSkyboxMaterial();

        if (skybox == null)
        {
            Log("Define primeiro o skybox (material ou textura) no campo da secção 4.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string originalScene = SceneManager.GetActiveScene().path;

        ApplySkyboxToScenes(skybox);

        if (!string.IsNullOrEmpty(originalScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(originalScene) != null)
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
    }

    private void ApplySkyboxToScenes(Material skybox)
    {
        List<string> paths = new List<string>();

        foreach (string sceneName in new[] { _menuScene, _singlePlayerScene, _lobbyScene, _gameScene })
        {
            string path = FindScenePath(sceneName);

            if (path != null && !paths.Contains(path))
                paths.Add(path);
        }

        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (buildScene.path.StartsWith("Assets/") && !paths.Contains(buildScene.path)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(buildScene.path) != null)
            {
                paths.Add(buildScene.path);
            }
        }

        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            ApplySkyboxToOpenScene(skybox);
            EditorSceneManager.SaveScene(scene);
            Log($"Skybox aplicado: {path}");
        }

        ApplySkyboxToCameraPrefab();
        Log($"Skybox \"{skybox.name}\" aplicado a {paths.Count} cenas.");
    }

    private void ApplyConfiguredSkyboxToOpenScene()
    {
        Material skybox = ResolveSkyboxMaterial();

        if (skybox != null)
            ApplySkyboxToOpenScene(skybox);
    }

    private void ApplySkyboxToOpenScene(Material skybox)
    {
        Scene scene = SceneManager.GetActiveScene();

        RenderSettings.skybox = skybox;
        DynamicGI.UpdateEnvironment();

        if (_setCamerasToSkybox)
        {
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.gameObject.scene != scene || cam.targetTexture != null || cam.clearFlags != CameraClearFlags.SolidColor)
                    continue;

                Undo.RecordObject(cam, "Camara em Skybox");
                cam.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(cam);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private void ApplySkyboxToCameraPrefab()
    {
        if (!_setCamerasToSkybox || _cameraPrefab == null || !PrefabUtility.IsPartOfPrefabAsset(_cameraPrefab))
            return;

        string path = AssetDatabase.GetAssetPath(_cameraPrefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            bool changed = false;

            foreach (Camera cam in root.GetComponentsInChildren<Camera>(true))
            {
                if (cam.targetTexture == null && cam.clearFlags == CameraClearFlags.SolidColor)
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                    changed = true;
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Log($"Prefab da câmara em Skybox: {path}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Devolve o material de skybox a usar: o próprio material, ou um material criado
    /// (ao lado da textura) a partir de uma cubemap ou de uma textura panorâmica.
    /// </summary>
    private Material ResolveSkyboxMaterial()
    {
        if (_skyboxSource == null)
            return null;

        if (_skyboxSource is Material material)
            return material;

        string shaderName;
        string textureProperty;

        if (_skyboxSource is Cubemap)
        {
            shaderName = "Skybox/Cubemap";
            textureProperty = "_Tex";
        }
        else if (_skyboxSource is Texture2D)
        {
            shaderName = "Skybox/Panoramic";
            textureProperty = "_MainTex";
        }
        else
        {
            Log("O skybox tem de ser um material, uma textura panorâmica ou uma cubemap.");
            return null;
        }

        string texturePath = AssetDatabase.GetAssetPath(_skyboxSource);
        Shader shader = Shader.Find(shaderName);

        if (string.IsNullOrEmpty(texturePath) || shader == null)
        {
            Log($"Não consegui criar o material de skybox (shader \"{shaderName}\" ou textura em falta).");
            return null;
        }

        string materialPath = $"{Path.GetDirectoryName(texturePath)}/{_skyboxSource.name}_Skybox.mat".Replace('\\', '/');
        Material created = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (created == null)
        {
            created = new Material(shader);
            AssetDatabase.CreateAsset(created, materialPath);
            Log($"Material de skybox criado: {materialPath}");
        }

        created.shader = shader;
        created.SetTexture(textureProperty, (Texture)_skyboxSource);

        if (_skyboxSource is Texture2D)
        {
            // Textura panorâmica (HDRI) em formato latitude/longitude.
            created.SetFloat("_Mapping", 1f);
            created.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            created.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
        }

        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        return created;
    }

    // ----------------------------------------------------------- Setup total

    private void FullSetup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string originalScene = SceneManager.GetActiveScene().path;

        if (_kartPrefab == null)
            AutoDetectPrefabs();

        if (_kartPrefab == null)
            CreateKartPrefabFromScene();

        if (_kartPrefab == null)
        {
            Log("Sem prefab do kart: arrasta o teu kart para o campo e volta a tentar.");
            return;
        }

        PrepareKartPrefab();

        if (_cameraPrefab == null)
            CreateCameraPrefab();

        if (FindScenePath(_lobbyScene) == null)
            BuildLobbyScene(ScenePathFor(_lobbyScene));
        else
            Log("Cena do lobby já existe (mantida).");

        if (FindScenePath(_menuScene) == null)
            BuildMenuScene(ScenePathFor(_menuScene));
        else
            Log("Cena do menu já existe (mantida). Liga os botões a MainMenu.OpenLocalMultiplayer se ainda não o fizeste.");

        string gamePath = FindScenePath(_gameScene);

        if (gamePath == null)
        {
            CreateGameSceneFromSinglePlayer(ScenePathFor(_gameScene));
        }
        else
        {
            EditorSceneManager.OpenScene(gamePath, OpenSceneMode.Single);
            SetupAndSaveOpenScene(gamePath);
        }

        AddScenesToBuildSettings();

        Material skybox = ResolveSkyboxMaterial();
        if (skybox != null)
            ApplySkyboxToScenes(skybox);

        if (!string.IsNullOrEmpty(originalScene) && FindScenePath(Path.GetFileNameWithoutExtension(originalScene)) != null)
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

        Log("Setup completo. Confirma a lista de renderers do KartColorizer no prefab e a posição dos SpawnPoints.");
    }

    // ------------------------------------------------------------ Verificação

    private void RunChecks()
    {
        _checks.Clear();
        CheckInputActions();
        CheckKartPrefab();
        CheckCameraPrefab();
        CheckScenes();
        CheckSkybox();
        CheckOpenScene();
        Repaint();
    }

    private void CheckSkybox()
    {
        if (_skyboxSource == null)
        {
            Add(Level.Warn, "Skybox não definido (secção 4).");
            return;
        }

        bool valid = _skyboxSource is Material || _skyboxSource is Texture2D || _skyboxSource is Cubemap;

        Add(valid ? Level.Ok : Level.Error,
            valid ? $"Skybox: {_skyboxSource.name}" : "O skybox tem de ser um material, uma textura panorâmica ou uma cubemap.");
    }

    private void Add(Level level, string text)
    {
        _checks.Add(new KeyValuePair<Level, string>(level, text));
    }

    private void CheckInputActions()
    {
        InputActionAsset actions = null;

        try
        {
            actions = InputSystem.actions;
        }
        catch (System.Exception)
        {
        }

        if (actions == null)
        {
            Add(Level.Error, "Project-wide Actions não definidas (Project Settings > Input System Package).");
            return;
        }

        foreach (string action in new[] { "Kart/Move", "Kart/Brake", "Kart/Drift" })
        {
            Add(actions.FindAction(action) != null ? Level.Ok : Level.Error,
                actions.FindAction(action) != null ? $"Action {action}" : $"Falta a action {action} nas Project-wide Actions.");
        }

        bool hasAttack = actions.FindAction("Kart/Attack") != null;
        Add(hasAttack ? Level.Ok : Level.Warn,
            hasAttack ? "Action Kart/Attack (usar item)" : "Falta a action Kart/Attack: o item só se usa no teclado e rato (E ou clique esquerdo).");
    }

    private void CheckKartPrefab()
    {
        if (_kartPrefab == null)
        {
            Add(Level.Warn, "Prefab do kart não definido.");
            return;
        }

        if (!PrefabUtility.IsPartOfPrefabAsset(_kartPrefab))
        {
            Add(Level.Error, "O kart tem de ser um prefab (asset), não um objeto da cena.");
            return;
        }

        bool hasController = _kartPrefab.GetComponent<KartController>() != null;
        Add(hasController ? Level.Ok : Level.Error, hasController ? $"Prefab do kart: {_kartPrefab.name}" : "O prefab do kart não tem KartController.");

        KartColorizer colorizer = _kartPrefab.GetComponent<KartColorizer>();

        if (colorizer == null)
        {
            Add(Level.Warn, "Falta o KartColorizer no prefab do kart (usa \"Preparar prefab do kart\").");
        }
        else
        {
            int count = CountRenderers(colorizer);
            Add(count > 0 ? Level.Ok : Level.Warn, count > 0 ? $"KartColorizer com {count} renderers." : "O KartColorizer não tem renderers.");
        }

        KartGroundDetector detector = _kartPrefab.GetComponent<KartGroundDetector>();

        if (detector != null)
        {
            int mask = new SerializedObject(detector).FindProperty("trackMask").intValue;
            bool includesKartLayer = (mask & (1 << _kartPrefab.layer)) != 0;

            Add(includesKartLayer ? Level.Warn : Level.Ok,
                includesKartLayer
                    ? "O trackMask inclui a layer do kart: com vários karts, um kart pode ser detetado como chão."
                    : "O trackMask ignora a layer do kart.");
        }
    }

    private void CheckCameraPrefab()
    {
        if (_cameraPrefab == null)
        {
            Add(Level.Warn, "Prefab da câmara não definido.");
            return;
        }

        bool ok = _cameraPrefab.GetComponent<Camera>() != null && _cameraPrefab.GetComponent<KartCamera>() != null;
        Add(ok ? Level.Ok : Level.Error, ok ? $"Prefab da câmara: {_cameraPrefab.name}" : "O prefab da câmara precisa de Camera + KartCamera.");

        if (ok && _cameraPrefab.GetComponent<AudioListener>() == null)
            Add(Level.Warn, "O prefab da câmara não tem AudioListener (não haverá som).");
    }

    private void CheckScenes()
    {
        foreach (string sceneName in new[] { _menuScene, _singlePlayerScene, _lobbyScene, _gameScene })
        {
            string path = FindScenePath(sceneName);

            if (path == null)
            {
                Add(Level.Warn, $"Cena \"{sceneName}\" não existe.");
                continue;
            }

            bool inBuild = EditorBuildSettings.scenes.Any(s => s.path == path && s.enabled);
            Add(inBuild ? Level.Ok : Level.Warn, inBuild ? $"Cena \"{sceneName}\" nas Build Settings." : $"Cena \"{sceneName}\" fora das Build Settings.");
        }
    }

    private void CheckOpenScene()
    {
        LocalMPManager manager = Object.FindFirstObjectByType<LocalMPManager>();
        if (manager == null)
            return;

        SerializedObject so = new SerializedObject(manager);
        bool prefabsOk = so.FindProperty("_kartPrefab").objectReferenceValue != null && so.FindProperty("_cameraPrefab").objectReferenceValue != null;
        int spawns = so.FindProperty("_spawnPoints").arraySize;

        Add(prefabsOk ? Level.Ok : Level.Error, prefabsOk ? "LocalMPManager na cena aberta com prefabs ligados." : "LocalMPManager na cena aberta sem prefabs ligados.");
        Add(spawns >= 4 ? Level.Ok : Level.Warn, $"LocalMPManager com {spawns} spawn points (ideal: 4).");

        RaceManager race = Object.FindFirstObjectByType<RaceManager>();

        if (race == null)
        {
            Add(Level.Warn, "Sem RaceManager na cena aberta (não há voltas nem posições).");
        }
        else
        {
            SerializedObject raceSo = new SerializedObject(race);
            int checkpoints = raceSo.FindProperty("_checkpoints").arraySize;
            Add(checkpoints >= 2 ? Level.Ok : Level.Warn,
                checkpoints >= 2 ? $"RaceManager com {checkpoints} checkpoints." : "RaceManager precisa de pelo menos 2 checkpoints.");

            SerializedProperty huds = raceSo.FindProperty("_huds");
            int hudCount = 0;

            for (int i = 0; i < huds.arraySize; i++)
            {
                if (huds.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    hudCount++;
            }

            Add(hudCount >= LocalMPSession.MaxPlayers ? Level.Ok : Level.Warn,
                hudCount >= LocalMPSession.MaxPlayers
                    ? "HUD da corrida na cena (4 jogadores)."
                    : $"RaceManager com {hudCount} HUDs (precisa de 4). Carrega em \"Criar / atualizar RaceManager\".");
        }
    }

    // -------------------------------------------------------------- Helpers

    private Scene OpenSinglePlayerAdditive(out bool openedHere)
    {
        openedHere = false;
        string path = FindScenePath(_singlePlayerScene);

        if (path == null)
            return default;

        Scene scene = SceneManager.GetSceneByPath(path);

        if (scene.isLoaded)
            return scene;

        openedHere = true;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    private static void CloseIfOpened(Scene scene, bool openedHere)
    {
        if (openedHere && scene.IsValid())
            EditorSceneManager.CloseScene(scene, true);
    }

    private static T FindInLoadedScenes<T>() where T : Component
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);

                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static string FindScenePath(string sceneName)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene " + sceneName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (path.StartsWith("Assets/") && Path.GetFileNameWithoutExtension(path) == sceneName)
                return path;
        }

        return null;
    }

    private string ScenePathFor(string sceneName)
    {
        return FindScenePath(sceneName) ?? $"{_scenesFolder.TrimEnd('/')}/{sceneName}.unity";
    }

    private static bool ConfirmOverwrite(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            return true;

        return EditorUtility.DisplayDialog("Substituir cena?", $"{path} já existe. Queres substituí-la?", "Substituir", "Cancelar");
    }

    private static void EnsureFolder(string folder)
    {
        folder = folder.Replace('\\', '/').TrimEnd('/');

        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void SetString(SerializedObject so, string property, string value)
    {
        SerializedProperty prop = so.FindProperty(property);

        if (prop != null)
            prop.stringValue = value;
        else
            Debug.LogWarning($"[LocalMP Setup] Campo \"{property}\" não encontrado.");
    }

    private static Canvas CreateCanvas()
    {
        GameObject go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(560f, 100f);
        rect.anchoredPosition = position;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.18f, 0.25f, 0.95f);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.55f, 0.65f, 1f, 1f);
        colors.selectedColor = new Color(0.55f, 0.65f, 1f, 1f);
        button.colors = colors;

        TextMeshProUGUI text = CreateLabel(go.transform, "Label", label, 40f, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        text.raycastTarget = false;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        return button;
    }
}
