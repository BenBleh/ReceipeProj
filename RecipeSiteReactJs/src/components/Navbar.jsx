import {Link} from 'react-router-dom'

import Container from 'react-bootstrap/Container'
import NavbarBS from 'react-bootstrap/Navbar'

export function Navbar()
{
    return (
    <>
     <NavbarBS className="bg-body-tertiary">
        <Container>
          <NavbarBS.Brand><Link to="/" >Recipe Site</Link></NavbarBS.Brand>
        </Container>
      </NavbarBS>
{/* 
        <div>
            <Link to="/" ><button className="navbutton">RecipeSite</button></Link>
        </div> */}
    </>
    )
}