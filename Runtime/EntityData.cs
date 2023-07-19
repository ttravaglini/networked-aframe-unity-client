using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets
{
    public class EntityData
    {
        public string networkId { get; set; }
        public string owner { get; set; }
        public string creator { get; set; }
        public double lastOwnerTime { get; set; }
        public string template { get; set; }
        public bool persistent { get; set; }
        public Dictionary<string, object> components { get; set; }
        public bool isFirstSync { get; set; }

        public Vector3 GetPositionData()
        {
            object entityPosition;
            if (!components.TryGetValue("position", out entityPosition))
            {
                if (!components.TryGetValue("0", out entityPosition))
                {
                    throw new Exception("Unable to get position from components data");
                }
            }

            return ParseEntityPositionComponent(entityPosition);
        }

        public static Vector3 ParseEntityPositionComponent(object entityPositionObject)
        {
            IDictionary<string, JToken> positionDict = (IDictionary<string, JToken>)entityPositionObject;

            return new Vector3(positionDict["x"].ToObject<float>(), positionDict["y"].ToObject<float>(), positionDict["z"].ToObject<float>());
        }

        public Quaternion GetRotationData()
        {
            object entityRotation;
            if (!components.TryGetValue("rotation", out entityRotation))
            {
                if (!components.TryGetValue("1", out entityRotation))
                {
                    throw new Exception("Unable to get rotation from components data");
                }
            }

            return ParseEntityRotationComponent(entityRotation);
        }

        public static Quaternion ParseEntityRotationComponent(object entityRotationObject)
        {
            IDictionary<string, JToken> rotationDict = (IDictionary<string, JToken>)entityRotationObject;

            return Quaternion.Euler(rotationDict["x"].ToObject<float>(), rotationDict["y"].ToObject<float>(), rotationDict["z"].ToObject<float>());
        }
    }

}