using UnityEngine;

public class ShowIfAttribute : PropertyAttribute
{
    public string boolFieldName;

    public ShowIfAttribute(string boolFieldName)
    {
        this.boolFieldName = boolFieldName;
    }
}
