using System;
using System.Collections.Generic;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using UnityEngine.Events;

namespace Assets
{
    [Serializable]
    public class OnConnectedEvent : UnityEvent<string> { }

    public class NAFScene : MonoBehaviour
    {
        //TODO: move this into adapter
        public SocketIOUnity socket;

        public OnConnectedEvent onConnected;

        public string serverUrl = "http://localhost:8080";
        public string RoomToJoin = "dev";
        public List<NAFTemplate> templatePrefabs;
        public int NetworkUpdatesPerSecond = 15;

        public NafInterface nafAdapter;

        //TODO: move into adapter
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

        private bool _isRoomJoined = false;
        private bool _IsRoomJoined
        {
            get => _isRoomJoined;
            set
            {
                _isRoomJoined = value;
                Debug.Log($"Set _isRoomJoined for room {RoomToJoin} with val {value}");
            }
        }

        //TODO: move into adapter?
        private EasyRTCRoomData _roomData;
        private Dictionary<string, EasyRTCClientData> _lastLoggedInList = new Dictionary<string, EasyRTCClientData>();
        private EasyRTCAuthApplication _applicationData;

        /// <summary>
        /// List of tracked network entities
        /// </summary>
        private Dictionary<string, NetworkedEntity> _networkedEntities = new Dictionary<string, NetworkedEntity>();

        void Start()
        {
            //TODO: pass appropriate parameters here
            Connect();
        }

        // See NetworkConnection.js-->connect() and networked-scene.js-->connect()
        private void Connect(string serverUrl)
        {
            nafAdapter.SetServerUrl(serverUrl);
            nafAdapter.Connect(socket);
            nafAdapter.SetRoom(RoomToJoin);
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

            if (roomDataMsg.msgData.roomData.TryGetValue(RoomToJoin, out RoomDataInfo roomDataInfo))
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

        /// <summary>
        /// See NetworkConnection.dataChannelClosed and NetworkedEntities.removeEntitiesOfClient
        /// </summary>
        /// <param name="clientId"></param>
        private void DataChannelClosed(string clientId)
        {
            List<string> entitiesToRemove = new List<string>();
            //TODO: implement persistence
            foreach (var entity in _networkedEntities)
            {
                if (entity.Value.Creator == clientId || (string.IsNullOrEmpty(entity.Value.Creator) && entity.Value.Owner == clientId))
                {
                    entitiesToRemove.Add(entity.Key);
                }
            }

            entitiesToRemove.ForEach(x => RemoveEntity(x));
        }

        /// <summary>
        /// See NetworkedEntities.removeEntity
        /// </summary>
        /// <param name="entityToRemove"></param>
        private void RemoveEntity(string entityNetworkId)
        {
            if (_networkedEntities.TryGetValue(entityNetworkId, out var entity))
            {
                _networkedEntities.Remove(entityNetworkId);
                Destroy(entity.gameObject);
            } else
            {
                Debug.LogWarning("Tried to remove entity I don't have.");
            }
            

        }

        //Check out NetworkEntities.UpdateEntityMulti
        private void ProcessMsgTypeUpdateMulti(UpdateMultiMsgType updateMultiMsg)
        {
            Debug.Log($"Processing UpdateMulti msgType: {updateMultiMsg}");

            if (updateMultiMsg.targetRoom != RoomToJoin)
            {
                Debug.LogWarning($"UpdateMultiMsg with wrong room. Expected: {RoomToJoin}; Actual: {updateMultiMsg.targetRoom}");
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

            if (_networkedEntities.ContainsKey(entityData.networkId))
            {
                //Update an existing entity
                _networkedEntities[entityData.networkId].NetworkUpdate(entityData, updateMsg.serverTime);
            }
            else if (entityData.isFirstSync)
            {
                if (entityData.persistent)
                {
                    // If we receive a firstSync for a persistent entity that we don't have yet,
                    // we assume the scene will create it at some point, so stash the update for later use.
                    // TODO: see NetworkedEntities.updateEntity()
                    throw new NotImplementedException();
                }
                else
                {
                    ReceiveFirstUpdateFromEntity(entityData);
                }
            }
            else
            {
                Debug.LogWarning($"Unknown msgType Update: {updateMsg}");
            }
        }

        private void ReceiveFirstUpdateFromEntity(EntityData entityData)
        {
            //TODO: ignoring parent/child relationships for now
            CreateRemoteEntity(entityData);
        }

        private string StripTemplateId(string input)
        {
            return input.TrimStart('#');
        }

        private void CreateRemoteEntity(EntityData entityData)
        {
            Debug.Log($"Creating remote entity: {entityData}");
            NAFTemplate templatePrefab;

            try
            {
                templatePrefab = templatePrefabs.First(x => x.TemplateId == StripTemplateId(entityData.template));
            }
            catch (Exception)
            {
                Debug.LogError($"No template registered in Unity scene for TemplateId: {entityData.template}");
                throw;
            }

            NAFTemplate newObj;
            newObj = Instantiate(templatePrefab, entityData.GetPositionData(), entityData.GetRotationData());

            var networkedComponent = newObj.gameObject.AddComponent<NetworkedEntity>();
            networkedComponent.TemplateId = StripTemplateId(entityData.template);
            networkedComponent.Creator = entityData.creator;
            networkedComponent.Owner = entityData.owner;
            networkedComponent.NetworkId = entityData.networkId;
            networkedComponent.IsPersistent = entityData.persistent;
            networkedComponent.IsLocal = false;

            _networkedEntities.Add(networkedComponent.NetworkId, networkedComponent);

            networkedComponent.NetworkUpdate(entityData);
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

        class EasyRTCAuthResponse
        {
            public string msgType { get; set; }
            public EasyRTCAuthMsgDataResponse msgData { get; set; }
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

        class EasyRTCRoomData
        {
            public string roomName { get; set; }
            public string roomStatus { get; set; }
            public Dictionary<string, EasyRTCClientData> clientList { get; set; }
            public Dictionary<string, EasyRTCClientData> clientListDelta { get; set; }
        }

        class EasyRTCClientData
        {
            public string easyrtcid { get; set; }
            public double roomJoinTime { get; set; }
            public object presence { get; set; }
        }

        class EasyRTCClientPresence
        {
            public string show { get; set; }
            public string status { get; set; }
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
                    onConnected.Invoke(_EasyRTCId);
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

        private void ProcessApplicationData(EasyRTCAuthApplication application)
        {
            _applicationData = application;
        }

        private void ProcessRoomData(Dictionary<string, EasyRTCRoomData> roomData)
        {
            EasyRTCRoomData myRoomData;
            if (!roomData.TryGetValue(RoomToJoin, out myRoomData))
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
    }
}
