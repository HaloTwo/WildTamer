using Unity.VisualScripting;
using UnityEngine;

public class GamaManager : Singleton<GamaManager>
{
    public GameObject player;

    //[SerializeField] GameObject fieldRoot;
    //[SerializeField] GameObject campRoot;


    //[SerializeField] Spawner spawner;
    //[SerializeField] CombatDirector combat;
    //[SerializeField] FogOfWarSystem fog;


    protected override void Awake()
    {
        base.Awake();

        EnterCamp(); // Ω√¿€¿∫ ƒ∑«¡
    }

    public void EnterCamp()
    {
        //fieldRoot.SetActive(false);
        //campRoot.SetActive(true);

        //spawner.enabled = false;
        //combat.enabled = false;
        //fog.enabled = false;

        //ui.ShowCamp();
    }

    public void EnterField()
    {
        //campRoot.SetActive(false);
        //fieldRoot.SetActive(true);

        //spawner.enabled = true;
        //combat.enabled = true;
        //fog.enabled = true;

        //ui.ShowHUD();
    }
}