using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class FixVRMShaders
{
    [MenuItem("Tools/Fix VRM Shader Import")]
    public static void FixShaders()
    {
        SerializedObject graphicsSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        SerializedProperty it = graphicsSettings.GetIterator();
        SerializedProperty alwaysIncludedShaders = null;

        while (it.NextVisible(true))
        {
            if (it.name == "m_AlwaysIncludedShaders")
            {
                alwaysIncludedShaders = it;
                break;
            }
        }

        if (alwaysIncludedShaders != null)
        {
            string[] shadersToAdd = new string[] {
                "Universal Render Pipeline/Lit",
                "VRM10/Universal Render Pipeline/MToon10",
                "VRM10/MToon10"
            };

            foreach (string shaderName in shadersToAdd)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    bool found = false;
                    for (int i = 0; i < alwaysIncludedShaders.arraySize; i++)
                    {
                        if (alwaysIncludedShaders.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        alwaysIncludedShaders.InsertArrayElementAtIndex(alwaysIncludedShaders.arraySize);
                        alwaysIncludedShaders.GetArrayElementAtIndex(alwaysIncludedShaders.arraySize - 1).objectReferenceValue = shader;
                        Debug.Log("Added " + shaderName + " to Always Included Shaders");
                    }
                }
            }

            graphicsSettings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("VRM Shader Fix Applied! Try re-importing the VRM file now.");
        }
    }
}
