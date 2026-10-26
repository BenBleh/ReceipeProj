using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nibbles.Models;
using Nibbles.Services;
using System.Collections.ObjectModel;
using System.Net;
using System.Text.Json;

namespace Nibbles.ViewModels;

public partial class AddEditViewModel : ObservableObject
{
    private readonly ReceipeAPIService service;
    private string original = string.Empty;
    private string? selectedPhotoPath;
    private bool isNew = true;

    [ObservableProperty]
    public partial Recipe Recipe { get; set; } = new() { Id = Guid.NewGuid().ToString(), ImagePath = "nibbles_chef.png" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetails))]
    [NotifyPropertyChangedFor(nameof(IsIngredients))]
    [NotifyPropertyChangedFor(nameof(IsMethod))]
    public partial int SelectedSection { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(IsEditingEnabled))]
    [NotifyPropertyChangedFor(nameof(CanLeave))]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(IsEditingEnabled))]
    public partial bool IsReady { get; set; } = true;

    public ObservableCollection<IngredientSection> IngredientSections { get; } = [];
    public bool IsDetails => SelectedSection == 0;
    public bool IsIngredients => SelectedSection == 1;
    public bool IsMethod => SelectedSection == 2;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsEditingEnabled => IsReady && !IsSaving;
    public bool CanLeave => !IsSaving;
    public string EditorTitle => isNew ? "Add a recipe" : "Edit recipe";
    public string RecipeSummary =>
        $"{Recipe.Ingredients.Count} {(Recipe.Ingredients.Count == 1 ? "ingredient" : "ingredients")} / " +
        $"{Recipe.IngredientGroups.Count} {(Recipe.IngredientGroups.Count == 1 ? "group" : "groups")}";
    public bool HasUnsavedChanges => selectedPhotoPath is not null || JsonSerializer.Serialize(Recipe) != original;
    private bool CanSave => IsEditingEnabled;

    public AddEditViewModel(ReceipeAPIService service)
    {
        this.service = service;
        Recipe.Ingredients.Add(new Ingredient());
        Recipe.Steps.Add(new Step());
        RefreshSections();
        original = JsonSerializer.Serialize(Recipe);
    }

    public async Task LoadAsync(string id)
    {
        IsReady = false;
        try
        {
            Recipe = await service.GetRecipe(id) ?? throw new InvalidDataException("Recipe not found.");
            isNew = false;
            RefreshSections();
            original = JsonSerializer.Serialize(Recipe);
            OnPropertyChanged(nameof(EditorTitle));
            IsReady = true;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not load this recipe. Return to your recipe box and refresh. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    public async Task<bool> ConfirmDiscardAsync()
    {
        if (IsSaving)
            return false;
        return !HasUnsavedChanges || await AppDialogs.ConfirmAsync(Page, "Discard changes?",
            "Your changes have not been saved.", "Discard", "Keep editing", destructive: true);
    }

    private static Page Page => Shell.Current.CurrentPage;

    public void RefreshSections()
    {
        IngredientSections.Clear();
        foreach (var section in RecipeIngredients.Sections(Recipe, includeEmptyUngrouped: true))
            IngredientSections.Add(section);
        OnPropertyChanged(nameof(RecipeSummary));
    }

    [RelayCommand]
    private void SelectSection(string section) => SelectedSection = int.Parse(section, System.Globalization.CultureInfo.InvariantCulture);

    [RelayCommand]
    private async Task GetImage()
    {
        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync();
            var photo = photos?.FirstOrDefault();
            if (photo is null)
                return;
            var path = Path.Combine(FileSystem.CacheDirectory, Guid.NewGuid().ToString("N") + Path.GetExtension(photo.FileName));
            await using (var source = await photo.OpenReadAsync())
            await using (var target = File.Create(path))
                await source.CopyToAsync(target);
            if (selectedPhotoPath is not null)
                File.Delete(selectedPhotoPath);
            selectedPhotoPath = path;
            Recipe.ImagePath = path;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not choose this photo. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    [RelayCommand]
    private void AddIngredient(IngredientSection section)
    {
        Recipe.Ingredients.Add(new Ingredient { GroupId = section.Id });
        RefreshSections();
    }

    [RelayCommand]
    private async Task AddGroup()
    {
        var name = await AppDialogs.PromptAsync(Page, "Add ingredient group", "For example: Marinade, Broth, or To serve",
            accept: "Add group", maxLength: 100);
        if (name is null)
            return;
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Give your ingredient group a name.";
            return;
        }
        Recipe.IngredientGroups.Add(new IngredientGroup { Name = name.Trim(), Order = Recipe.IngredientGroups.Count });
        RefreshSections();
    }

    [RelayCommand]
    private async Task GroupOptions(IngredientSection section)
    {
        var group = Recipe.IngredientGroups.FirstOrDefault(group => group.Id == section.Id);
        if (group is null)
            return;
        var index = Recipe.IngredientGroups.IndexOf(group);
        var actions = new List<string> { "Rename" };
        if (index > 0) actions.Add("Move up");
        if (index < Recipe.IngredientGroups.Count - 1) actions.Add("Move down");
        actions.Add("Remove heading");
        var action = await AppDialogs.ChooseAsync(Page, group.Name, actions, "Remove heading");
        switch (action)
        {
            case "Rename":
                var name = await AppDialogs.PromptAsync(Page, "Rename group", "Choose a name for these ingredients.", accept: "Save name", initialValue: group.Name, maxLength: 100);
                if (name is null) return;
                if (string.IsNullOrWhiteSpace(name))
                {
                    ErrorMessage = "Give your ingredient group a name.";
                    return;
                }
                group.Name = name.Trim();
                break;
            case "Move up":
                Recipe.IngredientGroups.Move(index, index - 1);
                RecipeIngredients.ReorderGroups(Recipe);
                break;
            case "Move down":
                Recipe.IngredientGroups.Move(index, index + 1);
                RecipeIngredients.ReorderGroups(Recipe);
                break;
            case "Remove heading":
                var destination = await ChooseGroupAsync("Keep these ingredients in…", group.Id);
                if (!destination.Chosen) return;
                if (!await AppDialogs.ConfirmAsync(Page, "Remove group heading?",
                    $"Remove “{group.Name}”? All its ingredients will be kept in {destination.Name}.", "Remove heading", destructive: true))
                    return;
                RecipeIngredients.RemoveGroup(Recipe, group.Id, destination.Id);
                break;
            default:
                return;
        }
        RefreshSections();
    }

    [RelayCommand]
    private async Task IngredientOptions(Ingredient ingredient)
    {
        var siblings = RecipeIngredients.Sections(Recipe).SelectMany(section => section.Ingredients.Contains(ingredient)
            ? section.Ingredients : []).ToList();
        var index = siblings.IndexOf(ingredient);
        var actions = new List<string>();
        if (index > 0) actions.Add("Move up");
        if (index < siblings.Count - 1) actions.Add("Move down");
        if (Recipe.IngredientGroups.Count > 0) actions.Add("Move to group");
        actions.Add("Remove ingredient");
        var action = await AppDialogs.ChooseAsync(Page, "Ingredient", actions, "Remove ingredient");
        switch (action)
        {
            case "Move up":
            case "Move down":
                var target = siblings[index + (action == "Move up" ? -1 : 1)];
                Recipe.Ingredients.Move(Recipe.Ingredients.IndexOf(ingredient), Recipe.Ingredients.IndexOf(target));
                break;
            case "Move to group":
                var destination = await ChooseGroupAsync("Move ingredient to…", ingredient.GroupId);
                if (!destination.Chosen) return;
                ingredient.GroupId = destination.Id;
                break;
            case "Remove ingredient":
                Recipe.Ingredients.Remove(ingredient);
                break;
            default:
                return;
        }
        RefreshSections();
    }

    private async Task<(bool Chosen, string? Id, string Name)> ChooseGroupAsync(string title, string? excluded)
    {
        var choices = Recipe.IngredientGroups.Where(group => group.Id != excluded)
            .Select((group, index) => (Label: $"{index + 1}. {group.Name}", group.Id)).ToList();
        var labels = choices.Select(choice => choice.Label).Append("Other ingredients").ToArray();
        var result = await AppDialogs.ChooseAsync(Page, title, labels);
        if (result is null || result == "Cancel")
            return (false, null, string.Empty);
        if (result == "Other ingredients")
            return (true, null, "Other ingredients");
        var selected = choices.First(choice => choice.Label == result);
        return (true, selected.Id, selected.Label);
    }

    [RelayCommand]
    private async Task PasteList(IngredientSection section)
    {
        var lines = await PasteIngredientsPage.ShowAsync(section.Name);
        if (lines is null)
            return;
        RecipeIngredients.AppendPastedList(Recipe, section.Id, lines);
        RefreshSections();
    }

    [RelayCommand]
    private void AddStep() => Recipe.Steps.Add(new Step { Num = Recipe.Steps.Count });

    [RelayCommand]
    private async Task StepOptions(Step step)
    {
        var index = Recipe.Steps.IndexOf(step);
        var actions = new List<string>();
        if (index > 0) actions.Add("Move up");
        if (index < Recipe.Steps.Count - 1) actions.Add("Move down");
        actions.Add("Remove step");
        var action = await AppDialogs.ChooseAsync(Page, "Method step", actions, "Remove step");
        if (action == "Move up") Recipe.Steps.Move(index, index - 1);
        else if (action == "Move down") Recipe.Steps.Move(index, index + 1);
        else if (action == "Remove step") Recipe.Steps.Remove(step);
        for (var number = 0; number < Recipe.Steps.Count; number++)
            Recipe.Steps[number].Num = number;
    }

    private bool Validate()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Recipe.Title))
        {
            SelectedSection = 0;
            ErrorMessage = "Recipe title: give your recipe a name.";
        }
        else if (Recipe.Ingredients.Count == 0 || Recipe.Ingredients.Any(ingredient => string.IsNullOrWhiteSpace(ingredient.FQIngrediantDescription)))
        {
            SelectedSection = 1;
            ErrorMessage = "Ingredients: add at least one ingredient and complete or remove every empty line.";
        }
        else if (Recipe.Steps.Count == 0 || Recipe.Steps.Any(step => string.IsNullOrWhiteSpace(step.Instructions)))
        {
            SelectedSection = 2;
            ErrorMessage = "Method: add at least one step and complete or remove every empty step.";
        }
        return !HasError;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task Save()
    {
        if (!Validate()) return;
        IsSaving = true;
        var saved = false;
        try
        {
            Recipe.Title = Recipe.Title!.Replace("\r", "").Replace("\n", "").Replace("\t", " ").Trim();
            if (selectedPhotoPath is not null)
                Recipe.ImageData = "data:image/jpeg;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(selectedPhotoPath));
            await service.SaveRecipe(Recipe, isNew);
            saved = true;
            isNew = false;
            try
            {
                await service.CacheSavedRecipe(Recipe, selectedPhotoPath);
            }
            catch (Exception exception) when (ReceipeAPIService.IsNetworkFailure(exception) || exception is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine(exception);
                await AppDialogs.AlertAsync(Page, "Saved to your server", "The downloaded copy could not be refreshed. Reconnect and refresh your recipe box before going offline.");
            }
            CleanupPhoto();
            Recipe.ImageData = null;
            original = JsonSerializer.Serialize(Recipe);
        }
        catch (HttpRequestException exception) when (isNew && exception.StatusCode == HttpStatusCode.Conflict)
        {
            isNew = false;
            OnPropertyChanged(nameof(EditorTitle));
            ErrorMessage = "This recipe already reached the server. Your edits are still here; select Save again to update the same recipe.";
            System.Diagnostics.Debug.WriteLine(exception);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Could not save. Your edits are still here; reconnect and try Save again. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally
        {
            IsSaving = false;
        }
        if (saved)
            await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
    }

    public void CleanupPhoto()
    {
        if (selectedPhotoPath is null) return;
        var path = selectedPhotoPath;
        selectedPhotoPath = null;
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not remove the temporary photo {path}: {exception}");
        }
    }
}
