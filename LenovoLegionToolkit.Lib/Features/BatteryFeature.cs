using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;

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
        uint[] commands = state switch
        {
            BatteryState.Conservation => new uint[] { 0x08, 0x03 },
            BatteryState.Normal => new uint[] { 0x05, 0x08 },
            BatteryState.RapidCharge => new uint[] { 0x05, 0x07 },
            _ => throw new InvalidOperationException("Invalid battery mode.")
        };

        return Task.FromResult(commands);
    }

    protected override Task<BatteryState> FromInternalAsync(uint state)
    {
        // Storage - bit 0x20
        if ((state & 0x20) != 0)
            return Task.FromResult(BatteryState.Conservation);

        // Express - bit 0x04
        if ((state & 0x04) != 0)
            return Task.FromResult(BatteryState.RapidCharge);

        return Task.FromResult(BatteryState.Normal);
    }

    public override async Task SetStateAsync(BatteryState state)
    {
        await base.SetStateAsync(state).ConfigureAwait(false);

        BatteryState actualState;
        bool success = false;
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(50).ConfigureAwait(false);
            actualState = await GetStateAsync().ConfigureAwait(false);
            if (actualState == state)
            {
                success = true;
                break;
            }
        }

        actualState = await GetStateAsync().ConfigureAwait(false);
        SetStateInRegistry(actualState);

        if (!success)
        {
            throw new InvalidOperationException($"Failed to set battery mode to: {state}, Current: {actualState}");
        }
    }

    public async Task EnsureCorrectBatteryModeIsSetAsync()
    {
        var registryState = GetStateFromRegistry();
        if (!registryState.HasValue)
            return;

        var actualState = await GetStateAsync().ConfigureAwait(false);
        if (actualState == registryState.Value)
            return;

        SetStateInRegistry(actualState);
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
