using UnityEngine;
using UnityEngine.UI;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Samples
{
    public sealed class FingerViewerSliders : MonoBehaviour
    {
        public SliderFingerValueSet[] Angles=new SliderFingerValueSet[20];
        public Text State;
        public bool CompactState;
        public void Show(ErgonomicsSnapshot snapshot, double staleAfter)
        {
            bool fresh=snapshot!=null && snapshot.AgeSeconds<=staleAfter;
            if(Angles!=null)for(int i=0;i<Angles.Length;i++) if(Angles[i]!=null)Angles[i].SetDegrees(fresh && i<20 ? (float?)snapshot.Angles[i] : null);
            string status=snapshot==null?": unavailable":!fresh?": stale":snapshot.Tracking==TrackingState.IkFailed?": IK failed":"";
            if(State!=null)State.text=(CompactState?"Angles (degrees)":"Ergonomics")+status;
        }
    }
}
