using XNode;
using UnityEngine;

[CreateNodeMenu("Dialogue/End")]
[NodeTint(200, 60, 60)]
public class EndNode : BaseDialogueNode
{
    public DialogueEnum.DialogueOutcome outcome;

    public override BaseDialogueNode GetNextNode(int optionIndex = 0) => null;
}
