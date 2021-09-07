#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.Rendering;
using System.Text.RegularExpressions;

namespace AltSpace_Unity_Uploader
{
    static class URPInstaller
    {
        private struct LMReplacements_s
        {
            public LMReplacements_s(string _old, string _repl)
            {
                old = _old;
                repl = _repl;
            }

            private string old;
            private string repl;

            public string pattern => "\"LightMode\"\\s*=\\s*\"" + old + "\"";
            public string replacement => string.IsNullOrEmpty(repl) ? null : "\"LightMode\" = \"" + repl + "\"";
            public bool same => old == repl;
        }

        private static LMReplacements_s[] lmReplacements =
        {
            new LMReplacements_s("Vertex", "UniversalForward"),
            new LMReplacements_s("ForwardBase", "UniversalForward"),
            new LMReplacements_s("VertexLM", "UniversalForward"),
            new LMReplacements_s("Always", "UniversalForward"),
            new LMReplacements_s("UniversalForward", "UniversalForward"),
            new LMReplacements_s("Meta", "Meta"),
            new LMReplacements_s("\\w+", null)
        };

        // Triggered an update to the Universal Rendering Pipeline
        public static void BeginUpdate()
        {
            EditorApplication.update -= BeginUpdate;
            _ = TriggerStage();
        }

        // We come here when the URP package is installed, but not yet configured.
        public static string TriggerStage()
        {
            Debug.Log("Post-installation trigger, doing remaining configuration.");

            PlayerSettings.colorSpace = ColorSpace.Linear;

            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                bool res = false;
                res |= Common.InstallResource("Altspace_URP_Asset.asset", "URP");
                res |= Common.InstallResource("Altspace_URP_Asset_Renderer.asset", "URP");
                res |= Common.InstallResource("EmptyMaterial.mat", "URP");

                if (res)
                    AssetDatabase.Refresh();

                Debug.Log("Updating Render Pipeline settings and default shaders in materials...");
                GraphicsSettings.defaultRenderPipeline =
                    AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/URP/Altspace_URP_Asset.asset");

                Debug.Log("Upgrading shaders in project...");
                UpgradeShaders();

                EditorApplication.ExecuteMenuItem("Edit/Render Pipeline/Universal Render Pipeline/Upgrade Project Materials to UniversalRP Materials");
            }



            return "";
        }

        public static bool UpgradeShader(string fname)
        {
            bool replError = false;
            string PickReplacement(Match m)
            {
                string repl;
                switch(m.Groups["lmtype"].Value)
                {
                    case "Vertex":
                    case "ForwardBase":
                    case "VertexLM":
                    case "UniversalForward":
                        repl = "UniversalForward";
                        break;
                    case "Meta":
                        repl = "Meta";
                        break;
                    default:
                        replError = true;
                        repl = "error-" + m.Groups["lmtype"].Value;
                        break;
                }

                return "\"LightMode\" = \"" + repl + "\"";
            }

            string[] badCode =
            {
                "ShadeSH9",
                "unity_LightPosition",
                "_LightColor0",
                "unity_LightColor",
                "unity_WorldToShadow",
                "unity_4LightAtten0",
                "_WorldSpaceLightPos0",
                "#pragma surface"
            };

            string badCodePattern = "(" + string.Join("|", badCode) + ")";

            string code = File.ReadAllText(fname);

            if(Regex.IsMatch(code, badCodePattern))
            {
                Debug.LogWarning("Shader " + fname + ": Failed to convert - unsupported feature used.");
                return false;
            }

            // Replace the LightMode tags, if we can
            code = Regex.Replace(code, "\"LightMode\"\\s*=\\s*\"(?<lmtype>\\w+)\"", PickReplacement, RegexOptions.IgnoreCase);
            if (replError)
            {
                Debug.LogWarning("Shader " + fname + ": Failed to convert - unsupported LightMode tag value.");
                return false;
            }

            // Replace 'RenderMode' with 'RenderType' tag keyword and add the Universal Pipeline Tag
            code = Regex.Replace(code,
                "\"RenderMode\"\\s*=\\s*\"",
                "\"RenderPipeline\" = \"UniversalPipeline\" \"RenderType\" = \"");


            // Replace CGPROGRAM and ENDCG with the appropiate HLSL tags
            code = Regex.Replace(code, "CGPROGRAM", "HLSLPROGRAM");
            code = Regex.Replace(code, "ENDCG", "ENDHLSL");

            File.WriteAllText(fname, code);

            return true;
        }

        public static void UpgradeShaders(string directory)
        {
            IEnumerable<string> dirs = Directory.EnumerateDirectories(directory);
            foreach(string subdir in dirs)
                UpgradeShaders(subdir);

            IEnumerable<string> files = Directory.EnumerateFiles(directory, "*.shader");
            foreach (string shaderfile in files)
                UpgradeShader(shaderfile);
        }

        public static void UpgradeShaders() => UpgradeShaders("Assets");
    }
}

#endif