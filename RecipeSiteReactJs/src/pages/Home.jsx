import React, { useState, useEffect } from 'react'
import axios from 'axios'
import {Link} from 'react-router-dom'

import MaterialIcon, {colorPalette} from 'material-icons-react'

import { RecipeCard } from '../components/RecipeCard';
import { SearchBar } from '../components/SearchBar';

export function Home()
{

    const [recipies, setRecipies] = useState([])
    const [filteredRecipies, setfilteredRecipies] = useState([])
    const [query, setquery] = useState('')
    
    const handleChange = (e) => {
        const results = recipies.filter(recipe => {
            if (e.target.value === "") return recipies
            return recipe.title.toLowerCase().includes(e.target.value.toLowerCase())
        })
        setquery(e.target.value)
        setfilteredRecipies(results)
        
    } 

     useEffect(() => {
        axios.get('http://192.168.1.116:8080/MasterRecipeList')
          .then(response => {
            setRecipies(response.data)
            setfilteredRecipies(response.data)
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
        <SearchBar query={query} handleChange={handleChange}/>
        <div className='row'>
            <div className='col-1'></div>
            <div className='col-9'>               
            </div>
            <div className='col-1'>
                <Link to="/Add">
                    <MaterialIcon icon="add" />
                </Link>
            </div>
        </div>
        
            {filteredRecipies.map((recipe) => 
                {
                    return (
                        <>
                            <RecipeCard key={recipe.id} recipe={recipe} GetImgSRC={GetThumbNailSRC}/>
                            <br/>
                        </>
                    )
                }
                )
            }
        
        </>
     )

}