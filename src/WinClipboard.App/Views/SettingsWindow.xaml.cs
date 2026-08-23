using System.Windows;
using WinClipboard.Core.Models;

namespace WinClipboard.App.Views;

public partial class SettingsWindow : Window
{
    private readonly App _app;

    public SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        PopulateHoldKeyOptions();
        LoadFromSettings(_app.Settings);
    }

    private void PopulateHoldKeyOptions()
    {
        var labels = new (ModifierHoldKey Value, string Label)[]
        {
            (ModifierHoldKey.None, "Không dùng"),
            (ModifierHoldKey.RightShift, "Shift phải"),
            (ModifierHoldKey.LeftShift, "Shift trái"),
            (ModifierHoldKey.RightControl, "Ctrl phải"),
            (ModifierHoldKey.LeftControl, "Ctrl trái"),
            (ModifierHoldKey.RightAlt, "Alt phải"),
            (ModifierHoldKey.LeftAlt, "Alt trái"),
        };
        foreach (var (value, label) in labels)
        {
            HoldKeyCombo.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = label, Tag = value });
        }
    }

    private void LoadFromSettings(AppSettings settings)
    {
        EdgeLeftCheck.IsChecked = settings.EnabledEdges.Contains(ScreenEdge.Left);
        EdgeRightCheck.IsChecked = settings.EnabledEdges.Contains(ScreenEdge.Right);
        EdgeTopCheck.IsChecked = settings.EnabledEdges.Contains(ScreenEdge.Top);
        EdgeBottomCheck.IsChecked = settings.EnabledEdges.Contains(ScreenEdge.Bottom);

        EdgeTriggerCheck.IsChecked = settings.EdgeTriggerEnabled;
        EdgeMarginBox.Text = settings.EdgeMarginPx.ToString();

        HotkeyTriggerCheck.IsChecked = settings.HotkeyTriggerEnabled;
        for (var i = 0; i < HoldKeyCombo.Items.Count; i++)
        {
            var item = (System.Windows.Controls.ComboBoxItem)HoldKeyCombo.Items[i];
            if ((ModifierHoldKey)item.Tag == settings.HoldKey)
            {
                HoldKeyCombo.SelectedIndex = i;
                break;
            }
        }

        ShakeTriggerCheck.IsChecked = settings.ShakeTriggerEnabled;
        ShakeDistanceBox.Text = settings.ShakeSegmentDistancePx.ToString();
        ShakeChangesBox.Text = settings.ShakeDirectionChanges.ToString();

        AutoHideCheck.IsChecked = settings.AutoHideBubbleWhenIdle;
        AutoHideSecondsBox.Text = settings.AutoHideIdleSeconds.ToString();

        MaxHistoryBox.Text = settings.MaxHistoryItems.ToString();
        PurgeEnabledCheck.IsChecked = settings.SensitiveDataPurgeAfterHours.HasValue;
        PurgeHoursBox.Text = (settings.SensitiveDataPurgeAfterHours ?? 24).ToString("0.#");

        StartWithWindowsCheck.IsChecked = settings.StartWithWindows;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        var settings = _app.Settings;

        var edges = new List<ScreenEdge>();
        if (EdgeLeftCheck.IsChecked == true) edges.Add(ScreenEdge.Left);
        if (EdgeRightCheck.IsChecked == true) edges.Add(ScreenEdge.Right);
        if (EdgeTopCheck.IsChecked == true) edges.Add(ScreenEdge.Top);
        if (EdgeBottomCheck.IsChecked == true) edges.Add(ScreenEdge.Bottom);
        settings.EnabledEdges = edges;

        settings.EdgeTriggerEnabled = EdgeTriggerCheck.IsChecked == true;
        if (int.TryParse(EdgeMarginBox.Text, out var margin) && margin > 0)
        {
            settings.EdgeMarginPx = margin;
        }

        settings.HotkeyTriggerEnabled = HotkeyTriggerCheck.IsChecked == true;
        if (HoldKeyCombo.SelectedItem is System.Windows.Controls.ComboBoxItem selected)
        {
            settings.HoldKey = (ModifierHoldKey)selected.Tag;
        }

        settings.ShakeTriggerEnabled = ShakeTriggerCheck.IsChecked == true;
        if (int.TryParse(ShakeDistanceBox.Text, out var shakeDistance) && shakeDistance > 0)
        {
            settings.ShakeSegmentDistancePx = shakeDistance;
        }
        if (int.TryParse(ShakeChangesBox.Text, out var shakeChanges) && shakeChanges > 0)
        {
            settings.ShakeDirectionChanges = shakeChanges;
        }

        settings.AutoHideBubbleWhenIdle = AutoHideCheck.IsChecked == true;
        if (int.TryParse(AutoHideSecondsBox.Text, out var autoHideSeconds) && autoHideSeconds > 0)
        {
            settings.AutoHideIdleSeconds = autoHideSeconds;
        }

        if (int.TryParse(MaxHistoryBox.Text, out var maxHistory) && maxHistory > 0)
        {
            settings.MaxHistoryItems = maxHistory;
        }

        if (PurgeEnabledCheck.IsChecked == true && double.TryParse(PurgeHoursBox.Text, out var purgeHours) && purgeHours > 0)
        {
            settings.SensitiveDataPurgeAfterHours = purgeHours;
        }
        else
        {
            settings.SensitiveDataPurgeAfterHours = null;
        }

        settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;

        _app.ApplySettingsChanges();
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => Close();
}
