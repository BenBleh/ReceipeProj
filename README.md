# ReceipeProj
Vehicle to practice API creation and Agular/Typescript sites!

The approved Nibbles redesign, assets, ingredient-group schema, and deployment
notes are in [Design](Design/README.md).


Steps to set up

1.Install IIS
2. set up to host ASP sites
	https://learn.microsoft.com/en-us/aspnet/core/tutorials/publish-to-iis?view=aspnetcore-9.0&tabs=visual-studio
3.Add 2 websites
	1. I called it recipeAPI, must be http, and on port 8080
	2. I called it rfiles, must be port 8081
	
4.open ports
5.may need to add user permissions
6.Publish api to recipeAPI site
	I didn't need to do anything else for this to work this time
7.Copy over rfiles if you have them
	I enabled directory browsing, may not be nessicary
8. if end point url has changed, you may need to update the app

## Nibbles Android development

Open the `Nibbles` folder in VS Code with Microsoft's .NET MAUI extension enabled.
Select an Android emulator or device using `.NET MAUI: Select Android Device`, then
select the `.NET MAUI Android` launch configuration and press F5.
If prompted for a startup project, select `Nibbles`. The launch profile uses the
loaded startup project rather than a `${workspaceFolder}` project path, which the
MAUI extension tries to resolve before VS Code expands the variable.
The project uses Mono for Android; Meteor 10 requires .NET 11+ / CoreCLR and is not
compatible with this .NET 10 Mono debugging setup.

To build from PowerShell:

```powershell
Set-Location C:\Dev\ReceipeProj\Nibbles
dotnet build Nibbles.csproj -f net10.0-android -c Debug
```

If Android resource extraction fails with `XARLP7000`, stop active builds/debugging
and regenerate the intermediate resources:

```powershell
dotnet build Nibbles.csproj -f net10.0-android -c Debug -t:Rebuild
```
