namespace LenovoLegionToolkit.Lib.Features;

// Exposes the BIOS FanFullSpeed capability (max-fan boost) as an on/off feature so
// it can be driven from a dashboard toggle. Supported when the machine reports the
// FanFullSpeed capability, which the modern Legion fan interface does.
public class FanFullSpeedFeature() : AbstractCapabilityFeature<FanMaxSpeedState>(CapabilityID.FanFullSpeed)
{
}
