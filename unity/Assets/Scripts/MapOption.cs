using UnityEngine;

[System.Serializable]
public class MapOption
{
    public string displayName;
    public string sceneName;
    [TextArea(2, 4)]
    public string description;
    public Sprite previewSprite;
    public bool isRecommendedForVR = true;
    public string renderPipelineType = "URP"; // URP or HDRP
}
