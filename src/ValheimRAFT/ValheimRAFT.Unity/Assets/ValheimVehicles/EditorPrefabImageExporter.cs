using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// [ExecuteInEditMode]
public class EditorPrefabImageExporter : MonoBehaviour
{
    private void Awake()
    {
        // gameObject.AddComponent<EditorPrefabImageExporter>();
        // BuildPrefabImages();
    }

    void Start()
    {
        // BuildPrefabImages();
    }
    // Update is called once per frame
    void Update()
    {
    }

    private List<Texture2D> thumbnails = new List<Texture2D>();
    private const int pngDimensions = 64; 
    
    private void WriteFileAsPng(Texture2D texture2d, string objName)
    {
        if (!texture2d)
        {
            thumbnails.Remove(texture2d);
            return;
        }
        // Texture2D texture = new Texture2D(pngDimensions, pngDimensions, TextureFormat.RGB24, false);
         
        //then Save To Disk as PNG
        byte[] bytes = texture2d.EncodeToPNG();
        var dirPath = Application.dataPath + "/../ValheimVehicles/Generated/Icons";
        if(!Directory.Exists(dirPath)) {
            Directory.CreateDirectory(dirPath);
        }
        File.WriteAllBytes(dirPath + objName + ".png", bytes);
    }

    private void BuildPrefabImages()
    {
        var childObjects = gameObject.GetComponentsInChildren<Transform>();
        
        foreach (var childObject in childObjects)
        {
            if (childObject.parent == transform)
            {
                var miniThumbnail = AssetPreview.GetAssetPreview(childObject);
                // var png = miniThumbnail.EncodeToPNG();
                var pixels = miniThumbnail.GetPixels();
                Debug.Log(pixels);
                Debug.Log($"PNG, minithumbnail: {miniThumbnail.isReadable}");
                // thumbnails.Add(miniThumbnail);
                // WriteFileAsPng(miniThumbnail, childObject.name);
            }
        }
        
        // foreach (var texture2d in thumbnails.ToList())
        // {
        // }
    } 
}
