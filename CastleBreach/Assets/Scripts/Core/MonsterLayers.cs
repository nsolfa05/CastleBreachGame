using UnityEngine;

/// <summary>
/// One place that knows monsters can live on EITHER of two physics layers.
///
/// Ordinary monsters sit on the Enemy layer. A monster with Passes Through
/// Gates is moved onto the separate GatePasser layer at spawn (see
/// MonsterAI.Start) purely so a Gate's solid collider can be told to let it
/// through via the layer collision matrix. That trick has one sharp edge: any
/// query that means "find/hit a monster" by filtering on the Enemy layer will
/// silently miss every gate-passer — which is exactly what made the Goblin
/// stop taking damage from the sword, and would also have hidden it from tower
/// targeting and splash.
///
/// So every enemy-facing LayerMask set in the Inspector (the sword's Hit
/// Layers, a tower's Enemy Layers, …) runs through this in Awake, guaranteeing
/// both monster layers are always included without anyone having to remember to
/// tick a second checkbox on every prefab.
/// </summary>
public static class MonsterLayers
{
    public static LayerMask IncludeGatePasser(LayerMask mask)
    {
        int gatePasser = LayerMask.NameToLayer("GatePasser");
        if (gatePasser >= 0) mask |= 1 << gatePasser;
        return mask;
    }
}
