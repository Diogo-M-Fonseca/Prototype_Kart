using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Janela de setup do sistema de itens: lista de itens, caixas na pista e HUD do item.
// Menu: Tools > Kart > Itens e HUD
public class ItemSetupWindow : EditorWindow
{
    public enum ItemHudCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private const string ItemsFolder = "Assets/Items";
    private const string PrefabsFolder = "Assets/Prefabs";
    private const string BoxPrefabPath = PrefabsFolder + "/ItemBox.prefab";
    private const string BoxMaterialPath = PrefabsFolder + "/ItemBoxMaterial.mat";
    private const string BoxesRootName = "ItemBoxes";
    private const string HudObjectName = "ItemHud";
    private const string UndoName = "Setup de itens";

    [SerializeField] private List<KartItem> _items = new List<KartItem>();

    [SerializeField] private GameObject _boxPrefab;
    [SerializeField, Min(1)] private int _boxesPerRow = 3;
    [SerializeField, Min(1)] private int _checkpointStep = 3;
    [SerializeField] private float _boxSpacing = 4f;
    [SerializeField] private float _boxHeight = 1.2f;

    [SerializeField] private ItemHudCorner _corner = ItemHudCorner.BottomLeft;
    [SerializeField] private float _slotSize = 110f;
    [SerializeField] private float _margin = 24f;

    private readonly List<string> _log = new List<string>();
    private SerializedObject _so;
    private SerializedProperty _itemsProperty;
    private Vector2 _scroll;
    private int _hudCount;
    private int _boxCount;

    [MenuItem("Tools/Kart/Itens e HUD")]
    private static void Open()
    {
        GetWindow<ItemSetupWindow>("Itens e HUD");
    }

    private void OnEnable()
    {
        _so = new SerializedObject(this);
        _itemsProperty = _so.FindProperty("_items");

        if (_items.Count == 0)
        {
            FindItemsInProject();
        }

        RefreshSceneInfo();
    }

    private void OnHierarchyChange()
    {
        RefreshSceneInfo();
        Repaint();
    }

    private void RefreshSceneInfo()
    {
        _hudCount = FindObjectsByType<RacerHud>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        _boxCount = FindObjectsByType<ItemBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }

    // ------------------------------------------------------------------ GUI

    private void OnGUI()
    {
        _so.Update();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.HelpBox(
            "Cria as caixas de item, a lista de itens e o HUD do item de cada jogador.\n" +
            "O HUD e adicionado como filho de cada RacerHud ja existente: nada do que la esta e movido ou apagado.",
            MessageType.Info);

        DrawItemsSection();
        DrawBoxesSection();
        DrawHudSection();

        EditorGUILayout.Space(12f);

        if (GUILayout.Button("Fazer tudo (itens + caixas + HUD)", GUILayout.Height(32f)))
        {
            Defer("Fazer tudo", DoEverything);
        }

        EditorGUILayout.EndScrollView();
        _so.ApplyModifiedProperties();
    }

    private void DrawItemsSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("1. Lista de itens", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_itemsProperty, new GUIContent("Itens que as caixas dao"), true);

        foreach (KartItem item in _items)
        {
            if (item != null && item.Icon == null && item.IconPrefab == null)
            {
                EditorGUILayout.HelpBox(
                    $"\"{item.name}\" nao tem Icon Prefab nem Icon, por isso o HUD mostra so o nome. " +
                    "No RocketItem basta preencher o Rocket Prefab.",
                    MessageType.Warning);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Procurar itens no projeto"))
            {
                Defer("Procurar itens", FindItemsInProject);
            }

            if (GUILayout.Button("Criar RocketItem"))
            {
                Defer("Criar RocketItem", CreateRocketItem);
            }
        }
    }

    private void DrawBoxesSection()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("2. Caixas de item", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Caixas na cena", _boxCount.ToString());

        _boxPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Prefab da caixa", "Deixa vazio para o tool criar uma caixa simples em Assets/Prefabs."),
            _boxPrefab, typeof(GameObject), false);

