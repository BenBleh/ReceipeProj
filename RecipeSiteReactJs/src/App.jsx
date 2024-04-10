import {HashRouter as Router, Routes, Route} from 'react-router-dom'

//import './App.css'

import 'bootstrap/dist/css/bootstrap.min.css'

import { Home } from './pages/Home'
import { EditAddRecipe } from './pages/EditAddRecipe'
import { ViewRecipe } from './pages/ViewRecipe'
import {Layout} from './components/Layout'

function App() {
  return (
    <Router>
    <Routes>
      <Route element={<Layout/>}>
        <Route path="/" element={<Home/>}/>
        <Route path="/Add" element={<EditAddRecipe/>}/>
        <Route path="/Edit" element={<EditAddRecipe/>}/>
        <Route path="/ViewRecipe" element={<ViewRecipe/>}/>
      </Route>
    </Routes>
  </Router>
  )
}

export default App
