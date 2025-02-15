using System;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif
using UnityEngine;

public class ItemDropSetupPage : ScriptableObject
{
#if UNITY_EDITOR
    [TextArea(1, 10)]
    public string title;

    [TextArea(1, 10)]    
    public string description;

    [TextArea(1, 10)]
    public string path;

    [TextArea(1, 10)]
    public string fileName;

    [Serializable]
    public struct Text
    {
        public bool installed;
        [TextArea(1, 10)]
        public string search, replace;
    }
    [Header("Text")]
    public Text[] text;

    static ItemDropSetupPage[] classicCache;
    public static ItemDropSetupPage[] ClassicAll => classicCache ?? (classicCache = FindResources<ItemDropSetupPage>("Assets/uMMORPG/Scripts/Addons/ItemDrop/ItemDropSetup/Classic"));
    //public static ItemDropSetupPage[] ClassicAll => classicCache ?? (classicCache = Resources.LoadAll<ItemDropSetupPage>("ItemDropSetup/Classic").OrderBy(p => p.name.Length).ThenBy(p => p.name).ToArray());

    static ItemDropSetupPage[] remasteredCache;    
    public static ItemDropSetupPage[] RemasteredAll => remasteredCache ?? (remasteredCache = FindResources<ItemDropSetupPage>("Assets/uMMORPG/Scripts/Addons/ItemDrop/ItemDropSetup/Remastered"));
    //public static ItemDropSetupPage[] RemasteredAll => remasteredCache ?? (remasteredCache = Resources.LoadAll<ItemDropSetupPage>("ItemDropSetup/Remastered").OrderBy(p => p.name.Length).ThenBy(p => p.name).ToArray());

    /// <summary>
    /// Returns all pages from the selected folder required for the installation.
    /// </summary>
    public static T[] FindResources<T>(string path) where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { path });
        return guids.Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
    }
#endif
}
