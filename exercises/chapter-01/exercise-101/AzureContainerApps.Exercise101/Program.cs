using Spectre.Console;

AnsiConsole.Write(
    new FigletText("Hello, World!")
        .Centered()
        .Color(Color.Aqua));

AnsiConsole.Write(new Rule("[bold aqua]Azure Container Apps Workshop[/]"));

AnsiConsole.Write(
    new Panel("[bold white]Welcome to Exercise 101![/]\nYour first .NET container is alive :rocket:")
    {
        Border = BoxBorder.Rounded,
        Padding = new Padding(2, 1),
        Header = new PanelHeader("[aqua] Exercise 101 [/]"),
    });

