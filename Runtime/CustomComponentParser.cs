using UnityEngine;

namespace Assets
{
    public abstract class CustomComponentParser : MonoBehaviour
    {
        public int ComponentIndex;

        /// <summary>
        /// Parses the custom networkedEntity component value
        /// returned from the server.
        /// </summary>
        /// <param name="componentVal"></param>
        public abstract void ParseCustomComponentValue(object componentVal);

        /// <summary>
        /// Generates the custom component value from the local
        /// Unity object that will then be broadcast to the server.
        /// </summary>
        /// <returns></returns>
        public abstract object GenerateCustomComponentValue();

        public bool RequiresNetworkUpdate()
        {
            return false;
        }
    }
}