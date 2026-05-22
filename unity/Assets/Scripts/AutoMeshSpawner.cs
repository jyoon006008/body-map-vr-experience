using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;

public class AutoMeshSpawner : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Settings")]
    public string meshFolder = "GeneratedMeshes";
    public float spawnDistance = 10f;
    public float spawnScale = 4f;

    [Header("Material Fix")]
    public Material fallbackMaterial;

    private GameObject currentSpawnedObject;

    void Start()
    {
        SpawnLatestMesh();
    }

    [ContextMenu("Spawn Latest Mesh")]
    public void SpawnLatestMesh()
    {
        if (player == null)
        {
            Debug.LogError("Player reference is missing.");
            return;
        }

        string fullMeshFolderPath = Path.Combine(Application.dataPath, meshFolder);

        if (!Directory.Exists(fullMeshFolderPath))
        {
            Debug.LogWarning("Mesh folder not found: " + fullMeshFolderPath);
            return;
        }

        string[] files = Directory.GetFiles(fullMeshFolderPath, "*.glb");

        if (files.Length == 0)
        {
            Debug.LogWarning("No .glb files found in GeneratedMeshes.");
            return;
        }

        string latestFile = GetLatestFile(files);
        string latestAssetPath = "Assets/" + meshFolder + "/" + Path.GetFileName(latestFile);

        if (currentSpawnedObject != null)
        {
            DestroyImmediate(currentSpawnedObject);
            currentSpawnedObject = null;
        }

#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(latestAssetPath);
#else
        GameObject prefab = null;
#endif

        if (prefab == null)
        {
            Debug.LogError("Failed to load mesh prefab: " + latestAssetPath);
            return;
        }

        Vector3 forward = player.forward;
        forward.y = 0f;

        if (forward.magnitude < 0.001f)
            forward = player.transform.forward;

        forward.Normalize();

        Vector3 spawnPosition = player.position + forward * spawnDistance;
        spawnPosition.y = 0f;

        currentSpawnedObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
        currentSpawnedObject.name = Path.GetFileNameWithoutExtension(latestFile);

        currentSpawnedObject.transform.localScale *= spawnScale;

        SnapObjectBottomToGround(currentSpawnedObject, 0f);

        ApplyFallbackMaterial(currentSpawnedObject);

        Debug.Log("Spawned latest mesh: " + latestAssetPath);
    }

    string GetLatestFile(string[] files)
    {
        string latestFile = files[0];
        System.DateTime latestTime = File.GetLastWriteTime(latestFile);

        foreach (string file in files)
        {
            System.DateTime time = File.GetLastWriteTime(file);

            if (time > latestTime)
            {
                latestTime = time;
                latestFile = file;
            }
        }

        return latestFile;
    }

    void ApplyFallbackMaterial(GameObject obj)
    {
        if (fallbackMaterial == null)
        {
            fallbackMaterial = new Material(Shader.Find("Standard"));
            fallbackMaterial.color = Color.gray;
        }

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        foreach (Renderer r in renderers)
        {
            r.sharedMaterial = fallbackMaterial;
        }
    }

    void SnapObjectBottomToGround(GameObject obj, float groundY)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("No renderers found for ground snapping.");
            return;
        }

        Bounds bounds = renderers[0].bounds;

        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        float bottomY = bounds.min.y;
        float offsetY = groundY - bottomY;

        obj.transform.position += new Vector3(0f, offsetY, 0f);
    }
}