namespace PokeDexAkash.Services;

/// <summary>Plays a Pokémon cry (ogg from the PokeAPI/cries repo) through Android's MediaPlayer.</summary>
public sealed class CryPlayer
{
#if ANDROID
    Android.Media.MediaPlayer? _player;

    public void Play(string url)
    {
        Stop();
        try
        {
            var player = new Android.Media.MediaPlayer();
            _player = player;
            player.SetAudioAttributes(new Android.Media.AudioAttributes.Builder()!
                .SetContentType(Android.Media.AudioContentType.Music)!
                .SetUsage(Android.Media.AudioUsageKind.Media)!
                .Build()!);
            player.SetDataSource(url);
            player.Prepared += (_, _) => player.Start();
            player.Completion += (_, _) => { if (ReferenceEquals(_player, player)) Stop(); };
            player.Error += (_, _) => { if (ReferenceEquals(_player, player)) Stop(); };
            player.PrepareAsync();
        }
        catch
        {
            Stop();
        }
    }

    public void Stop()
    {
        var p = _player;
        _player = null;
        if (p is null) return;
        try { p.Release(); p.Dispose(); } catch { /* already released */ }
    }
#else
    public void Play(string url) { }
    public void Stop() { }
#endif
}
