using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ItemIconRenderer : MonoBehaviour
{
    private const int Size = 256;

    private static readonly Vector3 StudioPosition = new Vector3(0f, 5000f, 0f);

    private static readonly Quaternion ViewRotation = Quaternion.Euler(25f, 145f, 0f);

    private static readonly Dictionary<KartItem, Sprite> Cache = new Dictionary<KartItem, Sprite>();
    private static readonly HashSet<KartItem> Failed = new HashSet<KartItem>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Cache.Clear();
        Failed.Clear();
    }

    public static Sprite GetIcon(KartItem item)
    {
        if (item == null)
        {
            return null;
        }

        if (item.Icon != null)
        {
            return item.Icon;
        }

        if (Cache.TryGetValue(item, out Sprite cached) && cached != null)
        {
            return cached;
        }

        if (Failed.Contains(item))
        {
            return null;
        }

        Sprite sprite = Capture(item);

        if (sprite != null)
        {
            Cache[item] = sprite;
        }
        else
        {
            Failed.Add(item);
            Debug.LogWarning($"ItemIconRenderer: nao consegui criar o icone do item \"{item.name}\". Preenche o Icon Prefab (ou o Rocket Prefab) no item.", item);
        }

        return sprite;
    }

    private static Sprite Capture(KartItem item)
    {
        string itemName = item.name;
        GameObject prefab = item.IconPrefab;

        GameObject stage = new GameObject("ItemIconStage");
        stage.SetActive(false);
        stage.transform.position = StudioPosition;

        GameObject rig = null;
        RenderTexture target = null;

        try
        {
            GameObject model;

            if (prefab != null)
            {
                model = Object.Instantiate(prefab, stage.transform);
            }
            else
            {
                model = item.CreateFallbackModel();

                if (model == null)
                {
                    return null;
                }

                model.transform.SetParent(stage.transform, false);
            }

            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.SetActive(true);

            Strip(model);
            stage.SetActive(true);

            if (!TryGetBounds(model, out Bounds bounds))
            {
                Debug.LogWarning($"ItemIconRenderer: o prefab do item \"{itemName}\" nao tem renderers visiveis.");
                return null;
            }

            float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);

            rig = new GameObject("ItemIconCamera");
            Camera camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = radius * 1.02f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = radius * 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = ~0;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.transform.SetPositionAndRotation(
                bounds.center - (ViewRotation * Vector3.forward) * radius * 3f,
                ViewRotation);

            GameObject lightObject = new GameObject("ItemIconLight");
            lightObject.transform.SetParent(rig.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.8f;
            light.shadows = LightShadows.None;

            target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            camera.targetTexture = target;

            RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = target };

            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.Render();
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.name = $"{itemName}_Icon";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            texture.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
            texture.Apply(false, true);

            RenderTexture.active = previous;
            camera.targetTexture = null;

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }
        finally
        {
            if (target != null)
            {
                RenderTexture.ReleaseTemporary(target);
            }

            if (rig != null)
            {
                Object.DestroyImmediate(rig);
            }

            Object.DestroyImmediate(stage);
        }
    }

    private static bool TryGetBounds(GameObject model, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private static void Strip(GameObject model)
    {
        bool previousLogState = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;

        try
        {
            for (int pass = 0; pass < 6; pass++)
            {
                int remaining = 0;
                remaining += DestroyAll<MonoBehaviour>(model);
                remaining += DestroyAll<Joint>(model);
                remaining += DestroyAll<Collider>(model);
                remaining += DestroyAll<Rigidbody>(model);
                remaining += DestroyAll<AudioSource>(model);
                remaining += DestroyAll<Light>(model);
                remaining += DestroyAll<ParticleSystemRenderer>(model);
                remaining += DestroyAll<ParticleSystem>(model);

                if (remaining == 0)
                {
                    break;
                }
            }
        }
        finally
        {
            Debug.unityLogger.logEnabled = previousLogState;
        }
    }

    private static int DestroyAll<T>(GameObject root) where T : Component
    {
        int destroyed = 0;

        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component == null)
            {
                continue;
            }

            Object.DestroyImmediate(component);
            destroyed++;
        }

        return destroyed;
    }
}