using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using Newtonsoft.Json;



namespace Assets
{
    public class WsEasyRTCAdapter : NafInterface
    {
        private SocketIOUnity _socketIOUnity;
        private string room;
        private string serverUrl;

        private bool _isRoomJoined = false;
        private bool _IsRoomJoined
        {
            get => _isRoomJoined;
            set
            {
                _isRoomJoined = value;
                Debug.Log($"Set _isRoomJoined for room {this.room} with val {value}");
            }
        }

        private EasyRTCRoomData _roomData;
        private Dictionary<string, EasyRTCClientData> _lastLoggedInList = new Dictionary<string, EasyRTCClientData>();
        private EasyRTCAuthApplication _applicationData;

        private string _easyRTCId;
        private string _EasyRTCId
        {
            get => _easyRTCId;
            set
            {
                _easyRTCId = value;
                Debug.Log($"Setting _EasyRTCId: {value}");
            }
        }

        #region EasyRTC DTOs
        class EasyRTCAuthResponse
        {
            public string msgType { get; set; }
            public EasyRTCAuthMsgDataResponse msgData { get; set; }
        }

        class EasyRTCClientData
        {
            public string easyrtcid { get; set; }
            public double roomJoinTime { get; set; }
            public object presence { get; set; }
        }

        class EasyRTCRoomData
        {
            public string roomName { get; set; }
            public string roomStatus { get; set; }
            public Dictionary<string, EasyRTCClientData> clientList { get; set; }
            public Dictionary<string, EasyRTCClientData> clientListDelta { get; set; }
        }

        class EasyRTCAuthApplication
        {
            public string applicationName { get; set; }
        }

        class EasyRTCAuthMsgDataResponse
        {
            public Dictionary<string, EasyRTCRoomData> roomData { get; set; }
            public EasyRTCAuthApplication application { get; set; }
            public string easyrtcid { get; set; }
            public Dictionary<string, object> iceConfig { get; set; }
            public double serverTime { get; set; }
        }

        class EasyRTCAuthMsgDataDTO
        {
            public string apiVersion { get; set; }
            public string applicationName { get; set; }
            public Dictionary<string, object> roomJoin { get; set; }
            public Dictionary<string, object> setUserCfg { get; set; }
        }
        class EasyRTCAuthDTO
        {
            public string msgType { get; set; }
            public EasyRTCAuthMsgDataDTO msgData { get; set; }
        }


        class BaseUpdateMsgType
        {
            public string senderEasyrtcid { get; set; }
            public string msgType { get; set; }
            public string easyrtcid { get; set; }
            public double serverTime { get; set; }
        }

        class UpdateMsgType : BaseUpdateMsgType
        {
            public string targetEasyrtcid { get; set; }
            public EntityData msgData { get; set; }
        }

        class UpdateMultiMsgType : BaseUpdateMsgType
        {
            public string targetRoom { get; set; }

            public Dictionary<string, List<EntityData>> msgData { get; set; }
        }

        class RoomDataMsgType: EasyRTCMsgDTO
        {
            public double serverTime { get; set; }
            public string easyrtcid { get; set; }
            public new RoomDataDict msgData { get; set; }

        }

        class RoomDataDict
        {
            public Dictionary<string, RoomDataInfo> roomData { get; set; }
        }

        class RoomDataInfo
        {
            public string roomName { get; set; }
            public string roomStatus { get; set; }
            public ClientListDelta clientListDelta { get; set; }
        }

        class ClientListDelta
        {
            public Dictionary<string, EasyRTCClientInfo> removeClient { get; set; }
        }

        class EasyRTCClientInfo
        {
            public string easyrtcid { get; set; }
        }
        class EasyRTCMsgDTO
        {
            public string msgType { get; set; }
            public object msgData { get; set; }
        }

        #endregion

