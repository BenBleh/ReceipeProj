using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using RecipeApp.Models;
using RecipeApp.Services;
using System.ComponentModel;
using System.Text;

namespace RecipeApp.ViewModels
{
    public partial class AddEditViewModel : ObservableObject
    {
        [ObservableProperty]
        Recipe recipe = new();

        [ObservableProperty]
        string lazyIngredientString = string.Empty;

        public string CurrentError;

        ReceipeAPIService ReceipeAPIService;

        public AddEditViewModel()
        {
            recipe.Ingredients = [];
            recipe.Steps = [];
            AddNewIngredient();
            AddNewStep();
            this.ReceipeAPIService = new ReceipeAPIService();
            this.CurrentError = string.Empty;
            this.PropertyChanged += AddEditViewModel_PropertyChanged;
        }

        public AddEditViewModel(Recipe recipe)
        {
            this.recipe = recipe;
            this.ReceipeAPIService = new ReceipeAPIService();
            this.CurrentError = string.Empty;

            ImagePath = this.Recipe.ImagePath;
            HasImage = (ImagePath is not null);
            SetImageStream();

            var sb = new StringBuilder();
            foreach (var ing in this.Recipe.Ingredients)
            {
                sb.AppendLine(ing.FQIngrediantDescription);
            }
            LazyIngredientString = sb.ToString();

            this.PropertyChanged += AddEditViewModel_PropertyChanged;
        }


        private void SetImageStream()
        {
            if (HasImage)
            {
                if (!String.IsNullOrWhiteSpace(ImagePath))
                {
                    ImageStream = ImageSource.FromStream(() =>
                    {
                        return File.OpenRead(ImagePath);
                    });
                }
            }
        }

        private void AddEditViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LazyIngredientString))
            {
                Recipe.Ingredients.Clear();

                //split lazyIngredientString by line
                string[] lines = LazyIngredientString.Trim().Split(
                     ["\r\n", "\r", "\n"],
                     StringSplitOptions.None
                 );

                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        this.Recipe.Ingredients.Add(new Ingredient()
                        {
                            FQIngrediantDescription = line.Trim()
                        });
                    }
                }

            }
        }

        [RelayCommand]
        private async Task GetImage()
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                try
                {
                    FileResult fileResult = await MediaPicker.PickPhotoAsync();
                    if (fileResult is not null)
                    {
                        ImagePath = fileResult.FullPath;
                        HasImage = (ImagePath is not null);
                        SetImageStream();
                        hasImageBeenUpdated = true;
                    }
                }
                catch { }
            }
        }

        [ObservableProperty]
        bool hasImage = false;

        [ObservableProperty]
        string imagePath = string.Empty;

        bool hasImageBeenUpdated = false;

        [ObservableProperty]
        ImageSource imageStream;

        [RelayCommand]
        private void AddNewIngredient()
        {
            this.Recipe.Ingredients.Add(new Ingredient());
        }

        [RelayCommand]
        private async Task Save()
        {
            if (IsValid())
            {

                if (hasImageBeenUpdated)
                {
                    var imageData = await File.ReadAllBytesAsync(ImagePath);
                    this.Recipe.ImageData = Convert.ToBase64String(imageData);
                    //this is a hack so I don't have to update the webAPI
                    this.Recipe.ImageData = "??;base64," + this.Recipe.ImageData;
                }

                bool saveResult = false;
                if (Recipe.Id is not null)
                {
                    saveResult = await this.ReceipeAPIService.UpdateRecipe(this.Recipe);
                }
                else
                {
                    saveResult = await this.ReceipeAPIService.PostNewRecipe(this.Recipe);
                }
                if (saveResult)
                {
                    //refresh back end data
                    await this.ReceipeAPIService.GetMasterList();
                    await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Save failed!", "🤷 The save failed for some reason 🤷", "OK");
                }

            }
            else
            {
                //this is probably not best practice.
                await Application.Current.MainPage.DisplayAlert("Validation errors!", CurrentError, "OK");
            }
        }

        [RelayCommand]
        private void AddNewStep()
        {
            this.Recipe.Steps.Add(new Step()
            {
                Num = Recipe.Steps.Count
            });

            OnPropertyChanged("Steps");
        }

        private bool IsValid()
        {
            this.CurrentError = string.Empty;
            var sb = new StringBuilder();
            if (Recipe is null)
                sb.AppendLine("Recipe is null");
            if (string.IsNullOrEmpty(Recipe.Title))
                sb.AppendLine("Recipe has no title");
            if (Recipe.Ingredients is null || Recipe.Ingredients.Count < 1)
                sb.AppendLine("Recipe has no ingredients");
            if (Recipe.Steps is null || Recipe.Steps.Count < 1)
                sb.AppendLine("Recipe has no steps");

            //ensure all steps and ingredients are valid
            foreach (var ing in Recipe.Ingredients)
            {
                if (string.IsNullOrEmpty(ing.FQIngrediantDescription))
                    sb.AppendLine("An ingredient has no description");
            }
            foreach (var step in Recipe.Steps)
            {
                if (string.IsNullOrEmpty(step.Instructions))
                    sb.AppendLine("A step has no instructions");
            }

            this.CurrentError = sb.ToString();
            return String.IsNullOrEmpty(sb.ToString());
        }
    }
}
