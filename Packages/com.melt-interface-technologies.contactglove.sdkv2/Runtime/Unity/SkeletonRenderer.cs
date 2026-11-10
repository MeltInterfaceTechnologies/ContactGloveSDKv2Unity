using UnityEngine;
using UnityEngine.Rendering;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Unity
{
    /// <summary>Model-independent diagnostic display using 25 spheres and 24 cylinders.</summary>
    public sealed class SkeletonRenderer : MonoBehaviour
    {
        public float JointRadius = 0.004f;
        public float BoneRadius = 0.002f;
        public Material TrackedMaterial;
        public Material DiagnosticMaterial;
        // Serialized references keep the optional pipeline shaders in Player builds.
        [SerializeField, HideInInspector] private Shader universalShader, highDefinitionShader;
        private Material trackedCopy, diagnosticCopy;
        private Material trackedSource, diagnosticSource;
        private Transform[] joints, bones;
        public void Initialize()
        {
            if (joints != null) return;
            joints = new Transform[25]; bones = new Transform[25];
            for(int i=0;i<25;i++)
            {
                joints[i] = Primitive(PrimitiveType.Sphere,"Joint "+i); joints[i].localScale=Vector3.one*JointRadius*2;
                if(i>0) bones[i] = Primitive(PrimitiveType.Cylinder,"Bone "+HandTopology.Parents[i]+" - "+i);
            }
            SetVisible(false);
        }
        private Transform Primitive(PrimitiveType type,string label)
        {
            var item=GameObject.CreatePrimitive(type); item.name=label; item.transform.SetParent(transform,false);
            var collider=item.GetComponent<Collider>();
            if(collider != null) { collider.enabled=false; if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider); }
            return item.transform;
        }
        public void SetVisible(bool visible)
        { if(joints==null)return; for(int i=0;i<25;i++){joints[i].gameObject.SetActive(visible); if(i>0)bones[i].gameObject.SetActive(visible);} }
        public void Apply(HandSnapshot snapshot)
        {
            Initialize(); if(snapshot==null){SetVisible(false);return;}
            // NotAttempted can include scheduled IK skips; reserve gold for an explicit IK failure.
            SetVisible(true); var material=snapshot.Tracking==TrackingState.IkFailed
                ? ResolveMaterial(DiagnosticMaterial,ref diagnosticSource,ref diagnosticCopy)
                : ResolveMaterial(TrackedMaterial,ref trackedSource,ref trackedCopy);
            for(int i=0;i<25;i++)
            {
                Vector3 p=UnityPoseConverter.Position(snapshot.Positions[i]); joints[i].localPosition=p;
                if(material!=null)joints[i].GetComponent<MeshRenderer>().sharedMaterial=material;
                if(i==0)continue;
                Vector3 parent=UnityPoseConverter.Position(snapshot.Positions[HandTopology.Parents[i]]), delta=p-parent;
                bones[i].localPosition=(parent+p)*0.5f;
                bones[i].localScale=new Vector3(BoneRadius*2,delta.magnitude*0.5f,BoneRadius*2);
                bones[i].localRotation=delta.sqrMagnitude>1e-12f ? Quaternion.FromToRotation(Vector3.up,delta) : Quaternion.identity;
                if(material!=null)bones[i].GetComponent<MeshRenderer>().sharedMaterial=material;
            }
        }
        private Material ResolveMaterial(Material source,ref Material cachedSource,ref Material copy)
        {
            if(source!=null && source==copy)return copy;
            // Only adapt Built-in Standard materials; authored SRP/custom materials stay untouched.
            var pipeline=GraphicsSettings.currentRenderPipeline;
            Shader shader=null;
            if(pipeline!=null && source!=null && source.shader!=null && source.shader.name=="Standard")
            {
                if(pipeline.renderPipelineShaderTag=="UniversalPipeline")shader=universalShader;
                else if(pipeline.renderPipelineShaderTag=="HDRenderPipeline")shader=highDefinitionShader;
            }
            // Build scenes retain pipeline materials even when all skeletons are created at runtime.
            // Shader.Find therefore also works for instances with no serialized shader references.
            if(shader==null && pipeline!=null && source!=null && source.shader!=null && source.shader.name=="Standard")
            {
                if(pipeline.renderPipelineShaderTag=="UniversalPipeline")shader=Shader.Find("Universal Render Pipeline/Lit");
                else if(pipeline.renderPipelineShaderTag=="HDRenderPipeline")shader=Shader.Find("HDRP/Lit");
            }
            if(shader==null){ReleaseMaterial(ref copy);cachedSource=null;return source;}
            if(copy==null || cachedSource!=source || copy.shader!=shader)
            {
                ReleaseMaterial(ref copy);
                copy=new Material(shader){name=source.name+" (pipeline)",hideFlags=HideFlags.HideAndDontSave};
                cachedSource=source;
            }
            copy.SetColor("_BaseColor",source.color);
            if(copy.HasProperty("_Metallic"))copy.SetFloat("_Metallic",source.GetFloat("_Metallic"));
            if(copy.HasProperty("_Smoothness"))copy.SetFloat("_Smoothness",source.GetFloat("_Glossiness"));
            return copy;
        }
        private static void ReleaseMaterial(ref Material material)
        {
            if(material==null)return;
            if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);
            material=null;
        }
#if UNITY_EDITOR
        private void OnEnable()=>PrepareRenderPipelineShaders();
        /// <summary>Retain installed pipeline shaders when importing a scene or processing a build.</summary>
        public void PrepareRenderPipelineShaders()
        {
            universalShader=Shader.Find("Universal Render Pipeline/Lit");
            highDefinitionShader=Shader.Find("HDRP/Lit");
        }
        /// <summary>Serialize compatible materials into the build scene so its shader variants are retained.</summary>
        public void PrepareMaterialsForBuild()
        {
            PrepareRenderPipelineShaders();
            TrackedMaterial=ResolveMaterial(TrackedMaterial,ref trackedSource,ref trackedCopy);
            DiagnosticMaterial=ResolveMaterial(DiagnosticMaterial,ref diagnosticSource,ref diagnosticCopy);
            if(trackedCopy!=null)trackedCopy.hideFlags=HideFlags.None;
            if(diagnosticCopy!=null)diagnosticCopy.hideFlags=HideFlags.None;
        }
#endif
        private void OnDestroy()
        {
            ReleaseMaterial(ref trackedCopy);ReleaseMaterial(ref diagnosticCopy);
            if(joints==null)return;
            for(int i=0;i<25;i++)
            {
                if(joints[i]!=null)ReleasePrimitive(joints[i].gameObject);
                if(i>0 && bones[i]!=null)ReleasePrimitive(bones[i].gameObject);
            }
        }
        private static void ReleasePrimitive(GameObject item)
        {if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
    }
}
