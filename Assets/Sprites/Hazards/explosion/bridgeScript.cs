using UnityEngine;

public class ExplosionEventBridge : MonoBehaviour
{
    public ExplosiveCrate2D parentCrate;

    public void DestroySelf()
    {
        if (parentCrate) parentCrate.DestroySelf();
    }
}