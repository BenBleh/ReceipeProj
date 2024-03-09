export interface Recipe {
  id: string;
  title: string;
  timeToComplete: string;
  notes: string;
  imgId: string;
  steps: Step[];
  ingredients: Ingredient[];
}

export interface Ingredient {
  description: string;
  qty: string;
  unit: string;
}

export interface Step {
  num: number;
  instructions: string;
  imgId: string;
}
