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
            if (!isWide() && !await AppDialogs.IngredientAsync(owner, args.Reference, model.Recipe, model.Multiplier))
                return;
            returnPosition = position;
            HasReturn = true;
            returnButton.Text = $"Back to step {args.Step.Num + 1} →";
            SemanticProperties.SetDescription(returnButton, $"Back to method step {args.Step.Num + 1}");
            model.IsIngredients = true;
            UpdateReturnVisibility();
            await Task.Delay(120);
            foreach (var ingredient in model.Recipe.Ingredients)
                ingredient.IsHighlighted = args.Reference.Ingredients.Contains(ingredient);
            var first = args.Reference.Ingredients[0];
            var target = ingredientsScroll.GetVisualTreeDescendants().OfType<IngredientItemView>()
                .FirstOrDefault(view => ReferenceEquals(view.BindingContext, first))
                ?? throw new InvalidOperationException("The related ingredient could not be found in the visible list.");
            await ingredientsScroll.ScrollToAsync(target, ScrollToPosition.Center, false);
            SemanticScreenReader.Default.Announce(string.Join(". ", args.Reference.Ingredients.Select(item => item.DisplayDescription)));
            var generation = ++highlightGeneration;
            owner.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(12), () =>
            {
                if (generation == highlightGeneration) ClearHighlights();
            });
        }
        catch (Exception exception)
        {
            model.ErrorMessage = $"Could not show this ingredient. {exception.Message}";
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
    }
}
