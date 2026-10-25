using Microsoft.AspNetCore.Mvc;
using RecipeAPI.Helpers;
using RecipeAPI.Interfaces;
using RecipeAPI.Models;

namespace RecipeAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class RecipeController(ILogger<RecipeController> logger, IRecipeAccess recipeAccess) : ControllerBase
{
    [HttpGet("{id}")]
    public ActionResult<Recipe> GetRecipe(string id)
    {
        if (!Guid.TryParse(id, out _))
            return BadRequest("The recipe ID must be a UUID.");
        try
        {
            return recipeAccess.GetRecipe(id);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public ActionResult<Recipe> Post(Recipe recipe)
    {
        return Write(recipe, null);
    }

    [HttpPut("{id}")]
    public ActionResult<Recipe> UpdateRecipe(string id, Recipe recipe)
    {
        if (!Guid.TryParse(id, out _))
            return BadRequest("The recipe ID must be a UUID.");
        return Write(recipe, id);
    }

    private ActionResult<Recipe> Write(Recipe recipe, string? id)
    {
        var errors = RecipeValidation.NormalizeAndValidate(recipe);
        if (errors.Count > 0)
            return ValidationProblem(new ValidationProblemDetails(errors));
        try
        {
            if (id is null)
            {
                var saved = recipeAccess.CreateRecipe(recipe);
                return CreatedAtAction(nameof(GetRecipe), new { id = saved.Id }, saved);
            }
            recipeAccess.UpdateRecipe(id, recipe);
            return Ok(recipe);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Recipe write conflict for {RecipeId}", id ?? recipe.Id);
            return Conflict(new ProblemDetails { Title = exception.Message, Status = StatusCodes.Status409Conflict });
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(exception, "Invalid recipe or photo for {RecipeId}", id ?? recipe.Id);
            return BadRequest(new ProblemDetails { Title = exception.Message, Status = StatusCodes.Status400BadRequest });
        }
    }
}
