using UnityEngine;
using UnityEngine.Events;

public class MonsterAnimationEventHelper : MonoBehaviour
{
    [SerializeField] private UnityEvent onAnimationEvent;

    public void OnAnimationEvent()
    {
        onAnimationEvent?.Invoke();
    }
}
