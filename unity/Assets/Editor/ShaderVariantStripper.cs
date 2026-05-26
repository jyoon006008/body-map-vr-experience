using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

public class ShaderVariantStripper : IPreprocessShaders
{
    // High priority / early execution
    public int callbackOrder => 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (shader == null) return;

        // Forcefully strip all variants of the TextMeshPro URP Lit shader
        // to prevent compiling millions of variants.
        if (shader.name.Contains("TMP_SDF URP Lit") || 
            shader.name.Contains("TextMeshPro/SRP") || 
            shader.name.Contains("TMP_SDF URP"))
        {
            data.Clear();
        }
    }
}
