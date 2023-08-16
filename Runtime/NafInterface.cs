using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Assets
{
    public class OnEntityUpdate: UnityEvent<EntityData, double> {}
    public class OnDataChannelClosed: UnityEvent<string> {}

    public class OnConnected : UnityEvent<string> { }

    //see NoOpAdapter.js
    public abstract class NafInterface : MonoBehaviour
    {
        public OnConnected OnConnected { get; set; }
        public OnEntityUpdate OnEntityUpdate { get; set; }
        public OnDataChannelClosed OnDataChannelClosed { get; set; }
        
        public abstract void Connect();
        
        public abstract void BroadcastDataGuaranteed(string dataType, dynamic data);

        public abstract void BroadcastData(string dataType, dynamic data);

        public abstract void SetRoom(string roomName);

        public abstract void SetServerUrl(string serverUrl);
    }
}