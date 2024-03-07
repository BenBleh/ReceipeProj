import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RecipeEditAddComponent } from './recipe-edit-add.component';

describe('RecipeEditAddComponent', () => {
  let component: RecipeEditAddComponent;
  let fixture: ComponentFixture<RecipeEditAddComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [RecipeEditAddComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(RecipeEditAddComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
