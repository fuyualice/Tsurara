using CommunityToolkit.Mvvm.ComponentModel;

namespace Whiteboard.ViewModels;

/// <summary>How の候補1件と、その選択状態。</summary>
public sealed partial class HowOptionViewModel : ObservableObject
{
    public HowOptionViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; internal set; }
}
