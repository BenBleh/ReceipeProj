import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormArray, FormBuilder, Validators } from '@angular/forms';

import { Recipe, Step } from '../recipe';

@Component({
  selector: 'app-recipe-edit-add',
  templateUrl: './recipe-edit-add.component.html',
  styleUrl: './recipe-edit-add.component.css'
})
export class RecipeEditAddComponent implements OnInit {

  recipe!: Recipe;
  isEdit: boolean = false;

  form = this.fb.group({
    //...other form controls ...
    steps: this.fb.array([]),
    ingredients: this.fb.array([])
  });

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient,
    private fb: FormBuilder
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

          //try this for prepopulating form array
          //https://stackblitz.com/edit/angular-prepopulate-dynamic-reactive-form-array-wsbcq6?file=src%2Fapp%2Fapp.component.ts


          this.recipe.steps.forEach((step) => {
            const StepForm = this.fb.group({
              num: [step.num],
              instructions: [step.instructions, Validators.required],
              imgId: [step.imgId, Validators.required]
            });
            this.steps.push(StepForm);
          });

        },
        (error) => {
          console.error(error);
        }
        );

      
    }
  }

  get steps() {
    return this.form.controls["steps"] as FormArray;
  }

  addStep() {
    const StepForm = this.fb.group({
      num: [this.steps.length],
      instructions: ['', Validators.required],
      imgId: ['beginner', Validators.required]
    });
    this.steps.push(StepForm);
  }

  deleteStep(StepIndex: number) {
    this.steps.removeAt(StepIndex);
  }

  get ingredients() {
    return this.form.controls["ingredients"] as FormArray;
  }

  addIngredient() {
    const IngrediantForm = this.fb.group({
      description: ['', Validators.required],
      qty: ['', Validators.required],
      unit: ['', Validators.required]
    });
    this.ingredients.push(IngrediantForm);
  }

  deleteIngredient(ingredientIndex: number) {
    this.ingredients.removeAt(ingredientIndex);
  }
}
