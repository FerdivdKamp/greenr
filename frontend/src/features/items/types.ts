/** A row returned by /api/items */
export interface Item {
  itemId: string;
  itemName: string;
  useCase: string;
  price: number;
  footprintKg: number;
  dateOfPurchase: string; // ISO date
}
