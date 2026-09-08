using XNode;
using UnityEngine;

[CreateNodeMenu("Dialogue/Simple Dialogue")]
public class SimpleDialogueNode : BaseDialogueNode
{
    public string speakerName;

    [Output(connectionType = ConnectionType.Override)]
    public BaseDialogueNode next;

    public override BaseDialogueNode GetNextNode(int optionIndex = 0)
    {
        NodePort port = GetOutputPort(nameof(next));
        return port?.Connection?.node as BaseDialogueNode;
    }
}
