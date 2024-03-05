using Newtonsoft.Json.Serialization;
using RecipeAPI.Helpers;
using RecipeAPI.Interfaces;
using RecipeAPI.Models;
using System.Collections.Generic;
using System.Text.Json;

namespace RecipeAPI.DataAccess.JsonFileBased
{
    public class RecipeAccessJson : IRecipeAccess
    {
        MasterRecipeList IRecipeAccess.GetRecipes()
        {
            MasterRecipeList masterList = new MasterRecipeList { Recipes = new List<RecipeListItem>() };

            using (var reader = new StreamReader(ConfigurationHelper.Instance.RecipeFilePathValue + "MasterList.csv"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    masterList.Recipes.Add(new RecipeListItem() 
                    {
                        Id = values[0],
                        Title = values[1]
                    });                    
                }
            }
        
            return masterList;
        }

        Recipe IRecipeAccess.GetRecipe(string id)
        {
            Recipe? recipe;
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
                
                //write to file
                string jsonString = JsonSerializer.Serialize(recipe);

                File.WriteAllText(ConfigurationHelper.Instance.RecipeFilePathValue + recipe.Id + ".json", jsonString);

                //update master list
                UpdateMasterList(recipe.Id, recipe.Title);
            }
            catch(Exception ex) 
            {
                throw ex;    
            }            
        }

        Recipe IRecipeAccess.CreateRecipe(Recipe recipe)
        {
            //generate new id
            recipe.Id = Guid.NewGuid().ToString();

            //write to file
            string jsonString = JsonSerializer.Serialize(recipe);

            File.WriteAllText(ConfigurationHelper.Instance.RecipeFilePathValue + recipe.Id + ".json", jsonString);

            //update master list
            UpdateMasterListWithNewRecipe(recipe.Id, recipe.Title);

            return recipe;
        }

        private async Task UpdateMasterList(string id, string title)
        {
            var fileName = ConfigurationHelper.Instance.RecipeFilePathValue + "MasterList.csv";
            string[] arrLine = File.ReadAllLines(fileName);
            for(int i = 0; i < arrLine.Length; i++) 
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
    }
}
