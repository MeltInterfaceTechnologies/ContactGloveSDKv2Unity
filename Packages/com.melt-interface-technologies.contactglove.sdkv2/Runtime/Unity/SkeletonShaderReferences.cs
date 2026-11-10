using UnityEngine;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Unity
{
    // Added only to build-scene copies. Material references retain shader variants
    // for skeletons created later by scripts, without changing project assets/settings.
    [AddComponentMenu("")]
    public sealed class SkeletonShaderReferences : MonoBehaviour
    {
        [SerializeField] private Material pipelineMaterial;

#if UNITY_EDITOR
        public void PrepareForBuild(Shader shader)
        {
            pipelineMaterial=new Material(shader){name="ContactGlove skeleton shader reference"};
        }
#endif
    }
}
