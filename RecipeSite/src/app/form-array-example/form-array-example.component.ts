import { Component } from '@angular/core';
import { FormArray, FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';



@Component({
  selector: 'app-form-array-example',
  templateUrl: './form-array-example.component.html',
  styleUrl: './form-array-example.component.css' 
})
export class FormArrayExampleComponent {

  form = this.fb.group({
    //...other form controls ...
    steps: this.fb.array([]),
    ingredients : this.fb.array([])
  });

  constructor(private fb: FormBuilder) { }

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
