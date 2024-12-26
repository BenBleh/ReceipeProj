using RecipeApp.Models;
using System.Linq;
using System.Text;
using System.Text.Json;


namespace RecipeApp.Services
{
    public class ReceipeAPIService
    {
        private readonly string APIUrl = "http://192.168.1.108:8080/";
        private readonly string FilesUrl = "http://192.168.1.108:8081/";

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
                if (jsonResponse is not null)
                {
                    recipeListItems = JsonSerializer.Deserialize<List<RecipeListItem>>(jsonResponse);

                    SetUpFileSystem(recipeListItems);
                    await GetReceipeFiles(recipeListItems);
                    loadedFromServer = true;
                }
                else
                {
                    throw new Exception("no response from server");
                }
            }
            catch (Exception ex)
            {
                //if connection to server failed use local files
                using (var reader = new StreamReader(System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", "MasterList.csv")))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = line?.Split(',');
                        if (values is not null && values.Count() > 0)
                        {
                            recipeListItems.Add(new RecipeListItem()
                            {
                                Id = values[0],
                                Title = values[1]
                            });
                        }
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
            var sourcePath = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles");
            if (!File.Exists(sourcePath))
            {
                Directory.CreateDirectory(sourcePath);
            }

            foreach (var recipeListItem in recipeListItems)
            {
                var recipePath = Path.Combine(sourcePath, recipeListItem.Id);
                if (!File.Exists(recipePath))
                {
                    Directory.CreateDirectory(recipePath);
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
                    var filePath = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipe.Id);
                    if (!File.Exists(filePath))
                    {
                        Directory.CreateDirectory(filePath);
                    }

                    filePath = Path.Combine(filePath, recipe.Id + ".jpeg");
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
            var fileName = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", "MasterList.csv");
            await DownloadAFile(uri, fileName);

            foreach (var recipe in recipeListItems)
            {
                //get the recipe file
                uri = new Uri(FilesUrl + "/" + recipe.Id + ".json");
                fileName = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipe.Id + ".json");
                await DownloadAFile(uri, fileName);


                //get the main image
                uri = new Uri(FilesUrl + "/" + recipe.Id + "/" + recipe.Id + ".jpeg");
                fileName = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipe.Id, recipe.Id + ".jpeg");
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
                var path = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipeId + ".json");
                using (var reader = new StreamReader(path))
                {
                    while (!reader.EndOfStream)
                    {
                        var recipe = JsonSerializer.Deserialize<Recipe>(reader.ReadToEnd());
                        recipe.ImagePath = Path.Combine(FileSystem.Current.AppDataDirectory, "rFiles", recipe.Id, recipe.Id + ".jpeg");
                        return recipe;
                    }
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return null;
        }

        public async Task<bool> PostNewRecipe(Recipe recipe)
        {
            var json = JsonSerializer.Serialize<Recipe>(recipe);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("Recipe", content);
            try
            {
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> UpdateRecipe(Recipe recipe)
        {
            var json = JsonSerializer.Serialize<Recipe>(recipe);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync("Recipe/" + recipe.Id, content);
            try
            {
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
