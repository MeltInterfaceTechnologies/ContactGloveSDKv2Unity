using System.Collections.Generic;
using UnityEngine;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Unity
{
    [DisallowMultipleComponent]
    public sealed class ContactGloveBehaviour : MonoBehaviour
    {
        public HostOptions[] Hosts = { new HostOptions() };
        public int NotificationBudgetPerHost = 256;
        public ContactGloveClient Client { get; private set; }
        public OperationResult LastStartResult { get; private set; }
        private static readonly HashSet<ContactGloveBehaviour> Instances = new HashSet<ContactGloveBehaviour>();
        private void OnEnable() { if (Application.isPlaying) StartClient(); }
        private void Update() { Client?.PumpNotifications(Mathf.Max(1, NotificationBudgetPerHost)); }
        private void OnDisable() => StopClient();
        private void OnDestroy() => StopClient();
        private void OnApplicationQuit() => StopClient();
        private void OnApplicationPause(bool paused) { if (!paused && Client != null && Hosts != null) foreach (var host in Hosts) if (host != null) Client.Resume(host.HostId); }
        public void StartClient()
        {
            if (Client != null) return;
            Client = new ContactGloveClient(); Instances.Add(this);
            LastStartResult = new OperationResult(ResultCode.Success);
            if (Hosts == null) return;
            foreach (var host in Hosts)
            {
                var result = Client.Connect(host);
                if (!result.Succeeded) { LastStartResult = result; Debug.LogWarning("ContactGlove v2: " + result.Code + " " + result.Detail, this); }
            }
        }
        public void StopClient() { Client?.Dispose(); Client = null; Instances.Remove(this); }
        public static void StopAll()
        { foreach (var owner in new List<ContactGloveBehaviour>(Instances)) if (owner != null) owner.StopClient(); Instances.Clear(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaySession() => StopAll();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartEnabledSceneOwners()
        {
            // With scene reload disabled, OnEnable need not run for an existing scene object.
            foreach (var owner in Object.FindObjectsByType<ContactGloveBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (owner.isActiveAndEnabled) owner.StartClient();
        }
    }
}
