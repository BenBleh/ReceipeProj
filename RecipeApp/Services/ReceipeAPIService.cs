using RecipeApp.Models;
using RecipeApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;


namespace RecipeApp.Services
{
    public class ReceipeAPIService
    {
        private readonly string APIUrl = "http://192.168.1.116:8080/";
        private readonly string FilesUrl = "http://192.168.1.116:8081/";

        HttpClient httpClient;

        public ReceipeAPIService()
        {
            httpClient = new()
            {
                BaseAddress = new Uri(APIUrl),
                Timeout = TimeSpan.FromSeconds(5)
            };
        }


        public async Task<MasterRecipeList> GetMasterList()
        {
            bool loadedFromServer = false;
            List<RecipeListItem> recipeListItems = [];
            try
            {

                using HttpResponseMessage response = await httpClient.GetAsync("MasterRecipeList");
                response.EnsureSuccessStatusCode();
                var jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"{jsonResponse}\n");

                recipeListItems = JsonSerializer.Deserialize<List<RecipeListItem>>(jsonResponse);
                SetUpFileSystem(recipeListItems);
                await GetReceipeFiles(recipeListItems);
                loadedFromServer = true;
            }
            catch (Exception ex)
            {
                //if connection to server failed use local files
                using (var reader = new StreamReader(System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", "MasterList.csv")))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = line.Split(',');

                        recipeListItems.Add(new RecipeListItem()
                        {
                            Id = values[0],
                            Title = values[1]
                        });
                    }
                }
                loadedFromServer = false;
            }

            //set up 'main' image

            foreach (var recpie in recipeListItems)
            {
                var path = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recpie.Id, recpie.Id + ".jpeg");
                if (File.Exists(path))
                {
                    recpie.ImageData = path;
                }
                else
                {
                    recpie.ImageData = "kittys.png";
                }
            }

            return new MasterRecipeList { Recipes = recipeListItems, LoadedFromServer = loadedFromServer };
        }

        private void SetUpFileSystem(List<RecipeListItem> recipeListItems)
        {
            var sourcePath = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles");
            if (!File.Exists(sourcePath))
            {
                System.IO.Directory.CreateDirectory(sourcePath);
            }

            foreach (var recipeListItem in recipeListItems)
            {
                var recipePath = System.IO.Path.Combine(sourcePath, recipeListItem.Id);
                if (!File.Exists(recipePath))
                {
                    System.IO.Directory.CreateDirectory(recipePath);
                }
            }
        }

        private async Task GetThumbnails(List<RecipeListItem> recipeListItems)
        {
            try
            {
                foreach (var recipe in recipeListItems)
                {
                    var uri = new Uri(FilesUrl + "/" + recipe.Id + "/thumb.jpeg");
                    var filePath = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipe.Id);
                    if (!File.Exists(filePath))
                    {
                        System.IO.Directory.CreateDirectory(filePath);
                    }

                    filePath = System.IO.Path.Combine(filePath, recipe.Id + ".jpeg");
                    recipe.ImageData = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipe.Id, recipe.Id + ".jpeg");

                    await DownloadAFile(uri, filePath);
                }
            }
            catch
            { //ignore
            }

        }

        private async Task GetReceipeFiles(List<RecipeListItem> recipeListItems)
        {
            var uri = new Uri(FilesUrl + "/" + "MasterList.csv");
            var fileName = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", "MasterList.csv");
            await DownloadAFile(uri, fileName);

            foreach (var recipe in recipeListItems)
            {
                //get the recipe file
                uri = new Uri(FilesUrl + "/" + recipe.Id + ".json");
                fileName = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipe.Id + ".json");
                await DownloadAFile(uri, fileName);


                //get the main image
                uri = new Uri(FilesUrl + "/" + recipe.Id + "/" + recipe.Id + ".jpeg");
                fileName = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipe.Id, recipe.Id + ".jpeg");
                await DownloadAFile(uri, fileName);
            }
        }

        private async Task DownloadAFile(Uri uri, string filePath)
        {
            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage response = await client.GetAsync(uri))

                    if (response.IsSuccessStatusCode)
                    {

                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        {
                            using (var fs = new FileStream(filePath, FileMode.Create))
                            {
                                await stream.CopyToAsync(fs);
                            }
                        }
                    }
            }
        }

        public async Task<Recipe> GetRecipe(string recipeId)
        {
            try
            {
                var path = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipeId + ".json");
                using (var reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        return JsonSerializer.Deserialize<Recipe>(reader.ReadToEnd());
                    }
                }

            }
            catch (Exception ex)
            {

            }

            return null;
        }
    }
}
