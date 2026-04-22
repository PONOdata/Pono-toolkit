using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

public class BatteryFeature() : AbstractDriverFeature<BatteryState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_BATTERY_CHARGE_MODE)
{
    private const string BATTERY_CHARGE_MODE_HIVE = "HKEY_CURRENT_USER";
    private const string BATTERY_CHARGE_MODE_PATH = "Software\\Lenovo\\VantageService\\AddinData\\IdeaNotebookAddin";
    private const string BATTERY_CHARGE_MODE_KEY = "BatteryChargeMode";
    private const string BATTERY_CHARGE_MODE_NORMAL = "Normal";
    private const string BATTERY_CHARGE_MODE_RAPID_CHARGE = "Quick";
    private const string BATTERY_CHARGE_MODE_CONSERVATION = "Storage";

    protected override uint GetInBufferValue() => 0xFF;

    protected override Task<uint[]> ToInternalAsync(BatteryState state)
    {
        var result = state switch
        {
            BatteryState.Conservation => LastState == BatteryState.RapidCharge ? new uint[] { 0x8, 0x3 } : [0x3],
            BatteryState.Normal => LastState == BatteryState.Conservation ? [0x5] : [0x8],
            BatteryState.RapidCharge => LastState == BatteryState.Conservation ? [0x5, 0x7] : [0x7],
            _ => throw new InvalidOperationException("Invalid state")
        };
        return Task.FromResult(result);
    }

    protected override Task<BatteryState> FromInternalAsync(uint state)
    {
        var raw = state;
        var reversed = state.ReverseEndianness();

        // 0x20 -> Conservation/Storage, 0x04 -> Rapid/Express.
        if ((raw & 0x20) != 0)
            return Task.FromResult(BatteryState.Conservation);

        if ((raw & 0x04) != 0)
            return Task.FromResult(BatteryState.RapidCharge);

        // For Legacy
        if (reversed.GetNthBit(17)) // Is charging?
            return Task.FromResult(reversed.GetNthBit(26) ? BatteryState.RapidCharge : BatteryState.Normal);

        if (reversed.GetNthBit(29))
            return Task.FromResult(BatteryState.Conservation);

        Log.Instance.Trace($"Unknown battery state, falling back to Normal. [raw={raw}, rawHex=0x{raw:X8}, rawBits={Convert.ToString(raw, 2)}, reversedHex=0x{reversed:X8}, reversedBits={Convert.ToString(reversed, 2)}]");
        return Task.FromResult(BatteryState.Normal);
    }

    public override async Task SetStateAsync(BatteryState state)
    {
        await base.SetStateAsync(state).ConfigureAwait(false);
        SetStateInRegistry(state);
    }

    public async Task EnsureCorrectBatteryModeIsSetAsync()
    {
        var state = GetStateFromRegistry();

        if (!state.HasValue)
            return;

        if (await GetStateAsync().ConfigureAwait(false) == state.Value)
            return;

        await SetStateAsync(state.Value).ConfigureAwait(false);
    }

    private static BatteryState? GetStateFromRegistry()
    {
        var batteryModeString = Registry.GetValue(BATTERY_CHARGE_MODE_HIVE, BATTERY_CHARGE_MODE_PATH, BATTERY_CHARGE_MODE_KEY, string.Empty);
        return batteryModeString switch
        {
            BATTERY_CHARGE_MODE_NORMAL => BatteryState.Normal,
            BATTERY_CHARGE_MODE_RAPID_CHARGE => BatteryState.RapidCharge,
            BATTERY_CHARGE_MODE_CONSERVATION => BatteryState.Conservation,
            _ => null
        };
    }

    private static void SetStateInRegistry(BatteryState state)
    {
        var batteryModeString = state switch
        {
            BatteryState.Normal => BATTERY_CHARGE_MODE_NORMAL,
            BatteryState.RapidCharge => BATTERY_CHARGE_MODE_RAPID_CHARGE,
            BatteryState.Conservation => BATTERY_CHARGE_MODE_CONSERVATION,
            _ => null
        };

        if (batteryModeString is null)
            return;

        Registry.SetValue(BATTERY_CHARGE_MODE_HIVE, BATTERY_CHARGE_MODE_PATH, BATTERY_CHARGE_MODE_KEY, batteryModeString);
    }
}
