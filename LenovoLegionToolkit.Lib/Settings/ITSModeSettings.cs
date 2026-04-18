namespace LenovoLegionToolkit.Lib.Settings;

public class ITSModeSettings() : AbstractSettings<ITSModeSettings.ITSModeSettingsStore>("itsmode_settings.json")
{
    public class ITSModeSettingsStore
    {
        public ITSMode LastState { get; set; } = ITSMode.None;
    }
}
