import { createBrowserRouter } from "react-router-dom";
import App from "../App";
import Home from "../home/Home";
import ItemsPage from "../features/items/ItemsPage";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <App />,
    children: [
      { index: true, element: <Home /> },
      { path: "items", element: <ItemsPage /> },
    ],
  },
]);
