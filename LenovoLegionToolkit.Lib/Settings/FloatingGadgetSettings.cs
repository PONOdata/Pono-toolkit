using System.Collections.Generic;
using System.IO;
using LenovoLegionToolkit.Lib.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static LenovoLegionToolkit.Lib.Settings.FloatingGadgetSettings;

namespace LenovoLegionToolkit.Lib.Settings;

public class FloatingGadgetSettings() : AbstractSettings<FloatingGadgetSettingsStore>("floating_gadget.json")
{
    public class FloatingGadgetSettingsStore
    {
        public List<FloatingGadgetItem> Items { get; set; } = [];
    }

    public override FloatingGadgetSettingsStore? LoadStore()
    {
        var store = base.LoadStore();
        if (store != null)
            return store;

        try
        {
            var oldSettingsPath = Path.Combine(Folders.AppData, "settings.json");
            if (!File.Exists(oldSettingsPath))
                return null;

            var json = File.ReadAllText(oldSettingsPath);
            var jObject = JsonConvert.DeserializeObject<JObject>(json, JsonSerializerSettings);
            var itemsToken = jObject?["FloatingGadgetItems"];
            if (itemsToken == null)
                return null;

            var items = itemsToken.ToObject<List<FloatingGadgetItem>>();
            if (items is not { Count: > 0 })
                return null;

            var migrated = new FloatingGadgetSettingsStore { Items = items };
            Store = migrated;
            Save();

            return migrated;
        }
        catch
        {
            return null;
        }
    }
}
