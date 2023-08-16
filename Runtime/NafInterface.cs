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
        public OnConnected OnConnected = new OnConnected();
        public OnEntityUpdate OnEntityUpdate = new OnEntityUpdate();
        public OnDataChannelClosed OnDataChannelClosed = new OnDataChannelClosed();
        
        public abstract void Connect();
        
        public abstract void BroadcastDataGuaranteed(string dataType, dynamic data);

        public abstract void BroadcastData(string dataType, dynamic data);

        public abstract void SetRoom(string roomName);

        public abstract void SetServerUrl(string serverUrl);
    }
}