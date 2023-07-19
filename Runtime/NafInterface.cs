using UnityEditor;
using UnityEngine;

namespace Assets
{
    public abstract class NafInterface : MonoBehaviour
    {
        public abstract void BroadcastDataGuaranteed(string dataType, dynamic data);

        public abstract void BroadcastData(string dataType, dynamic data);

        public abstract void SetRoom(string roomName);
    }
}