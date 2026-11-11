// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import QuestionnairePage from "../src/features/questionnaires/QuestionnairePage";
import QuestionnaireListPage from "../src/features/questionnaires/QuestionnaireListPage";

vi.mock("survey-react-ui", () => ({ Survey: ({ model }: { model: { onComplete: { fire: (sender: unknown) => void } } }) => <button onClick={() => model.onComplete.fire({ data: { answer: "yes" } })}>Finish survey</button> }));
function mount(path: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(<QueryClientProvider client={client}><MemoryRouter initialEntries={[path]}><Routes><Route path="/questionnaires" element={<QuestionnaireListPage />} /><Route path="/questionnaires/:canonicalId" element={<QuestionnairePage />} /></Routes></MemoryRouter></QueryClientProvider>);
}
afterEach(() => { cleanup(); vi.unstubAllGlobals(); });
it("shows published questionnaires and hides administration from participants", async () => {
  vi.stubGlobal("fetch", vi.fn((url: string) => Promise.resolve(new Response(JSON.stringify(url.endsWith("/me") ? { questionnaireAdmin: false } : [{ id: "1", canonicalId: "family", title: "Travel", status: "active", version: 1 }]), { status: 200 }))));
  mount("/questionnaires");
  expect(await screen.findByRole("link", { name: /Travel/ })).toBeTruthy();
  expect(screen.queryByRole("link", { name: /Manage/ })).toBeNull();
});
it("reports invalid questionnaire definitions", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: "1", title: "Broken", definition: "{" }), { status: 200 })));
  mount("/questionnaires/family");
  expect((await screen.findByRole("alert")).textContent).toContain("invalid definition");
});
it("keeps answers available after a failed submission and retries", async () => {
  const fetchMock = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ id: "1", title: "Travel", definition: { elements: [{ name: "answer", type: "text" }] } }), { status: 200 })).mockResolvedValueOnce(new Response("failed", { status: 500 })).mockResolvedValueOnce(new Response('{"id":"response"}', { status: 201 }));
  vi.stubGlobal("fetch", fetchMock);
  mount("/questionnaires/family");
  fireEvent.click(await screen.findByRole("button", { name: "Finish survey" }));
  fireEvent.click(screen.getByRole("button", { name: "Submit answers" }));
  expect((await screen.findByRole("alert")).textContent).toContain("try again");
  fireEvent.click(screen.getByRole("button", { name: "Submit answers" }));
  expect((await screen.findByRole("status")).textContent).toContain("saved");
  expect(JSON.parse(fetchMock.mock.calls[2][1].body).answers).toEqual({ answer: "yes" });
});
