using UnityEngine;
namespace MeltInterfaceTechnologies.ContactGlove.V2.Samples
{
    /// <summary>Interaction visualization only. Does not send finger-force commands.</summary>
    public sealed class CollisionIndicator : MonoBehaviour
    {
        private int overlaps;
        public bool IsColliding=>overlaps>0;
        private void OnTriggerEnter(Collider other){overlaps++;}
        private void OnTriggerExit(Collider other){overlaps=Mathf.Max(0,overlaps-1);}
        private void OnDisable(){overlaps=0;}
    }
}
