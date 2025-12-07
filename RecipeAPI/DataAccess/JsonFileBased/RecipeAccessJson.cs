using RecipeAPI.Helpers;
using RecipeAPI.Interfaces;
using RecipeAPI.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;

namespace RecipeAPI.DataAccess.JsonFileBased
{
    public class RecipeAccessJson : IRecipeAccess
    {
        MasterRecipeList IRecipeAccess.GetRecipes()
        {
            MasterRecipeList masterList = new MasterRecipeList { Recipes = new List<RecipeListItem>() };
            try
            {
                using (var reader = new StreamReader(ConfigurationHelper.Instance.RecipeFilePathValue + "MasterList.csv"))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = line?.Split(',');

                        masterList.Recipes.Add(new RecipeListItem()
                        {
                            Id = values[0],
                            Title = values[1]
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return masterList;
        }

        Recipe IRecipeAccess.GetRecipe(string id)
        {
            Recipe? recipe;
            CheckFileExists(ConfigurationHelper.Instance.RecipeFilePathValue + id + ".json");
            string jsonString = File.ReadAllText(ConfigurationHelper.Instance.RecipeFilePathValue + id + ".json");
            if (!string.IsNullOrEmpty(jsonString))
            {
                recipe =
                    JsonSerializer.Deserialize<Recipe>(jsonString);
            }
            else
            {
                throw new FileNotFoundException("File with following name not found: " + id);
            }
            return recipe;
        }

        void IRecipeAccess.UpdateRecipe(string Id, Recipe recipe)
        {
            try
            {
                recipe.Id = Id;

                CheckFileExists(ConfigurationHelper.Instance.RecipeFilePathValue + recipe.Id + ".json");

                manageRecipeImages(recipe);
                OrderSteps(recipe);
                UpdateLegacyIngredients(recipe);

                //write to file
                string jsonString = JsonSerializer.Serialize(recipe);

                File.WriteAllText(ConfigurationHelper.Instance.RecipeFilePathValue + recipe.Id + ".json", jsonString);

                //update master list
                UpdateMasterList(recipe.Id, recipe.Title);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        Recipe IRecipeAccess.CreateRecipe(Recipe recipe)
        {
            //generate new id
            recipe.Id = Guid.NewGuid().ToString();

            manageRecipeImages(recipe);
            OrderSteps(recipe);
            UpdateLegacyIngredients(recipe);

            //write to file
            string jsonString = JsonSerializer.Serialize(recipe);

            File.WriteAllText(ConfigurationHelper.Instance.RecipeFilePathValue + recipe.Id + ".json", jsonString);

            //update master list
            UpdateMasterListWithNewRecipe(recipe.Id, recipe.Title);

            return recipe;
        }

        //this should handle cases where steps between steps might have been deleted.
        private void OrderSteps(Recipe recipe)
        {
            if (recipe.Steps != null)
            {
                for (int i = 0; i < recipe.Steps.Count; i++)
                {
                    recipe.Steps[i].Num = i;
                }
            }
        }

        private void UpdateLegacyIngredients(Recipe recipe)
        {
            if (recipe.Ingredients is not null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (ingredient.Unit is not null
                        || ingredient.Qty is not null
                        || ingredient.Description is not null)
                    {
                        ingredient.FQIngrediantDescription = string.Format("{0} {1} {2}", ingredient.Qty, ingredient.Unit, ingredient.Description);

                    }
                }
            }
        }

        private async Task UpdateMasterList(string id, string title)
        {
            var fileName = ConfigurationHelper.Instance.RecipeFilePathValue + "MasterList.csv";
            string[] arrLine = File.ReadAllLines(fileName);
            for (int i = 0; i < arrLine.Length; i++)
            {
                if (arrLine[i].Contains(id))
                {
                    arrLine[i] = id + "," + title;
                    break;
                }

            }
            File.WriteAllLines(fileName, arrLine);
        }

        private async Task UpdateMasterListWithNewRecipe(string id, string title)
        {
            using (StreamWriter sw = File.AppendText(ConfigurationHelper.Instance.RecipeFilePathValue + "MasterList.csv"))
            {
                sw.WriteLine(id + "," + title);
            }
        }

        private void CheckFileExists(string filepath)
        {
            if (!File.Exists(filepath))
            {
                throw new Exception("File could not be found");
            }
        }

        private void manageRecipeImages(Recipe recipe)
        {
            if (!string.IsNullOrEmpty(recipe.ImageData) && recipe.ImageData.Contains("base64"))
            {
                WriteImage(ConfigurationHelper.Instance.RecipeFilePathValue + '/' + recipe.Id + '/'
                    , recipe.Id
                    , recipe.ImageData
                    , true
                    );
                recipe.ImageData = null;
            }
            foreach (var step in recipe.Steps)
            {
                if (!string.IsNullOrEmpty(step.ImageData) && step.ImageData.Contains("base64"))
                {
                    WriteImage(ConfigurationHelper.Instance.RecipeFilePathValue + '/' + recipe.Id + '/'
                        , step.Num.ToString()
                        , step.ImageData
                        , false
                        );
                }
                step.ImageData = null;
            }
        }

        private void WriteImage(string filepath, string fileName, string imageData, bool createThumbNail)
        {
            //process input data
            var values = imageData.Split(';');
            string filetype = values[0].Substring(values[0].LastIndexOf('/') + 1); //text after / gives us file type
            string base64 = values[1].Substring(values[0].LastIndexOf("base64,") + 8); //text after base64, is the base64 string
            var base64EncodedBytes = System.Convert.FromBase64String(base64);
            //var decodedImg = Convert.FromBase64String()

            var file = new FileInfo(filepath);
            file.Directory.Create(); // If the directory already exists, this method does nothing.            

            Image image;
            using (MemoryStream ms = new MemoryStream(base64EncodedBytes))
            {
                image = Image.FromStream(ms);
                image.Save(filepath + fileName + ".jpeg", ImageFormat.Jpeg);
            }

            if (createThumbNail)
            {
                var thumbnail = image.GetThumbnailImage(120, 120, () => false, IntPtr.Zero);
                thumbnail.Save(filepath + "thumb" + ".jpeg", ImageFormat.Jpeg);
            }
        }
    }
}
