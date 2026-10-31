using UnityEngine;

namespace UnityEditorOnline.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class SyncId : MonoBehaviour
    {
        [HideInInspector, SerializeField] 
        private string _syncNetId;
        public string Guid => _syncNetId;
        public void Assign(string guid) 
        { 
            hideFlags = HideFlags.HideInInspector;
            _syncNetId = guid; 
        }
        public static string NewGuid() => System.Guid.NewGuid().ToString("N");
    }
}
