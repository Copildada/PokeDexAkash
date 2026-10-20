namespace PokeDexAkash.Helpers;

public static class Haptics
{
    public static void Tick()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch { /* unsupported */ }
    }

    public static void Thump()
    {
        try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { /* unsupported */ }
    }
}

public static class Nav
{
    public static Task OpenDetail(string id) =>
        Shell.Current.GoToAsync($"detail?id={Uri.EscapeDataString(id)}");
}

public static class Img
{
    /// <summary>Remote image with a long on-disk cache so the Pokédex works smoothly offline.</summary>
    public static ImageSource From(string url) => new UriImageSource
    {
        Uri = new Uri(url),
        CachingEnabled = true,
        CacheValidity = TimeSpan.FromDays(30),
    };
}

public static class Anim
{
    /// <summary>Quick squash-and-spring used for taps.</summary>
    public static async Task Pop(VisualElement v, double to = 0.9)
    {
        await v.ScaleToAsync(to, 70, Easing.CubicOut);
        await v.ScaleToAsync(1, 160, Easing.SpringOut);
    }

    /// <summary>Fade + rise into place. Call after setting IsVisible = true.</summary>
    public static async Task Reveal(VisualElement v, double fromY = 18, uint ms = 280)
    {
        v.Opacity = 0;
        v.TranslationY = fromY;
        await Task.WhenAll(
            v.FadeToAsync(1, ms, Easing.CubicOut),
            v.TranslateToAsync(0, 0, ms, Easing.CubicOut));
    }
}
