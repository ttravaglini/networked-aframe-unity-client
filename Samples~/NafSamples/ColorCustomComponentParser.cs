using System.Collections;
using UnityEngine;

namespace Assets
{
    public class ColorCustomComponentParser : CustomComponentParser
    {

        public GameObject componentToColor;

        public override void ParseCustomComponentValue(object componentVal)
        {
            string newColor = (string)componentVal;

            if (ColorUtility.TryParseHtmlString(newColor, out var parsedColor))
            {
                componentToColor.GetComponent<Renderer>().material.color = parsedColor;
            } else
            {
                Debug.LogWarning($"Invalid color received in ColorCustomComponentParser: {newColor}");
            }

        }

        public override object GenerateCustomComponentValue()
        {
            //Pull the hex-code for the color value from the componentToColor gameObject.
            //Send this as part of the payload to other clients rendering this component.
            var colorToSend = componentToColor.GetComponent<Renderer>().material.color;
            return $"#{ColorUtility.ToHtmlStringRGB(colorToSend)}";

        }
    }
}