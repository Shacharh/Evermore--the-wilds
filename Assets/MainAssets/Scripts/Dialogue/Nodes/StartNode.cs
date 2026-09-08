using XNode;
using UnityEngine;

[CreateNodeMenu("Dialogue/Start")]
[NodeTint(40, 200, 80)]
public class StartNode : Node
{
    public string speakerName;

    [Output(connectionType = ConnectionType.Override)]
    public BaseDialogueNode next;

    public BaseDialogueNode GetNext()
    {
        NodePort port = GetOutputPort(nameof(next));
        return port?.Connection?.node as BaseDialogueNode;
    }
}
