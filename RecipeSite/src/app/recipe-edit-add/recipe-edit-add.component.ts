import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormArray, FormBuilder, FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';

import { Recipe, Step } from '../recipe';

@Component({
  selector: 'app-recipe-edit-add',
  templateUrl: './recipe-edit-add.component.html',
  styleUrl: './recipe-edit-add.component.css'
})
export class RecipeEditAddComponent implements OnInit {

  recipe!: Recipe;
  isEdit: boolean = false;

  model: any;
  form!: FormGroup;

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient,
    private fb: FormBuilder
  ) { }



  ngOnInit() {
    // First get the product id from the current route.
    const routeParams = this.route.snapshot.paramMap;
    const IdFromRoute = routeParams.get('recipeId');

    this.createForm();

    //if (IdFromRoute)
    //{
    //  this.isEdit = true;
    
    //  // Find the recipe that correspond with the id provided in route.
    //  let path: string = 'https://localhost:7087/Recipe/' + IdFromRoute;

    //  this.http.get<Recipe>(path).subscribe(
    //    (result) => {
    //      this.recipe = result;

    //      //try this for prepopulating form array
    //      //https://stackblitz.com/edit/angular-prepopulate-dynamic-reactive-form-array-wsbcq6?file=src%2Fapp%2Fapp.component.ts


    //      //this.recipe.steps.forEach((step) => {
    //      //  const StepForm = this.fb.group({
    //      //    num: [step.num],
    //      //    Ingredients: [step.Ingredients, Validators.required],
    //      //    imgId: [step.imgId, Validators.required]
    //      //  });
    //      //  this.steps.push(StepForm);
    //      //});

    //    },
    //    (error) => {
    //      console.error(error);
    //    }
    //    );


      
      
    //}
  }

  public createForm() {
    this.getmodel().subscribe((response: any) => {
      this.model = response;

      

      this.form = this.fb.group({
        stepItems: this.fb.array(
          this.model.stepItems.map((x: any) =>
            this.buildStepItemsFields(x)
          )
        ),
        ingredientItems: this.fb.array(
          this.model.ingredientItems.map((x: any) =>
            this.buildIngredientItemsFields(x)
          )
        )
      });
    });

  }

  getmodel() {
    return this.http.get('/assets/organisation.json');
  }


  buildStepItemsFields(x: any): FormGroup {
    return new FormGroup({
      num: new FormControl(x.num),
      instructions: new FormControl(x.instructions),
      imgId: new FormControl(x.imgId),
    });
  }


  addStepItem(): void {
    this.model = this.form.get('stepItems') as FormArray;
    this.model.push(this.createStepItemField());
  }


  createStepItemField(): FormGroup {
    return this.fb.group({
      num: '',
      instructions: '',
      imgId: ''
    });
  }


  deleteStep(index: number) {
    this.model = this.form.get('stepItems') as FormArray;
    this.model.removeAt(index);
  }







  buildIngredientItemsFields(x: any): FormGroup {
    return new FormGroup({
      description: new FormControl(x.description),
      qty: new FormControl(x.Ingredients),
      unit: new FormControl(x.unit),
    });
  }


  addIngredientItem(): void {
    this.model = this.form.get('ingredientItems') as FormArray;
    this.model.push(this.createStepItemField());
  }


  createIngredientItemField(): FormGroup {
    return this.fb.group({
      num: '',
      Ingredients: '',
      imgId: ''
    });
  }


  deleteIngredientItem(index: number) {
    this.model = this.form.get('ingredientItems') as FormArray;
    this.model.removeAt(index);
  }







  //get steps() {
  //  return this.form.controls["steps"] as FormArray;
  //}

  //addStep() {
  //  const StepForm = this.fb.group({
  //    num: [this.steps.length],
  //    Ingredients: ['', Validators.required],
  //    imgId: ['beginner', Validators.required]
  //  });
  //  this.steps.push(StepForm);
  //}

  //deleteStep(StepIndex: number) {
  //  this.steps.removeAt(StepIndex);
  //}

  //get ingredients() {
  //  return this.form.controls["ingredients"] as FormArray;
  //}

  //addIngredient() {
  //  const IngrediantForm = this.fb.group({
  //    description: ['', Validators.required],
  //    qty: ['', Validators.required],
  //    unit: ['', Validators.required]
  //  });
  //  this.ingredients.push(IngrediantForm);
  //}

  //deleteIngredient(ingredientIndex: number) {
  //  this.ingredients.removeAt(ingredientIndex);
  //}
}
