using UnityEngine;

public class GFXFollow : MonoBehaviour
{
    public Transform hitBox; // assign your HitBox here

    void LateUpdate()
    {
        if (hitBox == null) return;

        // follow HitBox position and rotation
        transform.position = hitBox.position;
        transform.rotation = hitBox.rotation;
    }
}
