import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { http } from "../../lib/http";
import type { Item } from "./types";

type Input = { itemName: string; useCase: string; price: number; footprintKg: number; dateOfPurchase: string | null };
type Summary = { itemCount: number; totalPrice: number; totalFootprintKg: number; byUseCase: { useCase: string; totalFootprintKg: number }[] };
const blank = { itemName: "", useCase: "", price: "", footprintKg: "", dateOfPurchase: "" };

export default function ItemsPage() {
  const client = useQueryClient();
  const items = useQuery({ queryKey: ["items"], queryFn: () => http.get<Item[]>("/api/items") });
  const summary = useQuery({ queryKey: ["items", "summary"], queryFn: () => http.get<Summary>("/api/items/summary") });
  const [form, setForm] = useState(blank);
  const [editing, setEditing] = useState<string | null>(null);
  const [filter, setFilter] = useState("");
  const save = useMutation({ mutationFn: (body: Input) => editing ? http.put<Item>(`/api/items/${editing}`, body) : http.post<Item>("/api/items", body), onSuccess: async () => { setForm(blank); setEditing(null); await client.invalidateQueries({ queryKey: ["items"] }); } });
  const remove = useMutation({ mutationFn: (id: string) => http.del<void>(`/api/items/${id}`), onSuccess: async () => { await client.invalidateQueries({ queryKey: ["items"] }); } });
  const filtered = (items.data ?? []).filter(item => !filter || item.useCase === filter);
  return <section><h1>Your items</h1>
    {summary.data && <section><h2>Summary</h2><p>{summary.data.itemCount} items · {summary.data.totalFootprintKg} kg CO₂e · {summary.data.totalPrice} total price</p><p>Footprint is the sum of the values you recorded.</p><h3>Footprint by use case</h3><ul>{summary.data.byUseCase.map(group => <li key={group.useCase || "uncategorized"}>{group.useCase || "Uncategorized"}: {group.totalFootprintKg} kg CO₂e <meter min="0" max={Math.max(summary.data.totalFootprintKg, 1)} value={group.totalFootprintKg} aria-label={`${group.useCase || "Uncategorized"} footprint`} /></li>)}</ul></section>}
    {summary.error && <p role="alert">Summary: {summary.error.message}</p>}
    <h2>{editing ? "Edit item" : "Add item"}</h2>
    <form onSubmit={e => { e.preventDefault(); save.mutate({ ...form, price: Number(form.price), footprintKg: Number(form.footprintKg), dateOfPurchase: form.dateOfPurchase || null }); }}>
      <label htmlFor="item-name">Name</label><input id="item-name" required maxLength={50} value={form.itemName} onChange={e => setForm({ ...form, itemName: e.target.value })} />
      <label htmlFor="use-case">Use case</label><input id="use-case" maxLength={20} value={form.useCase} onChange={e => setForm({ ...form, useCase: e.target.value })} />
      <label htmlFor="price">Price</label><input id="price" type="number" min="0" max="99999999.99" step="0.01" required value={form.price} onChange={e => setForm({ ...form, price: e.target.value })} />
      <label htmlFor="footprint">Footprint (kg CO₂e)</label><input id="footprint" type="number" min="0" max="9999999.999" step="0.001" required value={form.footprintKg} onChange={e => setForm({ ...form, footprintKg: e.target.value })} />
      <label htmlFor="purchase-date">Purchase date</label><input id="purchase-date" type="date" max={new Date().toISOString().slice(0, 10)} value={form.dateOfPurchase} onChange={e => setForm({ ...form, dateOfPurchase: e.target.value })} />
      <button disabled={save.isPending}>{editing ? "Save changes" : "Add item"}</button>{editing && <button type="button" onClick={() => { setEditing(null); setForm(blank); }}>Cancel editing</button>}
      {save.error && <p role="alert">{save.error.message}</p>}
    </form>
    <h2>Items</h2><label htmlFor="filter">Filter by use case </label><select id="filter" value={filter} onChange={e => setFilter(e.target.value)}><option value="">All</option>{[...new Set((items.data ?? []).map(i => i.useCase).filter(Boolean))].sort().map(value => <option key={value} value={value}>{value}</option>)}</select>
    {items.isPending && <p>Loading items…</p>}{items.error && <p role="alert">{items.error.message} <button onClick={() => void items.refetch()}>Try again</button></p>}
    {items.data && filtered.length === 0 && <p>No items found.</p>}
    <ul>{filtered.map(item => <li key={item.itemId}><strong>{item.itemName}</strong> · {item.useCase} · {item.price} · {item.footprintKg} kg CO₂e · {item.dateOfPurchase || "No purchase date"} <button onClick={() => { setEditing(item.itemId); setForm({ itemName: item.itemName, useCase: item.useCase, price: String(item.price), footprintKg: String(item.footprintKg), dateOfPurchase: item.dateOfPurchase?.slice(0, 10) ?? "" }); }}>Edit {item.itemName}</button> <button disabled={remove.isPending} onClick={() => { if (window.confirm(`Delete ${item.itemName}?`)) remove.mutate(item.itemId); }}>Delete {item.itemName}</button></li>)}</ul>
    {remove.error && <p role="alert">{remove.error.message}</p>}
  </section>;
}
