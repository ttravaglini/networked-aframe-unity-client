using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Newtonsoft.Json;

namespace Assets
{
    public class WsEasyRTCAdapter : NafInterface
    {
        private SocketIOUnity _socketIOUnity;
        private string room;
        private string serverUrl;

        public void Connect(SocketIOUnity socketIOUnity)
        {
            //TODO: need to add appropriate arguments to the ConnectSocket method
            _socketIOUnity = ConnectSocket();
        }
        public override void BroadcastData(string dataType, dynamic data)
        {
            this.sendDataWS(msgType: dataType, msgData: data, targetRoom: this.room);
        }

        public override void BroadcastDataGuaranteed(string dataType, dynamic data)
        {
            this.BroadcastData(dataType, data);
        }

        public override void SetRoom(string roomName)
        {
            this.room = roomName;
        }

        public override void SetServerUrl(string serverUrl)
        {
            this.serverUrl = serverUrl;
        }

        private SocketIOUnity ConnectSocket()
        {
            var uri = new Uri("http://localhost:8080");
            Debug.Log($"Starting WS connection for uri: {uri}");
            socket = new SocketIOUnity(uri, new SocketIOOptions
            {
                Query = new Dictionary<string, string>
                {
                    {"token", "UNITY" }
                }
                ,
                EIO = 3
                ,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
            });
            socket.JsonSerializer = new NewtonsoftJsonSerializer();

            ///// reserved socketio events
            socket.OnConnected += (sender, e) =>
            {
                Debug.Log("socket.OnConnected");
                EmitEasyRTCAuth(socket);
            };
            socket.OnDisconnected += (sender, e) =>
            {
                Debug.Log("disconnect: " + e);
            };
            socket.OnReconnectAttempt += (sender, e) =>
            {
                Debug.Log($"{DateTime.Now} Reconnecting: attempt = {e}");
            };
            ///////

            //My custom events from easyrtc ===============
            socket.On("easyrtcCmd", (msg) =>
            {
                Debug.Log($"easyrtcCmd with msg: '{msg}'");
                ProcessMessage(msg);
            });

            socket.On("easyrtcMsg", (msg) =>
            {
                Debug.Log($"easyrtcMsg with msg: '{msg}'");
                ProcessMessage(msg);
            });

            //============================================

            Debug.Log("Connecting...");
            socket.Connect();

            return socket;
        }

        /// <summary>
        /// See easyrtc.js-->sendDataWS
        /// </summary>
        private void sendDataWS(string msgType, dynamic msgData, string targetRoom = null)
        {
            try
            {
                Dictionary<string, dynamic> outgoingMessage = new Dictionary<string, dynamic>
                {
                    { "msgType", msgType },
                    { "msgData", msgData }
                };

                if (!string.IsNullOrEmpty(targetRoom))
                {
                    outgoingMessage.Add("targetRoom", targetRoom);
                }

                var fullMsgJson = JsonConvert.SerializeObject(outgoingMessage);

                //Debug.Log($"sendDataWS. eventName: 'easyrtcMsg'; data: {JsonUtility.ToJson(msgData)}");

                _socketIOUnity.Emit("easyrtcMsg", (response) => {
                    string text = response.GetValue<string>();
                    Debug.Log("easyrtcMsg ack response: ");
                    Debug.Log(text);
                }, outgoingMessage);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
            
        }
    }
}