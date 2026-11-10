// Display structure adapted from v1 ControllerInputViewer; input meaning follows v2.
using UnityEngine;
using UnityEngine.UI;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Samples
{
    public sealed class ControllerInputViewer : MonoBehaviour
    {
        public Text Values;
        public bool CompactValues;
        public Image[] Buttons=new Image[6];
        public Color ReleasedColor=Color.gray, PressedColor=Color.green, TouchingColor=Color.yellow;
        public RectTransform JoystickHandle, TrackpadHandle;
        public float JoystickRadius=50, TrackpadRadius=50;
        public Slider TriggerValue, GripValue, GripForce;
        public void Show(ControllerSnapshot snapshot, double staleAfter)
        {
            bool fresh=snapshot!=null && snapshot.AgeSeconds<=staleAfter;
            if(Values!=null)Values.text=!fresh ? "Controller: "+(snapshot==null?"unavailable":"stale — input released") :
                $"A {snapshot.A}  B {snapshot.B}  System {snapshot.System}\nTrigger button {snapshot.TriggerButton}; Trigger value {snapshot.Trigger:F2}   Grip value {snapshot.GripValue:F2}   Grip force {snapshot.GripForce:F2}\nJoystick {snapshot.JoystickButton}: ({snapshot.JoystickX:F2}, {snapshot.JoystickY:F2})   Trackpad {snapshot.TrackpadButton}: ({snapshot.TrackpadX:F2}, {snapshot.TrackpadY:F2})";
            if(fresh && CompactValues && Values!=null)Values.text=$"Trigger value {snapshot.Trigger:F2} | Grip value {snapshot.GripValue:F2} | Grip force {snapshot.GripForce:F2}";
            var states=fresh?new[]{snapshot.A,snapshot.B,snapshot.System,snapshot.TriggerButton,snapshot.JoystickButton,snapshot.TrackpadButton}:null;
            if(Buttons!=null)for(int i=0;i<Buttons.Length;i++) if(Buttons[i]!=null)Buttons[i].color=!fresh || i>=states.Length?ReleasedColor:states[i]==ButtonState.Pressed?PressedColor:states[i]==ButtonState.Touching?TouchingColor:ReleasedColor;
            ShowHandle(JoystickHandle,fresh,new Vector2(fresh?snapshot.JoystickX:0,fresh?snapshot.JoystickY:0),JoystickRadius);
            ShowHandle(TrackpadHandle,fresh,new Vector2(fresh?snapshot.TrackpadX:0,fresh?snapshot.TrackpadY:0),TrackpadRadius);
            ShowValue(TriggerValue,fresh,fresh?snapshot.Trigger:0);
            ShowValue(GripValue,fresh,fresh?snapshot.GripValue:0);
            ShowValue(GripForce,fresh,fresh?snapshot.GripForce:0);
        }
        private static void ShowHandle(RectTransform handle,bool fresh,Vector2 value,float radius)
        {if(handle==null)return;handle.gameObject.SetActive(fresh);if(fresh)handle.anchoredPosition=value*radius;}
        private static void ShowValue(Slider slider,bool fresh,float value)
        {if(slider==null)return;slider.interactable=false;slider.gameObject.SetActive(fresh);if(fresh)slider.SetValueWithoutNotify(value);}
    }
}
