# Lignuz.Wpf.ToolTips

A tiny, zero-dependency **WPF** utility that makes ToolTips behave nicely app-wide:

- Closes the current ToolTip when the pointer leaves **both the host and the tooltip**.
- Re-opens the same ToolTip when the pointer re-enters the host, respecting `InitialShowDelay`.
- Works reliably on **disabled controls** (`IsEnabled=false`) using **coordinate-based** hit checks.
- Prevents flicker with a small **grace window** right after opening.
- Global, no per-control setup; just call `Initialize()` once at startup.

> If you ever saw ToolTips sticking around when moving right/down, or refusing to re-open on disabled buttons — this fixes it.

## Quick start

**App.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Controls;
using Lignuz.Wpf.ToolTips; // <-- namespace

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // (Optional) System-wide defaults
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(100));

        // (Optional) Show tooltips on disabled controls on first hover
        // ToolTipService.ShowOnDisabledProperty.OverrideMetadata(
        //     typeof(FrameworkElement), new FrameworkPropertyMetadata(true));

        // Required: enable global manager
        GlobalToolTipManager.Initialize();
    }
}
```

**(Optional) XAML defaults**

```xml
<Application.Resources>
  <Style TargetType="{x:Type ToolTip}">
    <Setter Property="ToolTipService.InitialShowDelay" Value="100"/>
    <Setter Property="ToolTipService.BetweenShowDelay" Value="0"/>
  </Style>
</Application.Resources>
```

## API

```csharp
GlobalToolTipManager.Initialize();  // enable global hooks (call once)
GlobalToolTipManager.CloseCurrent(); // force-close the current tooltip
```

## How it works

- Listens to `ToolTip.Opened/Closed` to track the **actual** instance shown by WPF.
- Watches `PreviewMouseMove` at the **Window** scope; considers both the **host** and its **tooltip** surface.
- Uses **pointer coordinates** (not `IsMouseOver`) to decide “inside/outside”, which also works when controls are **disabled**.
- Applies a short **grace period** after opening to ignore layout jitter.
- If a tooltip was closed by the manager and the pointer re-enters the same host, it re-opens the tooltip after the host’s `InitialShowDelay`.

## Target frameworks

- .NET 8 (Windows, WPF)

> No external dependencies.

## License

MIT License. See [LICENSE](./LICENSE) for details.
