using Microsoft.AspNetCore.Mvc;
using RecipeAPI.Interfaces;
using RecipeAPI.Models;

namespace RecipeAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MasterRecipeListController : ControllerBase
    {
        private readonly ILogger<MasterRecipeListController> _logger;
        private IRecipeAccess _recipeAccess;

        public MasterRecipeListController(ILogger<MasterRecipeListController> logger, IRecipeAccess recipeAccess)
        {
            _logger = logger;
            _recipeAccess = recipeAccess;

        }

        [HttpGet(Name = "GetRecipeList")]
        public List<RecipeListItem> Get()
        {
            return _recipeAccess.GetRecipes().Recipes;
        }
    }
}
