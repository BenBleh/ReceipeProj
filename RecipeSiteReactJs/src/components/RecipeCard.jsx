import {Link} from 'react-router-dom'
import Card from 'react-bootstrap/Card'
import './RecipeCardStyles.css'

export function RecipeCard({recipe, GetImgSRC})
{

function GetDescription()    {
    if(recipe.hasOwnProperty('notes'))
    {
        return (
            <Card.Text>
                {recipe.notes}
          </Card.Text>
        )    }
}

    return (
        <div key={recipe.id} className='row justify-content-center'>
            <Card style={{ width: '18rem' }} key={recipe.id}>
            <Card.Img variant="top" src={GetImgSRC(recipe.id)} onError={e => e.target.style.display = 'none'}/>
            <Card.Body>
                <Link to={`/ViewRecipe/${recipe.id}`} className='RCardTitle'>
                    <Card.Title>{recipe.title}</Card.Title>
                </Link>
                {
                    GetDescription()
                    
                }
            </Card.Body>
            </Card>     
        </div>
    )
}