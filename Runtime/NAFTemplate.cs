using Assets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NAFTemplate : MonoBehaviour
{
    /// <summary>
    /// The identifier for this template in the a-frame scene. This is the field identified
    /// by NAF.schemas.template. For example, the #avatar-tempalte in the NAF examples.
    /// This templateId field in C# should not contain the leading '#' sign.
    /// </summary>
    public string TemplateId;

    /// <summary>
    /// A list of the custom component parsers required for this template, excluding the
    /// 'position' and 'rotation' components.
    /// </summary>
    public List<CustomComponentParser> CustomComponentParsers = new List<CustomComponentParser>();

    void Start()
    {
        if(string.IsNullOrEmpty(TemplateId))
        {
            Debug.LogWarning("TemplateId not set for NAFTemplate");
        }
    }
}
