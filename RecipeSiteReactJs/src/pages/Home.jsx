import React, { useState, useEffect } from 'react'
import axios from 'axios'
import {Link} from 'react-router-dom'

import MaterialIcon, {colorPalette} from 'material-icons-react'

import { RecipeCard } from '../components/RecipeCard';
import { SearchBar } from '../components/SearchBar';

export function Home()
{

    const [posts, setPosts] = useState([])
    const [filteredRecipies, setfilteredRecipies] = useState([])
    const [query, setquery] = useState('')
    
    const handleChange = (e) => {
        const results = posts.filter(post => {
            if (e.target.value === "") return posts
            return post.title.toLowerCase().includes(e.target.value.toLowerCase())
        })
        setquery(e.target.value)
        setfilteredRecipies(results)
        
    } 

     useEffect(() => {
        axios.get('http://192.168.1.116:8080/MasterRecipeList')
          .then(response => {
            setPosts(response.data)
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
                <h1>Welcome home</h1>
            </div>
            <div className='col-1'>
                <Link to="/Edit">
                    <MaterialIcon icon="edit" />
                </Link>
            </div>
        </div>
        
            {filteredRecipies.map((recipe) => 
                    {
                        return (
                            <>
                                <RecipeCard key={recipe.id} recipe={recipe} GetThumbNailSRC={GetThumbNailSRC}/>
                                <br/>
                            </>
                        )
                    }
                    )}
        
        </>
     )

}