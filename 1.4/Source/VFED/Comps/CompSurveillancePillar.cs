﻿using RimWorld;
using Verse;

namespace VFED;

public class CompSurveillancePillar : CompMotionDetector
{
    private CompPowerTrader compPower;

    public override bool Active => base.Active && compPower.PowerOn;

    protected override void Trigger(Thing initiator)
    {
        base.Trigger(initiator);
        Utilities.ChangeVisibility(10);
        Messages.Message("VFED.SurveillancePillarActivated".Translate(WorldComponent_Deserters.Instance.Visibility), initiator, MessageTypeDefOf.NegativeEvent);
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        compPower = parent.TryGetComp<CompPowerTrader>();
    }
}
