import React, { useState, useEffect } from 'react'
import axios from 'axios'
import {Link} from 'react-router-dom'


import MaterialIcon, {colorPalette} from 'material-icons-react'

import { RecipeCard } from '../components/RecipeCard';

export function Home()
{

    const [posts, setPosts] = useState([]);

     useEffect(() => {
        axios.get('http://192.168.1.116:8080/MasterRecipeList')
          .then(response => {
            setPosts(response.data);
          })
          .catch(error => {
            console.error(error);
          })
      }, [])

function GetThumbNailSRC(id)
{

return ( "http://192.168.1.116:8081/" + id + "/thumb.jpeg" )
}

     return (
        <>
        <div className='row'>
            <div className='col-1'></div>
            <div className='col-9'>
            <h1>Welcome home</h1>
            </div>
            <div className='col-1'>
                <Link to="/Edit">
                    <MaterialIcon icon="edit" />
                </Link>
            </div>
            </div>
            {posts.map((recipe) => 
                    {
                        return (
                            <>
                                <RecipeCard key={recipe.id} recipe={recipe} GetThumbNailSRC={GetThumbNailSRC}/>
                            </>
                        )
                    }
                    )}
            
        </>
     )

}