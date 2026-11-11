import { createBrowserRouter } from "react-router-dom";
import App, { ProtectedRoute } from "../App";
import Home from "../home/Home";
import ItemsPage from "../features/items/ItemsPage";
import QuestionnairePage from "../features/questionnaires/QuestionnairePage";
import QuestionnaireListPage from "../features/questionnaires/QuestionnaireListPage";
import QuestionnaireHistoryPage from "../features/questionnaires/QuestionnaireHistoryPage";
import { AdminQuestionnairesGuard } from "../features/admin/questionnaires/AdminQuestionnairesGuard";
import { AuthPage } from "../features/auth/AuthPage";
import { AccountPage } from "../features/auth/AccountPage";
export const router = createBrowserRouter([{ path: "/", element: <App />, children: [
  { index: true, element: <Home /> },
  { path: "login", element: <AuthPage /> },
  { path: "register", element: <AuthPage register /> },
  { element: <ProtectedRoute />, children: [
    { path: "items/*", element: <ItemsPage /> },
    { path: "questionnaires", element: <QuestionnaireListPage /> },
    { path: "questionnaires/:canonicalId", element: <QuestionnairePage /> },
    { path: "questionnaires/history", element: <QuestionnaireHistoryPage /> },
    { path: "questionnaires/admin", element: <AdminQuestionnairesGuard /> },
    { path: "account", element: <AccountPage /> },
  ] },
] }]);
