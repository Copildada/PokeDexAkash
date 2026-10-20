using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokeDexAkash.Helpers;

namespace PokeDexAkash.ViewModels;

/// <summary>A selectable pill (type filter, generation, sort, tab, form...).</summary>
public partial class ChipVM : ObservableObject
{
    static readonly Color Idle = Color.FromArgb("#232640");
    static readonly Color IdleText = Color.FromArgb("#B4B9D2");

    readonly Action<ChipVM> _onTap;

    public ChipVM(string key, string label, Color accent, Action<ChipVM> onTap, bool selected = false)
    {
        Key = key;
        Label = label;
        Accent = accent;
        _onTap = onTap;
        _isSelected = selected;
    }

    public string Key { get; }
    public string Label { get; }
    public Color Accent { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Fill))]
    [NotifyPropertyChangedFor(nameof(TextColor))]
    bool _isSelected;

    public Color Fill => IsSelected ? Accent : Idle;
    public Color TextColor => IsSelected ? TypeHelper.ReadableOn(Accent) : IdleText;

    [RelayCommand]
    void Tap() => _onTap(this);
}
