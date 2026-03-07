// Place this file anywhere in your Scripts folder (NOT in Editor).
// It defines the attribute that tags a string field for the dropdown.

/// <summary>
/// Tag any string field with this attribute to replace the plain text input
/// with a dropdown listing all Trigger parameters from every AnimatorController
/// in the project.
///
/// Example:
///     [AnimatorTrigger] public string AnimationTrigger;
/// </summary>
public class AnimatorTriggerAttribute : UnityEngine.PropertyAttribute { }