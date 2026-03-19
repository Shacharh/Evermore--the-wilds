/// <summary>
/// Tag any string field with this attribute to replace the plain text input
/// with a dropdown listing all Bool parameters from every AnimatorController
/// in the project.
///
/// Example:
///     [AnimatorBool] public string AnimationBool;
/// </summary>
public class AnimatorBoolAttribute : UnityEngine.PropertyAttribute { }