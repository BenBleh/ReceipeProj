import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from './../../environments/environment';

import { Recipe } from '../recipe';
import { fakeAsync } from '@angular/core/testing';


@Component({
  selector: 'app-recipe-view',
  templateUrl: './recipe-view.component.html',
  styleUrl: './recipe-view.component.css'
})
export class RecipeViewComponent implements OnInit {

  recipe: Recipe | undefined;
  public environment = environment;
  sourceIsLink: boolean | undefined; 

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient
  ) { }

  ngOnInit() {
    // First get the product id from the current route.
    const routeParams = this.route.snapshot.paramMap;
    const IdFromRoute = routeParams.get('recipeId');
    this.sourceIsLink = false;

    // Find the recipe that correspond with the id provided in route.

    let path: string = environment.apiUrl + 'Recipe/' + IdFromRoute;

    this.http.get<Recipe>(path).subscribe(
      (result) => {
        this.recipe = result;
        this.determineSourceType();
      },
      (error) => {
        console.error(error);
      }
    );
  }
  determineSourceType()
  {
    this.sourceIsLink = this.recipe?.source.includes("http");
  }
  

}

