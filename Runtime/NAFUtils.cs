using System.Collections;
using UnityEngine;

namespace Assets
{
    /// <summary>
    /// See utils.js
    /// </summary>
    public class NAFUtils
    {
        public static string CreateNetworkId()
        {
            const string characters = "abcdefghijklmnopqrstuvwxyz0123456789";
            char[] result = new char[7];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = characters[Random.Range(0, characters.Length)];
            }

            return new string(result);
        }
    }
}