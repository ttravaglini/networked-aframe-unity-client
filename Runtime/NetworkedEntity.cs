using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets
{
    /// <summary>
    /// Defines the behavior for objects that are instantiated/updated over the network.
    /// Largely mirrors the "networked.js" class in the NAF repo.
    /// </summary>
    public class NetworkedEntity : MonoBehaviour
    {
        //Only used for local instances that are synced over the network
        public NAFScene NafScene = null;

        public string TemplateId { get; set; }

        [HideInInspector]
        public string Creator { get; set; }

        [HideInInspector]
        public string Owner { get; set; }

        [HideInInspector]
        public string NetworkId { get; set; }

        [HideInInspector]
        public bool IsPersistent { get; set; }

        /// <summary>
        /// If True, then this NetworkedEntity is a local object
        /// within Unity that will be sending out network updates to
        /// other objects. If false, then this object was instantiated
        /// by a client outside of this Unity instance.
        /// </summary>
        [HideInInspector]
        public bool IsLocal = true;

        private double _lastOwnerTime = -1;

        ///Position lerp variables
        private double? _previousServerTime;
        private double? _currentServerTime;
        private Vector3? _previousServerPosition;
        private Vector3? _currentServerPosition;
        private float _serverSpeed = 1.0f;


        //Rotation lerp variables
        private Quaternion? _previousServerRotation;
        private Quaternion? _currentServerRotation;
        // Angular speed in degrees per sec.
        private float _serverRotationSpeed = 180.0f;


        //Network update private variables
        private float _nextSyncTime = 0f;
        private Vector3 _positionCache;
        private Quaternion _rotationCache;


        private Dictionary<string, string> _componentSchemaMapping = new Dictionary<string, string>
        {
            { "0", "position" },
            { "1", "rotation" }
        };

        private List<CustomComponentParser> _customComponentParsers;

        private void Awake()
        {
            var template = this.gameObject.GetComponent<NAFTemplate>();
            TemplateId = template.TemplateId;
            _customComponentParsers = this.gameObject.GetComponent<NAFTemplate>().CustomComponentParsers;
        }

        /// <summary>
        /// Process a network update for this component.
        /// See networked.js->networkUpdate
        /// </summary>
        /// <param name="entityData"></param>
        public void NetworkUpdate(EntityData entityData, double? serverTime = null)
        {
            if (entityData.lastOwnerTime < _lastOwnerTime)
            {
                Debug.LogWarning("entityData NetworkUpdate received with wrong lastOwnerTime");
                return;
            }

            if (entityData.owner != Owner)
            {
                //TODO: implement ownership transfer
                Debug.LogWarning("entityData NetworkUpdate received with wrong owner");
                return;
            }

            _previousServerTime = _currentServerTime;
            _currentServerTime = serverTime;

            UpdateNetworkedComponents(entityData.components);
        }

        /// <summary>
        /// Updates the individual components for a networked entity.
        /// See networked.js-->updateNetworkedComponents
        /// </summary>
        /// <param name="components"></param>
        public void UpdateNetworkedComponents(Dictionary<string, object> components)
        {
            foreach (var component in components)
            {
                string componentSchema;
                if (_componentSchemaMapping.TryGetValue(component.Key, out componentSchema))
                {
                    UpdateNetworkedComponent(componentSchema, component.Value); 
                } else
                {
                    UpdateCustomNetworkedComponent(component.Key, component.Value);
                }
            }
            return;
        }

        private void UpdateNetworkedComponent(string componentSchema, object value)
        {
            switch(componentSchema)
            {
                case "position":
                    _previousServerPosition = _currentServerPosition;
                    _currentServerPosition = EntityData.ParseEntityPositionComponent(value);

                    //Calculate network speed
                    if (_currentServerPosition.HasValue & _previousServerPosition.HasValue 
                        & _currentServerTime.HasValue & _previousServerTime.HasValue 
                        & (_currentServerTime - _previousServerTime) > 0)
                    {
                        _serverSpeed = (_currentServerPosition.Value - _previousServerPosition.Value).magnitude /((float)(_currentServerTime.Value - _previousServerTime)/1000);
                    }

                    MoveTowardsServerPosition();
                    break;
                case "rotation":
                    _previousServerRotation = _currentServerRotation;
                    _currentServerRotation = EntityData.ParseEntityRotationComponent(value);

                    //Calculate network rotation speed
                    if (_currentServerRotation.HasValue & _previousServerRotation.HasValue
                        & _currentServerTime.HasValue & _previousServerTime.HasValue
                        & (_currentServerTime - _previousServerTime) > 0)
                    {
                        _serverRotationSpeed = CalculateAngularVelocity(_previousServerRotation.Value, _currentServerRotation.Value, ((float)(_currentServerTime - _previousServerTime))/1000);
                    }

                    RotateTowardsServerRotation();
                    break;
                case "scale":
                    //TODO: implement lerped scaling changes
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Updates the value for a custom component on the template.
        /// The custom component must have a CustomComponentParser defined.
        /// </summary>
        /// <param name="componentIndex"></param>
        /// <param name="value"></param>
        private void UpdateCustomNetworkedComponent(string componentIndex, object value)
        {
            try
            {
                //Pull custom parser from attached NAFTemplate
                CustomComponentParser customComponentParser =
                    _customComponentParsers.SingleOrDefault(x => x.ComponentIndex.ToString() == componentIndex);

                if (customComponentParser == null)
                {
                    Debug.Log($"Custom component parser not defined in NAFTemplate for index {componentIndex}");
                    return;
                }

                customComponentParser.ParseCustomComponentValue(value);


            } catch(InvalidOperationException)
            {
                Debug.LogError($"Invalid NAFTemplate; more than one custom component parser defined for index {componentIndex}");
            }
            
        }

        /// <summary>
        /// Calculates angular velocity in degrees per second.
        /// </summary>
        /// <param name="previousRotation"></param>
        /// <param name="currentRotation"></param>
        /// <param name="deltaTime"></param>
        /// <returns></returns>
        private float CalculateAngularVelocity(Quaternion previousRotation, Quaternion currentRotation, float deltaTime)
        {
            Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
            float angle;
            Vector3 axis;
            deltaRotation.ToAngleAxis(out angle, out axis);
            return angle / deltaTime;
        }

        /// <summary>
        /// Moves the current gameObject towards the last received server position.
        /// https://docs.unity3d.com/ScriptReference/Vector3.MoveTowards.html
        /// https://gamedevbeginner.com/the-right-way-to-lerp-in-unity-with-examples/
        /// </summary>
        private void MoveTowardsServerPosition()
        {
            if (_currentServerPosition.HasValue && _currentServerPosition.Value != transform.position)
            {
                float step = _serverSpeed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, _currentServerPosition.Value, step);
            }
        }

        private void RotateTowardsServerRotation()
        {
            if (_currentServerRotation.HasValue && _currentServerRotation.Value != transform.rotation)
            {
                float step = _serverRotationSpeed * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _currentServerRotation.Value, step);
            }
        }

        /// <summary>
        /// See networked.js-->onConnected
        /// </summary>
        private void OnConnected(string ownerId)
        {
            UnityThread.executeInUpdate(() => {
                //TODO: set lastOwnerTime
                Debug.Log($"Inside NetworkedEntity.OnConnected with ownerId {ownerId}");
                this.Owner = ownerId;
                this.Creator = ownerId;

                SyncAll();
            });
        }

        /// <summary>
        /// See networked.js-->SyncAll
        /// </summary>
        private void SyncAll()
        {
            var components = GatherComponentsData();

            var syncData = this.CreateSyncData(components);

            this.NafScene.nafAdapter.BroadcastDataGuaranteed("u", syncData);
        }

        /// <summary>
        /// See networked.js-->createSyncData
        /// </summary>
        /// <returns></returns>
        private EntityData CreateSyncData(Dictionary<string, object> components)
        {
            return new EntityData
            {
                networkId = this.NetworkId,
                owner = this.Owner,
                creator = this.Creator,
                lastOwnerTime = this._lastOwnerTime,
                template = $"#{this.TemplateId}",
                persistent = this.IsPersistent,
                components = components,
                isFirstSync = true
            };
        }

        /// <summary>
        /// See networked.js-->gatherComponentsData
        /// </summary>
        private Dictionary<string, object> GatherComponentsData(bool fullSync = true)
        {
            Dictionary<string, object> retVal = new Dictionary<string, object>();
            foreach (var schema in _componentSchemaMapping)
            {
                switch(schema.Value)
                {
                    case "position":
                        if (fullSync || this.transform.position != _positionCache)
                        {
                            retVal.Add(schema.Key, this.Vector3ToDictionary(this.gameObject.transform.position));
                            _positionCache = this.transform.position;
                        }
                        break;
                    case "rotation":
                        if (fullSync || this.transform.rotation != _rotationCache)
                        {
                            retVal.Add(schema.Key, this.Vector3ToDictionary(this.gameObject.transform.eulerAngles));
                            _rotationCache = this.transform.rotation;
                        }
                        break;
                    default:
                        break;
                }
            }

            foreach(var customComponent in _customComponentParsers)
            {
                if (fullSync || customComponent.RequiresNetworkUpdate())
                {
                    retVal.Add(customComponent.ComponentIndex.ToString(), customComponent.GenerateCustomComponentValue());
                }
            }

            return retVal;
        }

        /// <summary>
        /// Converts a position/rotation Vector3 to a Dictionary that can be sent through network
        /// </summary>
        /// <param name="inputVector"></param>
        private Dictionary<string, float> Vector3ToDictionary(Vector3 inputVector)
        {
            return new Dictionary<string, float>
            {
                { "x", inputVector.x },
                { "y", inputVector.y },
                { "z", inputVector.z }
            };
        }

        /// <summary>
        /// Sends an update from this NetworkedEntity to other clients
        /// informing them of a change to this NetworkedEntity.
        /// 
        /// Refer to networked.js --> tick()
        /// </summary>
        private void UpdateNetworkComponents()
        {
            if (!this.IsLocal || this.NafScene == null || string.IsNullOrEmpty(this.Owner))
            {
                return;
            }

            //Check if it's time to send out another network update
            if (Time.time < _nextSyncTime)
            {
                return;
            }

            var syncData = this.SyncDirty();

            //Return early if we don't have any component data to send an update for
            if (syncData == null)
            {
                return;
            }

            //In the NAF code, it loops through all of the components and builds an individual
            //syncData object for each component. Not sure why it does that... for now
            //we'll attempt to just pull all of the data to be synced at once.
            Dictionary<string, dynamic> dataToSend = new Dictionary<string, dynamic>
            {
                { "d", new EntityData[] { syncData } }
            };

            this.NafScene.nafAdapter.BroadcastData("um", dataToSend);

            this.UpdateNextSyncTime();
        }

        /// <summary>
        /// Generates updated sync data for the networked entity
        /// that only includes updated component values (in order to cut down
        /// on the number of network updates)
        /// </summary>
        /// <returns></returns>
        private EntityData SyncDirty()
        {
            var components = this.GatherComponentsData(fullSync: false);

            if (components.Count == 0)
            {
                return null;
            }

            return this.CreateSyncData(components);
        }

        /// <summary>
        /// See networked.js --> UpdateNextSyncTime
        /// </summary>
        private void UpdateNextSyncTime()
        {
            this._nextSyncTime = Time.time + 1f / (float)NafScene.NetworkUpdatesPerSecond;
        }

        private void Start()
        {
            if (this.IsLocal && this.NafScene != null)
            {
                _positionCache = this.gameObject.transform.position;
                _rotationCache = this.gameObject.transform.rotation;
                this.NafScene.onConnected.AddListener(this.OnConnected);
            }

            if (string.IsNullOrEmpty(NetworkId))
            {
                NetworkId = NAFUtils.CreateNetworkId();
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (!this.IsLocal)
            {
                //Update this object to reflect coordinates received from the NAF server
                MoveTowardsServerPosition();
                RotateTowardsServerRotation();
            }
            else
            {
                //Send the NAF server the updates about this object
                UpdateNetworkComponents();
            }
        }
    }
}