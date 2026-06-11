using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using GridPuzzle.Data;

namespace GridPuzzle.Editor
{
    public class LevelImporter : EditorWindow
    {
        [MenuItem("GridPuzzle/Import Level JSON")]
        // Imports generated JSON and overwrites the single assignment level asset.
        public static void ImportLevel()
        {
            string path = EditorUtility.OpenFilePanel("Select Level JSON", "Tool/Output", "json");
            if (string.IsNullOrEmpty(path)) return;

            string jsonContent = File.ReadAllText(path);
            LevelDataWrapper wrapper = JsonUtility.FromJson<LevelDataWrapper>(jsonContent);

            if (wrapper == null)
            {
                Debug.LogError("Failed to parse JSON Level Data.");
                return;
            }

            // Create or Overwrite Level_1 Asset
            string savePath = "Assets/Resources/Levels/Level_1.asset";
            LevelData levelAsset = AssetDatabase.LoadAssetAtPath<LevelData>(savePath);
            
            if (levelAsset == null)
            {
                levelAsset = ScriptableObject.CreateInstance<LevelData>();
                if (!Directory.Exists("Assets/Resources/Levels"))
                {
                    Directory.CreateDirectory("Assets/Resources/Levels");
                }
                AssetDatabase.CreateAsset(levelAsset, savePath);
            }

            levelAsset.levelName = "Level_1";
            levelAsset.gridSize = new Vector2Int(wrapper.gridSize.x, wrapper.gridSize.y);
            
            // Map voxels correctly
            levelAsset.voxels = new List<VoxelData>();
            foreach (var v in wrapper.voxels)
            {
                VoxelData data = new VoxelData(v.uniqueID, (ColorType)v.colorType, new Vector2Int(v.gridPosition.x, v.gridPosition.y));
                data.exposedFaces = (ExposeDirection)v.exposedFaces;
                data.isAbsorbed = v.isAbsorbed;
                levelAsset.voxels.Add(data);
            }

            // Convert palette objects to Unity Colors
            if (wrapper.palette != null && wrapper.palette.Count > 0)
            {
                levelAsset.palette = new Color[wrapper.palette.Count];
                for (int i = 0; i < wrapper.palette.Count; i++)
                {
                    RGBData rgb = wrapper.palette[i];
                    levelAsset.palette[i] = new Color(rgb.r / 255f, rgb.g / 255f, rgb.b / 255f);
                }
                Debug.Log($"Successfully imported {wrapper.palette.Count} palette colors.");
            }
            else
            {
                Debug.LogWarning("No palette data found in JSON!");
            }

            EditorUtility.SetDirty(levelAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = levelAsset;

            Debug.Log($"Level imported and unified as Level_1 at: {savePath}");
        }

        [System.Serializable]
        private class RGBData
        {
            public int r;
            public int g;
            public int b;
        }

        [System.Serializable]
        private class Vector2IntData
        {
            public int x;
            public int y;
        }

        [System.Serializable]
        private class VoxelDataWrapper
        {
            public string uniqueID;
            public int colorType;
            public Vector2IntData gridPosition;
            public int exposedFaces;
            public bool isAbsorbed;
        }

        [System.Serializable]
        private class LevelDataWrapper
        {
            public string levelName;
            public Vector2IntData gridSize;
            public List<RGBData> palette;
            public List<VoxelDataWrapper> voxels;
        }
    }
}
