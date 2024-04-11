export function SearchBar({query ,handleChange}) 
{
return (
    <div className="container p-2">
    <div className="row d-flex justify-content-center align-items-center">
        <div className="col-sm-6">
        <div className="form">
            <i className="fa fa-search"></i>
            <input type="search" className="form-control form-input" placeholder="Search" value={query} onChange={handleChange} />                   
        </div>
        </div>
    </div>
    </div>
)

}