using Nibbles.Components;
using Nibbles.Models;
using Nibbles.ViewModels;

namespace Nibbles.Services;

public sealed class IngredientReferenceNavigation(Page owner, RecipeDetailsViewModel model,
    ScrollView ingredientsScroll, ScrollView methodScroll, Func<bool> isWide, Button returnButton)
{
    private double returnPosition;
    private bool opening;
    private int highlightGeneration;
    public bool HasReturn { get; private set; }

    public async Task OpenAsync(IngredientReferenceEventArgs args)
    {
        if (opening || model.Recipe is null) return;
        opening = true;
        try
        {
            var position = methodScroll.ScrollY;
            var reference = args.Reference;
            if (reference.NeedsChoice)
            {
                var choices = new List<(string Label, IngredientReference Reference)>();
                foreach (var candidate in reference.Groups)
                    choices.Add(($"Ingredient group: {candidate.Group.Name} ({candidate.CountDescription})\n" +
                        string.Join("; ", candidate.Ingredients.Take(2).Select(ingredient => ingredient.DisplayDescription)),
                        reference with { Ingredients = [], Groups = [candidate] }));
                foreach (var ingredient in reference.Ingredients)
                {
                    var section = model.IngredientSections.First(section => section.Ingredients.Contains(ingredient));
                    choices.Add(($"Ingredient: {ingredient.DisplayDescription}" + (section.IsNamed ? $"\n{section.Name}" : ""),
                        reference with { Ingredients = [ingredient], Groups = [] }));
                }
                var labels = choices.Select((choice, index) => $"{index + 1}. {choice.Label}").ToArray();
                var selected = await AppDialogs.ChooseAsync(owner, $"“{reference.Text}” refers to…", labels);
                if (selected is null) return;
                var selectedIndex = Array.IndexOf(labels, selected);
                if (selectedIndex < 0) throw new InvalidOperationException("The selected reference could not be found.");
                reference = choices[selectedIndex].Reference;
            }
            var group = reference.Groups.SingleOrDefault();
            var ingredients = group?.Ingredients ?? reference.Ingredients;
            if (ingredients.Count == 0) throw new InvalidOperationException("The reference has no related ingredients.");
            if (!isWide() && !await AppDialogs.IngredientAsync(owner, reference, model.Recipe, model.Multiplier))
                return;
            returnPosition = position;
            HasReturn = true;
            returnButton.Text = $"Back to step {args.Step.Num + 1} →";
            SemanticProperties.SetDescription(returnButton, $"Back to method step {args.Step.Num + 1}");
            model.IsIngredients = true;
            UpdateReturnVisibility();
            await Task.Delay(120);
            foreach (var ingredient in model.Recipe.Ingredients)
                ingredient.IsHighlighted = ingredients.Contains(ingredient);
            foreach (var section in model.IngredientSections)
                section.IsHighlighted = group is not null && section.Id == group.Group.Id;
            VisualElement? target = group is null
                ? ingredientsScroll.GetVisualTreeDescendants().OfType<IngredientItemView>()
                    .FirstOrDefault(view => ReferenceEquals(view.BindingContext, ingredients[0]))
                : ingredientsScroll.GetVisualTreeDescendants().OfType<Grid>()
                    .FirstOrDefault(view => view.BindingContext is IngredientSection section && section.Id == group.Group.Id);
            if (target is null) throw new InvalidOperationException("The related ingredient or group could not be found in the visible list.");
            await ingredientsScroll.ScrollToAsync(target, group is null ? ScrollToPosition.Center : ScrollToPosition.Start, false);
            SemanticScreenReader.Default.Announce(group is null
                ? string.Join(". ", ingredients.Select(item => item.DisplayDescription))
                : $"Ingredient group {group.Group.Name}. {group.CountDescription}.");
            var generation = ++highlightGeneration;
            owner.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(12), () =>
            {
                if (generation == highlightGeneration) ClearHighlights();
            });
        }
        catch (Exception exception)
        {
            model.ErrorMessage = $"Could not show this ingredient reference. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
        finally { opening = false; }
    }

    public async Task ReturnAsync()
    {
        try
        {
            model.IsIngredients = false;
            await Task.Delay(120);
            await methodScroll.ScrollToAsync(0, returnPosition, false);
            returnButton.IsVisible = false;
            HasReturn = false;
            ClearHighlights();
        }
        catch (Exception exception)
        {
            model.ErrorMessage = $"Could not return to the method. {exception.Message}";
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    public void Clear()
    {
        HasReturn = false;
        returnButton.IsVisible = false;
        ClearHighlights();
    }

    public void UpdateReturnVisibility() => returnButton.IsVisible = HasReturn && !isWide() && model.IsIngredients;

    private void ClearHighlights()
    {
        highlightGeneration++;
        if (model.Recipe is not null)
            foreach (var ingredient in model.Recipe.Ingredients) ingredient.IsHighlighted = false;
        foreach (var section in model.IngredientSections) section.IsHighlighted = false;
    }
}
