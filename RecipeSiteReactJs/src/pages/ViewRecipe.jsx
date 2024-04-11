import axios from 'axios'
import React, { useState, useEffect } from 'react'
import { useParams } from 'react-router-dom'
import {Link} from 'react-router-dom'
import { RecipeCard } from '../components/RecipeCard'
import MaterialIcon, {colorPalette} from 'material-icons-react'
import { IngredientLayout } from '../components/IngredientLayout'
import { StepLayout } from '../components/StepLayout'

export function ViewRecipe()
{
    const [recipe, setRecipe] = useState({})
     const {id} = useParams()

    useEffect(() => {
        axios.get('http://192.168.1.116:8080/Recipe/' + id)
          .then(response => {
            setRecipe(response.data)            
          })
          .catch(error => {
            console.error(error);
          })
      }, [])

      function GetMainIMGSRC(id)
      {
          return ( "http://192.168.1.116:8081/" + id + "/" + id +".jpeg" )
      }

    //   console.log(recipe)

     return (
        <>
        <div className='row'>
            <div className='col-10'>
                <h1>{recipe.title}</h1>
            </div>
            <div className='col-1'>
                <Link to="/Edit">
                    <MaterialIcon icon="edit" />
                </Link></div>
        </div>
        <div className='row'>
            <div className='col-6'>
                <IngredientLayout ingredients={recipe.ingredients} />         
            </div>
            <div className='col-6'>     
                <RecipeCard recipe={recipe} GetImgSRC={GetMainIMGSRC}/>
            </div>
        </div>

        <div>
            <StepLayout steps={recipe.steps} />
        </div>            
            
        </>
     )

}