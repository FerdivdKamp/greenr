import { lazy } from "react";
import { createBrowserRouter, Link } from "react-router-dom";
import type { RouteObject } from "react-router-dom";
import App, { ProtectedRoute } from "../App";
import Home from "../home/Home";
const ItemsPage = lazy(() => import("../features/items/ItemsPage"));
const QuestionnairePage = lazy(() => import("../features/questionnaires/QuestionnairePage"));
import QuestionnaireListPage from "../features/questionnaires/QuestionnaireListPage";
import QuestionnaireHistoryPage from "../features/questionnaires/QuestionnaireHistoryPage";
import { AdminQuestionnairesGuard } from "../features/admin/questionnaires/AdminQuestionnairesGuard";
import { AuthPage } from "../features/auth/AuthPage";
import { AccountPage } from "../features/auth/AccountPage";
const pageLoadError = <div role="alert"><p>Unable to load this page. Check your connection and reload to try again.</p><Link to="/">Return home</Link></div>;

export const routes: RouteObject[] = [{ path: "/", element: <App />, children: [
  { index: true, element: <Home /> },
  { path: "login", element: <AuthPage /> },
  { path: "register", element: <AuthPage register /> },
  { element: <ProtectedRoute />, children: [
    { path: "items/*", element: <ItemsPage />, errorElement: pageLoadError },
    { path: "questionnaires", element: <QuestionnaireListPage /> },
    { path: "questionnaires/:canonicalId", element: <QuestionnairePage />, errorElement: pageLoadError },
    { path: "questionnaires/history", element: <QuestionnaireHistoryPage /> },
    { path: "questionnaires/admin", element: <AdminQuestionnairesGuard /> },
    { path: "account", element: <AccountPage /> },
  ] },
] }];

export const router = createBrowserRouter(routes);
