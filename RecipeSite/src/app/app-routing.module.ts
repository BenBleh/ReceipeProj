import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';

import { RecipeListComponent } from './recipe-list/recipe-list.component';
import { RecipeViewComponent } from './recipe-view/recipe-view.component';
import { RecipeEditAddComponent } from './recipe-edit-add/recipe-edit-add.component';

const routes: Routes =
  [
    { path: '', component: RecipeListComponent },
    { path: 'recipe/:recipeId', component: RecipeViewComponent },
    { path: 'addRecipe', component: RecipeEditAddComponent },
    { path: 'editRecipe/:recipeId', component: RecipeEditAddComponent }    
  ];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
