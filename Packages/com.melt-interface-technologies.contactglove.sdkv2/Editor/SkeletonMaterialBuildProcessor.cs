using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using MeltInterfaceTechnologies.ContactGlove.V2.Unity;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Editor
{
    // Work on the build's scene copy, including inactive renderers, without editing source assets.
    public sealed class SkeletonMaterialBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene,BuildReport report)
        {
            foreach(var root in scene.GetRootGameObjects())
                foreach(var renderer in root.GetComponentsInChildren<SkeletonRenderer>(true))
                    renderer.PrepareMaterialsForBuild();

            // A build can contain no SkeletonRenderer at all: applications may add
            // every instance with AddComponent. Retain material variants independently
            // of scene-authored renderers so the runtime shader lookup remains valid.
            string tag=GraphicsSettings.currentRenderPipeline?.renderPipelineShaderTag;
            var shader=tag=="UniversalPipeline"?Shader.Find("Universal Render Pipeline/Lit")
                :tag=="HDRenderPipeline"?Shader.Find("HDRP/Lit"):null;
            if(shader==null)return;
            var references=new GameObject("ContactGlove skeleton shader references");
            references.hideFlags=HideFlags.HideInHierarchy;
            references.SetActive(false);
            SceneManager.MoveGameObjectToScene(references,scene);
            references.AddComponent<SkeletonShaderReferences>().PrepareForBuild(shader);
        }
    }
}
