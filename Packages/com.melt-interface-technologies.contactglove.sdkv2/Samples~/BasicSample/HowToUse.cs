using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MeltInterfaceTechnologies.ContactGlove.V2.Unity;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Samples
{
    /// <summary>F/J and A-button body vibration; fixed roots, degree UI and model-independent skeleton display.</summary>
    public sealed class HowToUse : MonoBehaviour
    {
        public ContactGloveBehaviour Connection;
        public string HostId="localhost";
        public string LeftGloveId="", RightGloveId="";
        [Tooltip("Sample safety threshold in seconds; not a measured hardware default.")]
        public float FreshnessSeconds=0.5f;
        public bool UseRawSkeleton;
        public bool CompactDisplay;
        public Text Status, LeftStatus, RightStatus, Result, ProfileInfo;
        public FingerViewerSliders LeftAngles, RightAngles;
        public ControllerInputViewer LeftController, RightController;
        public SkeletonRenderer LeftSkeleton, RightSkeleton;
        public Button LeftVibrateButton, RightVibrateButton;
        public Button RawSkeletonButton, RetargetSkeletonButton;
        public Dropdown LeftSelection, RightSelection;
        private readonly List<string> leftChoices=new List<string>(), rightChoices=new List<string>();
        private ContactGloveClient subscribedClient;
        private string subscribedHostId;
        private readonly List<IDisposable> tokens=new List<IDisposable>();
        private readonly Dictionary<DeviceKey,ButtonState> previousA=new Dictionary<DeviceKey,ButtonState>();
        private int lossCount;
        private const double ProfileRefreshSeconds=10;
        private sealed class ProfileEntry
        {
            public int? PendingRequest;
            public double NextRefresh;
            public string Label="loading...";
        }
        private readonly Dictionary<string,ProfileEntry> profiles=new Dictionary<string,ProfileEntry>(StringComparer.OrdinalIgnoreCase);
        private string selectedLeftProfile,selectedRightProfile;
        private void Awake()
        {
            Application.runInBackground=true;
            SampleInputBackend.ConfigureEventSystems();
            if(LeftSelection!=null)LeftSelection.onValueChanged.AddListener(SelectLeftGlove);
            if(RightSelection!=null)RightSelection.onValueChanged.AddListener(SelectRightGlove);
            RefreshSkeletonButtons();
        }
        public void SelectLeftGlove(int index){if(index>=0 && index<leftChoices.Count)LeftGloveId=leftChoices[index];}
        public void SelectRightGlove(int index){if(index>=0 && index<rightChoices.Count)RightGloveId=rightChoices[index];}
        private void RefreshSelection(Dropdown menu,List<string> choices,bool left,ContactGloveClient client)
        {
            if(menu==null)return;
            string selected=left?LeftGloveId:RightGloveId;
            var next=new List<string>{""};
            if(client!=null)foreach(var device in client.GetDevices(HostId))if(device.IsLeft==left && device.Connection==ConnectionState.Connected)next.Add(device.Key.GloveId);
            if(!string.IsNullOrEmpty(selected) && !next.Contains(selected))next.Add(selected);
            next.Sort(1,next.Count-1,StringComparer.Ordinal);
            bool changed=choices.Count!=next.Count;
            if(!changed)for(int i=0;i<next.Count;i++)if(choices[i]!=next[i]){changed=true;break;}
            if(changed)
            {
                choices.Clear();choices.AddRange(next);menu.ClearOptions();
                var labels=new List<string>();foreach(string id in choices)labels.Add(id.Length==0?"Auto (one glove)":id);
                menu.AddOptions(labels);
            }
            menu.SetValueWithoutNotify(Mathf.Max(0,choices.IndexOf(selected)));menu.RefreshShownValue();
        }
        private void Update()
        {
            RefreshSkeletonButtons();
            var client=Connection!=null ? Connection.Client : null;
            RefreshSelection(LeftSelection,leftChoices,true,client);RefreshSelection(RightSelection,rightChoices,false,client);
            if(client!=subscribedClient || subscribedHostId!=HostId || (client!=null && tokens.Count==0 && client.GetDiagnostics(HostId)!=null))
            {
                ReleaseSubscriptions(); subscribedClient=client;subscribedHostId=HostId;
                if(client!=null && client.GetDiagnostics(HostId)!=null)
                {
                    tokens.Add(client.SubscribeController(HostId,OnController));
                    tokens.Add(client.SubscribeDevice(HostId,d=>{if(d.Connection==ConnectionState.Unconnected)previousA.Remove(d.Key);}));
                    tokens.Add(client.SubscribeNotificationLoss(HostId,_=>{previousA.Clear();foreach(var entry in profiles.Values)entry.PendingRequest=null;lossCount++;}));
                    tokens.Add(client.SubscribeProfileResponse(HostId,OnProfileResponse));
                    tokens.Add(client.SubscribeProfileTimeout(HostId,p=>CompleteProfile(p.RequestId,"timeout")));
                }
            }
            if(client==null){if(Status!=null)Status.text="Stopped";if(ProfileInfo!=null)ProfileInfo.text="Profile: waiting for glove";ClearHand(true);ClearHand(false);return;}
            if(SampleInputBackend.WasPressed(true))Vibrate(true);
            if(SampleInputBackend.WasPressed(false))Vibrate(false);
            var diagnostics=client.GetDiagnostics(HostId);
            RefreshProfiles(false);
            if(Status!=null)Status.text=diagnostics==null ? "Host unavailable" : $"{diagnostics.State}  receive {diagnostics.ReceivePort} / send {diagnostics.SendPort}  queue {diagnostics.QueueDepth} / lost {diagnostics.NotificationDrops} / parse errors {diagnostics.ParseErrors}\nnotification loss events {lossCount}";
            DrawHand(true);DrawHand(false);
        }
        private void OnController(ControllerSnapshot current)
        {
            if(current.AgeSeconds>FreshnessSeconds){previousA.Remove(current.Key);return;}
            bool had=previousA.TryGetValue(current.Key,out var previous); previousA[current.Key]=current.A;
            // Baseline acquisition / overflow resync cannot manufacture an input edge.
            if(had && current.A==ButtonState.Pressed && previous!=ButtonState.Pressed && TrySelected(current.IsLeft,out var key) && key.Equals(current.Key))Vibrate(current.IsLeft);
        }
        public bool TrySelected(bool left,out DeviceKey key)
        {
            key=default;var client=Connection!=null?Connection.Client:null;if(client==null)return false;
            string id=left?LeftGloveId:RightGloveId;
            if(!string.IsNullOrEmpty(id)){key=new DeviceKey(HostId,id);return client.TryGetDevice(key,out var d) && d.IsLeft==left && d.Connection==ConnectionState.Connected;}
            DeviceSnapshot only=null;
            foreach(var d in client.GetDevices(HostId))if(d.IsLeft==left && d.Connection==ConnectionState.Connected){if(only!=null)return false;only=d;}
            if(only==null)return false;key=only.Key;return true;
        }
        public void VibrateLeft()=>Vibrate(true);
        public void VibrateRight()=>Vibrate(false);
        public void Vibrate(bool left)
        {
            if(!TrySelected(left,out var key)){if(Result!=null)Result.text="Select one connected glove per side.";return;}
            var result=Connection.Client.Vibrate(key,160,0.2f,0.1f);
            if(Result!=null)Result.text=result.Succeeded?"Body vibration submitted locally (no actuation acknowledgement).":"Body vibration: "+result.Code;
        }
        public void ToggleConnection(){if(Connection==null)return;if(Connection.Client==null)Connection.StartClient();else Connection.StopClient();}
        public void ToggleStream(){UseRawSkeleton=!UseRawSkeleton;RefreshSkeletonButtons();}
        public void SelectRawSkeleton(){UseRawSkeleton=true;RefreshSkeletonButtons();}
        public void SelectRetargetSkeleton(){UseRawSkeleton=false;RefreshSkeletonButtons();}
        private void RefreshSkeletonButtons()
        {SetSkeletonButton(RawSkeletonButton,UseRawSkeleton);SetSkeletonButton(RetargetSkeletonButton,!UseRawSkeleton);}
        private static void SetSkeletonButton(Button button,bool selected)
        {
            if(button==null)return;
            if(button.image!=null)button.image.color=selected?new Color(0.12f,0.48f,0.62f):new Color(0.32f,0.32f,0.32f);
            var colors=button.colors;colors.normalColor=colors.highlightedColor=colors.selectedColor=Color.white;
            button.colors=colors;
        }
        public void RequestProfiles()=>RefreshProfiles(true);
        private string SelectedProfile(bool left)
        {
            return TrySelected(left,out var key) && Connection.Client.TryGetSkeleton(key,out var skeleton)?skeleton.ProfileUuid:null;
        }
        private void RefreshProfiles(bool force)
        {
            string left=SelectedProfile(true),right=SelectedProfile(false);
            EnsureProfile(left,force || !WasSelectedProfile(left));
            if(!string.Equals(left,right,StringComparison.OrdinalIgnoreCase))EnsureProfile(right,force || !WasSelectedProfile(right));
            selectedLeftProfile=left;selectedRightProfile=right;
            if(ProfileInfo==null)return;
            if(left==null && right==null)ProfileInfo.text="Profile: waiting for glove data";
            else if(left==null || right==null || string.Equals(left,right,StringComparison.OrdinalIgnoreCase))ProfileInfo.text="Profile: "+profiles[left??right].Label;
            else ProfileInfo.text="Left: "+profiles[left].Label+"\nRight: "+profiles[right].Label;
        }
        private bool WasSelectedProfile(string uuid)=>string.Equals(uuid,selectedLeftProfile,StringComparison.OrdinalIgnoreCase)
            || string.Equals(uuid,selectedRightProfile,StringComparison.OrdinalIgnoreCase);
        private void EnsureProfile(string uuid,bool force)
        {
            if(uuid==null)return;
            if(!profiles.TryGetValue(uuid,out var entry)){entry=new ProfileEntry();profiles.Add(uuid,entry);}
            double now=MonotonicTime.Now;
            if(entry.PendingRequest.HasValue || (!force && now<entry.NextRefresh))return;
            entry.NextRefresh=now+ProfileRefreshSeconds;
            var result=Connection.Client.RequestRetargetProfileInfo(HostId,uuid,out int id);
            if(result.Succeeded)entry.PendingRequest=id;else entry.Label="unavailable";
        }
        private void OnProfileResponse(ProfileResponseSnapshot response)
        {
            string label=response.Status==ProfileStatus.Success?(string.IsNullOrEmpty(response.Name)?"(unnamed)":response.Name)
                :response.Status==ProfileStatus.NotFound?"not found":"request rejected";
            CompleteProfile(response.RequestId,label);
        }
        private void CompleteProfile(int requestId,string label)
        {
            foreach(var entry in profiles.Values)if(entry.PendingRequest==requestId)
            {entry.PendingRequest=null;entry.Label=label;return;}
        }
        private HandSnapshot GetHand(DeviceKey key)
        {if(UseRawSkeleton){Connection.Client.TryGetRawSkeleton(key,out var raw);return raw;}Connection.Client.TryGetSkeleton(key,out var s);return s;}
        private void ClearHand(bool left)
        {
            (left?LeftAngles:RightAngles)?.Show(null,FreshnessSeconds);(left?LeftController:RightController)?.Show(null,FreshnessSeconds);
            (left?LeftSkeleton:RightSkeleton)?.Apply(null);
            var text=left?LeftStatus:RightStatus;if(text!=null)text.text="unavailable / select ID if multiple gloves";
            var button=left?LeftVibrateButton:RightVibrateButton;if(button!=null)button.interactable=false;
        }
        private void DrawHand(bool left)
        {
            if(!TrySelected(left,out var key)){ClearHand(left);return;}
            DrawSelectedHand(Connection.Client,key,left);
        }
        private void DrawSelectedHand(ContactGloveClient client,DeviceKey key,bool left)
        {
            // Selection and rendering are separate polls: a receive worker can remove or replace metadata between them.
            if(client==null || !client.TryGetDevice(key,out var device) || device.Connection!=ConnectionState.Connected || device.IsLeft!=left)
            {previousA.Remove(key);ClearHand(left);return;}
            client.TryGetErgonomics(key,out var e);client.TryGetController(key,out var c);
            (left?LeftAngles:RightAngles)?.Show(e,FreshnessSeconds);(left?LeftController:RightController)?.Show(c,FreshnessSeconds);
            if(c==null || c.AgeSeconds>FreshnessSeconds)previousA.Remove(key);
            var s=GetHand(key);bool fresh=s!=null && s.AgeSeconds<=FreshnessSeconds;
            (left?LeftSkeleton:RightSkeleton)?.Apply(fresh ? s : null);
            var button=left?LeftVibrateButton:RightVibrateButton;if(button!=null)button.interactable=device.SupportsHaptics;
            var text=left?LeftStatus:RightStatus;
            string skeletonStatus=s==null?": unavailable":!fresh?": stale":s.Tracking==TrackingState.IkFailed?": IK failed":"";
            if(text!=null)text.text=$"{key.GloveId} / battery {(device.BatteryPercentage<0?"invalid":device.BatteryPercentage+"%")} / external power {device.ExternalPowerPresent}\n{(UseRawSkeleton?"Raw source":"Retargeted")} skeleton{skeletonStatus}";
            if(CompactDisplay)
            {
                if(text!=null)text.text=$"{key.GloveId} | battery {(device.BatteryPercentage<0?"invalid":device.BatteryPercentage+"%")} | external power {device.ExternalPowerPresent}";
                if(text!=null && skeletonStatus.Length>0)text.text+=" | Skeleton"+skeletonStatus;
            }
        }
        private void ReleaseSubscriptions(){foreach(var token in tokens)token.Dispose();tokens.Clear();previousA.Clear();profiles.Clear();selectedLeftProfile=selectedRightProfile=null;}
        private void OnDisable(){ReleaseSubscriptions();subscribedClient=null;}
    }
}
