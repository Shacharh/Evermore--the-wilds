using XNode;
using UnityEngine;

public abstract class BaseDialogueNode : Node
{
    [TextArea(2, 5)]
    public string prompt;

    [Input(backingValue = ShowBackingValue.Never, connectionType = ConnectionType.Override)]
    public BaseDialogueNode input;

    public abstract BaseDialogueNode GetNextNode(int optionIndex = 0);
}
