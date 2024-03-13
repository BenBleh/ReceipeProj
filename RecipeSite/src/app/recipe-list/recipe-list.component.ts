import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';


interface RecipeListItem {
  id: string;
  title: string;
  imageData: string;

}

@Component({
  selector: 'app-recipe-list',
  templateUrl: './recipe-list.component.html',
  styleUrl: './recipe-list.component.css'
})
export class RecipeListComponent implements OnInit {
  public recipes: RecipeListItem[] = [];
  public filteredRecipes: RecipeListItem[] = [];
  
  review!: string;

  constructor(private http: HttpClient) { }

  ngOnInit() {
    this.getRecipeList();    
  }

  search()
  {
    if (!this.review || this.review == "") {
      //i.e. # no filter
      this.filteredRecipes = this.recipes
    }
    else
    {      
      this.filteredRecipes = this.recipes.filter(w =>
        w.title.toLowerCase().includes(this.review.toLowerCase()));        
    }
  }


  getRecipeList() {
    this.http.get<RecipeListItem[]>('https://localhost:7087/MasterRecipeList').subscribe
      (

        (result) => {
          console.log(result);
          this.recipes = result;
          this.filteredRecipes = result;
        },
        (error) => {
          console.error(error);
        }
      );
  }

}
