import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { environment } from './../../environments/environment';

interface RecipeListItem {
  id: string;
  title: string;
  thumbnailSRC: string;
  

}

@Component({
  selector: 'app-recipe-list',
  templateUrl: './recipe-list.component.html',
  styleUrl: './recipe-list.component.css'
})
export class RecipeListComponent implements OnInit {
  public recipes: RecipeListItem[] = [];
  public filteredRecipes: RecipeListItem[] = [];
  public environment = environment;
  
  searchString!: string;
  
  constructor(private http: HttpClient) { }

  ngOnInit() {
    this.getRecipeList();    
  }

  search()
  {
    if (!this.searchString || this.searchString == "") {
      //i.e. # no filter
      this.filteredRecipes = this.recipes
    }
    else
    {      
      this.filteredRecipes = this.recipes.filter(w =>
        w.title.toLowerCase().includes(this.searchString.toLowerCase()));        
    }
  }


  getRecipeList() {    
    this.http.get<RecipeListItem[]>(environment.apiUrl + 'MasterRecipeList').subscribe
      (
        (result) => {
          console.log(result);
          this.recipes = result;
          this.filteredRecipes = result;
          this.setThumbnailSRCs();
        },
        (error) => {
          console.error(error);
        }
    );
    
  }

  setThumbnailSRCs() {

    this.recipes.forEach(function (value) {
      value.thumbnailSRC = environment.imgSRC + value.id + "/thumb.jpeg"
    });

  }


}
