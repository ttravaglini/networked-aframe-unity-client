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
        public OnConnectedEvent onConnected;

        public string serverUrl = "http://localhost:8080";
        public string RoomToJoin = "dev";
        public List<NAFTemplate> templatePrefabs;
        public int NetworkUpdatesPerSecond = 15;

        public NafInterface nafAdapter;

        /// <summary>
        /// List of tracked network entities
        /// </summary>
        private Dictionary<string, NetworkedEntity> _networkedEntities = new Dictionary<string, NetworkedEntity>();

        void Start()
        {
            //TODO: do we remove the call to Connect() from here? So that library clients can decide when to
            //initiate the network connection themselves.
            Connect();

            //Subscribe to events from the nafAdapter
            this.nafAdapter.OnConnected.AddListener(this.OnConnectedListener);
            this.nafAdapter.OnEntityUpdate.AddListener(this.EntityUpdateListener);
            this.nafAdapter.OnDataChannelClosed.AddListener(this.RemoveEntitiesOfClient);
        }

        // See NetworkConnection.js-->connect() and networked-scene.js-->connect()
        private void Connect()
        {
            nafAdapter.SetServerUrl(this.serverUrl);
            nafAdapter.Connect();
            nafAdapter.SetRoom(RoomToJoin);
        }

        private void EntityUpdateListener(EntityData entityData, double serverTime)
        {
            if (_networkedEntities.ContainsKey(entityData.networkId))
            {
                //Update an existing entity
                _networkedEntities[entityData.networkId].NetworkUpdate(entityData, serverTime);
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
                Debug.LogWarning($"Unknown entityData in EntityUpdateListener: {entityData}");
            }
        }

        private void OnConnectedListener(string clientId)
        {
            onConnected.Invoke(clientId);
        }

        /// <summary>
        /// See NetworkConnection.dataChannelClosed and NetworkedEntities.removeEntitiesOfClient
        /// </summary>
        /// <param name="clientId"></param>
        private void RemoveEntitiesOfClient(string clientId)
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
    }
}
