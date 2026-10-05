using CommunityToolkit.Mvvm.ComponentModel;

namespace Whiteboard.ViewModels;

/// <summary>設定欄の選択肢1件（テーマ・文字サイズなど）と、その選択状態。</summary>
public sealed partial class SettingOptionViewModel<T> : ObservableObject
{
    public SettingOptionViewModel(T value, string label)
    {
        Value = value;
        Label = label;
    }

    public T Value { get; }

    public string Label { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; internal set; }
}
