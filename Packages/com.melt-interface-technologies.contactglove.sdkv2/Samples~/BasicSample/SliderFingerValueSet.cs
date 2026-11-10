// Adapted from the ContactGlove SDK sample for Protocol v2.
using UnityEngine;
using UnityEngine.UI;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Samples
{
    public sealed class SliderFingerValueSet : MonoBehaviour
    {
        [SerializeField] private Text title;
        [SerializeField] private Slider slider;
        private string label;
        public void SetTitle(string text) { label=text; if(title!=null)title.text=text; }
        public void SetDegrees(float? value)
        {
            if(label==null)label=title!=null ? title.text : name;
            if(slider!=null){slider.minValue=-180;slider.maxValue=180;slider.interactable=false;if(value.HasValue)slider.SetValueWithoutNotify(value.Value);slider.gameObject.SetActive(value.HasValue);}
            if(title!=null)title.text=label+": "+(value.HasValue ? value.Value.ToString("F1")+"°" : "unavailable");
        }
    }
}
