using UnityEngine;

public class ShowIfAttribute : PropertyAttribute
{
    public string conditionFieldName;
    public string compareValue;
    public bool invert;

    // Optional AND condition (enum field + value)
    public string andConditionFieldName;
    public string andCompareValue;

    // Constructor for boolean fields
    public ShowIfAttribute(string boolFieldName)
    {
        this.conditionFieldName = boolFieldName.StartsWith("!") ? boolFieldName.Substring(1) : boolFieldName;
        this.compareValue = null;
        this.invert = boolFieldName.StartsWith("!");
    }

    // Constructor for enum comparisons - use "|" to separate multiple values
    public ShowIfAttribute(string fieldName, string value)
    {
        this.conditionFieldName = fieldName;
        this.compareValue = value;
        this.invert = false;
    }

    // Constructor for bool AND enum - shows only when bool condition passes AND enum matches
    public ShowIfAttribute(string boolFieldName, string andEnumFieldName, string andEnumValue)
    {
        this.conditionFieldName = boolFieldName.StartsWith("!") ? boolFieldName.Substring(1) : boolFieldName;
        this.compareValue = null;
        this.invert = boolFieldName.StartsWith("!");
        this.andConditionFieldName = andEnumFieldName;
        this.andCompareValue = andEnumValue;
    }
}