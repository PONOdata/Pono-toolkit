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
        if (TryNormalizeChargingMode(state, out var mode))
            return Task.FromResult(MapChargingMode(mode, state));

        var reversed = state.ReverseEndianness();
        Log.Instance.Trace($"Unknown battery state, falling back to Normal. [raw={state}, rawHex=0x{state:X8}, rawBits={Convert.ToString(state, 2)}, reversedHex=0x{reversed:X8}, reversedBits={Convert.ToString(reversed, 2)}]");
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

    private static bool TryNormalizeChargingMode(uint rawState, out int mode)
    {
        var rawMode = 0;

        // From reverse
        if ((rawState & 0x20) != 0)
            rawMode |= 0x1;

        if ((rawState & 0x04) != 0)
            rawMode |= 0x2;

        if (rawMode is >= 0 and <= 3 && rawMode != 0)
        {
            mode = rawMode;
            return true;
        }

        // Legacy
        var reversed = rawState.ReverseEndianness();
        var legacyMode = 0;

        if (reversed.GetNthBit(29))
            legacyMode |= 0x1;

        if (reversed.GetNthBit(17) && reversed.GetNthBit(26))
            legacyMode |= 0x2;

        if (legacyMode != 0)
        {
            mode = legacyMode;
            return true;
        }

        // Legacy: charging-state bit set
        if (reversed.GetNthBit(17))
        {
            mode = 0;
            return true;
        }

        if (rawState == 0)
        {
            mode = 0;
            return true;
        }

        mode = default;
        return false;
    }

    private static BatteryState MapChargingMode(int mode, uint rawState)
    {
        if ((mode & 0x1) != 0)
        {
            if ((mode & 0x2) != 0)
                Log.Instance.Trace($"Battery charging mode contains both conservation and rapid bits. Preferring Conservation. [mode={mode}, rawHex=0x{rawState:X8}]");

            return BatteryState.Conservation;
        }

        if ((mode & 0x2) != 0)
            return BatteryState.RapidCharge;

        return BatteryState.Normal;
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
