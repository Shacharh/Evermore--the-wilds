using UnityEngine;

public class ShowIfAttribute : PropertyAttribute
{
    public string conditionFieldName;
    public string compareValue;
    public bool invert;

    // Constructor for boolean fields
    public ShowIfAttribute(string boolFieldName)
    {
        this.conditionFieldName = boolFieldName;
        this.compareValue = null;
        this.invert = boolFieldName.StartsWith("!");
        if (this.invert)
            this.conditionFieldName = boolFieldName.Substring(1);
    }

    // Constructor for enum comparisons - use "|" to separate multiple values
    public ShowIfAttribute(string fieldName, string value)
    {
        this.conditionFieldName = fieldName;
        this.compareValue = value;
        this.invert = false;
    }
}