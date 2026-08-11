using Microsoft.AspNetCore.Components;

namespace VerduleriaAugust.Web;

internal static class NavigationManagerExtensions
{
    public static void NavigateTo(this NavigationManager navigation, char route) =>
        navigation.NavigateTo(route.ToString());
}
