import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { FormArray, FormBuilder, FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';

import { Recipe } from '../recipe';
import { catchError } from 'rxjs/internal/operators/catchError';

@Component({
  selector: 'app-recipe-edit-add',
  templateUrl: './recipe-edit-add.component.html',
  styleUrl: './recipe-edit-add.component.css'
})
export class RecipeEditAddComponent implements OnInit {

  isEdit: boolean = false;
  IdFromRoute!: string | null;
  model: any;
  form!: FormGroup;
  payLoad = '';
  recipe: Recipe | undefined;


  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private http: HttpClient,
    private fb: FormBuilder
  ) { }

  //this is required to prevent the DOM refreshing each time an input field is updated(which causes the field to loose focus)
  trackByFn(index: any, item: any) { return index; }

  ngOnInit() {

    this.createForm();


  }

  public createForm() {
    //this mapping fails if the object being mapped is missing.
    //Should probably handle it here to be 100% safe, but will put validaiton on both sides of submit to ensure at least one item exists in each array
    this.getmodel().subscribe((response: any) => {
      this.model = response;
      this.form = this.fb.group({
        id: this.fb.control(this.model.id),
        title: this.fb.control(this.model.title),
        timeToComplete: this.fb.control(this.model.timeToComplete),
        notes: this.fb.control(this.model.notes),
        imgId: this.fb.control(this.model.imgId),
        steps: this.fb.array(
          this.model.steps.map((x: any) =>
            this.buildStepItemsFields(x)
          )
        ),
        ingredients: this.fb.array(
          this.model.ingredients.map((x: any) =>
            this.buildIngredientItemsFields(x)
          )
        )
      });

      //if this not edit(adding), then a template was used to build the form group. clear all values to defult.
      if (!this.isEdit) {
        this.form.reset({});
      }
    });   
  }

  getmodel() {

    // First get the product id from the current route.
    const routeParams = this.route.snapshot.paramMap;
    this.IdFromRoute = routeParams.get('recipeId');

    let path: string = '';

    if (this.IdFromRoute) {
      this.isEdit = true;
      // Find the recipe that correspond with the id provided in route.
      path = 'https://localhost:7087/Recipe/' + this.IdFromRoute;
    }
    else {
      //use the template
      path = '/assets/recipeTemplate.json';
    }

    return this.http.get(path);
  }

  //step operations

  buildStepItemsFields(x: any): FormGroup {
    return new FormGroup({
      num: new FormControl(x.num),
      instructions: new FormControl(x.instructions),
      imgId: new FormControl(x.imgId),
    });
  }


  addStepItem(): void {
    this.model = this.form.get('steps') as FormArray;
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
    this.model = this.form.get('steps') as FormArray;
    this.model.removeAt(index);
  }
  //end step operations

  //Ingredient operations
  buildIngredientItemsFields(x: any): FormGroup {
    return new FormGroup({
      description: new FormControl(x.description),
      qty: new FormControl(x.qty),
      unit: new FormControl(x.unit),
    });
  }


  addIngredientItem(): void {
    this.model = this.form.get('ingredients') as FormArray;
    this.model.push(this.createStepItemField());
  }


  createIngredientItemField(): FormGroup {
    return this.fb.group({
      description: '',
      qty: '',
      unit: ''
    });
  }

  deleteIngredientItem(index: number) {
    this.model = this.form.get('ingredients') as FormArray;
    this.model.removeAt(index);
  }

  //end Ingredient operations

    onSubmit() {
    this.payLoad = JSON.stringify(this.form.getRawValue());

    const httpOptions = {
      headers: new HttpHeaders({ 'Content-Type': 'application/json' })
    }

    if (this.isEdit) {
      this.http.put('https://localhost:7087/Recipe/' + this.IdFromRoute, this.payLoad, httpOptions).subscribe({           
        error: error => {
          console.error('There was an error!', error);
        }
      });
      //on sucess, navigate to display page
      this.router.navigate(['recipe/:recipeId', { recipeId: this.IdFromRoute }]);
    }
    else {
      this.http.post<Recipe>('https://localhost:7087/Recipe/', this.payLoad, httpOptions).subscribe(
        (result) => {
          //on sucess, navigate to display page
          this.recipe = result;          
          this.router.navigate(['recipe/:recipeId', { recipeId: this.recipe.id }]);         
        },
        (error) => {
          console.error(error);
        });
    }
  }

}
