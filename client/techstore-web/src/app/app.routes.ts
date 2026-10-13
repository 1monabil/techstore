import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'TechStore — find the exact part',
    loadComponent: () => import('./features/home/home.page').then((m) => m.HomePage),
  },
  {
    path: 'products',
    title: 'Products — TechStore',
    loadComponent: () => import('./features/products/products.page').then((m) => m.ProductsPage),
  },
  { path: '**', redirectTo: '' },
];
