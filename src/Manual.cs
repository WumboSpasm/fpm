namespace FlashpointManagerCLI
{
    public static partial class Program
    {
        static string HelpText { get; } =
@"NAME:
    fpm - BlueMaxima's Flashpoint Component Manager

USAGE:
    fpm <command> [<arguments>...]

DESCRIPTION:
    Adds, removes, or updates components from a local Flashpoint copy.

COMMANDS:
    list [available|downloaded|updates]
        Displays a list of all components. Downloaded components will be
        prefixed with an asterisk (*) if up-to-date, or an exclamation mark (!)
        if outdated.
        * If the [available] argument is specified, only components that haven't
          been downloaded will be displayed.
        * If the [downloaded] argument is specified, only components that are
          downloaded will be displayed.
        * If the [updates] argument is specified, only downloaded components
          with pending updates will be displayed.

    info <component>
        Displays detailed information about the specified component, including
        name, description, size, dependencies, and download status.

    download [component component2 ...]
        Downloads the specified component(s) and any dependencies. The total
        size will be displayed and you will be asked if you want to proceed.
        * If no components are specified, all non-extra components will be
          downloaded.
        * Components that are already downloaded will be skipped.

    remove <component component2 ...>
        Removes the specified component(s). The total freed size will be 
        displayed and you will be asked if you want to proceed.

    update [component component2 ...]
        Updates the specified component(s) to the latest version and downloads
        any new dependencies. The total changed size will be displayed and you
        will be asked if you want to proceed.
        * If no components are specified, all outdated components will be
          updated.
        * Components that are up-to-date will be skipped.

    path <value>
        Modifies fpm.cfg with the specified value as the Flashpoint path.
        * If fpm.cfg does not exist, it will be created with the specified value
          as the Flashpoint path and a default value as the source URL.

    source <value>
        Modifies fpm.cfg with the specified value as the source URL.
        * If fpm.cfg does not exist, it will be created with a default value as
          the Flashpoint path and the specified value as the source URL.

NOTES:
    If any command except for (path) and (source) is run without an existing
    fpm.cfg, a folder dialog will be opened and you will be prompted to select
    the Flashpoint path. Afterwards, fpm.cfg will be created with the specified
    Flashpoint path and the default value as the source URL, and the command
    will proceed.

    The default value for the Flashpoint path is the working directory.

    The default value for the source URL is ???.

EXAMPLES:
    fpm list
        Displays a list of all components.

    fpm list downloaded
        Displays a list of all downloaded components.

    fpm info curatortools
        Displays detailed information about the curatortools (Curator Tools)
        component.

    fpm download theme-flatmetal logoset-adobeblue
        Downloads the theme-flatmetal (Flat Metal launcher theme) and
        logoset-adobeblue (Adobe Blue logo set) components.

    fpm remove supportpack-java
        Removes the supportpack-java (Java support pack) component.

    fpm update
        Updates all outdated components to the latest version.

    fpm update supportpack-common-navigator
        Updates the supportpack-common-navigator (Flashpoint Navigator)
        component.

    fpm path C:\Flashpoint
        Modifies fpm.cfg with C:\Flashpoint as the Flashpoint path.

    fpm source http://localhost/components.xml
        Modifies fpm.cfg with http://localhost/components.xml as the source URL.";
    }
}