        _boxesPerRow = Mathf.Max(1, EditorGUILayout.IntField("Caixas por fila", _boxesPerRow));
        _checkpointStep = Mathf.Max(1, EditorGUILayout.IntField("Uma fila a cada N checkpoints", _checkpointStep));
        _boxSpacing = Mathf.Max(0.5f, EditorGUILayout.FloatField("Distancia entre caixas", _boxSpacing));
        _boxHeight = Mathf.Max(0f, EditorGUILayout.FloatField("Altura acima do chao", _boxHeight));

        if (GUILayout.Button("Criar / atualizar prefab da caixa"))
        {
            Defer("Prefab da caixa", () => EnsureBoxPrefab());
        }

        if (GUILayout.Button("Colocar caixas ao longo dos checkpoints"))
        {
            Defer("Colocar caixas", () =>
            {
                GameObject prefab = EnsureBoxPrefab();

                if (prefab != null)
                {
                    PlaceBoxesAtCheckpoints(prefab);
                }
            });
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Criar 1 caixa na Scene view"))
            {
                Defer("Criar caixa", () =>
                {
                    GameObject prefab = EnsureBoxPrefab();

                    if (prefab != null)
                    {
                        CreateSingleBox(prefab);
                    }
                });
            }

            if (GUILayout.Button("Converter selecao em caixa"))
            {
                Defer("Converter selecao", ConvertSelection);
            }
        }

