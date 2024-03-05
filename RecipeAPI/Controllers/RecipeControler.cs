using Microsoft.AspNetCore.Mvc;
using RecipeAPI.Interfaces;
using RecipeAPI.Models;

namespace RecipeAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RecipeController : ControllerBase
    {
        private readonly ILogger<RecipeController> _logger;
        private IRecipeAccess _recipeAccess;

        public RecipeController(ILogger<RecipeController> logger, IRecipeAccess recipeAccess)
        {
            _logger = logger;
            _recipeAccess = recipeAccess;
        }

        [HttpGet("{Id}")]
        public Recipe GetRecipe(string Id)
        {
            return _recipeAccess.GetRecipe(Id);
        }

        [HttpPost]
        public async Task<ActionResult<Recipe>> Post(Recipe recipe)
        {
            recipe = _recipeAccess.CreateRecipe(recipe);

            return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, recipe);
        }

        [HttpPut("{Id}")]
        public async Task<ActionResult<Recipe>> UpdateRecipe(string Id, Recipe recipe)
        {
            _recipeAccess.UpdateRecipe(Id, recipe);
            return Ok();
        }
    }
}
