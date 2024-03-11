export interface Recipe {
  id: string;
  title: string;
  timeToComplete: string;
  notes: string;
  imageData: string;
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
  imageData: string;
}