        if (GUILayout.Button("Aplicar a lista a todas as caixas da cena"))
        {
            Defer("Aplicar lista", ApplyItemsToSceneBoxes);
        }
    }

    private void DrawHudSection()
    {
        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("3. HUD do item", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("RacerHud na cena", _hudCount.ToString());

        _corner = (ItemHudCorner)EditorGUILayout.EnumPopup("Canto do ecra de cada jogador", _corner);
        _slotSize = Mathf.Max(32f, EditorGUILayout.FloatField("Tamanho do slot", _slotSize));
        _margin = Mathf.Max(0f, EditorGUILayout.FloatField("Margem ao canto", _margin));

        if (GUILayout.Button("Criar / atualizar HUD de itens"))
        {
            Defer("HUD de itens", CreateHud);
        }
    }

    // ------------------------------------------------------------ execucao

    // Os dialogos e as alteracoes de cena correm fora do OnGUI para nao partir o layout.
    private void Defer(string title, Action action)
    {
        EditorApplication.delayCall += () =>
        {
            if (this == null)
            {
                return;
            }

            _log.Clear();

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _log.Add("Erro: " + exception.Message + " (ver a Console)");
            }

            RefreshSceneInfo();
            _so.Update();
            Repaint();

            if (_log.Count > 0)
            {
                string message = string.Join("\n", _log);
                Debug.Log($"[Itens e HUD] {title}:\n{message}");
                EditorUtility.DisplayDialog(title, message, "OK");
            }
        };
    }

    private void DoEverything()
    {
        if (ValidItems().Count == 0)
        {
            FindItemsInProject();

            if (ValidItems().Count == 0)
            {
                CreateRocketItem();
            }
        }

        GameObject prefab = EnsureBoxPrefab();

        if (prefab != null)
        {
            ApplyItemsToSceneBoxes();

            if (FindObjectsByType<ItemBox>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                PlaceBoxesAtCheckpoints(prefab);
            }
            else
            {
                _log.Add("Ja existem caixas na cena: nao coloquei mais.");
            }
        }

        CreateHud();

        foreach (KartItem item in ValidItems())
        {
            if (item.Icon == null && item.IconPrefab == null)
            {
                _log.Add($"Aviso: \"{item.name}\" nao tem prefab nem icone; o HUD vai mostrar so o nome.");
            }
        }
    }

    // ---------------------------------------------------------------- itens

    private List<KartItem> ValidItems()
    {
        return _items.Where(i => i != null).Distinct().ToList();
    }

    private void FindItemsInProject()
    {
        int added = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:KartItem"))
        {
            KartItem item = AssetDatabase.LoadAssetAtPath<KartItem>(AssetDatabase.GUIDToAssetPath(guid));

            if (item != null && !_items.Contains(item))
            {
                _items.Add(item);
                added++;
            }
        }

        _items.RemoveAll(i => i == null);
        _log.Add(added > 0 ? $"{added} item(ns) encontrado(s) no projeto." : "Nenhum item novo encontrado no projeto.");
    }

    private void CreateRocketItem()
    {
        EnsureFolder(ItemsFolder);

        string path = AssetDatabase.GenerateUniqueAssetPath(ItemsFolder + "/RocketItem.asset");
        RocketItem asset = CreateInstance<RocketItem>();
        AssetDatabase.CreateAsset(asset, path);

        SerializedObject assetObject = new SerializedObject(asset);
        assetObject.FindProperty("_displayName").stringValue = "Foguete";
        assetObject.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        _items.Add(asset);
        EditorGUIUtility.PingObject(asset);
        _log.Add($"Criei {path}. Preenche o Rocket Prefab nele para o HUD mostrar a foto do foguete.");
    }

    // --------------------------------------------------------------- caixas

    private GameObject EnsureBoxPrefab()
    {
        List<KartItem> items = ValidItems();

        if (items.Count == 0)
        {
            _log.Add("A lista de itens esta vazia. Procura ou cria itens primeiro.");
            return null;
        }

        GameObject prefab = _boxPrefab;

        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
        }

        if (prefab == null)
        {
            prefab = CreateDefaultBoxPrefab(items);
            _log.Add($"Criei o prefab da caixa em {BoxPrefabPath}.");
        }
        else
        {
            ApplyItemsToPrefab(AssetDatabase.GetAssetPath(prefab), items);
            _log.Add($"Atualizei a lista de itens no prefab \"{prefab.name}\".");
        }

        _boxPrefab = prefab;
        return prefab;
    }

    private GameObject CreateDefaultBoxPrefab(List<KartItem> items)
    {
        EnsureFolder(PrefabsFolder);

        GameObject root = new GameObject("ItemBox");
        BoxCollider trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = Vector3.one * 2.2f;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 1.2f;

        Renderer renderer = visual.GetComponent<Renderer>();
        renderer.sharedMaterial = GetOrCreateBoxMaterial(renderer.sharedMaterial);

        ItemBox box = root.AddComponent<ItemBox>();
        SerializedObject boxObject = new SerializedObject(box);
        boxObject.FindProperty("_visual").objectReferenceValue = visual.transform;
        SetItems(boxObject, items);
        boxObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BoxPrefabPath);
        DestroyImmediate(root);
        return prefab;
    }

    private static Material GetOrCreateBoxMaterial(Material template)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(BoxMaterialPath);

        if (existing != null)
        {
            return existing;
        }

        Material material;

        if (template != null)
        {
            material = new Material(template);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
        }

        Color color = new Color(1f, 0.78f, 0.15f);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        AssetDatabase.CreateAsset(material, BoxMaterialPath);
        return material;
    }

    private static void ApplyItemsToPrefab(string path, List<KartItem> items)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);

        try
        {
            ItemBox box = contents.GetComponent<ItemBox>();

            if (box == null)
            {
                if (!contents.TryGetComponent<Collider>(out _))
                {
                    FitCollider(contents.AddComponent<BoxCollider>(), contents);
                }

                box = contents.AddComponent<ItemBox>();
            }

            SerializedObject boxObject = new SerializedObject(box);
            SetItems(boxObject, items);
            boxObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private void ApplyItemsToSceneBoxes()
    {
        List<KartItem> items = ValidItems();
        ItemBox[] boxes = FindObjectsByType<ItemBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (ItemBox box in boxes)
        {
            SerializedObject boxObject = new SerializedObject(box);
            SetItems(boxObject, items);
            boxObject.ApplyModifiedProperties();
        }

        if (boxes.Length > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
        }

        _log.Add($"Lista aplicada a {boxes.Length} caixa(s) da cena.");
    }

    private void PlaceBoxesAtCheckpoints(GameObject prefab)
    {
        List<RaceCheckpoint> checkpoints = GetCheckpoints();

        if (checkpoints.Count < 2)
        {
            _log.Add("O RaceManager nao tem checkpoints, por isso nao coloquei caixas. Usa \"Criar 1 caixa na Scene view\".");
            return;
        }

        GameObject existing = GameObject.Find(BoxesRootName);

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = new GameObject(BoxesRootName);
        Undo.RegisterCreatedObjectUndo(root, UndoName);

        int placed = 0;

        for (int i = _checkpointStep; i < checkpoints.Count; i += _checkpointStep)
        {
            Transform checkpoint = checkpoints[i].transform;

            for (int b = 0; b < _boxesPerRow; b++)
            {
                float offset = (b - (_boxesPerRow - 1) * 0.5f) * _boxSpacing;
                Vector3 position = SnapToGround(checkpoint.position + checkpoint.right * offset);

                GameObject box = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                Undo.RegisterCreatedObjectUndo(box, UndoName);
                box.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, checkpoint.eulerAngles.y, 0f));
                placed++;
            }
        }

        EditorSceneManager.MarkAllScenesDirty();

        _log.Add(placed > 0
            ? $"Coloquei {placed} caixa(s) em \"{BoxesRootName}\". Podes mover cada uma a vontade."
            : "Nao coloquei caixas: aumenta o numero de checkpoints ou diminui o \"a cada N checkpoints\".");
    }

    private void CreateSingleBox(GameObject prefab)
    {
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 position = SnapToGround(view != null ? view.pivot : Vector3.zero);

        GameObject box = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(box, UndoName);
        box.transform.position = position;

        Selection.activeGameObject = box;
        EditorSceneManager.MarkAllScenesDirty();
        _log.Add("Caixa criada no centro da Scene view.");
    }

    private void ConvertSelection()
    {
        List<KartItem> items = ValidItems();
        int converted = 0;

        foreach (GameObject target in Selection.gameObjects)
        {
            if (!target.scene.IsValid())
            {
                continue;
            }

            ItemBox box = target.GetComponent<ItemBox>();

            if (box == null)
            {
                if (!target.TryGetComponent<Collider>(out _))
                {
                    FitCollider(Undo.AddComponent<BoxCollider>(target), target);
                }

                box = Undo.AddComponent<ItemBox>(target);
                converted++;
            }

            SerializedObject boxObject = new SerializedObject(box);
            SetItems(boxObject, items);
            boxObject.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkAllScenesDirty();
        _log.Add(converted > 0
            ? $"{converted} objeto(s) convertido(s) em ItemBox."
            : "Seleciona primeiro objetos da cena (os que ja sao caixas so recebem a lista).");
    }

    private Vector3 SnapToGround(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * _boxHeight;
        }

        return position + Vector3.up * _boxHeight;
    }

    private static List<RaceCheckpoint> GetCheckpoints()
    {
        List<RaceCheckpoint> result = new List<RaceCheckpoint>();
        RaceManager manager = FindFirstObjectByType<RaceManager>();

        if (manager == null)
        {
            return result;
        }

        SerializedProperty array = new SerializedObject(manager).FindProperty("_checkpoints");

        for (int i = 0; i < array.arraySize; i++)
        {
            RaceCheckpoint checkpoint = array.GetArrayElementAtIndex(i).objectReferenceValue as RaceCheckpoint;

            if (checkpoint != null)
            {
                result.Add(checkpoint);
            }
        }

        return result;
    }

    private static void FitCollider(BoxCollider collider, GameObject root)
    {
        collider.isTrigger = true;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            collider.size = Vector3.one * 2f;
            return;
        }

        Bounds bounds = renderers[0].bounds;

        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        Vector3 scale = root.transform.lossyScale;
        collider.center = root.transform.InverseTransformPoint(bounds.center);
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z))) * 1.25f;
    }

    private static void SetItems(SerializedObject boxObject, List<KartItem> items)
    {
        SerializedProperty list = boxObject.FindProperty("_items");
        list.arraySize = items.Count;

        for (int i = 0; i < items.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    // ------------------------------------------------------------------ HUD

    private void CreateHud()
    {
        RacerHud[] huds = FindObjectsByType<RacerHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (huds.Length == 0)
        {
            _log.Add("Nao ha nenhum RacerHud na cena. Cria primeiro o canvas com o setup do RaceManager e volta a correr isto.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Criar HUD de itens");

        foreach (RacerHud hud in huds)
        {
            BuildItemHud(hud, _corner, _slotSize, _margin);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkAllScenesDirty();
        _log.Add($"HUD de itens criado em {huds.Length} RacerHud. Os elementos que ja la estavam ficaram como estavam.");
    }

    public static void BuildItemHud(RacerHud hud, ItemHudCorner corner, float slotSize, float margin)
    {
        SerializedObject hudObject = new SerializedObject(hud);
        SerializedProperty itemHudProperty = hudObject.FindProperty("_itemHud");

        // So apaga o ItemHud que este tool criou antes (o que tem KartItemHud).
        KartItemHud previous = itemHudProperty.objectReferenceValue as KartItemHud;

        if (previous == null)
        {
            Transform child = hud.transform.Find(HudObjectName);

            if (child != null)
            {
                previous = child.GetComponent<KartItemHud>();
            }
        }

        if (previous != null)
        {
            Undo.DestroyObjectImmediate(previous.gameObject);
        }

        Sprite frameSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        RectTransform slot = CreateRect(HudObjectName, hud.transform);
        bool top = corner == ItemHudCorner.TopLeft || corner == ItemHudCorner.TopRight;
        bool right = corner == ItemHudCorner.TopRight || corner == ItemHudCorner.BottomRight;
        Vector2 anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
        slot.anchorMin = anchor;
        slot.anchorMax = anchor;
        slot.pivot = anchor;
        slot.sizeDelta = Vector2.one * slotSize;
        slot.anchoredPosition = new Vector2(right ? -margin : margin, top ? -margin : margin);

        Image frame = slot.gameObject.AddComponent<Image>();
        frame.sprite = frameSprite;
        frame.type = Image.Type.Sliced;
        frame.color = new Color(0f, 0f, 0f, 0.45f);
        frame.raycastTarget = false;

        RectTransform iconRect = CreateRect("Icon", slot);
        Stretch(iconRect, slotSize * 0.1f);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;

        RectTransform labelRect = CreateRect("Label", slot);
        Stretch(labelRect, slotSize * 0.08f);
        TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 28f;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = string.Empty;

        RectTransform nextRect = CreateRect("NextSlot", slot);
        nextRect.anchorMin = new Vector2(1f, 0f);
        nextRect.anchorMax = new Vector2(1f, 0f);
        nextRect.pivot = new Vector2(1f, 0f);
        nextRect.sizeDelta = Vector2.one * (slotSize * 0.42f);
        nextRect.anchoredPosition = new Vector2(-slotSize * 0.04f, slotSize * 0.04f);

        Image nextFrame = nextRect.gameObject.AddComponent<Image>();
        nextFrame.sprite = frameSprite;
        nextFrame.type = Image.Type.Sliced;
        nextFrame.color = new Color(0f, 0f, 0f, 0.7f);
        nextFrame.raycastTarget = false;

        RectTransform nextIconRect = CreateRect("Icon", nextRect);
        Stretch(nextIconRect, slotSize * 0.04f);
        Image nextIcon = nextIconRect.gameObject.AddComponent<Image>();
        nextIcon.preserveAspect = true;
        nextIcon.raycastTarget = false;
        nextIcon.enabled = false;

        nextRect.gameObject.SetActive(false);

        KartItemHud itemHud = slot.gameObject.AddComponent<KartItemHud>();
        SerializedObject itemHudObject = new SerializedObject(itemHud);
        itemHudObject.FindProperty("_currentIcon").objectReferenceValue = icon;
        itemHudObject.FindProperty("_currentLabel").objectReferenceValue = label;
        itemHudObject.FindProperty("_nextSlot").objectReferenceValue = nextRect.gameObject;
        itemHudObject.FindProperty("_nextIcon").objectReferenceValue = nextIcon;
        itemHudObject.ApplyModifiedPropertiesWithoutUndo();

        hudObject.Update();
        hudObject.FindProperty("_itemHud").objectReferenceValue = itemHud;
        hudObject.ApplyModifiedProperties();
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Criar HUD de itens");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    // -------------------------------------------------------------- helpers

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
