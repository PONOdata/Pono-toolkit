using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Utils;

internal class SmartKeyHelper
{
    private readonly TimeSpan _smartKeyDoublePressInterval = TimeSpan.FromMilliseconds(500);

    private DateTime _lastSmartKeyPress = DateTime.MinValue;
    private CancellationTokenSource? _smartKeyDoublePressCancellationTokenSource;

    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly FnKeysDisabler _fnKeysDisabler = IoCContainer.Resolve<FnKeysDisabler>();
    private readonly SpecialKeyListener _specialKeyListener = IoCContainer.Resolve<SpecialKeyListener>();
    private readonly AutomationProcessor _automationProcessor = IoCContainer.Resolve<AutomationProcessor>();

    public Action? BringToForeground { get; set; }

    private static SmartKeyHelper? _instance;

    public static SmartKeyHelper Instance => _instance ??= new SmartKeyHelper();

    private SmartKeyHelper()
    {
        _specialKeyListener.Changed += SpecialKeyListener_Changed;
        _automationProcessor.PipelineRan += AutomationProcessor_PipelineRan;
    }

    // Keeps the smart-key cycle index in sync when a different code path runs
    // a pipeline that happens to be in the cycle list. Without this, an
    // automation that runs the same quick action the smart key is about to run
    // would not advance the cycle, so the next smart-key press would repeat
    // that quick action. Fires only on the listener-triggered automation path
    // (see PipelineRan in AutomationProcessor); explicit RunNowAsync paths are
    // handled by their own callers, so the smart key's own ProcessSpecialKey
    // continues to advance via its existing logic.
    private void AutomationProcessor_PipelineRan(object? sender, AutomationPipeline pipeline)
    {
        try
        {
            var changed = false;

            var single = _settings.Store.SmartKeySinglePressActionList;
            if (single.Count > 1)
            {
                var index = single.IndexOf(pipeline.Id);
                if (index >= 0)
                {
                    var nextIndex = (index + 1) % single.Count;
                    _settings.Store.SmartKeySinglePressActionId = single[nextIndex];
                    changed = true;
                }
            }

            var doublePress = _settings.Store.SmartKeyDoublePressActionList;
            if (doublePress.Count > 1)
            {
                var index = doublePress.IndexOf(pipeline.Id);
                if (index >= 0)
                {
                    var nextIndex = (index + 1) % doublePress.Count;
                    _settings.Store.SmartKeyDoublePressActionId = doublePress[nextIndex];
                    changed = true;
                }
            }

            if (changed)
                _settings.SynchronizeStore();
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Failed to sync smart key cycle index after pipeline run.", ex);
        }
    }

    private async void SpecialKeyListener_Changed(object? sender, SpecialKeyListener.ChangedEventArgs e)
    {
        if (e.SpecialKey != SpecialKey.FnF9)
            return;

        if (await _fnKeysDisabler.GetStatusAsync() == SoftwareStatus.Enabled)
        {
            Log.Instance.Trace($"Ignoring Fn+F9 FnKeys are enabled.");

            return;
        }

        if (_smartKeyDoublePressCancellationTokenSource is not null)
            await _smartKeyDoublePressCancellationTokenSource.CancelAsync();
        _smartKeyDoublePressCancellationTokenSource = new CancellationTokenSource();

        var token = _smartKeyDoublePressCancellationTokenSource.Token;

        _ = Task.Run(async () =>
        {
            var now = DateTime.UtcNow;
            var diff = now - _lastSmartKeyPress;
            _lastSmartKeyPress = now;

            if (diff < _smartKeyDoublePressInterval)
            {
                await ProcessSpecialKey(true);
                return;
            }

            await Task.Delay(_smartKeyDoublePressInterval, token);
            await ProcessSpecialKey(false);
        }, token);
    }

    private async Task ProcessSpecialKey(bool isDoublePress)
    {
        var currentGuid = isDoublePress
            ? _settings.Store.SmartKeyDoublePressActionId
            : _settings.Store.SmartKeySinglePressActionId;
        var actionList = isDoublePress
            ? _settings.Store.SmartKeyDoublePressActionList
            : _settings.Store.SmartKeySinglePressActionList;

        if (!currentGuid.HasValue)
        {
            Log.Instance.Trace($"Bringing to foreground after {(isDoublePress ? "double" : "single")} Fn+F9 press.");
            BringToForeground?.Invoke();
            return;
        }

        if (currentGuid.Value == Guid.Empty)
            return;

        if (actionList.IsEmpty())
            actionList.Add(currentGuid.Value);

        var currentIndex = Math.Max(0, actionList.IndexOf(currentGuid.Value));
        var nextIndex = (currentIndex + 1) % actionList.Count;

        currentGuid = actionList[currentIndex];

        Log.Instance.Trace($"Running action {currentGuid} after {(isDoublePress ? "double" : "single")} Fn+F9 press.");

        try
        {
            var pipelines = await _automationProcessor.GetPipelinesAsync();
            var pipeline = pipelines.FirstOrDefault(p => p.Id == currentGuid);
            if (pipeline is not null)
            {
                Log.Instance.Trace($"Running action {currentGuid} after {(isDoublePress ? "double" : "single")} Fn+F9 press.");

                await _automationProcessor.RunNowAsync(pipeline.Id);

                MessagingCenter.Publish(new NotificationMessage(isDoublePress ? NotificationType.SmartKeyDoublePress : NotificationType.SmartKeySinglePress, pipeline.Name ?? string.Empty));
            }
        }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Running action {currentGuid} after {(isDoublePress ? "double" : "single")} Fn+F9 press failed.", ex);
        }

        if (isDoublePress)
        {
            _settings.Store.SmartKeyDoublePressActionList = actionList;
            _settings.Store.SmartKeyDoublePressActionId = actionList[nextIndex];
        }
        else
        {
            _settings.Store.SmartKeySinglePressActionList = actionList;
            _settings.Store.SmartKeySinglePressActionId = actionList[nextIndex];
        }

        _settings.SynchronizeStore();
    }
}
