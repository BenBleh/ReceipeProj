import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';

import { Recipe } from '../recipe';

@Component({
  selector: 'app-recipe-edit-add',
  templateUrl: './recipe-edit-add.component.html',
  styleUrl: './recipe-edit-add.component.css'
})
export class RecipeEditAddComponent implements OnInit {

  recipe: Recipe | undefined;

  isEdit: boolean = false;
  constructor(
    private route: ActivatedRoute,
    private http: HttpClient
  ) { }

  ngOnInit() {
    // First get the product id from the current route.
    const routeParams = this.route.snapshot.paramMap;
    const IdFromRoute = routeParams.get('recipeId');

    if (IdFromRoute)
    {
      this.isEdit = true;
    
      // Find the recipe that correspond with the id provided in route.
      let path: string = 'https://localhost:7087/Recipe/' + IdFromRoute;

      this.http.get<Recipe>(path).subscribe(
        (result) => {
          this.recipe = result;
        },
        (error) => {
          console.error(error);
        }
        );

    }
  }
}
