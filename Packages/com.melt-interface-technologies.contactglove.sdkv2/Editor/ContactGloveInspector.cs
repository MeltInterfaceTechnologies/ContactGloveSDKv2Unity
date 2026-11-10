using UnityEditor;
using UnityEngine;
using MeltInterfaceTechnologies.ContactGlove.V2.Unity;

namespace MeltInterfaceTechnologies.ContactGlove.V2.Editor
{
    [CustomEditor(typeof(ContactGloveBehaviour))]
    public sealed class ContactGloveInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var owner=(ContactGloveBehaviour)target;
            EditorGUILayout.HelpBox("Connect starts a local UDP session. Data receipt does not prove LAN approval or tracking success. StaleAfterSeconds=0 disables stale diagnosis.",MessageType.Info);
            if(owner.Client==null || owner.Hosts==null)return;
            foreach(var host in owner.Hosts)
            {
                if(host==null)continue; var d=owner.Client.GetDiagnostics(host.HostId); if(d==null)continue;
                EditorGUILayout.LabelField(host.HostId,d.State.ToString());
                EditorGUILayout.LabelField("Bound receive / send",d.ReceivePort+" / "+d.SendPort);
                EditorGUILayout.LabelField("Queue / dropped / parse errors",d.QueueDepth+" / "+d.NotificationDrops+" / "+d.ParseErrors);
            }
            Repaint();
        }
    }
    [InitializeOnLoad]
    internal static class EditorSessionCleanup
    {
        static EditorSessionCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ContactGloveBehaviour.StopAll;
            EditorApplication.quitting += ContactGloveBehaviour.StopAll;
            EditorApplication.playModeStateChanged += state =>
            {
                if(state==PlayModeStateChange.ExitingPlayMode) ContactGloveBehaviour.StopAll();
                if(state==PlayModeStateChange.EnteredPlayMode)
                    foreach(var owner in Object.FindObjectsByType<ContactGloveBehaviour>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                        if(owner.isActiveAndEnabled)owner.StartClient();
            };
        }
    }
}
