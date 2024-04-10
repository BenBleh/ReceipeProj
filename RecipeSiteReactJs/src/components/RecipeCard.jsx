import {Link} from 'react-router-dom'

import Button from 'react-bootstrap/Button';
import Card from 'react-bootstrap/Card';

export function RecipeCard({recipe, GetThumbNailSRC})
{

    return (
        <Card style={{ width: '18rem' }}>
        <Card.Img variant="top" src={GetThumbNailSRC(recipe.id)} onError={e => e.target.style.display = 'none'}/>
        <Card.Body>
        <Link to="/ViewRecipe">
            <Card.Title>{recipe.title}</Card.Title>
        </Link>
        {/* //maybe use rest odata thing to just get description?
        <Card.Text>
            Some quick example text to build on the card title and make up the
            bulk of the card's content.
        </Card.Text>                 */}
        </Card.Body>
        </Card>     
    

    )
}