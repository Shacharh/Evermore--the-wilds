using System.Collections.Generic;
using System.Linq;
using XNode;
using UnityEngine;

[CreateNodeMenu("Dialogue/Option Dialogue")]
[NodeTint(80, 120, 200)]
public class OptionDialogueNode : BaseDialogueNode
{
    [System.Serializable]
    public class DialogueOption
    {
        public string text;
    }

    public List<DialogueOption> options = new List<DialogueOption>();

    private void OnValidate() => SyncPorts();

    public void SyncPorts()
    {
        if (graph == null) return;

        for (int i = 0; i < options.Count; i++)
        {
            string portName = "option_" + i;
            if (GetPort(portName) == null)
                AddDynamicOutput(typeof(BaseDialogueNode), ConnectionType.Override,
                                 TypeConstraint.Inherited, portName);
        }

        var toRemove = DynamicOutputs
            .Where(p => p.fieldName.StartsWith("option_") &&
                        (!int.TryParse(p.fieldName.Substring("option_".Length), out int idx) || idx >= options.Count))
            .ToList();
        foreach (var p in toRemove) RemoveDynamicPort(p);
    }

    public override BaseDialogueNode GetNextNode(int optionIndex = 0)
    {
        var port = GetOutputPort("option_" + optionIndex);
        return port?.Connection?.node as BaseDialogueNode;
    }
}
