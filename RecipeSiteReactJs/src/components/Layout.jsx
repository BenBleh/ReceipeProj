import {Navbar} from "./Navbar"
import {Outlet} from 'react-router-dom'

export function Layout()
{
    return (
    <>
        <Navbar/>
        <main>
        <div className="col">
            <div className="w-75 border  mx-auto">
                <Outlet/>
            </div>
        </div>  
        </main>
    </>
    )
}