import { http } from "../../lib/http";
import type { Item } from "./types";

export const getItems = () => http.get<Item[]>("/api/items");
