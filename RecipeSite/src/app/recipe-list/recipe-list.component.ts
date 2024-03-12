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

  constructor(private http: HttpClient) { }

  ngOnInit() {
    this.getRecipeList();
  }

  getRecipeList() {
    this.http.get<RecipeListItem[]>('https://localhost:7087/MasterRecipeList').subscribe
      (

        (result) => {
          console.log(result);
          this.recipes = result;
        },
        (error) => {
          console.error(error);
        }
      );
  }

  title = 'RecipieSite';
}
