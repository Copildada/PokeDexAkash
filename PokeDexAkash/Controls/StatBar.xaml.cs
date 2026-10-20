namespace PokeDexAkash.Controls;

/// <summary>A rounded progress bar (Value 0..1) whose fill animates to its target width.</summary>
public partial class StatBar : ContentView
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(StatBar), 0d,
        propertyChanged: (b, _, _) => ((StatBar)b).AnimateFill());

    public static readonly BindableProperty BarColorProperty = BindableProperty.Create(
        nameof(BarColor), typeof(Color), typeof(StatBar), Colors.White,
        propertyChanged: (b, _, n) => ((StatBar)b).Fill.BackgroundColor = (Color)n);

    public StatBar()
    {
        InitializeComponent();
        Root.SizeChanged += (_, _) => AnimateFill();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color BarColor
    {
        get => (Color)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    void AnimateFill()
    {
        var width = Root.Width;
        if (width <= 0) return;

        var target = Math.Clamp(Value, 0, 1) * width;
        var from = Math.Max(0, Fill.WidthRequest);
        if (Math.Abs(target - from) < 0.5) return;

        this.AbortAnimation("fill");
        new Animation(v => Fill.WidthRequest = v, from, target, Easing.CubicOut)
            .Commit(this, "fill", 16, 750);
    }
}
