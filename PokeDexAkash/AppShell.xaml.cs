using PokeDexAkash.Pages;

namespace PokeDexAkash;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("detail", typeof(DetailPage));
    }
}
