import { HttpClient } from '@angular/common/http';
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
    ingredients: this.fb.array([])
  });

  fileName = '';


  constructor(private fb: FormBuilder, private http: HttpClient) { }


  onFileSelected(event: any) {

    const file: File = event.target.files[0];

    if (file) {
      this.fileName = file.name;    
      this.getBase64(file);      
    }
  }

  getBase64(file: File) : String | undefined{
    var reader = new FileReader();
    reader.readAsDataURL(file);
    
      reader.onload = function () {
        console.log(reader.result);
      };
      reader.onerror = function (error) {
        console.log('Error: ', error);
    };


    return reader.result?.toString();
  }

  get steps() {
    return this.form.controls["steps"] as FormArray;
  }



}
