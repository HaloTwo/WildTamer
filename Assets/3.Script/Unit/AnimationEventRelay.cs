using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField]CombatAgent combatAgent;


    public void ApplyDamageEvent()
    {
        combatAgent.OnAttackHitEvent();
    }

}
