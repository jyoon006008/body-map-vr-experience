using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ObjectificationJsonExporter : MonoBehaviour
{
    public static ObjectificationJsonExporter Instance { get; private set; }

    [Serializable]
    public class SafePlaceRecord
    {
        public string userName = "";
        public string description = "";
        public string panoramaPrompt = "";
        public string skyboxAssetPath = "";
    }

    [Serializable]
    public class RegionRecord
    {
        public int regionId;
        public string bodyLocation = "";
        public string bodySide = "";
        public string color = "";
        public string colorHex = "";
        public string pattern = "";
        public string sourceDescription = "";
        public string emotion = "";
        public string texture = "";
        public string metaphor = "";
        public string movement = "";
        public string shape = "";
        public string generatedPrompt = "";
        public string completionState = "imported";
    }

    [Serializable]
    public class SessionRecord
    {
        public string sessionId = "";
        public string language = "ko";
        public SafePlaceRecord safePlace = new SafePlaceRecord();
        public List<RegionRecord> regions = new List<RegionRecord>();
    }

    public SessionRecord session = new SessionRecord();
    public string lastSavedPath = "";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSession();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static ObjectificationJsonExporter GetOrCreate()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("ObjectificationJsonExporter");
        return go.AddComponent<ObjectificationJsonExporter>();
    }

    public void InitializeSession()
    {
        if (string.IsNullOrEmpty(session.sessionId))
            session.sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        session.language = GameManager.Instance != null ? GameManager.Instance.currentLanguage : "ko";

        var safePlace = SafePlaceSessionData.Instance;
        if (safePlace != null)
        {
            session.safePlace.userName = safePlace.userName;
            session.safePlace.description = safePlace.description;
            session.safePlace.panoramaPrompt = safePlace.panoramaPrompt;
            session.safePlace.skyboxAssetPath = safePlace.skyboxAssetPath;
        }
    }

    public void RegisterImportedRegions(IEnumerable<InteractiveRegion3D> regions)
    {
        InitializeSession();
        foreach (var region in regions)
        {
            if (region == null) continue;
            RegionRecord record = GetOrCreateRegion(region.id);
            record.bodyLocation = region.bodyLocation;
            record.color = region.colorName;
            record.colorHex = region.colorHex;
            record.sourceDescription = region.description;
            record.pattern = GuessPattern(region.description);
            if (string.IsNullOrEmpty(record.completionState)) record.completionState = "imported";
        }
        Save();
    }

    public void UpdateRegionFromProfile(InteractiveRegion3D region, BodyMapAIController.EmotionProfile profile)
    {
        if (region == null || profile == null) return;

        InitializeSession();
        RegionRecord record = GetOrCreateRegion(region.id);
        record.bodyLocation = region.bodyLocation;
        record.color = region.colorName;
        record.colorHex = region.colorHex;
        record.sourceDescription = region.description;
        record.pattern = string.IsNullOrEmpty(record.pattern) ? GuessPattern(region.description) : record.pattern;
        record.emotion = profile.emotionDescription;
        record.texture = profile.textureDescription;
        record.movement = profile.movementDescription;
        record.shape = profile.shapeDescription;
        record.metaphor = profile.relationshipDescription;
        record.generatedPrompt = profile.finalPrompt;
        record.completionState = string.IsNullOrEmpty(profile.finalPrompt) ? "partial" : "complete";
        Save();
    }

    public RegionRecord GetOrCreateRegion(int id)
    {
        foreach (var record in session.regions)
        {
            if (record.regionId == id) return record;
        }

        RegionRecord newRecord = new RegionRecord { regionId = id };
        session.regions.Add(newRecord);
        return newRecord;
    }

    public bool IsRegionComplete(int id)
    {
        foreach (var record in session.regions)
        {
            if (record.regionId == id) return record.completionState == "complete";
        }
        return false;
    }

    public void Save()
    {
        InitializeSession();
        string dir = Path.Combine(Application.persistentDataPath, "ObjectificationSessions");
        Directory.CreateDirectory(dir);
        lastSavedPath = Path.Combine(dir, $"objectification_{session.sessionId}.json");
        File.WriteAllText(lastSavedPath, JsonUtility.ToJson(session, true));
        Debug.Log($"[ObjectificationJsonExporter] Saved JSON: {lastSavedPath}");
    }

    private string GuessPattern(string description)
    {
        if (string.IsNullOrEmpty(description)) return "colored body-map region";
        string lower = description.ToLowerInvariant();
        if (lower.Contains("circle") || lower.Contains("round")) return "rounded colored strokes";
        if (lower.Contains("line")) return "linear colored strokes";
        if (lower.Contains("dense")) return "dense colored strokes";
        return "imported colored body-map region";
    }
}
