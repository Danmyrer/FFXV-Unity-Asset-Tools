using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;   
using UnityEditor;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class FFXVAssetTools : EditorWindow
{
    private static bool _customExtractionDir;
    private static string _customExtractionDirPath = "";

    private static bool _customTextureDir;
    private static string _customTextureDirPath = "Assets/FFXV/Textures"; // TODO in config auslagern

    private static bool _showMaterialProperties;
    private static bool _showAssignmentNames;

    private const string ProgressBarAssignString = "FFXV Asset Tools Texture Assign";

    private static bool _highlightMissingTexture;
    private const string MissingTextureTexturePath = "Assets/FFXVAssetTools/Ressources/missing_texture.png";
    private static Texture _missingTextureTexture;

    private const string AggressiveAssignTooltip =
        "Use aggressive assignment. Assigns textures to similar materials, even if they are not spelled the same (can lead to wrong assignments)";
    private static bool _aggressiveAssign;
    private static int _aggressiveAssignThreshold = 3;
    
    private enum MaterialType
    {
        Diffuse,
        DiffuseAlpha,
        Normal,
        Occlusion,
        Metalness,
        Roughness,
        // Specular,
        Emissive
    }

    private static readonly Dictionary<MaterialType, string> ShaderProperty = new Dictionary<MaterialType, string>()
    {
        { MaterialType.Diffuse , "_MainTex" },
        { MaterialType.DiffuseAlpha , "_MainTex" },
        { MaterialType.Normal , "_BumpMap" },
        { MaterialType.Occlusion , "_OcclusionMap" },
        { MaterialType.Metalness , "_MetallicGlossMap" },
        { MaterialType.Roughness , "_SpecGlossMap" },
        { MaterialType.Emissive , "_EmissionMap" }
    };

    private static readonly Dictionary<MaterialType, string> AssignmentNames = new Dictionary<MaterialType, string>()
    {
        { MaterialType.Diffuse , "_b_$h" },
        { MaterialType.DiffuseAlpha , "_ba_$h" },
        { MaterialType.Normal , "_n_$h" },
        { MaterialType.Occlusion , "_o_$h" },
        { MaterialType.Metalness , "_m_$h" },
        { MaterialType.Roughness , "_r_$h" },
        { MaterialType.Emissive , "_e_$h" }
    };

    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int MetallicGlossMap = Shader.PropertyToID("_MetallicGlossMap");
    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
    private static readonly int ParallaxMap = Shader.PropertyToID("_ParallaxMap");
    private static readonly int OcclusionMap = Shader.PropertyToID("_OcclusionMap");
    private static readonly int DetailMask = Shader.PropertyToID("_DetailMask");
    private static readonly int DetailAlbedoMap = Shader.PropertyToID("_DetailAlbedoMap");
    private static readonly int DetailNormalMap = Shader.PropertyToID("_DetailNormalMap");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");

    [MenuItem("Window/FFXV Asset Tools")]
    public static void ShowWindow()
    {
        GetWindow<FFXVAssetTools>("FFXV Asset Tools");
    }
    
    private void OnGUI()
    {
        // Window code
        GUILayout.Label("FFXV Asset Tools", EditorStyles.whiteLargeLabel);
        ExtractMaterialsGUI();
        AssignTexturesGUI();
    }

    private void ExtractMaterialsGUI()
    {
        GUILayout.Label("Material Extractor Settings", EditorStyles.boldLabel);
        CustomDirGUI(
            ref _customExtractionDir, 
            ref _customExtractionDirPath, 
            "extraction", 
            "Materials will be extracted to the selected assets directory."
        );
    }

    private void AssignTexturesGUI()
    {
        GUILayout.Label("Texture Assign Settings", EditorStyles.boldLabel);
        CustomDirGUI(
            ref _customTextureDir, 
            ref _customTextureDirPath, 
            "texture", 
            "Textures in the selected directory will be assigned"
        );
        
        _showMaterialProperties = EditorGUILayout.Foldout(_showMaterialProperties, "Show material properties");
        if (_showMaterialProperties)
        {
            #region properties
            
            EditorGUI.indentLevel++;
            ShaderProperty[MaterialType.Diffuse] = EditorGUILayout.TextField(MaterialType.Diffuse.ToString(),
                ShaderProperty[MaterialType.Diffuse]);
            ShaderProperty[MaterialType.Normal] = EditorGUILayout.TextField(MaterialType.Normal.ToString(),
                ShaderProperty[MaterialType.Normal]);
            ShaderProperty[MaterialType.Occlusion] = EditorGUILayout.TextField(MaterialType.Occlusion.ToString(),
                ShaderProperty[MaterialType.Occlusion]);
            ShaderProperty[MaterialType.Metalness] = EditorGUILayout.TextField(MaterialType.Metalness.ToString(),
                ShaderProperty[MaterialType.Metalness]);
            ShaderProperty[MaterialType.Roughness] = EditorGUILayout.TextField(MaterialType.Roughness.ToString(),
                ShaderProperty[MaterialType.Roughness]);
            ShaderProperty[MaterialType.Emissive] = EditorGUILayout.TextField(MaterialType.Emissive.ToString(),
                ShaderProperty[MaterialType.Emissive]);
            EditorGUI.indentLevel--;
            
            #endregion
        }
        
        _showAssignmentNames = EditorGUILayout.Foldout(_showAssignmentNames, "Show assignment names");
        if (_showAssignmentNames)
        {
            #region names
            
            EditorGUI.indentLevel++;
            AssignmentNames[MaterialType.Diffuse] = EditorGUILayout.TextField(MaterialType.Diffuse.ToString(),
                AssignmentNames[MaterialType.Diffuse]);
            AssignmentNames[MaterialType.DiffuseAlpha] = EditorGUILayout.TextField(MaterialType.Diffuse.ToString(),
                AssignmentNames[MaterialType.DiffuseAlpha]);
            AssignmentNames[MaterialType.Normal] = EditorGUILayout.TextField(MaterialType.Normal.ToString(),
                AssignmentNames[MaterialType.Normal]);
            AssignmentNames[MaterialType.Occlusion] = EditorGUILayout.TextField(MaterialType.Occlusion.ToString(),
                AssignmentNames[MaterialType.Occlusion]);
            AssignmentNames[MaterialType.Metalness] = EditorGUILayout.TextField(MaterialType.Metalness.ToString(),
                AssignmentNames[MaterialType.Metalness]);
            AssignmentNames[MaterialType.Roughness] = EditorGUILayout.TextField(MaterialType.Roughness.ToString(),
                AssignmentNames[MaterialType.Roughness]);
            AssignmentNames[MaterialType.Emissive] = EditorGUILayout.TextField(MaterialType.Emissive.ToString(),
                AssignmentNames[MaterialType.Emissive]);
            EditorGUI.indentLevel--;
            
            #endregion
        }

        _highlightMissingTexture =
            EditorGUILayout.Toggle("Use 'empty' texture", _highlightMissingTexture);

        _aggressiveAssign =
            EditorGUILayout.Toggle(new GUIContent("Assign aggressively", AggressiveAssignTooltip), _aggressiveAssign);

        _aggressiveAssignThreshold =
            EditorGUILayout.DelayedIntField(new GUIContent("Assign threshold", "todo"), _aggressiveAssignThreshold);
    }

    private void CustomDirGUI(ref bool custom, ref string customPath, string dirType, string infoMsg)
    {
        custom = EditorGUILayout.Toggle($"Custom {dirType} Path", custom);
        if (custom)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            customPath = EditorGUILayout.TextField("Directory", customPath);
            if (GUILayout.Button("select", GUILayout.Width(75)))
            {
                customPath = EditorUtility.OpenFolderPanel($"Select {dirType} directory", "", "");

                if (customPath.StartsWith(Application.dataPath))
                {
                    customPath = "Assets" + customPath.Substring(Application.dataPath.Length);
                }

                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }
        else
        {
            EditorGUILayout.HelpBox(infoMsg, MessageType.Info);
        }
    }

    #region ExtractMaterials
    
    [MenuItem("Assets/FFXV Asset Tools/Extract Materials")]
    private static void ExtractMaterials()
    {
        List<string> paths = new List<string>();
        
        // handle selection
        foreach (string assetGUID in Selection.assetGUIDs)
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(assetGUID));
        }
        
        ExtractMaterials(paths);
    }

    private static void ExtractMaterials(List<string> paths)
    {
        foreach (string path in paths)
        {
            // handle selected folder
            if (!Path.HasExtension(path))
            {
                DirectoryInfo info = new DirectoryInfo(path);
                FileInfo[] fileInfos = info.GetFiles();
                List<string> folderPaths = new List<string>();
                
                foreach (FileInfo fileInfo in fileInfos)
                {
                    folderPaths.Add(path + '/' + fileInfo.Name);
                    ExtractMaterials(folderPaths);
                }
            }
            
            // handle selected fbx
            if (path.EndsWith(".fbx"))
            {
                string destPath = _customExtractionDir ? _customExtractionDirPath : Path.GetDirectoryName(path);
                if (!Directory.Exists(destPath))
                {
                    Debug.LogError("The destination path '" + destPath + "' does not exist.");
                    return;
                }
                ExtractMaterials(path, destPath);
            }
        }
    }

    /// <summary>
    /// <para>
    /// Extract Materials from fbx via script
    /// </para>
    /// Method written by @Exa-Adoran (http://answers.unity.com/answers/1672287/view.html)
    /// </summary>
    /// <param name="assetPath">Path of the selected fbx</param>
    /// <param name="destinationPath">Destination directory of the materials</param>
    private static void ExtractMaterials(string assetPath, string destinationPath)
    {
        HashSet<string> hashSet = new HashSet<string>();
        IEnumerable<Object> enumerable = from x in AssetDatabase.LoadAllAssetsAtPath(assetPath)
            where x.GetType() == typeof(Material)
            select x;
        foreach (Object item in enumerable)
        {
            string path = Path.Combine(destinationPath, item.name) + ".mat";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            string value = AssetDatabase.ExtractAsset(item, path);
            if (string.IsNullOrEmpty(value))
            {
                hashSet.Add(assetPath);
            }
        }
 
        foreach (string item2 in hashSet)
        {
            AssetDatabase.WriteImportSettingsIfDirty(item2);
            AssetDatabase.ImportAsset(item2, ImportAssetOptions.ForceUpdate);
        }
    }
    
    #endregion
    
    #region AssignMaterials
    
    [MenuItem("Assets/FFXV Asset Tools/Assign Materials")]
    private static void AssignMaterials()
    {
        List<string> paths = new List<string>();
        
        // handle selection
        foreach (string assetGUID in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGUID);
            
            // handle selected folder
            if (AssetDatabase.IsValidFolder(path))
            {
                DirectoryInfo info = new DirectoryInfo(path);
                FileInfo[] fileInfos = info.GetFiles();
                foreach (FileInfo fileInfo in fileInfos) paths.Add(path + '/' + fileInfo.Name);
            }
            else paths.Add(AssetDatabase.GUIDToAssetPath(assetGUID));
        }
        
        AssignMaterials(paths);
    }

    private static void AssignMaterials(List<string> paths)
    {
        try
        {
            EditorUtility.DisplayProgressBar(ProgressBarAssignString, "Starting ... ", 0);
        
            // prepare
            _missingTextureTexture = (Texture) AssetDatabase.LoadAssetAtPath(MissingTextureTexturePath, typeof(Texture));
            
            Dictionary<string, Material> materials = GetMaterials(paths);
            Dictionary<string, Texture> textures = _customTextureDir
                ? GetTextures(_customTextureDirPath)
                : GetTextures(Path.GetDirectoryName(paths[0]));

            Dictionary<string, Dictionary<MaterialType, Texture>> groupTextures = GroupTextures(textures);
            HashSet<string> identHash = new HashSet<string>(groupTextures.Keys);

            List<string> identList = _aggressiveAssign ? new List<string>(groupTextures.Keys) : new List<string>();

            int counter = 0;
            foreach (string material in materials.Keys)
            {
                EditorUtility.DisplayProgressBar(ProgressBarAssignString, "Assigning ... " + material, counter / (float) materials.Keys.Count);
                counter++;
             
                bool assigned = false;
                
                if (identHash.Contains(material))
                {
                    Dictionary<MaterialType, Texture> tempTextures = groupTextures[material];
                    foreach (MaterialType type in tempTextures.Keys)
                    {
                        Texture tempTexture = tempTextures[type];
                        string name = ShaderProperty[type];

                        materials[material].SetTexture(name, tempTexture);
                    }
                    assigned = true;
                }
                else if (_aggressiveAssign)
                {
                    Tuple<string, int> closestDamLevMatch = GetClosestDamerauLevenshteinMatch(material, identList); // das hier ist die liste mit den texturen
                    if (closestDamLevMatch.Item2 <= _aggressiveAssignThreshold)
                    {
                        Dictionary<MaterialType, Texture> tempTextures = groupTextures[closestDamLevMatch.Item1];
                        foreach (MaterialType type in tempTextures.Keys)
                        {
                            Texture tempTexture = tempTextures[type];
                            string name = ShaderProperty[type];

                            materials[material].SetTexture(name, tempTexture);
                        }
                        assigned = true;
                    }
                }
                
                if (!assigned && _highlightMissingTexture)
                {
                    Debug.Log("Material doesn't have any assigned textures");
                    materials[material].SetTexture(ShaderProperty[MaterialType.Diffuse], _missingTextureTexture); // assign missing Texture-Texture
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static Dictionary<string, Material> GetMaterials(List<string> paths)
    {
        Dictionary<string, Material> materials = new Dictionary<string, Material>();

        for (var i = 0; i < paths.Count; i++)
        {
            EditorUtility.DisplayProgressBar(ProgressBarAssignString, "Loading materials ... " + i + " / " + paths.Count, i / (float) paths.Count);
            var path = paths[i];

            // handle selected materials
            if (path.EndsWith(".mat"))
            {
                string parsedName = Path
                    .GetFileName(path)
                    .Substring(0, Path.GetFileName(path).Length - Path.GetExtension(path).Length)
                    .ToLower();

                // remove unnecessary _mat
                if (Regex.Match(parsedName, @"_ma?t\d*$").Success)
                {
                    parsedName = parsedName.Substring(0, parsedName.LastIndexOf('_'));
                    if (parsedName[parsedName.Length - 1] == '_')
                    {
                        parsedName = parsedName.Substring(0, parsedName.Length - 1);
                    }
                }

                Material material = (Material) AssetDatabase.LoadAssetAtPath(path, typeof(Material));
                materials.Add(parsedName, material);
            }
        }

        return materials;
    }

    private static Dictionary<string, Texture> GetTextures(string path)
    {
        if (!Directory.Exists(path))
        {
            Debug.LogError("The texture-directory path '" + path + "' does not exist.");
            return null;
        }

        Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        
        DirectoryInfo info = new DirectoryInfo(path);
        FileInfo[] fileInfos = info.GetFiles();

        for (var i = 0; i < fileInfos.Length; i++)
        {
            EditorUtility.DisplayProgressBar(ProgressBarAssignString, "Loading textures ... " + i + " / " + fileInfos.Length, i / (float) fileInfos.Length);
            var fileInfo = fileInfos[i];

            if (fileInfo.Extension == ".tga")
            {
                string texturePath = "Assets" + fileInfo.FullName.Substring(Application.dataPath.Length);
                Texture texture = (Texture)AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture));

                string ident = Path.GetFileNameWithoutExtension(texturePath).ToLower();
                if (ident.EndsWith("_$h"))
                {
                    ident = ident.Substring(0, ident.Length - "_$h".Length); // das "_$h" am ende entfernen
                }
                textures.Add(ident, texture);
            }
        }

        return textures;
    }

    private static Dictionary<string, Dictionary<MaterialType, Texture>> GroupTextures(
        Dictionary<string, Texture> textures)
    {
        Dictionary<string, Dictionary<MaterialType, Texture>> materialGroups = new Dictionary<string, Dictionary<MaterialType, Texture>>();
        Dictionary<string, MaterialType> reverseAssignment = AssignmentNames.ToDictionary(x => x.Value, x => x.Key); // to reverse search Materials

        int counter = 0;
        foreach (string key in textures.Keys)
        {
            EditorUtility.DisplayProgressBar(ProgressBarAssignString, "Grouping textures ... " + counter + " / " + textures.Keys.Count, counter / (float) textures.Keys.Count);
            counter++;

            int secondToLastIndex = GetSecondToLastIndex(key, '_');
            if (secondToLastIndex < 0 ) continue;

            string ident = key.Substring(0, key.LastIndexOf('_'));
            string typeStr = key.Substring(key.LastIndexOf('_'), key.Length - key.LastIndexOf('_')) + "_$h"; // FIXME ZEILE 360 ka wo das _$h hin ist, füge es deswegen wieder dazu

            if (!reverseAssignment.ContainsKey(typeStr)) continue;
            MaterialType type = reverseAssignment[typeStr];

            if (materialGroups.ContainsKey(ident))
            {
                materialGroups[ident].Add(type, textures[key]);
            }
            else materialGroups.Add(ident, new Dictionary<MaterialType, Texture>() { { type, textures[key]} });
        }

        return materialGroups;
    }

    private static Tuple<string, int> GetClosestDamerauLevenshteinMatch(string source, List<string> target)
    {
        int closestDist = int.MaxValue;
        string closestIdent = "";

        foreach (string t in target)
        {
            int dist = DamerauLevenshteinDistance(source, t);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIdent = t;
                if (dist == 0) break;
            }
        }

        return new Tuple<string, int>(closestIdent, closestDist);
    }

    private static int DamerauLevenshteinDistance(string source, string target)
    {
        if (String.IsNullOrEmpty(source))
        {
            if (String.IsNullOrEmpty(target))
            {
                return 0;
            }
            else
            {
                return target.Length;
            }
        }
        else if (String.IsNullOrEmpty(target))
        {
            return source.Length;
        }

        var score = new int[source.Length + 2, target.Length + 2];

        var inf = source.Length + target.Length;
        score[0, 0] = inf;
        for (var i = 0; i <= source.Length; i++) { score[i + 1, 1] = i; score[i + 1, 0] = inf; }
        for (var j = 0; j <= target.Length; j++) { score[1, j + 1] = j; score[0, j + 1] = inf; }

        var sd = new SortedDictionary<char, int>();
        foreach (var letter in (source + target))
        {
            if (!sd.ContainsKey(letter))
                sd.Add(letter, 0);
        }

        for (var i = 1; i <= source.Length; i++)
        {
            var db = 0;
            for (var j = 1; j <= target.Length; j++)
            {
                var i1 = sd[target[j - 1]];
                var j1 = db;

                if (source[i - 1] == target[j - 1])
                {
                    score[i + 1, j + 1] = score[i, j];
                    db = j;
                }
                else
                {
                    score[i + 1, j + 1] = Math.Min(score[i, j], Math.Min(score[i + 1, j], score[i, j + 1])) + 1;
                }

                score[i + 1, j + 1] = Math.Min(score[i + 1, j + 1], score[i1, j1] + (i - i1 - 1) + 1 + (j - j1 - 1));
            }

            sd[source[i - 1]] = i;
        }

        return score[source.Length + 1, target.Length + 1];
    }

    private static int GetSecondToLastIndex(string str, char of)
    {
        int last = str.LastIndexOf(of);
        return last > 0 ? str.LastIndexOf(of, last - 1) : -1;
    }
    
    #endregion

    [MenuItem("Assets/FFXV Asset Tools/Clear Textures")]
    private static void ClearTextures()
    {
        List<string> paths = new List<string>();
        
        // handle selection
        foreach (string assetGUID in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGUID);
            
            // handle selected folder
            if (AssetDatabase.IsValidFolder(path))
            {
                DirectoryInfo info = new DirectoryInfo(path);
                FileInfo[] fileInfos = info.GetFiles();
                foreach (FileInfo fileInfo in fileInfos) paths.Add(path + '/' + fileInfo.Name);
            }
            else paths.Add(AssetDatabase.GUIDToAssetPath(assetGUID));
        }
        
        ClearTextures(paths);
    }

    private static void ClearTextures(List<string> paths)
    {
        List<Material> materials = new List<Material>();

        try
        {
            for (var i = 0; i < paths.Count; i++)
            {
                EditorUtility.DisplayProgressBar(ProgressBarAssignString, "Loading materials ... " + i + " / " + paths.Count, i / (float) paths.Count);            
                var path = paths[i];
                if (path.EndsWith(".mat"))
                {
                    materials.Add((Material)AssetDatabase.LoadAssetAtPath(path, typeof(Material)));
                }
            }
            
            DoClear(materials);
            
            /*for (int i = 0; i < materials.Count; i++)
            {
                EditorUtility.DisplayProgressBar(ProgressBarAssignString, "Clearing materials ... " + i + " / " + paths.Count, i / (float) paths.Count);     
                var material = materials[i];
                
            }*/
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
    
    /// <summary>
    /// Clears textures off materials in a list
    /// </summary>
    private static void DoClear(List<Material> mats)
    {
        if (mats.Count <= 0) return;

        foreach (var selectedMat in mats)
        {
            Undo.RecordObject(selectedMat, "Clear Textures off " + selectedMat.name);

            selectedMat.color = Color.white;
            selectedMat.SetTexture(MainTex, null);
            selectedMat.mainTextureOffset = Vector2.zero;
            selectedMat.mainTextureScale = Vector2.one;

            selectedMat.DisableKeyword("_METALLICGLOSSMAP");
            selectedMat.SetTexture(MetallicGlossMap, null);

            selectedMat.DisableKeyword("_EMISSION");
            selectedMat.SetTexture(BumpMap, null);

            selectedMat.DisableKeyword("_NORMALMAP");
            selectedMat.SetTexture(ParallaxMap, null);

            selectedMat.SetTexture(OcclusionMap, null);

            selectedMat.DisableKeyword("_DETAIL_MU  LX2");
            selectedMat.SetTexture(DetailMask, null);
            selectedMat.SetTexture(DetailAlbedoMap, null);
            selectedMat.SetTexture(DetailNormalMap, null);

            selectedMat.DisableKeyword("_EMISSION");
            selectedMat.SetColor(EmissionColor, Color.clear);
            selectedMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            selectedMat.SetTexture(EmissionMap, null);
        }
    }
}
