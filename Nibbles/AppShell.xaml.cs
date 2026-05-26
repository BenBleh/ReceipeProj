using Microsoft.Maui.Controls;

namespace Nibbles
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            // Register routes for Shell navigation
            Routing.RegisterRoute(nameof(RecipeDetailsPage), typeof(RecipeDetailsPage));
            Routing.RegisterRoute(nameof(AddEditPage), typeof(AddEditPage));
        }
    }
}