        public override void Connect()
        {
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
            //var uri = new Uri("http://localhost:8080");
            var uri = new Uri(this.serverUrl);
            Debug.Log($"Starting WS connection for uri: {uri}");
            var socket = new SocketIOUnity(uri, new SocketIOOptions
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

            ///// reserved socketio events =====
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
            //===================================

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
        /// Sends the easyrtcAuth message to the WS server
        /// </summary>
        private async void EmitEasyRTCAuth(SocketIOUnity socket)
        {

            await socket.EmitAsync("easyrtcAuth", response => {
                Debug.Log("Response received from EasyRTC auth call:");
                Debug.Log(response);

                var responseVal = response.GetValue<EasyRTCAuthResponse>();
                if (responseVal.msgType == "error")
                {
                    Debug.LogError($"Error received while connecting to WS server: {JsonConvert.SerializeObject(responseVal.msgData)}");
                }
                else
                {
                    //TODO: port over processToken() method from easyrtc.js
                    var msgData = responseVal.msgData;
                    _EasyRTCId = msgData.easyrtcid;

                    if (msgData.roomData != null)
                    {
                        ProcessRoomData(msgData.roomData);
                    }

                    if (msgData.application != null)
                    {
                        ProcessApplicationData(msgData.application);
                    }

                    //Trigger event informing local Unity components of successful connection
                    OnConnected.Invoke(_EasyRTCId);
                }
            }, new EasyRTCAuthDTO
            {
                msgType = "authenticate",
                msgData = new EasyRTCAuthMsgDataDTO
                {
                    apiVersion = "1.1.1-beta",
                    applicationName = "default",
                    roomJoin = new Dictionary<string, object>()
                {
                    { "dev", new Dictionary<string, string>()
                        {
                            { "roomName", "dev" }
                        }
                    }
                }
                }
            });
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
    
        /// <summary>
        /// Look at NetworkConnection.setupDefaultDataSubscriptions
        /// That defines the NAF handlers for the Update/UpdateMulti/Remove
        /// msgTypes
        /// </summary>
        /// <param name="msg"></param>
        private void ProcessMessage(SocketIOResponse msg)
        {
            var parsedMsg = msg.GetValue<EasyRTCMsgDTO>();
            Debug.Log($"ParsedMsg value with msgType {parsedMsg.msgType}: {parsedMsg}");
            switch (parsedMsg.msgType)
            {
                case "u":
                    UnityThread.executeInUpdate(() => {
                        ProcessMsgTypeUpdate(msg.GetValue<UpdateMsgType>());
                    });
                    break;
                case "um":
                    UnityThread.executeInUpdate(() => {
                        ProcessMsgTypeUpdateMulti(msg.GetValue<UpdateMultiMsgType>());
                    });
                    break;
                case "roomData":
                    UnityThread.executeInUpdate(() => {
                        ProcessMsgTypeRoomData(msg.GetValue<RoomDataMsgType>());
                    });
                    
                    break;
                default:
                    Debug.LogError($"Unexpected message type received: {msg}");
                    break;
            }
        }

        private void ProcessMsgTypeRoomData(RoomDataMsgType roomDataMsg)
        {
            Debug.Log($"Processing RoomDataMsgType");

            if (roomDataMsg.msgData.roomData.TryGetValue(this.room, out RoomDataInfo roomDataInfo))
            {
                if (roomDataInfo.clientListDelta?.removeClient != null)
                {
                    foreach(var clientId in roomDataInfo.clientListDelta.removeClient.Keys)
                    {
                        DataChannelClosed(clientId);
                    }
                } else
                {
                    Debug.LogWarning("Unimplemented roomData message");
                }
            } else
            {
                Debug.LogWarning("RoomDataMsgType received with value missing for current room");
            }
        }

        //Check out NetworkEntities.UpdateEntityMulti
        private void ProcessMsgTypeUpdateMulti(UpdateMultiMsgType updateMultiMsg)
        {
            Debug.Log($"Processing UpdateMulti msgType: {updateMultiMsg}");

            if (updateMultiMsg.targetRoom != this.room)
            {
                Debug.LogWarning($"UpdateMultiMsg with wrong room. Expected: {this.room}; Actual: {updateMultiMsg.targetRoom}");
            }

            var entityDatum = updateMultiMsg.msgData["d"];
            foreach (EntityData entityData in entityDatum)
            {
                UpdateMsgType updateMsg = new UpdateMsgType
                {
                    easyrtcid = updateMultiMsg.easyrtcid,
                    msgData = entityData,
                    msgType = "u",
                    senderEasyrtcid = updateMultiMsg.senderEasyrtcid,
                    serverTime = updateMultiMsg.serverTime,
                    targetEasyrtcid = _EasyRTCId
                };
                ProcessMsgTypeUpdate(updateMsg);
            }
        }

        /// <summary>
        /// Check out NetworkEntities.updateEntity
        /// </summary>
        /// <param name="parsedMsg"></param>
        private void ProcessMsgTypeUpdate(UpdateMsgType updateMsg)
        {
            var entityData = updateMsg.msgData;

            Debug.Log($"Processing Update msgType with entityData: {entityData}");

            if (updateMsg.targetEasyrtcid != _EasyRTCId)
            {
                Debug.LogWarning($"Update message type received for wrong targetEasyrtcid. Msg: {updateMsg}");
            }

            this.OnEntityUpdate.Invoke(entityData, updateMsg.serverTime);
        }

        private void ProcessApplicationData(EasyRTCAuthApplication application)
        {
            _applicationData = application;
        }

        private void ProcessRoomData(Dictionary<string, EasyRTCRoomData> roomData)
        {
            EasyRTCRoomData myRoomData;
            if (!roomData.TryGetValue(this.room, out myRoomData))
            {
                Debug.LogError($"Invoked ProcessRoomData for an invalid room.");
            }

            if (myRoomData.roomStatus == "join")
            {
                _IsRoomJoined = true;
                _roomData = myRoomData;
            }
            else if (myRoomData.roomStatus == "leave")
            {
                _IsRoomJoined = false;
                _roomData = null;
            }

            if (myRoomData.clientList != null)
            {
                _lastLoggedInList = myRoomData.clientList;
            }
            else if (myRoomData.clientListDelta != null)
            {
                //TODO: update this once we get to the point of calling
                // on updates
            }
        }

        /// <summary>
        /// See NetworkConnection.dataChannelClosed and NetworkedEntities.removeEntitiesOfClient
        /// </summary>
        /// <param name="clientId"></param>
        private void DataChannelClosed(string clientId)
        {
            this.OnDataChannelClosed.Invoke(clientId);
        }
    }
}