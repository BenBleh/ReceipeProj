export function IngredientLayout({ingredients})
{

    return (
      
        <ul>
        {
            ingredients ?
            ingredients.map(ingredient  => {
                return (
                    
                    <li>
                        <input type='checkbox'/>
                        <label>{ingredient.description}</label>
                    </li>
                    
                )
            })
            : ""
        }           
    </ul>     
                    
               
    )
}