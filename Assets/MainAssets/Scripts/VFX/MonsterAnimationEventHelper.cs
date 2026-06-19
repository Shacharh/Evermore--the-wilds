using UnityEngine;
using UnityEngine.Events;

public class MonsterAnimationEventHelper : MonoBehaviour
{
    [SerializeField] private UnityEvent onAnimationEvent;
    [SerializeField] private UnityEvent onAnimationEnd;

    public void OnAnimationEvent() => onAnimationEvent?.Invoke();
    public void OnAnimationEnd()   => onAnimationEnd?.Invoke();
}
