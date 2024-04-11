import {Link} from 'react-router-dom'

import Button from 'react-bootstrap/Button';
import Card from 'react-bootstrap/Card';

import './RecipeCardStyles.css'

export function RecipeCard({recipe, GetThumbNailSRC})
{

    return (
        <div key={recipe.id} className='row justify-content-center'>
            <Card style={{ width: '18rem' }}>
            <Card.Img variant="top" src={GetThumbNailSRC(recipe.id)} onError={e => e.target.style.display = 'none'}/>
            <Card.Body>
            <Link to="/ViewRecipe" className='RCardTitle'>
                <Card.Title>{recipe.title}</Card.Title>
            </Link>
            </Card.Body>
            </Card>     
        </div>

    )
}